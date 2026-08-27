#include "audio_board.h"

#include <assert.h>
#include <math.h>
#include <stdlib.h>

#include "driver/i2c_master.h"
#include "driver/i2s_std.h"
#include "esp_codec_dev.h"
#include "esp_codec_dev_defaults.h"
#include "esp_codec_dev_vol.h"
#include "freertos/FreeRTOS.h"
#include "freertos/semphr.h"

// Waveshare ESP32-S3-AUDIO-Board wiring from the manufacturer's ESP-IDF demo.
#define AUDIO_I2C_PORT I2C_NUM_0
#define AUDIO_I2C_SCL GPIO_NUM_10
#define AUDIO_I2C_SDA GPIO_NUM_11
#define AUDIO_I2S_PORT I2S_NUM_1
#define AUDIO_I2S_MCLK GPIO_NUM_12
#define AUDIO_I2S_BCLK GPIO_NUM_13
#define AUDIO_I2S_LRCK GPIO_NUM_14
#define AUDIO_I2S_DIN GPIO_NUM_15
#define AUDIO_I2S_DOUT GPIO_NUM_16

#define IO_EXPANDER_ADDRESS 0x20
#define IO_EXPANDER_OUTPUT_PORT_1_REGISTER 0x03
#define IO_EXPANDER_CONFIG_PORT_1_REGISTER 0x07
#define IO_EXPANDER_PA_CTRL_MASK (1U << 0)
#define IO_EXPANDER_I2C_TIMEOUT_MS 1000

#define READ_CHUNK_FRAMES 256
#define PLAY_CHUNK_FRAMES 256
#define DEFAULT_INPUT_GAIN_DB 30.0f
#define DEFAULT_OUTPUT_VOLUME 55
#define TONE_RAMP_FRAMES 64
#define TWO_PI 6.28318530717958647692f

static i2s_chan_handle_t s_tx_handle;
static i2s_chan_handle_t s_rx_handle;
static i2c_master_bus_handle_t s_i2c_bus;
static i2c_master_dev_handle_t s_io_expander;
static const audio_codec_data_if_t *s_audio_data_interface;
static esp_codec_dev_handle_t s_record_device;
static esp_codec_dev_handle_t s_play_device;
static SemaphoreHandle_t s_playback_mutex;

static esp_err_t init_i2c(void)
{
    const i2c_master_bus_config_t config = {
        .i2c_port = AUDIO_I2C_PORT,
        .sda_io_num = AUDIO_I2C_SDA,
        .scl_io_num = AUDIO_I2C_SCL,
        .clk_source = I2C_CLK_SRC_DEFAULT,
        .glitch_ignore_cnt = 7,
        .flags.enable_internal_pullup = true,
    };

    return i2c_new_master_bus(&config, &s_i2c_bus);
}

static esp_err_t read_io_expander_register(uint8_t register_address, uint8_t *value)
{
    return i2c_master_transmit_receive(
        s_io_expander,
        &register_address,
        sizeof(register_address),
        value,
        sizeof(*value),
        IO_EXPANDER_I2C_TIMEOUT_MS);
}

static esp_err_t write_io_expander_register(uint8_t register_address, uint8_t value)
{
    const uint8_t command[] = {register_address, value};
    return i2c_master_transmit(
        s_io_expander,
        command,
        sizeof(command),
        IO_EXPANDER_I2C_TIMEOUT_MS);
}

static esp_err_t enable_speaker_amplifier(void)
{
    const i2c_device_config_t device_config = {
        .dev_addr_length = I2C_ADDR_BIT_LEN_7,
        .device_address = IO_EXPANDER_ADDRESS,
        .scl_speed_hz = 400000,
    };
    esp_err_t error = i2c_master_bus_add_device(
        s_i2c_bus,
        &device_config,
        &s_io_expander);
    if (error != ESP_OK) return error;

    // PA_CTRL is TCA9555 P10 (board signal Extend_IO8), not a native ESP32
    // GPIO. Drive its output latch high before changing the pin direction so
    // a cold boot cannot leave the NS4150B amplifier disabled by its pulldown.
    uint8_t output_port_1 = 0;
    error = read_io_expander_register(
        IO_EXPANDER_OUTPUT_PORT_1_REGISTER,
        &output_port_1);
    if (error != ESP_OK) return error;

    output_port_1 |= IO_EXPANDER_PA_CTRL_MASK;
    error = write_io_expander_register(
        IO_EXPANDER_OUTPUT_PORT_1_REGISTER,
        output_port_1);
    if (error != ESP_OK) return error;

    uint8_t config_port_1 = 0;
    error = read_io_expander_register(
        IO_EXPANDER_CONFIG_PORT_1_REGISTER,
        &config_port_1);
    if (error != ESP_OK) return error;

    config_port_1 &= (uint8_t)~IO_EXPANDER_PA_CTRL_MASK;
    return write_io_expander_register(
        IO_EXPANDER_CONFIG_PORT_1_REGISTER,
        config_port_1);
}

static esp_err_t init_i2s(void)
{
    i2s_chan_config_t channel_config =
        I2S_CHANNEL_DEFAULT_CONFIG(AUDIO_I2S_PORT, I2S_ROLE_MASTER);
    // Silence a completed TX buffer before the DMA engine can reuse it. Without
    // this, the I2S peripheral repeats the last cue indefinitely on underrun.
    channel_config.auto_clear_after_cb = true;

    esp_err_t error = i2s_new_channel(&channel_config, &s_tx_handle, &s_rx_handle);
    if (error != ESP_OK) {
        return error;
    }

    const i2s_std_config_t stream_config = {
        .clk_cfg = I2S_STD_CLK_DEFAULT_CONFIG(FRANKY_SAMPLE_RATE),
        .slot_cfg = I2S_STD_PHILIPS_SLOT_DEFAULT_CONFIG(
            I2S_DATA_BIT_WIDTH_32BIT,
            I2S_SLOT_MODE_STEREO),
        .gpio_cfg = {
            .mclk = AUDIO_I2S_MCLK,
            .bclk = AUDIO_I2S_BCLK,
            .ws = AUDIO_I2S_LRCK,
            .dout = AUDIO_I2S_DOUT,
            .din = AUDIO_I2S_DIN,
            .invert_flags = {
                .mclk_inv = false,
                .bclk_inv = false,
                .ws_inv = false,
            },
        },
    };

    error = i2s_channel_init_std_mode(s_tx_handle, &stream_config);
    if (error != ESP_OK) {
        return error;
    }

    error = i2s_channel_init_std_mode(s_rx_handle, &stream_config);
    if (error != ESP_OK) {
        return error;
    }

    error = i2s_channel_enable(s_tx_handle);
    if (error != ESP_OK) {
        return error;
    }

    return i2s_channel_enable(s_rx_handle);
}

static esp_err_t init_input_codec(void)
{
    audio_codec_i2c_cfg_t i2c_config = {
        .addr = ES7210_CODEC_DEFAULT_ADDR,
        .bus_handle = s_i2c_bus,
    };
    const audio_codec_ctrl_if_t *control_interface = audio_codec_new_i2c_ctrl(&i2c_config);
    if (control_interface == NULL) {
        return ESP_ERR_NO_MEM;
    }

    es7210_codec_cfg_t codec_config = {
        .ctrl_if = control_interface,
        .mic_selected = ES7210_SEL_MIC1 | ES7210_SEL_MIC2 |
                        ES7210_SEL_MIC3 | ES7210_SEL_MIC4,
    };
    const audio_codec_if_t *codec_interface = es7210_codec_new(&codec_config);
    if (codec_interface == NULL) {
        return ESP_ERR_NO_MEM;
    }

    esp_codec_dev_cfg_t device_config = {
        .dev_type = ESP_CODEC_DEV_TYPE_IN,
        .codec_if = codec_interface,
        .data_if = s_audio_data_interface,
    };
    s_record_device = esp_codec_dev_new(&device_config);
    if (s_record_device == NULL) {
        return ESP_ERR_NO_MEM;
    }

    esp_codec_dev_sample_info_t sample_config = {
        .sample_rate = FRANKY_SAMPLE_RATE,
        .channel = 2,
        .bits_per_sample = 32,
    };
    if (esp_codec_dev_open(s_record_device, &sample_config) != ESP_CODEC_DEV_OK) {
        return ESP_FAIL;
    }

    return audio_board_set_gain(DEFAULT_INPUT_GAIN_DB);
}

static esp_err_t init_output_codec(void)
{
    audio_codec_i2c_cfg_t i2c_config = {
        .addr = ES8311_CODEC_DEFAULT_ADDR,
        .bus_handle = s_i2c_bus,
    };
    const audio_codec_ctrl_if_t *control_interface = audio_codec_new_i2c_ctrl(&i2c_config);
    const audio_codec_gpio_if_t *gpio_interface = audio_codec_new_gpio();
    if (control_interface == NULL || gpio_interface == NULL) {
        return ESP_ERR_NO_MEM;
    }

    es8311_codec_cfg_t codec_config = {
        .codec_mode = ESP_CODEC_DEV_WORK_MODE_DAC,
        .ctrl_if = control_interface,
        .gpio_if = gpio_interface,
        .pa_pin = -1,
        .use_mclk = false,
    };
    const audio_codec_if_t *codec_interface = es8311_codec_new(&codec_config);
    if (codec_interface == NULL) {
        return ESP_ERR_NO_MEM;
    }

    esp_codec_dev_cfg_t device_config = {
        .dev_type = ESP_CODEC_DEV_TYPE_OUT,
        .codec_if = codec_interface,
        .data_if = s_audio_data_interface,
    };
    s_play_device = esp_codec_dev_new(&device_config);
    if (s_play_device == NULL) {
        return ESP_ERR_NO_MEM;
    }

    esp_codec_dev_sample_info_t sample_config = {
        .sample_rate = FRANKY_SAMPLE_RATE,
        .channel = FRANKY_CHANNELS,
        .bits_per_sample = 32,
    };
    if (esp_codec_dev_open(s_play_device, &sample_config) != ESP_CODEC_DEV_OK ||
        esp_codec_dev_set_out_vol(s_play_device, DEFAULT_OUTPUT_VOLUME) != ESP_CODEC_DEV_OK ||
        esp_codec_dev_set_out_mute(s_play_device, true) != ESP_CODEC_DEV_OK) {
        return ESP_FAIL;
    }

    esp_err_t error = enable_speaker_amplifier();
    if (error != ESP_OK) {
        return error;
    }

    s_playback_mutex = xSemaphoreCreateMutex();
    return s_playback_mutex != NULL ? ESP_OK : ESP_ERR_NO_MEM;
}

esp_err_t audio_board_init(void)
{
    esp_err_t error = init_i2c();
    if (error != ESP_OK) {
        return error;
    }

    error = init_i2s();
    if (error != ESP_OK) {
        return error;
    }

    audio_codec_i2s_cfg_t i2s_config = {
        .port = AUDIO_I2S_PORT,
        .rx_handle = s_rx_handle,
        .tx_handle = s_tx_handle,
    };
    s_audio_data_interface = audio_codec_new_i2s_data(&i2s_config);
    if (s_audio_data_interface == NULL) {
        return ESP_ERR_NO_MEM;
    }

    error = init_input_codec();
    if (error != ESP_OK) {
        return error;
    }

    return init_output_codec();
}

static esp_err_t write_tone(float frequency_hz, uint32_t duration_ms, float level)
{
    const size_t total_frames =
        ((size_t)FRANKY_SAMPLE_RATE * duration_ms) / 1000;
    const float phase_step = TWO_PI * frequency_hz / FRANKY_SAMPLE_RATE;
    float phase = 0.0f;
    int32_t samples[PLAY_CHUNK_FRAMES * FRANKY_CHANNELS];

    for (size_t first_frame = 0; first_frame < total_frames;) {
        size_t chunk_frames = total_frames - first_frame;
        if (chunk_frames > PLAY_CHUNK_FRAMES) chunk_frames = PLAY_CHUNK_FRAMES;

        for (size_t chunk_frame = 0; chunk_frame < chunk_frames; ++chunk_frame) {
            const size_t frame = first_frame + chunk_frame;
            float envelope = 1.0f;
            if (frame < TONE_RAMP_FRAMES) {
                envelope = (float)frame / TONE_RAMP_FRAMES;
            }
            const size_t remaining = total_frames - frame - 1;
            if (remaining < TONE_RAMP_FRAMES) {
                const float release = (float)remaining / TONE_RAMP_FRAMES;
                if (release < envelope) envelope = release;
            }

            const int16_t sample =
                (int16_t)(sinf(phase) * envelope * level * 32767.0f);
            const int32_t output_sample = (int32_t)sample * 65536;
            samples[chunk_frame * FRANKY_CHANNELS] = output_sample;
            samples[chunk_frame * FRANKY_CHANNELS + 1] = output_sample;

            phase += phase_step;
            if (phase >= TWO_PI) phase -= TWO_PI;
        }

        const size_t chunk_bytes =
            chunk_frames * FRANKY_CHANNELS * sizeof(int32_t);
        if (esp_codec_dev_write(s_play_device, samples, chunk_bytes) != ESP_CODEC_DEV_OK) {
            return ESP_FAIL;
        }
        first_frame += chunk_frames;
    }

    return ESP_OK;
}

static esp_err_t write_silence(uint32_t duration_ms)
{
    int32_t samples[PLAY_CHUNK_FRAMES * FRANKY_CHANNELS] = {0};
    size_t remaining_frames =
        ((size_t)FRANKY_SAMPLE_RATE * duration_ms) / 1000;

    while (remaining_frames > 0) {
        size_t chunk_frames = remaining_frames;
        if (chunk_frames > PLAY_CHUNK_FRAMES) chunk_frames = PLAY_CHUNK_FRAMES;
        const size_t chunk_bytes =
            chunk_frames * FRANKY_CHANNELS * sizeof(int32_t);
        if (esp_codec_dev_write(s_play_device, samples, chunk_bytes) != ESP_CODEC_DEV_OK) {
            return ESP_FAIL;
        }
        remaining_frames -= chunk_frames;
    }

    return ESP_OK;
}

esp_err_t audio_board_play_cue(audio_cue_t cue)
{
    if (s_play_device == NULL || s_playback_mutex == NULL) {
        return ESP_ERR_INVALID_STATE;
    }
    if (cue != AUDIO_CUE_CONNECTED && cue != AUDIO_CUE_DISCONNECTED &&
        cue != AUDIO_CUE_WAKE_WORD) {
        return ESP_ERR_INVALID_ARG;
    }

    if (xSemaphoreTake(s_playback_mutex, portMAX_DELAY) != pdTRUE) {
        return ESP_ERR_TIMEOUT;
    }

    esp_err_t error = ESP_OK;
    if (esp_codec_dev_set_out_mute(s_play_device, false) != ESP_CODEC_DEV_OK) {
        error = ESP_FAIL;
    } else if (cue == AUDIO_CUE_CONNECTED) {
        error = write_tone(622.25f, 70, 0.20f);
        if (error == ESP_OK) error = write_silence(12);
        if (error == ESP_OK) error = write_tone(830.61f, 100, 0.20f);
    } else if (cue == AUDIO_CUE_DISCONNECTED) {
        error = write_tone(830.61f, 70, 0.20f);
        if (error == ESP_OK) error = write_silence(12);
        if (error == ESP_OK) error = write_tone(622.25f, 100, 0.20f);
    } else {
        error = write_tone(987.77f, 55, 0.22f);
        if (error == ESP_OK) error = write_silence(18);
        if (error == ESP_OK) error = write_tone(1318.51f, 90, 0.22f);
    }

    // Push enough zero samples to drain every non-silent DMA descriptor, then
    // hardware-mute the codec as a second guard against an output underrun.
    if (error == ESP_OK) error = write_silence(100);
    const int mute_result = esp_codec_dev_set_out_mute(s_play_device, true);
    if (error == ESP_OK && mute_result != ESP_CODEC_DEV_OK) error = ESP_FAIL;

    xSemaphoreGive(s_playback_mutex);
    return error;
}

esp_err_t audio_board_read_stereo(int16_t *stereo_samples, size_t frame_count)
{
    if (stereo_samples == NULL || frame_count == 0 ||
        frame_count > READ_CHUNK_FRAMES) {
        return ESP_ERR_INVALID_ARG;
    }

    int16_t raw_samples[READ_CHUNK_FRAMES * FRANKY_RAW_CHANNELS];
    esp_err_t error = audio_board_read_raw(raw_samples, frame_count);
    if (error != ESP_OK) return error;

    // The manufacturer demo describes the four 16-bit words as reference,
    // microphone A, unused, microphone B. Export the two microphones as stereo.
    for (size_t frame = 0; frame < frame_count; ++frame) {
        stereo_samples[frame * 2] = raw_samples[frame * FRANKY_RAW_CHANNELS + 1];
        stereo_samples[frame * 2 + 1] = raw_samples[frame * FRANKY_RAW_CHANNELS + 3];
    }

    return ESP_OK;
}

esp_err_t audio_board_read_raw(int16_t *raw_samples, size_t frame_count)
{
    if (raw_samples == NULL || frame_count == 0) {
        return ESP_ERR_INVALID_ARG;
    }

    size_t frames_read = 0;
    while (frames_read < frame_count) {
        size_t chunk_frames = frame_count - frames_read;
        if (chunk_frames > READ_CHUNK_FRAMES) chunk_frames = READ_CHUNK_FRAMES;

        int16_t *chunk = raw_samples + frames_read * FRANKY_RAW_CHANNELS;
        const size_t chunk_bytes =
            chunk_frames * FRANKY_RAW_CHANNELS * sizeof(int16_t);
        if (esp_codec_dev_read(s_record_device, chunk, chunk_bytes) != ESP_CODEC_DEV_OK) {
            return ESP_FAIL;
        }

        frames_read += chunk_frames;
    }

    return ESP_OK;
}

esp_err_t audio_board_set_gain(float gain_db)
{
    if (s_record_device == NULL || gain_db < 0.0f || gain_db > 30.0f) {
        return ESP_ERR_INVALID_ARG;
    }

    for (int channel = 0; channel < 4; ++channel) {
        if (esp_codec_dev_set_in_channel_gain(
                s_record_device,
                ESP_CODEC_DEV_MAKE_CHANNEL_MASK(channel),
                gain_db) != ESP_CODEC_DEV_OK) {
            return ESP_FAIL;
        }
    }

    return ESP_OK;
}
