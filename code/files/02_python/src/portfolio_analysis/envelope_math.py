"""加速度包絡線と変位補間。元のHilbert計算を維持。"""
from __future__ import annotations
import numpy as np
import pandas as pd
from scipy.signal import hilbert


def compute_hilbert_envelope_mirror(x: np.ndarray, pad: int=100) -> np.ndarray:
    """
    ミラー拡張してから Hilbert を適用し、中央部のみ取り出す
    """
    x = np.asarray(x, dtype=float)
    valid = np.isfinite(x)
    if np.sum(valid) < 2:
        return np.full_like(x, np.nan, dtype=float)
    x_work = x.copy()
    if not np.all(valid):
        idx = np.arange(len(x_work))
        x_work[~valid] = np.interp(idx[~valid], idx[valid], x_work[valid])
    n = len(x_work)
    pad = int(min(max(pad, 1), max(n - 1, 1)))
    if n < 3:
        env = np.abs(hilbert(x_work))
        env[~valid] = np.nan
        return env
    left = x_work[1:pad + 1][::-1]
    right = x_work[-pad - 1:-1][::-1]
    x_pad = np.r_[left, x_work, right]
    env_pad = np.abs(hilbert(x_pad))
    env = env_pad[pad:pad + n]
    env[~valid] = np.nan
    return env

def interp_envelope_vs_displacement(disp: np.ndarray, env: np.ndarray, disp_common_raw: np.ndarray) -> np.ndarray:
    disp = np.asarray(disp, dtype=float)
    env = np.asarray(env, dtype=float)
    valid = np.isfinite(disp) & np.isfinite(env)
    if np.sum(valid) < 2:
        return np.full_like(disp_common_raw, np.nan, dtype=float)
    disp_valid = disp[valid]
    env_valid = env[valid]
    df_tmp = pd.DataFrame({'disp': disp_valid, 'env': env_valid})
    df_tmp = df_tmp.groupby('disp', as_index=False)['env'].mean().sort_values('disp')
    disp_unique = df_tmp['disp'].to_numpy(dtype=float)
    env_unique = df_tmp['env'].to_numpy(dtype=float)
    if len(disp_unique) < 2:
        return np.full_like(disp_common_raw, np.nan, dtype=float)
    y_interp = np.interp(disp_common_raw, disp_unique, env_unique)
    y_interp[disp_common_raw < disp_unique[0]] = np.nan
    y_interp[disp_common_raw > disp_unique[-1]] = np.nan
    return y_interp
