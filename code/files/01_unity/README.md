# Unity / C#：研究コードの掲載用サンプル

原本の研究機能から、U1 座標・姿勢計測、U2 実機変位の表示、U3 実験条件と質問紙保存を整理したコード集です。完成済みの元VRアプリを複製したものではありません。元プロジェクトと依存関係を分離するため名前空間を `Portfolio.Research` に変更しています。

| 対象 | 開始ファイル | 内容 |
|---|---|---|
| U1 | Runtime/U1_Pose/RelativePoseProvider.cs | 基準面座標、相対姿勢、侵入量、接触 |
| U2 | Runtime/U2_Visualization/SpongeVisualizer.cs | 変位のデッドバンド・平滑移動・色判定 |
| U3 | Runtime/U3_Experiment/ExperimentSequence.cs | 条件順・試行状態・CSV保存 |

## 使用方法

1. **新しいUnity 6プロジェクト**の `Assets/ResearchSamples` に、このフォルダの `Runtime` だけをコピーしてください。Tests、Validation、bin、objはAssetsへ入れません。
2. 各サンプルのREADMEに従ってGameObjectへコンポーネントを追加し、Inspector参照を設定します。
3. U2/U3で実機を接続する場合だけ `ExperimentTcpBridge` を追加します。既定待受ポートは12345、受信は `displacement,button\n`、送信は `stiffnessId,vibrationId\n`、`0,0` は停止です。接続先の既存実験とポートを合わせます。
4. 実機なしではU2のDemo InputとU3のサンプルパネルで確認できます。

依存はUnityEngineのみです。SteamVR、TextMesh Pro、元のGrinderTargetSelectorは要求しません。U3のIMGUIパネルは今回追加したマウス操作用の接続例で、元研究のVRコントローラUIそのものではありません。元UIを使う場合は同じ進行・CSVクラスを接続してください。

## 検証

- `dotnet run --project Tests/CoreTests.csproj -c Release`：条件順、元シャッフルとの一致、色判定境界、未回答表示の20項目。
- `dotnet run --project Tests/SessionTests/SessionTests.csproj -c Release`：元のセッション回帰検査を移植。実際のCSV保存、回答の修正、未来セットへの先行回答保持、セット間停止、セッション再開始を確認します。Tests内のUnityStubsはテスト専用で、描画・ライフサイクルの動作保証には使っていません。
- `dotnet build Validation/CompileWithUnity.csproj -c Release`：実在するUnity 6000.0.55f1のDLLを参照しRuntime全体をコンパイル。Unityが別の場所なら `-p:UnityManaged="…/Editor/Data/Managed/UnityEngine"` を指定。
- Inspectorから設定するprivateフィールドに対するCS0649警告は通常のC#ビルドでは発生します。
- Unity Editorでのシーン再生、VRヘッドセット、dSPACEとの接続、計測精度は未確認です。開いているUnityプロジェクトには変更していません。

## 整理と機能変更の区別

座標変換の式、目標100 Hzの取得手順、変位フィルタと色の判定、条件組み合わせ・seed式、CSV形式は維持しました。U1は元の選択コンポーネントを明示的なTransform参照／SetRawPointに置換し、PoseSampleをVector3単位へ整理しています。したがって元シーンへ上書きする互換置換ではありません。

U1の接触集合は無効化時に消去する改善を加えています。U2のデモ入力とU3のパネルは今回の新規サンプル機能です。補助のTCP・CSV・質問定義・履歴は原本を名前空間変更して同梱しています。

出自・ハッシュ・変更点は `source_provenance.json`、検証結果は `summary.json` を参照してください。第三者SDKや被験者データは同梱していません。

`Samples/answers_synthetic.csv` は上の回帰検査が生成した架空の回答データです。実験の被験者回答や研究結果ではありません。
