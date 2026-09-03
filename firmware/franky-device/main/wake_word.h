#pragma once

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#include "esp_err.h"

typedef void (*wake_word_detected_callback_t)(void);

typedef enum {
    WAKE_UTTERANCE_ENDED_BY_SILENCE = 0,
    WAKE_UTTERANCE_ENDED_BY_MAX_DURATION,
    WAKE_UTTERANCE_NO_SPEECH,
    WAKE_UTTERANCE_ENDED_BY_FIXED_DURATION,
} wake_utterance_end_t;

typedef struct {
    int16_t *samples;
    size_t sample_count;
    wake_utterance_end_t end_reason;
} wake_utterance_t;

esp_err_t wake_word_init(wake_word_detected_callback_t callback);
esp_err_t wake_word_pause(void);
esp_err_t wake_word_resume(void);
esp_err_t wake_word_set_threshold_percent(uint8_t threshold_percent);
uint8_t wake_word_get_threshold_percent(void);
esp_err_t wake_word_set_diagnostics(bool enabled);
bool wake_word_diagnostics_enabled(void);
const char *wake_word_engine_name(void);
const char *wake_word_phrase_id(void);
const char *wake_word_phrase_display_name(void);
esp_err_t wake_word_capture_utterance(
    uint32_t speech_start_timeout_ms,
    uint32_t max_speech_ms,
    wake_utterance_t *utterance);
esp_err_t wake_word_capture_sample(
    uint32_t duration_ms,
    wake_utterance_t *utterance,
    uint8_t *peak_score_percent);
void wake_word_release_utterance(wake_utterance_t *utterance);
