Unityの動作動画を追加する手順

1. 撮影したMP4をこのフォルダへ保存します。
   研磨の例: unity-polishing.mp4

2. assets/demo-videos.js の該当する作例の src を設定します。
   研磨（u1）の例:
   src: '../assets/videos/unity-polishing.mp4',

3. code/unity.html を再読み込みします。
   作例の説明とコードの間に、再生ボタン付きの動画が表示されます。
   自動再生はしません。src が空欄の作例には動画も空枠も表示されません。

作例の対応:
  u1: 研磨面に対する工具の位置・姿勢計測
  u2: 実空間のセンサデータとVR空間の連動
  u3: 実験条件の提示と主観評価の記録

caption は録画内容に合わせて変更できます。
poster は任意のサムネイル画像のパスです。不要なら空欄のままにします。
src / poster のパスは code/unity.html を基準に指定します。
公開するときは、動画ファイルと変更した demo-videos.js もアップロードします。
