#include "wake_word.h"

#include <stdbool.h>
#include <stdlib.h>
#include <string.h>

#include "audio_board.h"
#include "esp_afe_sr_iface.h"
#include "esp_afe_sr_models.h"
#include "esp_heap_caps.h"
#include "esp_log.h"
#include "esp_process_sdkconfig.h"
#include "model_path.h"
#include "freertos/FreeRTOS.h"
#include "freertos/event_groups.h"
#include "freertos/task.h"

#define WAKE_INPUT_FORMAT "RMNM"
#define WAKE_TASK_STACK_SIZE (8 * 1024)
#define WAKE_TASK_PRIORITY 5
#define FEED_PAUSED_BIT BIT0
#define CAPTURE_COMPLETE_BIT BIT1
#define PAUSE_TIMEOUT_MS 1000
#define VAD_TRAILING_SILENCE_MS 900

static const char *TAG = "wake_word";

static esp_afe_sr_iface_t *s_afe;
static esp_afe_sr_data_t *s_afe_data;
static srmodel_list_t *s_models;
static EventGroupHandle_t s_events;
static TaskHandle_t s_feed_task;
static TaskHandle_t s_detect_task;
static wake_word_detected_callback_t s_callback;
static volatile bool s_detection_enabled;
static volatile bool s_wake_armed;
static volatile bool s_capture_active;
static int16_t *s_capture_samples;
static size_t s_capture_capacity_samples;
static size_t s_capture_sample_count;
static size_t s_capture_elapsed_samples;
static size_t s_capture_start_timeout_samples;
static bool s_capture_speech_started;
static wake_utterance_end_t s_capture_end_reason;
static bool s_initialized;

static size_t append_capture_samples(const int16_t *samples, size_t sample_count)
{
    if (samples == NULL || sample_count == 0 || s_capture_samples == NULL) return 0;

    const size_t remaining = s_capture_capacity_samples - s_capture_sample_count;
    const size_t copied = sample_count < remaining ? sample_count : remaining;
    if (copied > 0) {
        memcpy(
            s_capture_samples + s_capture_sample_count,
            samples,
            copied * sizeof(int16_t));
        s_capture_sample_count += copied;
    }
    return copied;
}

static void complete_capture(wake_utterance_end_t reason)
{
    if (!s_capture_active) return;
    s_capture_end_reason = reason;
    s_capture_active = false;
    xEventGroupSetBits(s_events, CAPTURE_COMPLETE_BIT);
}

static void process_capture_result(const afe_fetch_result_t *result)
{
    if (!s_capture_active || result == NULL || result->data == NULL || result->data_size <= 0) return;

    const size_t frame_samples = (size_t)result->data_size / sizeof(int16_t);
    s_capture_elapsed_samples += frame_samples;

    if (!s_capture_speech_started) {
        if (result->vad_state != VAD_SPEECH) {
            if (s_capture_elapsed_samples >= s_capture_start_timeout_samples) {
                complete_capture(WAKE_UTTERANCE_NO_SPEECH);
            }
            return;
        }

        s_capture_speech_started = true;
        if (result->vad_cache != NULL && result->vad_cache_size > 0) {
            append_capture_samples(
                result->vad_cache,
                (size_t)result->vad_cache_size / sizeof(int16_t));
        }
    }

    append_capture_samples(result->data, frame_samples);
    if (s_capture_sample_count >= s_capture_capacity_samples) {
        complete_capture(WAKE_UTTERANCE_ENDED_BY_MAX_DURATION);
    } else if (result->vad_state == VAD_SILENCE) {
        complete_capture(WAKE_UTTERANCE_ENDED_BY_SILENCE);
    }
}

static void feed_task(void *argument)
{
    (void)argument;
    const int chunk_frames = s_afe->get_feed_chunksize(s_afe_data);
    const int feed_channels = s_afe->get_feed_channel_num(s_afe_data);

    if (feed_channels != FRANKY_RAW_CHANNELS) {
        ESP_LOGE(TAG, "Expected %d feed channels, got %d", FRANKY_RAW_CHANNELS, feed_channels);
        xEventGroupSetBits(s_events, FEED_PAUSED_BIT);
        vTaskDelete(NULL);
        return;
    }

    int16_t *samples = heap_caps_malloc(
        chunk_frames * feed_channels * sizeof(int16_t),
        MALLOC_CAP_SPIRAM | MALLOC_CAP_8BIT);
    if (samples == NULL) {
        samples = malloc(chunk_frames * feed_channels * sizeof(int16_t));
    }
    if (samples == NULL) {
        ESP_LOGE(TAG, "Could not allocate the WakeNet feed buffer");
        xEventGroupSetBits(s_events, FEED_PAUSED_BIT);
        vTaskDelete(NULL);
        return;
    }

    while (true) {
        if (!s_detection_enabled) {
            xEventGroupSetBits(s_events, FEED_PAUSED_BIT);
            vTaskDelay(pdMS_TO_TICKS(10));
            continue;
        }

        xEventGroupClearBits(s_events, FEED_PAUSED_BIT);
        esp_err_t error = audio_board_read_raw(samples, chunk_frames);
        if (error != ESP_OK) {
            ESP_LOGE(TAG, "Microphone read failed: %s", esp_err_to_name(error));
            vTaskDelay(pdMS_TO_TICKS(20));
            continue;
        }

        if (s_detection_enabled) {
            s_afe->feed(s_afe_data, samples);
        }
    }
}

static bool result_is_wake(const afe_fetch_result_t *result)
{
    if (result->raw_data_channels == 1) {
        return result->wakeup_state == WAKENET_DETECTED;
    }

    return result->wakeup_state == WAKENET_CHANNEL_VERIFIED;
}

static void detect_task(void *argument)
{
    (void)argument;
    while (true) {
        afe_fetch_result_t *result = s_afe->fetch(s_afe_data);
        if (result == NULL || result->ret_value == ESP_FAIL) {
            if (s_detection_enabled) ESP_LOGE(TAG, "AFE fetch failed");
            vTaskDelay(pdMS_TO_TICKS(20));
            continue;
        }

        process_capture_result(result);

        if (s_detection_enabled && s_wake_armed && !s_capture_active && result_is_wake(result)) {
            s_wake_armed = false;
            s_afe->disable_wakenet(s_afe_data);
            if (s_callback != NULL) s_callback();
        }
    }
}

esp_err_t wake_word_init(wake_word_detected_callback_t callback)
{
    if (callback == NULL || s_initialized) return ESP_ERR_INVALID_STATE;

    s_models = esp_srmodel_init("model");
    if (s_models == NULL) return ESP_FAIL;

    afe_config_t *config = afe_config_init(
        WAKE_INPUT_FORMAT,
        s_models,
        AFE_TYPE_SR,
        AFE_MODE_LOW_COST);
    if (config == NULL) return ESP_ERR_NO_MEM;

    config->ns_init = false;
    config->vad_init = true;
    config->vad_mode = VAD_MODE_0;
    config->vad_min_speech_ms = 128;
    config->vad_min_noise_ms = VAD_TRAILING_SILENCE_MS;
    config->vad_delay_ms = 160;
    s_afe = esp_afe_handle_from_config(config);
    if (s_afe == NULL) {
        afe_config_free(config);
        return ESP_FAIL;
    }

    s_afe_data = s_afe->create_from_config(config);
    afe_config_free(config);
    if (s_afe_data == NULL) return ESP_ERR_NO_MEM;

    s_events = xEventGroupCreate();
    if (s_events == NULL) return ESP_ERR_NO_MEM;

    s_callback = callback;
    s_detection_enabled = true;
    s_wake_armed = true;
    s_initialized = true;

    BaseType_t created = xTaskCreatePinnedToCore(
        feed_task,
        "wake_feed",
        WAKE_TASK_STACK_SIZE,
        NULL,
        WAKE_TASK_PRIORITY,
        &s_feed_task,
        0);
    if (created != pdPASS) return ESP_ERR_NO_MEM;

    created = xTaskCreatePinnedToCore(
        detect_task,
        "wake_detect",
        WAKE_TASK_STACK_SIZE,
        NULL,
        WAKE_TASK_PRIORITY,
        &s_detect_task,
        1);
    if (created != pdPASS) {
        vTaskDelete(s_feed_task);
        s_feed_task = NULL;
        return ESP_ERR_NO_MEM;
    }

    return ESP_OK;
}

esp_err_t wake_word_pause(void)
{
    if (!s_initialized || s_capture_active) return ESP_ERR_INVALID_STATE;

    s_wake_armed = false;
    s_afe->disable_wakenet(s_afe_data);
    s_detection_enabled = false;
    xEventGroupClearBits(s_events, FEED_PAUSED_BIT);
    EventBits_t bits = xEventGroupWaitBits(
        s_events,
        FEED_PAUSED_BIT,
        pdFALSE,
        pdTRUE,
        pdMS_TO_TICKS(PAUSE_TIMEOUT_MS));
    if ((bits & FEED_PAUSED_BIT) == 0) return ESP_ERR_TIMEOUT;

    s_afe->reset_buffer(s_afe_data);
    return ESP_OK;
}

esp_err_t wake_word_resume(void)
{
    if (!s_initialized || s_capture_active) return ESP_ERR_INVALID_STATE;

    if (!s_detection_enabled) s_afe->reset_buffer(s_afe_data);
    s_afe->reset_vad(s_afe_data);
    s_afe->enable_vad(s_afe_data);
    s_afe->enable_wakenet(s_afe_data);
    s_wake_armed = true;
    s_detection_enabled = true;
    return ESP_OK;
}

esp_err_t wake_word_capture_utterance(
    uint32_t speech_start_timeout_ms,
    uint32_t max_speech_ms,
    wake_utterance_t *utterance)
{
    if (!s_initialized || utterance == NULL || s_capture_active || s_wake_armed ||
        speech_start_timeout_ms == 0 || max_speech_ms == 0) {
        return ESP_ERR_INVALID_ARG;
    }

    memset(utterance, 0, sizeof(*utterance));
    const size_t capacity_samples =
        ((size_t)FRANKY_SAMPLE_RATE * max_speech_ms) / 1000;
    int16_t *samples = heap_caps_malloc(
        capacity_samples * sizeof(int16_t),
        MALLOC_CAP_SPIRAM | MALLOC_CAP_8BIT);
    if (samples == NULL) samples = malloc(capacity_samples * sizeof(int16_t));
    if (samples == NULL) return ESP_ERR_NO_MEM;

    xEventGroupClearBits(s_events, CAPTURE_COMPLETE_BIT);
    s_capture_samples = samples;
    s_capture_capacity_samples = capacity_samples;
    s_capture_sample_count = 0;
    s_capture_elapsed_samples = 0;
    s_capture_start_timeout_samples =
        ((size_t)FRANKY_SAMPLE_RATE * speech_start_timeout_ms) / 1000;
    s_capture_speech_started = false;
    s_capture_end_reason = WAKE_UTTERANCE_NO_SPEECH;
    s_afe->reset_vad(s_afe_data);
    s_afe->enable_vad(s_afe_data);
    s_capture_active = true;

    const TickType_t wait_ticks = pdMS_TO_TICKS(
        speech_start_timeout_ms + max_speech_ms + 3000);
    const EventBits_t bits = xEventGroupWaitBits(
        s_events,
        CAPTURE_COMPLETE_BIT,
        pdTRUE,
        pdTRUE,
        wait_ticks);

    if ((bits & CAPTURE_COMPLETE_BIT) == 0) {
        s_capture_active = false;
        free(samples);
        s_capture_samples = NULL;
        return ESP_ERR_TIMEOUT;
    }

    utterance->samples = samples;
    utterance->sample_count = s_capture_sample_count;
    utterance->end_reason = s_capture_end_reason;
    s_capture_samples = NULL;
    s_capture_capacity_samples = 0;
    s_capture_sample_count = 0;
    s_capture_elapsed_samples = 0;

    if (utterance->end_reason == WAKE_UTTERANCE_NO_SPEECH) {
        wake_word_release_utterance(utterance);
    }
    return ESP_OK;
}

void wake_word_release_utterance(wake_utterance_t *utterance)
{
    if (utterance == NULL) return;
    free(utterance->samples);
    utterance->samples = NULL;
    utterance->sample_count = 0;
}
