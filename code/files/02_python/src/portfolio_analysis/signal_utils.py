"""時系列の平滑化・連続区間処理。原実装の計算順を維持。"""
from __future__ import annotations
from typing import List, Tuple, Optional, Dict
import numpy as np


def safe_median_dt(time_s: np.ndarray) -> float:
    time_s = np.asarray(time_s, dtype=float)
    if len(time_s) < 2:
        return np.nan
    dt = np.diff(time_s)
    dt = dt[np.isfinite(dt) & (dt > 0)]
    if len(dt) == 0:
        return np.nan
    return float(np.median(dt))

def contiguous_regions(mask: np.ndarray) -> List[Tuple[int, int]]:
    if mask.size == 0:
        return []
    mask = mask.astype(bool)
    diff = np.diff(mask.astype(int))
    starts = list(np.where(diff == 1)[0] + 1)
    ends = list(np.where(diff == -1)[0])
    if mask[0]:
        starts = [0] + starts
    if mask[-1]:
        ends = ends + [len(mask) - 1]
    return list(zip(starts, ends))

def merge_close_regions(regions: List[Tuple[int, int]], gap_max_samples: int) -> List[Tuple[int, int]]:
    if not regions:
        return []
    merged = [regions[0]]
    for s, e in regions[1:]:
        ps, pe = merged[-1]
        if s - pe <= gap_max_samples:
            merged[-1] = (ps, e)
        else:
            merged.append((s, e))
    return merged

def smooth_signal(x: np.ndarray, window: int) -> np.ndarray:
    x = np.asarray(x, dtype=float)
    if x.size == 0 or window <= 1:
        return x.copy()
    pad = window // 2
    x_pad = np.pad(x, pad_width=pad, mode='edge')
    kernel = np.ones(window, dtype=float) / window
    y = np.convolve(x_pad, kernel, mode='valid')
    if len(y) != len(x):
        y = y[:len(x)]
    return y

def find_local_peaks(signal: np.ndarray) -> List[int]:
    x = np.asarray(signal, dtype=float)
    peaks = []
    if len(x) < 3:
        return peaks
    for i in range(1, len(x) - 1):
        a, b, c = (x[i - 1], x[i], x[i + 1])
        if not (np.isfinite(a) and np.isfinite(b) and np.isfinite(c)):
            continue
        if b >= a and b >= c:
            peaks.append(i)
    return peaks
