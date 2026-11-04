# Rによる研究データ解析：再実行できる3つの作例

選定した研究コードの統計モデルを保ち、入力・解析・作図を分けたファイル集です。実データの必要列だけをコピーし、参加者名を研究ごとにP01以降へ置き換えました。対応表、入力元のファイル名、絶対パス、回答時刻は同梱していません。元ファイルは変更していません。

`output/` の図は、このフォルダの匿名化CSVからモデルを再推定して生成したものです。過去に保存されていた論文図のコピーではありません。R1は過去のk=12版とは分析条件が異なるため、その図を混ぜずに再生成しました。

## 作例と入口

| 作例 | 主なコード | 生成図 |
|---|---|---|
| R1 主観評定のLMM | `R/r1_subjective_lmm.R` | `output/r1/figures/Subjective_*.png` / `.svg`：3指標 |
| R2 客観指標のLMM | `R/r2_objective_lmm.R`、`R/si_inputs.R` | `output/r2/figures/R2_objective_EMMs.*`、`R2_RMS_descriptive.*` |
| R3 7段階評定のCLMM | `R/r3_subjective_clmm.R`、`R/si_inputs.R` | `output/r3/figures/R3_Q1_Q2_predictions.*`、`R3_Q3_predictions.*` |

共通の保存・CLI処理は `R/bootstrap.R`、SI2026の統計補助関数は `R/si_common.R`、R2/R3の図は `R/plots.R` に分けています。モデル式・推定方法・対比・p値補正を担う既存関数はできるだけそのまま保ちました。SI解析は専用の環境内で入出力先を受け取り、グローバル変数への書込みを避けています。

## 再生成

このフォルダで次を実行します。RscriptをPATHに登録していない場合は、手元のRscript実行ファイルを指定してください。

```powershell
Rscript install_dependencies.R
Rscript run_analysis.R --input data --output output --analysis all
```

1件だけ実行する場合は `--analysis r1`、`r2`、`r3` を指定します。入力・出力はCLIで変更できます。入力と出力に同じフォルダは指定できません。既存の追加Rライブラリを利用する場合は `PORTFOLIO_R_EXTRA_LIB` 環境変数にそのディレクトリを設定します。

検証環境はR 4.6.1です。直接依存はordinal、lme4、lmerTest、emmeans、dplyr、tidyr、readr、tibble、stringr、ggplot2、svglite、jsonliteで、実行時の版は `output/package_versions.csv` に保存しています。`install_dependencies.R` は不足パッケージをこのフォルダの `r_library/` にインストールします。図はPNGとSVGを同時に出力します。モデルは毎回CSVから推定し、保存モデルを再利用しません。

## 保持した解析条件

### R1：SMC2026の主観評定

- 入力は353行・13名。このうち `Unknown(0)` の62行は、原本と同じ条件水準の指定により解析対象から外れます。Springiness・Resistanceは291観測、さらにNo-Vibを除くIntensityは231観測で、いずれも13名です。保存図に合わせた人数の削減はしていません。
- モデルは `outcome ~ Condition + Set + (1 | Participant)`、ML（`REML=FALSE`）。Type III F検定、Satterthwaite自由度、sum contrastsを保持します。
- Springiness・ResistanceはNo-Vibを含む5条件。IntensityだけNo-Vibを除く4条件です。欠測除外は指標ごとに原本と同じです。
- 条件の推定周辺平均と95% CI、条件内の全対比較にHolm補正を適用したp値を出力します。図の星印はこの補正後p値です。検定間・3指標全体の追加補正は行っていません。
- CIの見せ方、括弧の配置を保ちながら、図に対象人数・No-Vibの扱い・p値補正の注記を追加しています。

### R2：SI2026の客観計測

- 区間平均は元処理済みCSVを使います。反力・把持力は20Hz LPF後、速度は絶対速度の区間時間平均です。このパッケージで生波形を再フィルタしたものではありません。
- 各往復は停止区間を除いた押し込み・戻しの時間加重平均、試行値は2往復の等重み平均です。一方の往復が利用不能なら試行も欠測にします。
- 反力・把持力・速度・最大位置は12名・共通202試行。`value ~ stiffness * vibration + set + (1 | subject)`、REML、bobyqa、Satterthwaite自由度を保持します。
- 剛性・振動の周辺平均は等重み。主効果・交互作用のp値は未補正。交互作用が有意な指標のみ剛性別Vib−Nonの3対比を行い、指標内でHolm補正します。
- RMSはVibのみの100試行、`value ~ stiffness + set + (1 | subject)`。全体検定が有意な場合に剛性3対比較をHolm補正します。
- 客観4指標図はEMM±未補正95% CIです。RMS図は参加者内平均を計算した後の参加者間平均±SDで、EMMのCIではありません。RMSの原入力に物理単位メタデータがないため、図では元データ単位と明記しています。
- 元の試行数・参加者数・共通コホート確認と `F=t²` の整合検査を残しています。別研究のデータに転用するときは `si_inputs.R` と各解析関数内の研究固有の件数チェックも見直してください。

### R3：SI2026の順序評定

- 入力648行のうち、除外フラグが立った2試行×3質問＝6行を除きます。参加者IDを条件式に埋め込まず、3質問で同じ試行が除外されているかを検査します。原本の指定した除外と同じであることは原入力との比較で確認しました。
- Q1/Q2は各214試行、`rating ~ stiffness * vibration + set + (1 + vc | subject)`、`nAGQ=1`。vcはNon=−0.5、Vib=+0.5です。
- Q3はVibのみ106試行、`rating ~ stiffness + set + (1 | subject)`、`nAGQ=9`。
- logitリンク、柔軟閾値、nlminb設定を保持。剛性は潜在尺度EMMのWald χ²、振動はWald z、交互作用は同じランダム効果を持つ加法モデルとの尤度比検定です。
- 交互作用が有意なら剛性別Vib−Non、そうでなければ有意な剛性主効果の3対比較、Q3は剛性全体検定が有意な場合に3対比較を行います。項目内の事後比較p値だけHolm補正し、95% CIは未補正です。
- 図はランダム効果を0に置いたカテゴリ確率から `E[Y]=Σ kP(Y=k)` を算出し、Setを等重みで平均した予測評定です。元評定の単純平均でも、ランダム効果を積分した母集団平均でもありません。

## 原本との数値比較

`validation/comparison.csv` と `validation/validation_summary.json` に検査結果があります。元の名前入り入力と未変更の元解析関数をメモリ上で再実行し、匿名化CSV＋整理版と比較しました。確認した34項目すべてが一致し、今回の実行での最大絶対差は0でした。

比較対象は結果表の全数値・列順・文字列、客観LMM/CLMMの固定係数と対数尤度、R1のNo-Vib方針です。検査許容差は表が相対1e-7、モデルが絶対1e-7ですが、今回は許容差内に収まっただけではなく差0でした。名前入り原入力・参加者対応表・原本モデルRDSは保存していません。検証用の参照表に含まれるIDも匿名化しています。

原本を持っている環境では次のテストを再実行できます。R1は選定した整理版 `01_lmm_subjective_scores.R`、SI側は元の `00_run.R` / `01_subjective.R` / `02_objective.R` があるフォルダを指定します。

```powershell
Rscript tests/validate_against_sources.R --r1-source SOURCE_R1 --si-source SOURCE_SI_DIR --r1-input ORIGINAL_R1_CSV --si-input ORIGINAL_SI_INPUT_DIR --output output
```

モデルファイルは解析の再実行で生成されます。RDSにはRの実行環境が付随するため、配布ファイル集には含めず、数値CSVと図を収録します。テストの前に `run_analysis.R` を実行してください。

## 診断と解釈

R2の5モデルはいずれもoptimizer code=0、singular=FALSEでした。R3の5モデルはoptimizer code=0、Hessian最小固有値は正でしたが、記録された最大勾配はQ1/Q2系で約1.7e-4〜3.2e-4、Q3で約6.1e-6です。診断値を `output/r3/subjective_diagnostics.csv` にそのまま残しており、勾配がすべて1e-6以下になったとは説明しません。統計方針を変えないため、ここでは元の最適化設定を変更していません。

emmeansの「交互作用を含むため主効果の解釈に注意」というメッセージもログに残します。条件別の図、交互作用検定、規定の事後比較を合わせて読んでください。主効果だけから条件別の効果を断定しません。

入力・元コードのSHA-256、変更点は `source_provenance.json`、配布対象と全体結果は `summary.json` / `PACKAGING.txt` にあります。研究間で同じP番号が同一人物を指すとは限りません。
