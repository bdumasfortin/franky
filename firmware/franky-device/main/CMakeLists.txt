set(FRANKY_DEVICE_SOURCES
    "main.c"
    "audio_board.c"
    "system_led.c"
    "wake_word.c"
)
set(FRANKY_WAKE_MODEL "${CMAKE_CURRENT_SOURCE_DIR}/models/yo_franky.tflite")

if(EXISTS "${FRANKY_WAKE_MODEL}")
    list(APPEND FRANKY_DEVICE_SOURCES "wake_word_model.cc")
    idf_component_register(
        SRCS ${FRANKY_DEVICE_SOURCES}
        INCLUDE_DIRS "."
        REQUIRES esp_driver_i2c esp_driver_i2s esp_timer
        EMBED_FILES "models/yo_franky.tflite"
    )

    idf_component_get_property(
        FRANKY_TFLM_COMPONENT_DIR
        espressif__esp-tflite-micro
        COMPONENT_DIR
    )
    target_include_directories(
        ${COMPONENT_LIB}
        PRIVATE "${FRANKY_TFLM_COMPONENT_DIR}/examples/micro_speech/main"
    )
    target_compile_definitions(${COMPONENT_LIB} PRIVATE FRANKY_HAS_CUSTOM_WAKE_MODEL=1)
else()
    idf_component_register(
        SRCS ${FRANKY_DEVICE_SOURCES}
        INCLUDE_DIRS "."
        REQUIRES esp_driver_i2c esp_driver_i2s esp_timer
    )
    target_compile_definitions(${COMPONENT_LIB} PRIVATE FRANKY_HAS_CUSTOM_WAKE_MODEL=0)
endif()
