#include "wake_word_model.h"

#include <algorithm>
#include <cstring>
#include <new>

#include "audio_preprocessor_int8_model_data.h"
#include "esp_heap_caps.h"
#include "esp_log.h"
#include "tensorflow/lite/micro/micro_allocator.h"
#include "tensorflow/lite/micro/micro_interpreter.h"
#include "tensorflow/lite/micro/micro_mutable_op_resolver.h"
#include "tensorflow/lite/micro/micro_resource_variable.h"
#include "tensorflow/lite/schema/schema_generated.h"

namespace {

constexpr size_t kAudioWindowSamples = 480;
constexpr size_t kAudioStepSamples = 160;
constexpr size_t kFeatureCount = 40;
constexpr size_t kFeatureFramesPerInference = 3;
constexpr size_t kProbabilityWindow = 5;
constexpr uint8_t kDefaultThresholdPercent = 96;
constexpr uint8_t kMinimumThresholdPercent = 50;
constexpr uint8_t kMaximumThresholdPercent = 99;
constexpr size_t kPreprocessorArenaBytes = 24 * 1024;
constexpr size_t kWakeArenaBytes = 160 * 1024;
constexpr int kWakeResourceVariableCount = 6;

using PreprocessorResolver = tflite::MicroMutableOpResolver<18>;
using WakeResolver = tflite::MicroMutableOpResolver<14>;

const char *const kTag = "yo_franky";

extern const uint8_t g_yo_franky_model_start[]
    asm("_binary_yo_franky_tflite_start");

tflite::MicroInterpreter *s_preprocessor;
tflite::MicroInterpreter *s_wake_interpreter;
tflite::MicroResourceVariables *s_wake_resources;
uint8_t *s_preprocessor_arena;
uint8_t *s_wake_arena;
int16_t s_audio_window[kAudioWindowSamples];
size_t s_audio_window_size;
int8_t s_feature_frames[kFeatureFramesPerInference * kFeatureCount];
size_t s_feature_frame_count;
uint8_t s_probabilities[kProbabilityWindow];
size_t s_probability_count;
size_t s_probability_index;
uint8_t s_threshold_percent = kDefaultThresholdPercent;
uint8_t s_last_score_percent;
bool s_initialized;

uint8_t *allocate_arena(size_t bytes)
{
    uint8_t *arena = static_cast<uint8_t *>(heap_caps_aligned_alloc(
        16,
        bytes,
        MALLOC_CAP_SPIRAM | MALLOC_CAP_8BIT));
    if (arena == nullptr) {
        arena = static_cast<uint8_t *>(heap_caps_aligned_alloc(
            16,
            bytes,
            MALLOC_CAP_8BIT));
    }
    return arena;
}

TfLiteStatus register_preprocessor_ops(PreprocessorResolver &resolver)
{
    if (resolver.AddReshape() != kTfLiteOk ||
        resolver.AddCast() != kTfLiteOk ||
        resolver.AddStridedSlice() != kTfLiteOk ||
        resolver.AddConcatenation() != kTfLiteOk ||
        resolver.AddMul() != kTfLiteOk ||
        resolver.AddAdd() != kTfLiteOk ||
        resolver.AddDiv() != kTfLiteOk ||
        resolver.AddMinimum() != kTfLiteOk ||
        resolver.AddMaximum() != kTfLiteOk ||
        resolver.AddWindow() != kTfLiteOk ||
        resolver.AddFftAutoScale() != kTfLiteOk ||
        resolver.AddRfft() != kTfLiteOk ||
        resolver.AddEnergy() != kTfLiteOk ||
        resolver.AddFilterBank() != kTfLiteOk ||
        resolver.AddFilterBankSquareRoot() != kTfLiteOk ||
        resolver.AddFilterBankSpectralSubtraction() != kTfLiteOk ||
        resolver.AddPCAN() != kTfLiteOk ||
        resolver.AddFilterBankLog() != kTfLiteOk) {
        return kTfLiteError;
    }
    return kTfLiteOk;
}

TfLiteStatus register_wake_ops(WakeResolver &resolver)
{
    if (resolver.AddCallOnce() != kTfLiteOk ||
        resolver.AddVarHandle() != kTfLiteOk ||
        resolver.AddReadVariable() != kTfLiteOk ||
        resolver.AddAssignVariable() != kTfLiteOk ||
        resolver.AddReshape() != kTfLiteOk ||
        resolver.AddConcatenation() != kTfLiteOk ||
        resolver.AddStridedSlice() != kTfLiteOk ||
        resolver.AddConv2D() != kTfLiteOk ||
        resolver.AddDepthwiseConv2D() != kTfLiteOk ||
        resolver.AddSplitV() != kTfLiteOk ||
        resolver.AddFullyConnected() != kTfLiteOk ||
        resolver.AddLogistic() != kTfLiteOk ||
        resolver.AddQuantize() != kTfLiteOk) {
        return kTfLiteError;
    }
    return kTfLiteOk;
}

bool dimensions_match(const TfLiteTensor *tensor, const int *dimensions, size_t count)
{
    if (tensor == nullptr || tensor->dims == nullptr ||
        tensor->dims->size != static_cast<int>(count)) {
        return false;
    }
    for (size_t index = 0; index < count; ++index) {
        if (tensor->dims->data[index] != dimensions[index]) return false;
    }
    return true;
}

bool process_feature_frame(const int8_t *features)
{
    std::memcpy(
        s_feature_frames + s_feature_frame_count * kFeatureCount,
        features,
        kFeatureCount);
    ++s_feature_frame_count;
    if (s_feature_frame_count < kFeatureFramesPerInference) return false;

    s_feature_frame_count = 0;
    TfLiteTensor *input = s_wake_interpreter->input(0);
    std::memcpy(input->data.int8, s_feature_frames, sizeof(s_feature_frames));
    if (s_wake_interpreter->Invoke() != kTfLiteOk) {
        ESP_LOGE(kTag, "Wake model invocation failed");
        franky_wake_model_reset();
        return false;
    }

    const uint8_t probability = s_wake_interpreter->output(0)->data.uint8[0];
    s_probabilities[s_probability_index] = probability;
    s_probability_index = (s_probability_index + 1) % kProbabilityWindow;
    if (s_probability_count < kProbabilityWindow) ++s_probability_count;
    if (s_probability_count < kProbabilityWindow) return false;

    uint16_t sum = 0;
    for (uint8_t value : s_probabilities) sum += value;
    constexpr uint32_t probability_scale = 255 * kProbabilityWindow;
    s_last_score_percent = static_cast<uint8_t>(
        (static_cast<uint32_t>(sum) * 100 + probability_scale / 2) /
        probability_scale);
    return static_cast<uint32_t>(sum) * 100 >=
        static_cast<uint32_t>(s_threshold_percent) * probability_scale;
}

bool process_audio_window()
{
    TfLiteTensor *input = s_preprocessor->input(0);
    std::memcpy(input->data.i16, s_audio_window, sizeof(s_audio_window));
    if (s_preprocessor->Invoke() != kTfLiteOk) {
        ESP_LOGE(kTag, "Audio feature extraction failed");
        franky_wake_model_reset();
        return false;
    }
    return process_feature_frame(s_preprocessor->output(0)->data.int8);
}

}  // namespace

extern "C" esp_err_t franky_wake_model_init(void)
{
    if (s_initialized) return ESP_ERR_INVALID_STATE;

    const tflite::Model *preprocessor_model =
        tflite::GetModel(g_audio_preprocessor_int8_tflite);
    const tflite::Model *wake_model = tflite::GetModel(g_yo_franky_model_start);
    if (preprocessor_model->version() != TFLITE_SCHEMA_VERSION ||
        wake_model->version() != TFLITE_SCHEMA_VERSION) {
        ESP_LOGE(kTag, "Unsupported TensorFlow Lite model schema");
        return ESP_ERR_NOT_SUPPORTED;
    }

    static PreprocessorResolver preprocessor_resolver;
    static WakeResolver wake_resolver;
    if (register_preprocessor_ops(preprocessor_resolver) != kTfLiteOk ||
        register_wake_ops(wake_resolver) != kTfLiteOk) {
        ESP_LOGE(kTag, "Could not register TensorFlow Lite operators");
        return ESP_FAIL;
    }

    s_preprocessor_arena = allocate_arena(kPreprocessorArenaBytes);
    s_wake_arena = allocate_arena(kWakeArenaBytes);
    if (s_preprocessor_arena == nullptr || s_wake_arena == nullptr) {
        ESP_LOGE(kTag, "Could not allocate TensorFlow Lite arenas");
        return ESP_ERR_NO_MEM;
    }

    s_preprocessor = new (std::nothrow) tflite::MicroInterpreter(
        preprocessor_model,
        preprocessor_resolver,
        s_preprocessor_arena,
        kPreprocessorArenaBytes);
    if (s_preprocessor == nullptr || s_preprocessor->AllocateTensors() != kTfLiteOk) {
        ESP_LOGE(kTag, "Could not initialize audio feature extraction");
        return ESP_FAIL;
    }

    tflite::MicroAllocator *wake_allocator =
        tflite::MicroAllocator::Create(s_wake_arena, kWakeArenaBytes);
    if (wake_allocator == nullptr) return ESP_ERR_NO_MEM;
    s_wake_resources = tflite::MicroResourceVariables::Create(
        wake_allocator,
        kWakeResourceVariableCount);
    if (s_wake_resources == nullptr) return ESP_ERR_NO_MEM;

    s_wake_interpreter = new (std::nothrow) tflite::MicroInterpreter(
        wake_model,
        wake_resolver,
        wake_allocator,
        s_wake_resources);
    if (s_wake_interpreter == nullptr ||
        s_wake_interpreter->AllocateTensors() != kTfLiteOk) {
        ESP_LOGE(kTag, "Could not initialize wake-word inference");
        return ESP_FAIL;
    }

    const int preprocessor_input_shape[] = {1, 480};
    const int preprocessor_output_shape[] = {40};
    const int wake_input_shape[] = {1, 3, 40};
    const int wake_output_shape[] = {1, 1};
    if (s_preprocessor->input(0)->type != kTfLiteInt16 ||
        s_preprocessor->output(0)->type != kTfLiteInt8 ||
        s_wake_interpreter->input(0)->type != kTfLiteInt8 ||
        s_wake_interpreter->output(0)->type != kTfLiteUInt8 ||
        !dimensions_match(s_preprocessor->input(0), preprocessor_input_shape, 2) ||
        !dimensions_match(s_preprocessor->output(0), preprocessor_output_shape, 1) ||
        !dimensions_match(s_wake_interpreter->input(0), wake_input_shape, 3) ||
        !dimensions_match(s_wake_interpreter->output(0), wake_output_shape, 2)) {
        ESP_LOGE(kTag, "Unexpected wake-word model tensors");
        return ESP_ERR_INVALID_SIZE;
    }

    s_initialized = true;
    franky_wake_model_reset();
    ESP_LOGI(kTag, "Yo Franky model ready (%u + %u arena bytes)",
             static_cast<unsigned>(s_preprocessor->arena_used_bytes()),
             static_cast<unsigned>(s_wake_interpreter->arena_used_bytes()));
    return ESP_OK;
}

extern "C" void franky_wake_model_reset(void)
{
    std::memset(s_audio_window, 0, sizeof(s_audio_window));
    std::memset(s_feature_frames, 0, sizeof(s_feature_frames));
    std::memset(s_probabilities, 0, sizeof(s_probabilities));
    s_audio_window_size = 0;
    s_feature_frame_count = 0;
    s_probability_count = 0;
    s_probability_index = 0;
    s_last_score_percent = 0;
    if (s_wake_resources != nullptr) s_wake_resources->ResetAll();
}

extern "C" esp_err_t franky_wake_model_set_threshold_percent(
    uint8_t threshold_percent)
{
    if (!s_initialized) return ESP_ERR_INVALID_STATE;
    if (threshold_percent < kMinimumThresholdPercent ||
        threshold_percent > kMaximumThresholdPercent) {
        return ESP_ERR_INVALID_ARG;
    }

    s_threshold_percent = threshold_percent;
    franky_wake_model_reset();
    return ESP_OK;
}

extern "C" uint8_t franky_wake_model_get_threshold_percent(void)
{
    return s_threshold_percent;
}

extern "C" uint8_t franky_wake_model_get_last_score_percent(void)
{
    return s_last_score_percent;
}

extern "C" bool franky_wake_model_process(
    const int16_t *samples,
    size_t sample_count)
{
    if (!s_initialized || samples == nullptr) return false;

    while (sample_count > 0) {
        const size_t copied = std::min(
            sample_count,
            kAudioWindowSamples - s_audio_window_size);
        std::memcpy(
            s_audio_window + s_audio_window_size,
            samples,
            copied * sizeof(int16_t));
        s_audio_window_size += copied;
        samples += copied;
        sample_count -= copied;

        if (s_audio_window_size == kAudioWindowSamples) {
            const bool detected = process_audio_window();
            std::memmove(
                s_audio_window,
                s_audio_window + kAudioStepSamples,
                (kAudioWindowSamples - kAudioStepSamples) * sizeof(int16_t));
            s_audio_window_size = kAudioWindowSamples - kAudioStepSamples;
            if (detected) return true;
        }
    }
    return false;
}
