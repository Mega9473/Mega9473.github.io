#pragma once
#include <Arduino.h>
#include <driver/i2s.h>
#include "PortfolioHaptics.h"

namespace portfolio {
// 原本と同じBCK=19, WS=33, DOUT=22, DIN=23。実配線はREADME参照。
class I2sOutput {
public:
    bool begin(i2s_channel_fmt_t channels) {
        i2s_driver_uninstall(I2S_NUM_0); // 未導入ならESP_ERR_INVALID_STATE。旧実装と同じ所有前提。
        i2s_config_t config = {};
        config.mode = static_cast<i2s_mode_t>(I2S_MODE_MASTER | I2S_MODE_TX);
        config.sample_rate = kSampleRate;
        config.bits_per_sample = I2S_BITS_PER_SAMPLE_16BIT;
        config.channel_format = channels;
        config.communication_format = I2S_COMM_FORMAT_STAND_I2S;
        config.intr_alloc_flags = ESP_INTR_FLAG_LEVEL1;
        config.dma_buf_count = 8;
        config.dma_buf_len = 256;
        config.use_apll = false;
        config.tx_desc_auto_clear = true;
        esp_err_t error = i2s_driver_install(I2S_NUM_0, &config, 0, nullptr);
        if (error != ESP_OK) return fail("install", error);
        installed_ = true;
        i2s_pin_config_t pins = {};
        pins.mck_io_num = I2S_PIN_NO_CHANGE;
        pins.bck_io_num = 19;
        pins.ws_io_num = 33;
        pins.data_out_num = 22;
        pins.data_in_num = 23;
        error = i2s_set_pin(I2S_NUM_0, &pins);
        if (error != ESP_OK) return fail("pins", error);
        error = i2s_zero_dma_buffer(I2S_NUM_0);
        if (error != ESP_OK) return fail("zero buffer", error);
        ready_ = true;
        return true;
    }

    bool write(const int16_t* samples, std::size_t count) {
        if (!ready_) return false;
        const std::size_t bytes = count * sizeof(int16_t);
        std::size_t written = 0;
        const esp_err_t error = i2s_write(I2S_NUM_0, samples, bytes, &written, pdMS_TO_TICKS(100));
        if (error != ESP_OK || written != bytes) return fail("write / short write", error);
        return true;
    }
private:
    bool installed_ = false, ready_ = false;
    bool fail(const char* operation, esp_err_t error) {
        Serial.printf("I2S %s failed: %s\n", operation, esp_err_to_name(error));
        ready_ = false;
        if (installed_) { i2s_driver_uninstall(I2S_NUM_0); installed_ = false; }
        return false;
    }
};
}
