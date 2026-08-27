idf_component_register(
    SRCS "main.c" "audio_board.c" "system_led.c" "wake_word.c"
    INCLUDE_DIRS "."
    REQUIRES esp_driver_i2c esp_driver_i2s esp_timer
)
