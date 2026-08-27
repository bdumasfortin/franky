#pragma once

#include <stdbool.h>

#include "esp_err.h"

typedef enum {
    SYSTEM_LED_OFFLINE = 0,
    SYSTEM_LED_IDLE,
    SYSTEM_LED_LISTENING,
    SYSTEM_LED_PROCESSING,
    SYSTEM_LED_SPEAKING,
    SYSTEM_LED_SUCCESS,
    SYSTEM_LED_ERROR,
    SYSTEM_LED_UPDATING,
} system_led_state_t;

esp_err_t system_led_init(void);
esp_err_t system_led_set_state(system_led_state_t state);
const char *system_led_state_name(system_led_state_t state);
bool system_led_state_from_name(const char *name, system_led_state_t *state);
