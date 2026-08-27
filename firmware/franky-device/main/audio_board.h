#pragma once

#include <stddef.h>
#include <stdint.h>

#include "esp_err.h"

#define FRANKY_SAMPLE_RATE 16000
#define FRANKY_CHANNELS 2
#define FRANKY_RAW_CHANNELS 4

typedef enum {
    AUDIO_CUE_CONNECTED = 0,
    AUDIO_CUE_DISCONNECTED,
    AUDIO_CUE_WAKE_WORD,
} audio_cue_t;

typedef enum {
    AUDIO_SFX_FRANKY_SUUUPER = 0,
} audio_sfx_t;

esp_err_t audio_board_init(void);
esp_err_t audio_board_read_raw(int16_t *raw_samples, size_t frame_count);
esp_err_t audio_board_read_stereo(int16_t *stereo_samples, size_t frame_count);
esp_err_t audio_board_set_gain(float gain_db);
esp_err_t audio_board_play_cue(audio_cue_t cue);
esp_err_t audio_board_play_sfx(audio_sfx_t sfx);
