set(FRANKY_DEVICE_SOURCES
    "main.c"
    "audio_board.c"
    "system_led.c"
    "wake_word.c"
)
set(FRANKY_WAKE_MODEL "${CMAKE_CURRENT_SOURCE_DIR}/models/yo_franky.tflite")
set(FRANKY_SUUUPER_SFX "${CMAKE_CURRENT_SOURCE_DIR}/sfx/frankys-suuuper.pcm")
set(FRANKY_EMBED_FILES)
set(FRANKY_COMPILE_DEFINITIONS)

if(EXISTS "${FRANKY_WAKE_MODEL}")
    list(APPEND FRANKY_DEVICE_SOURCES "wake_word_model.cc")
    list(APPEND FRANKY_EMBED_FILES "models/yo_franky.tflite")
    list(APPEND FRANKY_COMPILE_DEFINITIONS FRANKY_HAS_CUSTOM_WAKE_MODEL=1)
else()
    list(APPEND FRANKY_COMPILE_DEFINITIONS FRANKY_HAS_CUSTOM_WAKE_MODEL=0)
endif()

if(EXISTS "${FRANKY_SUUUPER_SFX}")
    list(APPEND FRANKY_EMBED_FILES "sfx/frankys-suuuper.pcm")
    list(APPEND FRANKY_COMPILE_DEFINITIONS FRANKY_HAS_SUUUPER_SFX=1)
else()
    list(APPEND FRANKY_COMPILE_DEFINITIONS FRANKY_HAS_SUUUPER_SFX=0)
endif()

if(FRANKY_EMBED_FILES)
    idf_component_register(
        SRCS ${FRANKY_DEVICE_SOURCES}
        INCLUDE_DIRS "."
        REQUIRES esp_driver_i2c esp_driver_i2s esp_timer
        EMBED_FILES ${FRANKY_EMBED_FILES}
    )
else()
    idf_component_register(
        SRCS ${FRANKY_DEVICE_SOURCES}
        INCLUDE_DIRS "."
        REQUIRES esp_driver_i2c esp_driver_i2s esp_timer
    )
endif()

if(EXISTS "${FRANKY_WAKE_MODEL}")
    idf_component_get_property(
        FRANKY_TFLM_COMPONENT_DIR
        espressif__esp-tflite-micro
        COMPONENT_DIR
    )
    target_include_directories(
        ${COMPONENT_LIB}
        PRIVATE "${FRANKY_TFLM_COMPONENT_DIR}/examples/micro_speech/main"
    )
endif()

target_compile_definitions(${COMPONENT_LIB} PRIVATE ${FRANKY_COMPILE_DEFINITIONS})
