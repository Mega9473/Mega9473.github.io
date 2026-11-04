# U1：基準面に対する位置・姿勢と侵入量

`RelativePoseProvider` にorigin、visualPoint、rawPointのTransformを設定します。基準面はoriginのローカルY=0、負のYを侵入とする初期設定です。メートルからmmへ1000倍します。originを非等方スケールすると位置変換にも影響するため、計測基準のScaleは(1,1,1)を推奨します。

接触も取得する場合は表示点にTrigger Colliderと `ContactFlag` を配置し、UnityのTrigger発生条件を満たすRigidbodyを設定します。自分と同じrootのColliderを除く挙動は原本と同じです。新サンプルではGameObject無効化時に集合を消去します。

他コンポーネントから `TryGetLatestSample(out PoseSample sample)` を呼び、表示やCSV保存に利用できます。rawPoint切替は `SetRawPoint(transform)` を使います。元のGrinderTargetSelector／SessyokuTriへの依存を明示的な参照へ置き換えました。

`PoseMath.Measure` が座標変換・Quaternion・侵入量計算の掲載箇所です。100 Hzはスケジュールの目標値で、Updateが遅れると同じ姿勢を複数回取得します。独立した100 Hz計測器ではありません。Unity実DLLでコンパイルを確認済み、実行時の姿勢・実機精度は未確認です。
