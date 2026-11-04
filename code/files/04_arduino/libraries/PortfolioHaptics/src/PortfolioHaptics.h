#pragma once
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstring>

namespace portfolio {
constexpr double kPi = 3.14159265358979323846;
constexpr uint32_t kSampleRate = 44100;
constexpr std::size_t kBufferSamples = 256;
constexpr int16_t kMaxAmplitude = 32767;

// 位相はバッファ間・モード変更間で連続。無音時に位相を止めるA1の仕様を維持。
class SineOscillator {
public:
    void render(int16_t* samples, std::size_t count, double frequency,
                double amplitude, bool stereoPairs = false, bool freezeWhenSilent = false) {
        if (amplitude == 0 && freezeWhenSilent) {
            std::memset(samples, 0, count * sizeof(int16_t));
            return;
        }
        const double increment = frequency * 2.0 * kPi / kSampleRate;
        const std::size_t stride = stereoPairs ? 2 : 1;
        for (std::size_t i = 0; i < count; i += stride) {
            const auto sample = static_cast<int16_t>(amplitude * std::sin(phase_));
            samples[i] = sample;
            if (stereoPairs && i + 1 < count) samples[i + 1] = sample;
            phase_ += increment;
        }
        while (phase_ >= 2.0 * kPi) phase_ -= 2.0 * kPi;
    }
private:
    double phase_ = 0;
};

class ManualController {
public:
    static constexpr uint32_t kDoubleClickMs = 300;
    // 修正: 判定期限後の2回目を誤ってダブルクリック扱いしない。
    bool update(uint32_t now, bool pressed) {
        bool changed = false;
        if (pending_ && elapsed(now, firstClick_) > kDoubleClickMs) {
            pending_ = false;
            vibrating_ = !vibrating_;
            changed = true;
        }
        if (pressed) {
            if (pending_) {
                pending_ = false;
                mode_ = mode_ == 1 ? 2 : 1;
                changed = true;
            } else {
                pending_ = true;
                firstClick_ = now;
            }
        }
        return changed;
    }
    int mode() const { return mode_; }
    bool vibrating() const { return vibrating_; }
    double frequency() const { return mode_ == 1 ? 160.0 : 500.0; }
    void render(int16_t* samples, std::size_t count) {
        oscillator_.render(samples, count, frequency(), vibrating_ ? kMaxAmplitude : 0, true, true);
    }
private:
    static uint32_t elapsed(uint32_t now, uint32_t since) { return now - since; }
    int mode_ = 1;
    bool vibrating_ = false, pending_ = false;
    uint32_t firstClick_ = 0;
    SineOscillator oscillator_;
};

// 改行を区切りとする0〜13プロトコル。範囲外の正数は13に制限する。
// 修正: "-1"や不正文字列を"1"として受理しない。入力バッファの無制限成長を防ぐ。
class SerialLevelParser {
public:
    bool push(char c, int& result) {
        if (c == '\r') return false;
        if (c == '\n') {
            const bool valid = digits_ > 0 && !invalid_;
            if (valid) result = value_ > 13 ? 13 : value_;
            digits_ = 0; value_ = 0; invalid_ = false;
            return valid;
        }
        if (c < '0' || c > '9' || digits_ >= 6) { invalid_ = true; return false; }
        ++digits_;
        value_ = value_ * 10 + (c - '0');
        return false;
    }
private:
    int value_ = 0;
    unsigned digits_ = 0;
    bool invalid_ = false;
};

class VelocityController {
public:
    static constexpr int kMaxLevel = 13;
    static constexpr double kNormalHz = 250.0, kContactHz = 65.0;
    static constexpr uint32_t kContactMs = 100;
    static constexpr int kContactAmplitude = 30000;
    static constexpr double kFadeIn = 0.05, kFadeOut = 0.05;

    bool setLevel(int level, uint32_t now) {
        level = level < 0 ? 0 : level > kMaxLevel ? kMaxLevel : level;
        if (level == level_) return false;
        level_ = level;
        contact_ = level == 1;
        if (contact_) contactStart_ = now;
        // Arduino map()の整数除算を維持。
        targetAmplitude_ = level * static_cast<int>(kMaxAmplitude) / kMaxLevel;
        return true;
    }
    void render(int16_t* samples, std::size_t count, uint32_t now) {
        if (contact_ && static_cast<uint32_t>(now - contactStart_) >= kContactMs) contact_ = false;
        if (contact_) {
            contactOscillator_.render(samples, count, kContactHz, kContactAmplitude);
            return;
        }
        const double factor = targetAmplitude_ > currentAmplitude_ ? kFadeIn : kFadeOut;
        // 元コードと同じ1バッファにつき1回のフェード。
        currentAmplitude_ += (targetAmplitude_ - currentAmplitude_) * factor;
        normalOscillator_.render(samples, count, kNormalHz, currentAmplitude_);
    }
    int level() const { return level_; }
    bool contactActive() const { return contact_; }
    double amplitude() const { return currentAmplitude_; }
    double targetAmplitude() const { return targetAmplitude_; }
private:
    int level_ = -1;
    bool contact_ = false;
    uint32_t contactStart_ = 0;
    double targetAmplitude_ = 0, currentAmplitude_ = 0;
    SineOscillator normalOscillator_, contactOscillator_;
};
}
