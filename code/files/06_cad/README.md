# CAD / 3Dプリンタ用データ
ネイティブモデルと造形用データを用途別に整理しました。形状データの無断の改変はせず、校正治具の生成コードだけをリファクタリングしています。

| 例 | ファイル | プレビュー | 状態 |
|---|---|---|---|
| ハンドル・取付構造 | [Fusionモデル](01_handle/models/handle.f3d) | [Fusion保存プレビュー](01_handle/figures/fusion_preview.png) | 部品設計。造形完了は未確認 |
| ロードセル校正治具 | [生成コード](02_calibration_fixture/build_fixture.py) | [設計図](02_calibration_fixture/figures/fixture_preview.png) | CadQuery製の造形用設計 |
| 側面取付板 | [Fusionモデル](03_side_plate/models/side_plate.f3d) | [Fusion保存プレビュー](03_side_plate/figures/fusion_preview.png) | 用途・本人担当未確認の補欠 |

Fusionネイティブファイルは履歴を壊さないようコピーのまま収録しました。STLとF3Dは必ずしも同じ版ではありません。各モデルの完成実機との対応を確認したものではありません。

校正治具はCadQueryによる設計です。「Fusionを操作して製作した実績」とは説明しません。強度・摩擦・200 N荷重試験は実施していません。材質未指定で、200 Nは検証済み耐荷重ではありません。

`render_meshes.py` は同梱STLを読み、形状を変更せずPNG・SVGプレビューを生成します。numpyとmatplotlibが必要です。

