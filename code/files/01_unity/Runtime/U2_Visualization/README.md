# U2：実機の変位に連動するVR表示

CubeなどのGameObjectに `SpongeVisualizer` を追加します。Rendererは自動取得します。実機を使う場合は `ExperimentTcpBridge` の参照を設定してください。実機なしで確認する場合はUse Demo InputをONにしてDemo Displacement MmをInspectorで変更します。

移動量は `moveDirection × 変位 × movementScale` です。初期倍率は0.001（mm→m）。元実装と同じくmoveDirectionを正規化しないため、その長さも倍率に影響します。親のScaleにも注意してください。表示はメッシュ変形ではなく位置移動です。

微小な変位変化を除く `DisplacementSignal.Observe` と、生の変位による `Classify` を分けました。0.99→1.01 mmのように移動がデッドバンド内でも、色は1 mm境界をまたいだ時点で切り替わります。NearZeroがActiveより優先する判定順も維持しています。

Testsはデッドバンド、負値、境界値、色更新の分離を検査します。Unity実DLLでコンパイル済み。デモ入力は合成値であり、実験データや実機動作確認ではありません。
