# Python研究コード集

研究用の通信・区間検出・信号処理を、入力設定と計算処理に分けた実行可能なファイル集です。元研究ファイルには変更を加えていません。

| 内容 | 入力と結果の位置づけ | 最初に見るファイル |
|---|---|---|
| P1 双方向TCP | 実際のlocalhost TCP通信。センサ・指令は合成値、dSPACE実機は未検証 | `results/P1_tcp_demo/mock_tcp_exchange.png` |
| P2 押し込み・戻し区間の検出 | 実計測3試行・匿名1名から抽出したアルゴリズムの実例 | `results/P2_segments/detected_press_return_regions.png` |
| P3 加速度包絡線・全対象 | 元研究と同じ解析対象全体から包絡線を再計算した条件比較 | `results/P3_full_research/full_acceleration_envelope_comparison.png` |
| P3 加速度包絡線・小規模例 | 実計測8押込相・匿名2名で、波形から包絡線を再実行できる例 | `results/P3_envelopes/signal_and_recomputed_envelope.png` |

すべての図にSVG版、解析結果にCSV版があります。P2とP3の小規模図を、研究対象全体の結果として使用しないでください。P1の波形は研究計測結果ではありません。

## 再実行

Python 3.11で検証しました。依存は`requirements.txt`に固定されています。このフォルダで次を実行します。

```powershell
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
.\.venv\Scripts\python.exe run_all.py all --data data --output results
.\.venv\Scripts\python.exe -m pytest -q
```

既存環境を使う場合は`run_demo.ps1`で実行できます。Pythonの場所を指定する場合は`-Python <python.exeのパス>`を渡します。実機への接続は`all`に含まれません。

個別実行もできます。

```powershell
.\.venv\Scripts\python.exe run_all.py tcp-demo --output results
.\.venv\Scripts\python.exe run_all.py segments --data data --output results
.\.venv\Scripts\python.exe run_all.py envelopes --data data --output results
.\.venv\Scripts\python.exe run_all.py envelopes-full --data data --output results
```

## 変更した構成

- `tcp_bridge.py`: 2値の符号化、改行区切りの受信バッファ、装置アダプタ、通信1回分を分離しました。
- `config.py` / `signal_utils.py` / `segmentation.py`: 元コードの判定定数と純粋な計算関数を分離しました。関数の計算式と演算順序は維持しています。
- `envelope_math.py`: Hilbert包絡線と変位軸への補間を、入出力やグラフから分離しました。
- `workflows.py` / `cli.py`: 入力パスをCLIで指定し、匿名CSVから図と結果表を生成します。

元コードの関数分割後に構文を整えたため、行番号は元ファイルと一致しません。変更履歴と元ファイルのハッシュは内部の`source_provenance.json`に記録しています。

## 維持した解析条件

P2は既存の500 Hz前処理済みデータを使用します。条件信号が最初に減少した後の変位下位5%の平均を基線とし、見つからない場合は全体の下位5%を使います。20 ms移動平均、基線から5 mmの開始しきい値、最小変位幅10 mm、最小継続0.20秒、途切れの結合0.20秒、最初の2区間という条件を維持しました。60 mm以上の複数ピークと5 mm以上の谷を使う疑わしい試行の判定も残しています。入力は時間・変位・条件信号だけに絞り、時間方向の再間引きや値の丸めはしていません。

P3は元コードと同じ`csv_PtoRstart`の押込相を対象とします。No-Vibを除外し、最大変位84 mm以上の試行を採用します。既存の80–120 Hz帯域処理済み加速度を0.102で除し、前後100サンプルのミラー拡張でHilbert包絡線を計算します。変位5–85 mmを0.5 mm刻みで補間し、表示は5 mmを引いた0–80 mmです。同じ変位の重複値は平均し、元データ範囲外には外挿しません。試行を被験者内で平均してから、被験者を等重みで条件平均します。係数0.102は元コードを保持しており、その物理単位の校正根拠は今回独立に検証していません。

既存の前処理済み系列から始めるため、帯域フィルタや押込相分割そのものはP3の再実行には含まれません。

## 全対象の結果と小規模例

`data/envelope_full/metadata.json`に全対象の人数・採用押込相数・条件別人数、元コードとの比較と保存済み集計との差を記録しています。全対象の包絡線は元の計測系列から新たに計算しました。公開用の同梱データは次の2段階です。

- `data/envelope/`: 匿名2名×4条件の実系列。Hilbert処理・補間から再実行できます。
- `data/envelope_full/trial_curves.csv.gz`: 全対象の匿名試行×共通変位の包絡線。`envelopes-full`が被験者内平均・条件平均を再実行します。
- `data/envelope_full/participant_condition_curves.csv.gz`: 匿名の被験者×条件平均。集計の確認用です。

全対象の生系列からの再計算には元研究データが必要です。内部の`recompute_full_envelopes.py`がその手順です。同梱中間値から生成した図と、全対象の波形から再計算した検証結果を混同しないよう、検証JSONにも生成経路を残しています。

## 検証

`reference/`は元コードを実データに対して実行して作った回帰検査用の基準値です。結果図をコピーして基準とする方法は使っていません。

- P2: 区間の開始・終了インデックス、基線値、疑わしいピークの情報を元実装と比較。
- P3: 補間後の全161点を元実装と比較。さらに全対象の試行と二段階平均を比較。
- P1: 元コードの受信部分を実行し、分割受信・複数行・不正な最新行など7ケースを照合。小数4桁とLFの送信形式も検査。
- 欠損値の復元と補間範囲外のNaNを個別に検査。

`results/validation_against_originals.json`と`results/validation_full_envelopes.json`に数値比較の結果があります。元ファイルと入力のSHA-256も検査しています。数値はPythonのfloatでround-trip精度を保って読み込みます。

## P1の実機設定

`dspace_config.example.json`に接続先・MAPort設定・変数パスを入力し、必要なdSPACE XIL 2.2とpythonnetを導入した環境でのみ、次の実機コマンドを利用します。サンプル設定には実機のアドレスや変数パスを入れていません。

```powershell
.\.venv\Scripts\python.exe run_all.py tcp-real --config my_device_config.json --duration 1
```

実機アダプタは今回動作未検証です。受信形式と最新行採用は元実装を保持しました。一方、接続の再試行は呼出側に任せ、接続終了を明示的な例外にしています。公開デモは各応答を読み切るため、元実機ループの通信周期や制御性能を再現したものではありません。

## 公開対象

配布するのはコード、匿名入力、匿名中間値、図、結果表、基準値、検査コードです。`provenance_public.json`に元スクリプト名・ハッシュ・変更点・対象件数を記載しています。`source_provenance.json`と`internal/`は原パスや対応表を扱う内部記録です。公開ZIPから除外してください。`.venv/`、各キャッシュ、ローカル実機設定も除外します。匿名IDはこの資料内の対応用で、元の参加者名は公開データに含めていません。
