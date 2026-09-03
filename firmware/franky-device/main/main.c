#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

#include "audio_board.h"
#include "esp_check.h"
#include "esp_heap_caps.h"
#include "esp_log.h"
#include "esp_timer.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "system_led.h"
#include "wake_word.h"

#define MIN_RECORDING_MS 500
#define MAX_RECORDING_MS 30000
#define RECORDING_CHUNK_FRAMES 256
#define DEFAULT_GAIN_DB 30.0f
#define HOST_CONNECTION_TIMEOUT_MS 3500
#define WAKE_ACKNOWLEDGEMENT_MS 75
#define WAKE_SPEECH_START_TIMEOUT_MS 4000
#define WAKE_MAX_SPEECH_MS 20000
#define WAKE_ACTION_TASK_STACK_SIZE 8192
#define FRANKY_PROTOCOL_VERSION 5
#define SUUUPER_SFX_NAME "frankys_suuuper"

typedef struct {
    uint32_t duration_ms;
} recording_request_t;

static volatile bool s_recording;
static volatile bool s_stop_requested;
static volatile bool s_host_connected;
static volatile bool s_wake_action_pending;
static volatile bool s_wake_sample_pending;
static volatile uint32_t s_last_host_contact_ms;
static float s_gain_db = DEFAULT_GAIN_DB;

static void start_recording(uint32_t duration_ms);
static uint32_t now_ms(void);
static void show_state(system_led_state_t state);
static void show_resting_state(void);

static void print_device_info(void)
{
    printf(
        "READY FRANKY_DEVICE %u %u %u 16 %.1f\n",
        FRANKY_PROTOCOL_VERSION,
        FRANKY_SAMPLE_RATE,
        FRANKY_CHANNELS,
        s_gain_db);
    printf(
        "WAKE_ENGINE %s %s\n",
        wake_word_engine_name(),
        wake_word_phrase_id());
    if (strcmp(wake_word_engine_name(), "microwakeword") == 0) {
        printf("CAPABILITIES wake_capture named_sfx wake_threshold wake_diagnostics wake_sample wake_sample_score\n");
        printf("WAKE_THRESHOLD %u\n", wake_word_get_threshold_percent());
        printf(
            "WAKE_DIAGNOSTICS %s\n",
            wake_word_diagnostics_enabled() ? "ON" : "OFF");
    } else {
        printf("CAPABILITIES wake_capture named_sfx\n");
    }
}

static void play_named_sfx(const char *sfx_name)
{
    if (strcmp(sfx_name, SUUUPER_SFX_NAME) != 0) {
        printf("ERROR unknown_sfx\n");
        show_state(SYSTEM_LED_ERROR);
        return;
    }
    if (s_recording || s_wake_action_pending || s_wake_sample_pending) {
        printf("ERROR audio_capture_in_progress\n");
        return;
    }

    esp_err_t pause_error = wake_word_pause();
    if (pause_error != ESP_OK) {
        printf("ERROR wake_detector_did_not_pause\n");
        show_state(SYSTEM_LED_ERROR);
        wake_word_resume();
        return;
    }

    printf("SFX_START %s\n", sfx_name);
    show_state(SYSTEM_LED_SPEAKING);
    esp_err_t playback_error = audio_board_play_sfx(AUDIO_SFX_FRANKY_SUUUPER);
    if (playback_error == ESP_OK) {
        printf("SFX_DONE %s\n", sfx_name);
    } else {
        printf("ERROR sfx_playback_failed_%s\n", esp_err_to_name(playback_error));
        show_state(SYSTEM_LED_ERROR);
    }

    wake_word_resume();
    s_last_host_contact_ms = now_ms();
    show_resting_state();
}

static uint32_t now_ms(void)
{
    return (uint32_t)(esp_timer_get_time() / 1000);
}

static void show_state(system_led_state_t state)
{
    if (system_led_set_state(state) == ESP_OK) {
        printf("STATE %s\n", system_led_state_name(state));
    }
}

static void show_resting_state(void)
{
    show_state(s_host_connected ? SYSTEM_LED_IDLE : SYSTEM_LED_OFFLINE);
}

static void play_status_cue(audio_cue_t cue, const char *cue_name)
{
    if (s_recording || s_wake_action_pending || s_wake_sample_pending) return;

    esp_err_t pause_error = wake_word_pause();
    if (pause_error != ESP_OK) {
        ESP_LOGW("audio_cue", "Could not pause wake detector for %s cue: %s",
                 cue_name, esp_err_to_name(pause_error));
        wake_word_resume();
        return;
    }

    esp_err_t cue_error = audio_board_play_cue(cue);
    if (cue_error != ESP_OK) {
        ESP_LOGW("audio_cue", "Could not play %s cue: %s",
                 cue_name, esp_err_to_name(cue_error));
    }
    wake_word_resume();
}

static void note_host_contact(void)
{
    const bool reconnected = !s_host_connected;
    s_host_connected = true;
    s_last_host_contact_ms = now_ms();
    if (reconnected && !s_recording && !s_wake_sample_pending) {
        system_led_set_state(SYSTEM_LED_IDLE);
        play_status_cue(AUDIO_CUE_CONNECTED, "connection");
    }
}

static void recording_task(void *argument)
{
    recording_request_t *request = argument;
    const size_t requested_frames =
        ((size_t)FRANKY_SAMPLE_RATE * request->duration_ms) / 1000;
    const size_t capacity_bytes =
        requested_frames * FRANKY_CHANNELS * sizeof(int16_t);
    free(request);

    int16_t *recording = heap_caps_malloc(capacity_bytes, MALLOC_CAP_SPIRAM | MALLOC_CAP_8BIT);
    if (recording == NULL) {
        recording = malloc(capacity_bytes);
    }

    if (recording == NULL) {
        printf("ERROR not_enough_memory\n");
        show_state(SYSTEM_LED_ERROR);
        s_recording = false;
        wake_word_resume();
        vTaskDelete(NULL);
        return;
    }

    show_state(SYSTEM_LED_LISTENING);
    printf("RECORDING %u\n", (unsigned)((requested_frames * 1000) / FRANKY_SAMPLE_RATE));

    size_t captured_frames = 0;
    while (captured_frames < requested_frames && !s_stop_requested) {
        size_t frames = requested_frames - captured_frames;
        if (frames > RECORDING_CHUNK_FRAMES) {
            frames = RECORDING_CHUNK_FRAMES;
        }

        esp_err_t error = audio_board_read_stereo(
            recording + captured_frames * FRANKY_CHANNELS,
            frames);
        if (error != ESP_OK) {
            printf("ERROR audio_read_failed_%s\n", esp_err_to_name(error));
            show_state(SYSTEM_LED_ERROR);
            free(recording);
            s_recording = false;
            wake_word_resume();
            vTaskDelete(NULL);
            return;
        }

        captured_frames += frames;
    }

    const size_t captured_bytes =
        captured_frames * FRANKY_CHANNELS * sizeof(int16_t);
    show_state(SYSTEM_LED_PROCESSING);
    printf(
        "AUDIO %u %u %u 16\n",
        (unsigned)captured_bytes,
        FRANKY_SAMPLE_RATE,
        FRANKY_CHANNELS);
    fwrite(recording, 1, captured_bytes, stdout);
    printf("\nEND\n");

    free(recording);
    s_stop_requested = false;
    s_recording = false;
    wake_word_resume();
    show_resting_state();
    vTaskDelete(NULL);
}

static void start_recording(uint32_t duration_ms)
{
    if (s_recording || s_wake_sample_pending) {
        printf("ERROR already_recording\n");
        show_state(SYSTEM_LED_ERROR);
        return;
    }

    if (s_wake_action_pending) {
        printf("ERROR wake_acknowledgement_in_progress\n");
        return;
    }

    if (duration_ms < MIN_RECORDING_MS || duration_ms > MAX_RECORDING_MS) {
        printf("ERROR duration_must_be_500_to_30000_ms\n");
        show_state(SYSTEM_LED_ERROR);
        return;
    }

    esp_err_t pause_error = wake_word_pause();
    if (pause_error != ESP_OK) {
        printf("ERROR wake_detector_did_not_pause\n");
        show_state(SYSTEM_LED_ERROR);
        wake_word_resume();
        return;
    }

    recording_request_t *request = malloc(sizeof(recording_request_t));
    if (request == NULL) {
        printf("ERROR not_enough_memory\n");
        show_state(SYSTEM_LED_ERROR);
        wake_word_resume();
        return;
    }

    request->duration_ms = duration_ms;
    s_stop_requested = false;
    s_recording = true;

    BaseType_t task_created = xTaskCreatePinnedToCore(
        recording_task,
        "record_audio",
        4096,
        request,
        5,
        NULL,
        1);
    if (task_created != pdPASS) {
        free(request);
        s_recording = false;
        printf("ERROR could_not_start_recording\n");
        show_state(SYSTEM_LED_ERROR);
        wake_word_resume();
    }
}

static void wake_sample_task(void *argument)
{
    recording_request_t *request = argument;
    const uint32_t duration_ms = request->duration_ms;
    free(request);

    printf("WAKE_SAMPLE_START %u\n", (unsigned)duration_ms);
    show_state(SYSTEM_LED_LISTENING);
    wake_utterance_t sample = {0};
    uint8_t peak_score_percent = 0;
    esp_err_t capture_error = wake_word_capture_sample(
        duration_ms,
        &sample,
        &peak_score_percent);

    if (capture_error == ESP_OK) {
        const size_t captured_bytes = sample.sample_count * sizeof(int16_t);
        show_state(SYSTEM_LED_PROCESSING);
        printf("WAKE_SAMPLE_SCORE %u\n", peak_score_percent);
        printf(
            "AUDIO %u %u 1 16\n",
            (unsigned)captured_bytes,
            FRANKY_SAMPLE_RATE);
        fwrite(sample.samples, 1, captured_bytes, stdout);
        printf("\nEND\n");
    } else {
        printf("ERROR wake_sample_failed_%s\n", esp_err_to_name(capture_error));
        show_state(SYSTEM_LED_ERROR);
    }

    wake_word_release_utterance(&sample);
    esp_err_t resume_error = wake_word_resume();
    if (resume_error != ESP_OK) {
        printf("ERROR wake_detector_did_not_resume\n");
        show_state(SYSTEM_LED_ERROR);
    } else {
        show_resting_state();
    }
    s_wake_sample_pending = false;
    vTaskDelete(NULL);
}

static void start_wake_sample(uint32_t duration_ms)
{
    if (s_recording || s_wake_action_pending || s_wake_sample_pending) {
        printf("ERROR audio_capture_in_progress\n");
        return;
    }
    if (duration_ms < 500 || duration_ms > 5000) {
        printf("ERROR wake_sample_duration_must_be_500_to_5000_ms\n");
        return;
    }
    if (strcmp(wake_word_engine_name(), "microwakeword") != 0) {
        printf("ERROR wake_sample_not_supported\n");
        return;
    }

    recording_request_t *request = malloc(sizeof(recording_request_t));
    if (request == NULL) {
        printf("ERROR not_enough_memory\n");
        return;
    }
    request->duration_ms = duration_ms;
    s_wake_sample_pending = true;
    BaseType_t task_created = xTaskCreatePinnedToCore(
        wake_sample_task,
        "wake_sample",
        6144,
        request,
        6,
        NULL,
        1);
    if (task_created != pdPASS) {
        free(request);
        s_wake_sample_pending = false;
        printf("ERROR could_not_start_wake_sample\n");
    }
}

static void wake_action_task(void *argument)
{
    (void)argument;
    printf("WAKE %s\n", wake_word_phrase_id());
    show_state(SYSTEM_LED_SUCCESS);
    esp_err_t cue_error = audio_board_play_cue(AUDIO_CUE_WAKE_WORD);
    if (cue_error != ESP_OK) {
        ESP_LOGW("audio_cue", "Could not play wake-word cue: %s",
                 esp_err_to_name(cue_error));
    }
    vTaskDelay(pdMS_TO_TICKS(WAKE_ACKNOWLEDGEMENT_MS));

    if (!s_host_connected) {
        s_wake_action_pending = false;
        wake_word_resume();
        show_resting_state();
        vTaskDelete(NULL);
        return;
    }

    show_state(SYSTEM_LED_LISTENING);
    printf(
        "UTTERANCE_START %u %u\n",
        WAKE_SPEECH_START_TIMEOUT_MS,
        WAKE_MAX_SPEECH_MS);

    wake_utterance_t utterance = {0};
    esp_err_t capture_error = wake_word_capture_utterance(
        WAKE_SPEECH_START_TIMEOUT_MS,
        WAKE_MAX_SPEECH_MS,
        &utterance);

    if (capture_error != ESP_OK) {
        printf("ERROR utterance_capture_failed_%s\n", esp_err_to_name(capture_error));
        show_state(SYSTEM_LED_ERROR);
    } else if (utterance.end_reason == WAKE_UTTERANCE_NO_SPEECH) {
        printf("NO_SPEECH\n");
    } else {
        const char *reason = utterance.end_reason == WAKE_UTTERANCE_ENDED_BY_MAX_DURATION
            ? "max_duration"
            : "silence";
        const size_t captured_bytes = utterance.sample_count * sizeof(int16_t);
        show_state(SYSTEM_LED_PROCESSING);
        printf("UTTERANCE_END %s\n", reason);
        printf(
            "AUDIO %u %u 1 16\n",
            (unsigned)captured_bytes,
            FRANKY_SAMPLE_RATE);
        fwrite(utterance.samples, 1, captured_bytes, stdout);
        printf("\nEND\n");
    }

    wake_word_release_utterance(&utterance);
    s_wake_action_pending = false;
    wake_word_resume();
    show_resting_state();

    vTaskDelete(NULL);
}

static void wake_word_detected(void)
{
    if (s_recording || s_wake_action_pending || s_wake_sample_pending) {
        wake_word_resume();
        return;
    }

    s_wake_action_pending = true;
    BaseType_t task_created = xTaskCreatePinnedToCore(
        wake_action_task,
        "wake_action",
        WAKE_ACTION_TASK_STACK_SIZE,
        NULL,
        6,
        NULL,
        1);
    if (task_created != pdPASS) {
        s_wake_action_pending = false;
        printf("ERROR could_not_start_wake_action\n");
        show_state(SYSTEM_LED_ERROR);
        wake_word_resume();
    }
}

static void handle_command(char *line)
{
    line[strcspn(line, "\r\n")] = '\0';

    if (line[0] == '\0') {
        return;
    }

    note_host_contact();

    if (strcmp(line, "PING") == 0) {
        return;
    }

    if (strcmp(line, "BYE") == 0) {
        s_host_connected = false;
        system_led_set_state(SYSTEM_LED_OFFLINE);
        play_status_cue(AUDIO_CUE_DISCONNECTED, "disconnection");
        printf("BYE\n");
        return;
    }

    if (strcmp(line, "HELLO") == 0 || strcmp(line, "INFO") == 0) {
        if (!s_recording && !s_wake_sample_pending) {
            show_state(SYSTEM_LED_IDLE);
        }
        print_device_info();
        return;
    }

    if (strcmp(line, "STOP") == 0) {
        if (s_recording) {
            s_stop_requested = true;
            printf("STOPPING\n");
        } else if (s_wake_sample_pending) {
            printf("ERROR wake_sample_finishes_automatically\n");
        } else {
            printf("IDLE\n");
            show_state(SYSTEM_LED_IDLE);
        }
        return;
    }

    unsigned duration_ms = 0;
    if (sscanf(line, "RECORD %u", &duration_ms) == 1) {
        start_recording(duration_ms);
        return;
    }

    unsigned wake_sample_duration_ms = 0;
    if (sscanf(line, "WAKE_SAMPLE %u", &wake_sample_duration_ms) == 1) {
        start_wake_sample(wake_sample_duration_ms);
        return;
    }

    float gain_db = 0.0f;
    if (sscanf(line, "GAIN %f", &gain_db) == 1) {
        if (s_recording || s_wake_action_pending || s_wake_sample_pending) {
            printf("ERROR cannot_change_gain_while_recording\n");
            show_state(SYSTEM_LED_ERROR);
        } else {
            esp_err_t error = audio_board_set_gain(gain_db);
            if (error == ESP_OK) {
                s_gain_db = gain_db;
                printf("GAIN %.1f\n", s_gain_db);
            } else {
                printf("ERROR gain_must_be_0_to_30_db\n");
                show_state(SYSTEM_LED_ERROR);
            }
        }
        return;
    }

    char sfx_name[32];
    if (sscanf(line, "SFX %31s", sfx_name) == 1) {
        play_named_sfx(sfx_name);
        return;
    }

    unsigned threshold_percent = 0;
    if (sscanf(line, "WAKE_THRESHOLD %u", &threshold_percent) == 1) {
        if (s_recording || s_wake_action_pending || s_wake_sample_pending) {
            printf("ERROR cannot_change_wake_threshold_while_active\n");
            return;
        }
        if (threshold_percent > UINT8_MAX) {
            printf("ERROR wake_threshold_must_be_50_to_99_percent\n");
            return;
        }

        esp_err_t pause_error = wake_word_pause();
        if (pause_error != ESP_OK) {
            printf("ERROR wake_detector_did_not_pause\n");
            wake_word_resume();
            return;
        }
        esp_err_t threshold_error = wake_word_set_threshold_percent(
            (uint8_t)threshold_percent);
        esp_err_t resume_error = wake_word_resume();
        if (threshold_error == ESP_OK && resume_error == ESP_OK) {
            printf("WAKE_THRESHOLD %u\n", wake_word_get_threshold_percent());
        } else if (threshold_error == ESP_ERR_INVALID_ARG) {
            printf("ERROR wake_threshold_must_be_50_to_99_percent\n");
        } else if (threshold_error == ESP_ERR_NOT_SUPPORTED) {
            printf("ERROR wake_threshold_not_supported\n");
        } else {
            printf("ERROR wake_threshold_change_failed\n");
        }
        return;
    }

    char diagnostics_state[8];
    if (sscanf(line, "WAKE_DIAGNOSTICS %7s", diagnostics_state) == 1) {
        bool enabled;
        if (strcmp(diagnostics_state, "ON") == 0) {
            enabled = true;
        } else if (strcmp(diagnostics_state, "OFF") == 0) {
            enabled = false;
        } else {
            printf("ERROR wake_diagnostics_must_be_on_or_off\n");
            return;
        }

        esp_err_t error = wake_word_set_diagnostics(enabled);
        if (error == ESP_OK) {
            printf("WAKE_DIAGNOSTICS %s\n", enabled ? "ON" : "OFF");
        } else {
            printf("ERROR wake_diagnostics_not_supported\n");
        }
        return;
    }

    char state_name[24];
    if (sscanf(line, "STATE %23s", state_name) == 1) {
        system_led_state_t state;
        if (s_recording || s_wake_action_pending || s_wake_sample_pending) {
            printf("ERROR cannot_preview_state_while_recording\n");
        } else if (system_led_state_from_name(state_name, &state)) {
            show_state(state);
        } else {
            printf("ERROR unknown_led_state\n");
            show_state(SYSTEM_LED_ERROR);
        }
        return;
    }

    printf("ERROR unknown_command\n");
    show_state(SYSTEM_LED_ERROR);
}

void app_main(void)
{
    setvbuf(stdin, NULL, _IONBF, 0);
    setvbuf(stdout, NULL, _IONBF, 0);

    ESP_ERROR_CHECK(system_led_init());
    ESP_ERROR_CHECK(audio_board_init());
    esp_log_level_set("*", ESP_LOG_WARN);
    ESP_ERROR_CHECK(wake_word_init(wake_word_detected));

    print_device_info();

    char command[96];
    while (true) {
        if (fgets(command, sizeof(command), stdin) != NULL) {
            handle_command(command);
        } else {
            clearerr(stdin);
            vTaskDelay(pdMS_TO_TICKS(20));
        }

        if (s_host_connected && !s_recording &&
            (uint32_t)(now_ms() - s_last_host_contact_ms) > HOST_CONNECTION_TIMEOUT_MS) {
            s_host_connected = false;
            system_led_set_state(SYSTEM_LED_OFFLINE);
            play_status_cue(AUDIO_CUE_DISCONNECTED, "disconnection");
        }
    }
}
