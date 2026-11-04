# Arduino / C++：振動提示サンプルと体重計成果物

| 対象 | 内容 |
|---|---|
| A1_ManualDemo | 1クリックでON/OFF、2クリックで160/500 Hzを切替 |
| A2_SerialSpeed | Unityの0〜13入力で接触65 Hz・100 msと通常250 Hzを制御 |
| A3_ScaleArtifact | 共同製作の体重計PDF・実機写真。コード原本は未発見 |

A1/A2は原本スケッチから状態管理と波形生成を `libraries/PortfolioHaptics` に分離しました。ADC/DAC・増幅器・振動子の接続図は原本に含まれず、I2Sのピンだけを保持しています。M5Atom Matrix自体に内蔵スピーカーがあるとは説明しません。

## 依存とコンパイル

検証環境はArduino CLI、M5Stack ESP32ボードパッケージ3.2.2、M5Atom 0.1.3、FastLED 3.10.2です。ボード識別子は `m5stack:esp32:m5stack_atom`。ESP32のlegacy I2S APIを使用します。

Arduino CLIで、このフォルダをカレントにして実行します。

```powershell
arduino-cli compile --fqbn m5stack:esp32:m5stack_atom --libraries ./libraries ./A1_ManualDemo
arduino-cli compile --fqbn m5stack:esp32:m5stack_atom --libraries ./libraries ./A2_SerialSpeed
```

Arduino IDEを使う場合は `libraries/PortfolioHaptics` をスケッチブックのlibrariesへコピーし、各フォルダの同名inoを開きます。M5Atom/FastLEDも必要です。

I2S: BCK=19、WS=33、DOUT=22、DIN=23、44,100 samples/s、16 bit、DMA 8×256。A1は左右同一サンプル、A2は原本のALL_RIGHTを保持。外部DAC・増幅器のチャンネル設定や実際の波形周期は実機で照合してください。書き込みや振動子の駆動は今回実施していません。

## 動作を維持した部分

160/500 Hz、65/250 Hz、最大振幅32767、接触振幅30000、接触100 ms、ダブルクリック300 ms、通常振幅のフェード0.05/バッファ、0〜13の整数map、位相の継続を保持しています。

## 原本からの修正

- 原本のI2S初期化は失敗してもtrueを返していました。各戻り値を確認し、失敗時にドライバを停止します。100 msの送信タイムアウトと短い書込みの検出も追加しました。障害時LEDはマゼンタです。
- A1は300 msを過ぎた2回目を誤ってダブルクリックと認識しないように期限を先に確定します。
- A2は数字以外を削除して `-1` を `1` にする挙動を修正し、不正行を破棄します。6桁を超える入力は破棄し、正常な範囲外正数は原本同様13へ制限します。
- ボーレート115200を明記しました。対象Unity送信側も115200に合わせてください。
- I2S形式定数は同じ標準I2Sを示す非deprecated名へ変更しています。

## 検証と結果

`Tests/run_host_tests.ps1` はVisual Studio 2019のC++コンパイラで、クリック境界・時刻周回・原本PCMとの一致・入力・接触時間・フェードの532項目を検査します。外部ボードは不要です。Arduinoのコンパイルログと検証結果はTestsおよびsummary.jsonを参照してください。

`Tests/waveform_simulation.csv` と `figures/waveform_simulation.png` はリファクタしたC++の波形生成関数が出力した**合成PCM**です。実機の測定値や触覚強度の評価結果ではありません。図は `Tests/plot_waveforms.py` で再生成できます。

出自とSHA-256はsource_provenance.jsonに記録しています。原本は変更していません。

