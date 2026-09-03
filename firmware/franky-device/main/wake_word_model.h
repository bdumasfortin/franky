#pragma once

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#include "esp_err.h"

#ifdef __cplusplus
extern "C" {
#endif

esp_err_t franky_wake_model_init(void);
void franky_wake_model_reset(void);
esp_err_t franky_wake_model_set_threshold_percent(uint8_t threshold_percent);
uint8_t franky_wake_model_get_threshold_percent(void);
uint8_t franky_wake_model_get_last_score_percent(void);
uint8_t franky_wake_model_get_peak_score_percent(void);
bool franky_wake_model_process(const int16_t *samples, size_t sample_count);
void franky_wake_model_score_samples(const int16_t *samples, size_t sample_count);

#ifdef __cplusplus
}
#endif
