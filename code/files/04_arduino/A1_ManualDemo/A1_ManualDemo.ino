#include <M5Atom.h>
#include <PortfolioHaptics.h>
#include <I2sOutput.h>

portfolio::ManualController controller;
portfolio::I2sOutput output;
int16_t samples[portfolio::kBufferSamples];
bool outputReady = false;

const uint8_t digits[2][25] = {
    {0,0,1,0,0, 0,1,1,0,0, 0,0,1,0,0, 0,0,1,0,0, 0,1,1,1,0},
    {0,1,1,1,0, 0,0,0,1,0, 0,1,1,1,0, 0,1,0,0,0, 0,1,1,1,0}
};

void showState() {
    const uint32_t color = controller.vibrating() ? 0xff0000 : 0x00ff00;
    for (int i = 0; i < 25; ++i)
        M5.dis.drawpix(i, digits[controller.mode() - 1][i] ? color : 0);
}

void setup() {
    M5.begin(true, false, true);
    Serial.begin(115200);
    outputReady = output.begin(I2S_CHANNEL_FMT_RIGHT_LEFT);
    if (outputReady) showState();
    else M5.dis.fillpix(0xff00ff); // I2S障害
}

void loop() {
    M5.update();
    if (!outputReady) { delay(10); return; }
    if (controller.update(millis(), M5.Btn.wasPressed())) showState();
    controller.render(samples, portfolio::kBufferSamples);
    outputReady = output.write(samples, portfolio::kBufferSamples);
    if (!outputReady) M5.dis.fillpix(0xff00ff);
}
