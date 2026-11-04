# ロードセル校正用の150 mm載荷治具
元のCadQueryモデルの幾何形状を保ったまま、関数名、入力参照先、出力先、実行入口を整理しました。

- `build_parts(reference_dir)`：構成部品を生成。
- `validate_geometry(parts)`：形状、意図しない干渉、ガイドの移動余裕などを確認。
- `build_fixture(output_dir, reference_dir)`：STEP/STLと検証記録を出力。
- 元の `models/` は参照用です。標準の新規出力先は `generated/` です。

```powershell
python -m pip install -r requirements.txt
python build_fixture.py --output generated
```

150×150 mmの載荷板、104×64×8 mmの土台、80 mm間隔のガイドを設けた設計です。参照用の軸・ブッシュ・センサは簡略形状です。実機の強度・摩擦・耐荷重は検証していません。

`reference/original_geometry_checks.json` は以前の記録、`generated/geometry_checks.json` は今回の再生成で得られる記録です。FDM材料は未指定です。変形解析や200 N実荷重試験の代わりにはなりません。

