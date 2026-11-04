"""CSVを丸め直さず、Python floatのround-trip精度で読み込む。"""
from pathlib import Path
import numpy as np
import pandas as pd


def _number(text: str) -> float:
    try:
        return float(text)
    except (TypeError, ValueError):
        return np.nan


def read_float_csv(path: Path, numeric_columns: list[str]) -> pd.DataFrame:
    raw = pd.read_csv(path, dtype=str, keep_default_na=False)
    missing = set(numeric_columns) - set(raw)
    if missing:
        raise ValueError(f"Missing columns: {sorted(missing)} in {path.name}")
    numeric = set(numeric_columns)
    return pd.DataFrame({name: raw[name].map(_number).to_numpy(dtype=float) if name in numeric else raw[name] for name in raw})
