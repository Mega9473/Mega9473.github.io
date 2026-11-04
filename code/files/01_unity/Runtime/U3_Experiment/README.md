# U3：条件提示・7件法質問紙・CSV

空のGameObjectに `ExperimentSequence`、`SetCsvStore`、`ExperimentSamplePanel` を追加します。SequenceのCsv StoreへSetCsvStoreを、PanelのSequenceとCsvへそれぞれ設定します。実機なしならTcp Bridge参照は空のままで動作確認できます。

再生するとマウス操作パネルが出ます。Start experimentで条件順と各セットのCSVを準備し、質問に回答してSave answers、Next trialで進めます。セットの最後で停止し、Start next setで再開します。Previous trialで既存回答を修正できます。初期表示4は保存ボタンを押すまで回答ではありません。出力は `Assets/ExperimentResults/si2026/<participant_timestamp>/` です。確認には架空のparticipantIdを使ってください。

条件の既定値は原本コード同様、剛性2・3・4と振動1・2、5セットです。元の保存シーンにある「剛性1・2・3、3セット」とは設定が異なるため、必要ならInspectorで変更します。randomSeed=0で毎回生成、それ以外で提示順を再現できます。seedと実験設定を同じにして比較します。

元のFisher–Yatesシャッフルを `ConditionSchedule` に分離しました。試行進行、セット間の `0,0` 停止、CSVの一時ファイル保存と回答修正時の復元処理は原本を保持しています。条件順の原本との一致と重複条件拒否をTestsで検査しました。

新規のExperimentSamplePanelは接続を示すためのIMGUIです。元のSteamVR入力、7個のCubeを使う質問紙、VR内レイ操作は含みません。機器なしでビジネスロジックを試すためのサンプルであり、完成した元VR実験アプリの配布ではありません。実機の停止動作やCSV障害時の挙動は今回実機検証していません。
