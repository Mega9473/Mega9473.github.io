#include <M5Atom.h>
#include <PortfolioHaptics.h>
#include <I2sOutput.h>

portfolio::VelocityController controller;
portfolio::SerialLevelParser parser;
portfolio::I2sOutput output;
int16_t samples[portfolio::kBufferSamples];
bool outputReady = false;
const uint8_t ledPositions[13][2] = {
    {0,0},{1,0},{2,0},{3,0},{4,0},
    {4,1},{3,1},{2,1},{1,1},{0,1},{0,2},{1,2},{2,2}
};

void showLevel(int level) {
    M5.dis.clear();
    for (int i = 0; i < level; ++i)
        M5.dis.drawpix(ledPositions[i][0], ledPositions[i][1], CRGB(0, 80, 0));
}

void setup() {
    M5.begin(true, false, true);
    Serial.begin(115200);
    M5.dis.clear();
    M5.dis.setBrightness(20);
    // 原本のALL_RIGHTを維持。実際のDACのチャンネル・サンプル周期は実機確認が必要。
    outputReady = output.begin(I2S_CHANNEL_FMT_ALL_RIGHT);
    if (!outputReady) M5.dis.fillpix(0xff00ff);
}

void loop() {
    M5.update();
    if (!outputReady) { delay(10); return; }
    while (Serial.available() > 0) {
        int level = 0;
        if (parser.push(static_cast<char>(Serial.read()), level) && controller.setLevel(level, millis()))
            showLevel(level);
    }
    controller.render(samples, portfolio::kBufferSamples, millis());
    outputReady = output.write(samples, portfolio::kBufferSamples);
    if (!outputReady) M5.dis.fillpix(0xff00ff);
}
