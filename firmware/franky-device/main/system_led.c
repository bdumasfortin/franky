#include "system_led.h"

#include <stdbool.h>
#include <stdint.h>
#include <string.h>

#include "freertos/FreeRTOS.h"
#include "freertos/semphr.h"
#include "freertos/task.h"
#include "led_strip.h"

#define LED_STRIP_GPIO 38
#define LED_STRIP_COUNT 7
#define BREATH_FRAME_MS 40
#define BREATH_PHASE_STEPS 100

typedef struct {
    const char *name;
    uint8_t red;
    uint8_t green;
    uint8_t blue;
} state_color_t;

// Values are intentionally dim: seven full-bright pixels are distracting and
// draw considerably more USB power than a status indicator needs.
static const state_color_t s_colors[] = {
    [SYSTEM_LED_OFFLINE] = {"offline", 20, 6, 0},
    [SYSTEM_LED_IDLE] = {"idle", 0, 16, 18},
    [SYSTEM_LED_LISTENING] = {"listening", 0, 8, 36},
    [SYSTEM_LED_PROCESSING] = {"processing", 22, 0, 30},
    [SYSTEM_LED_SPEAKING] = {"speaking", 28, 19, 9},
    [SYSTEM_LED_SUCCESS] = {"success", 0, 28, 6},
    [SYSTEM_LED_ERROR] = {"error", 36, 0, 0},
    [SYSTEM_LED_UPDATING] = {"updating", 32, 8, 0},
};

static led_strip_handle_t s_strip;
static SemaphoreHandle_t s_lock;
static volatile system_led_state_t s_state = SYSTEM_LED_OFFLINE;

static esp_err_t render_color(state_color_t color, uint8_t brightness_percent)
{
    const uint8_t red = (uint8_t)((color.red * brightness_percent + 50) / 100);
    const uint8_t green = (uint8_t)((color.green * brightness_percent + 50) / 100);
    const uint8_t blue = (uint8_t)((color.blue * brightness_percent + 50) / 100);

    esp_err_t error = ESP_OK;
    for (int pixel = 0; pixel < LED_STRIP_COUNT && error == ESP_OK; ++pixel) {
        error = led_strip_set_pixel(s_strip, pixel, red, green, blue);
    }
    if (error == ESP_OK) {
        error = led_strip_refresh(s_strip);
    }
    return error;
}

static void breathing_task(void *argument)
{
    (void)argument;
    uint16_t phase = 0;

    while (true) {
        if (s_state == SYSTEM_LED_OFFLINE &&
            xSemaphoreTake(s_lock, pdMS_TO_TICKS(BREATH_FRAME_MS)) == pdTRUE) {
            if (s_state == SYSTEM_LED_OFFLINE) {
                const uint32_t triangle = phase <= BREATH_PHASE_STEPS / 2
                    ? phase * 2
                    : (BREATH_PHASE_STEPS - phase) * 2;
                // Smoothstep turns the linear triangle into a gentle inhale/exhale.
                const uint32_t eased =
                    (triangle * triangle * (300 - 2 * triangle) + 5000) / 10000;
                const uint8_t brightness = (uint8_t)(20 + (80 * eased) / 100);
                render_color(s_colors[SYSTEM_LED_OFFLINE], brightness);
                phase = (phase + 1) % BREATH_PHASE_STEPS;
            }
            xSemaphoreGive(s_lock);
        } else {
            phase = 0;
        }

        vTaskDelay(pdMS_TO_TICKS(BREATH_FRAME_MS));
    }
}

esp_err_t system_led_init(void)
{
    const led_strip_config_t strip_config = {
        .strip_gpio_num = LED_STRIP_GPIO,
        .max_leds = LED_STRIP_COUNT,
        .led_model = LED_MODEL_WS2812,
        .color_component_format = LED_STRIP_COLOR_COMPONENT_FMT_RGB,
        .flags.invert_out = false,
    };
    const led_strip_rmt_config_t rmt_config = {
        .clk_src = RMT_CLK_SRC_DEFAULT,
        .resolution_hz = 10 * 1000 * 1000,
        .mem_block_symbols = 0,
        .flags.with_dma = false,
    };

    esp_err_t error = led_strip_new_rmt_device(&strip_config, &rmt_config, &s_strip);
    if (error != ESP_OK) {
        return error;
    }

    s_lock = xSemaphoreCreateMutex();
    if (s_lock == NULL) {
        return ESP_ERR_NO_MEM;
    }

    if (xTaskCreate(breathing_task, "led_breath", 2048, NULL, 1, NULL) != pdPASS) {
        return ESP_ERR_NO_MEM;
    }

    return system_led_set_state(SYSTEM_LED_OFFLINE);
}

esp_err_t system_led_set_state(system_led_state_t state)
{
    if (s_strip == NULL || s_lock == NULL || state < 0 ||
        state >= (int)(sizeof(s_colors) / sizeof(s_colors[0]))) {
        return ESP_ERR_INVALID_ARG;
    }

    if (xSemaphoreTake(s_lock, pdMS_TO_TICKS(100)) != pdTRUE) {
        return ESP_ERR_TIMEOUT;
    }

    s_state = state;
    const uint8_t brightness = state == SYSTEM_LED_OFFLINE ? 20 : 100;
    const esp_err_t error = render_color(s_colors[state], brightness);

    xSemaphoreGive(s_lock);
    return error;
}

const char *system_led_state_name(system_led_state_t state)
{
    if (state < 0 || state >= (int)(sizeof(s_colors) / sizeof(s_colors[0]))) {
        return "unknown";
    }
    return s_colors[state].name;
}

bool system_led_state_from_name(const char *name, system_led_state_t *state)
{
    if (name == NULL || state == NULL) {
        return false;
    }

    for (int candidate = SYSTEM_LED_OFFLINE;
         candidate <= SYSTEM_LED_UPDATING;
         ++candidate) {
        if (strcmp(name, s_colors[candidate].name) == 0) {
            *state = (system_led_state_t)candidate;
            return true;
        }
    }
    return false;
}
