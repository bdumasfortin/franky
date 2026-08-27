#pragma once

#include <stddef.h>
#include <stdint.h>

#include "esp_err.h"

typedef void (*wake_word_detected_callback_t)(void);

typedef enum {
    WAKE_UTTERANCE_ENDED_BY_SILENCE = 0,
    WAKE_UTTERANCE_ENDED_BY_MAX_DURATION,
    WAKE_UTTERANCE_NO_SPEECH,
} wake_utterance_end_t;

typedef struct {
    int16_t *samples;
    size_t sample_count;
    wake_utterance_end_t end_reason;
} wake_utterance_t;

esp_err_t wake_word_init(wake_word_detected_callback_t callback);
esp_err_t wake_word_pause(void);
esp_err_t wake_word_resume(void);
const char *wake_word_engine_name(void);
const char *wake_word_phrase_id(void);
const char *wake_word_phrase_display_name(void);
esp_err_t wake_word_capture_utterance(
    uint32_t speech_start_timeout_ms,
    uint32_t max_speech_ms,
    wake_utterance_t *utterance);
void wake_word_release_utterance(wake_utterance_t *utterance);
