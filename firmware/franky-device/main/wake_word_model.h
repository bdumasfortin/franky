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
bool franky_wake_model_process(const int16_t *samples, size_t sample_count);

#ifdef __cplusplus
}
#endif
