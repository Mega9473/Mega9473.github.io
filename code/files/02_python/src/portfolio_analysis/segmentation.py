"""押し戻し区間の検出。入出力・図描画から計算を分離。"""
from __future__ import annotations
from typing import List, Tuple, Optional, Dict
import numpy as np
import pandas as pd
from .config import (EXPECTED_PRESS_COUNT, START_THRESHOLD_MM, BACKOFF_S, MIN_PRESS_EXCURSION_MM, MIN_PRESS_DURATION_S, MERGE_GAP_S, SMOOTH_WINDOW_S, BASELINE_PERCENTILE, FALLBACK_TO_GLOBAL_BASELINE, SUSPECT_MIN_PEAK_MM, SUSPECT_MIN_INTERPEAK_DROP_MM, SUSPECT_MAX_VALLEY_MM)
from .signal_utils import (safe_median_dt, contiguous_regions, merge_close_regions, smooth_signal, find_local_peaks)


def find_first_condi_drop_index(condi: np.ndarray) -> Optional[int]:
    """
    Condi(n) > Condi(n+1) となる最初の n を見つけ、
    その次のサンプル n+1 を「減少後開始位置」として返す。
    """
    c = np.asarray(condi, dtype=float)
    if len(c) < 2:
        return None
    diff = c[:-1] - c[1:]
    idx = np.where(np.isfinite(diff) & (diff > 0))[0]
    if len(idx) == 0:
        return None
    return int(idx[0] + 1)

def compute_lower5_mean_baseline(signal: np.ndarray, condi: np.ndarray, percentile: float=BASELINE_PERCENTILE, fallback_to_global: bool=FALLBACK_TO_GLOBAL_BASELINE) -> Tuple[float, str, Optional[int], int]:
    """
    Condi減少後区間の signal の下位5%値群の平均を baseline とする。
    戻り値:
      baseline_value, baseline_method, condi_drop_start_idx, baseline_n
    """
    x = np.asarray(signal, dtype=float)
    valid_all = np.isfinite(x)
    if np.sum(valid_all) == 0:
        return (np.nan, 'no_valid_signal', None, 0)
    drop_start_idx = find_first_condi_drop_index(condi)

    def lower_percentile_mean(vals: np.ndarray) -> Tuple[float, int]:
        vals = vals[np.isfinite(vals)]
        if len(vals) == 0:
            return (np.nan, 0)
        th = np.nanpercentile(vals, percentile)
        picked = vals[vals <= th]
        if len(picked) == 0:
            return (np.nan, 0)
        return (float(np.nanmean(picked)), int(len(picked)))
    if drop_start_idx is not None and drop_start_idx < len(x):
        tail = x[drop_start_idx:]
        base, n = lower_percentile_mean(tail)
        if n > 0:
            return (base, 'condi_drop_tail_lower5_mean', drop_start_idx, n)
    if fallback_to_global:
        base, n = lower_percentile_mean(x[valid_all])
        if n > 0:
            return (base, 'global_lower5_mean_fallback', drop_start_idx, n)
    return (np.nan, 'baseline_not_found', drop_start_idx, 0)

def detect_two_peaks_connected_in_region(disp_rel_region: np.ndarray, min_peak_mm: float=SUSPECT_MIN_PEAK_MM, min_interpeak_drop_mm: float=SUSPECT_MIN_INTERPEAK_DROP_MM, max_valley_mm: float=SUSPECT_MAX_VALLEY_MM) -> Dict[str, object]:
    x = np.asarray(disp_rel_region, dtype=float)
    result = {'suspect_connected': False, 'num_peaks_over_threshold': 0, 'peak1_idx_local': np.nan, 'peak2_idx_local': np.nan, 'peak1_mm': np.nan, 'peak2_mm': np.nan, 'valley_between_peaks_mm': np.nan, 'interpeak_drop_from_lower_peak_mm': np.nan}
    if len(x) < 3 or np.sum(np.isfinite(x)) < 3:
        return result
    peak_idx_all = find_local_peaks(x)
    peak_idx = [i for i in peak_idx_all if np.isfinite(x[i]) and x[i] >= min_peak_mm]
    result['num_peaks_over_threshold'] = len(peak_idx)
    if len(peak_idx) < 2:
        return result
    for p1, p2 in zip(peak_idx[:-1], peak_idx[1:]):
        if p2 <= p1 + 1:
            continue
        peak1 = float(x[p1])
        peak2 = float(x[p2])
        valley = float(np.nanmin(x[p1:p2 + 1]))
        lower_peak = min(peak1, peak2)
        interpeak_drop = lower_peak - valley
        if interpeak_drop >= min_interpeak_drop_mm and valley > max_valley_mm:
            result.update({'suspect_connected': True, 'peak1_idx_local': p1, 'peak2_idx_local': p2, 'peak1_mm': peak1, 'peak2_mm': peak2, 'valley_between_peaks_mm': valley, 'interpeak_drop_from_lower_peak_mm': interpeak_drop})
            return result
    return result

def detect_multi_press_in_region(disp_rel_region: np.ndarray, min_peak_mm: float=SUSPECT_MIN_PEAK_MM, min_interpeak_drop_mm: float=SUSPECT_MIN_INTERPEAK_DROP_MM) -> Dict[str, object]:
    x = np.asarray(disp_rel_region, dtype=float)
    result = {'suspect_multi_press': False, 'multi_peak1_idx_local': np.nan, 'multi_peak2_idx_local': np.nan, 'multi_peak1_mm': np.nan, 'multi_peak2_mm': np.nan, 'multi_valley_between_peaks_mm': np.nan, 'multi_interpeak_drop_from_lower_peak_mm': np.nan}
    if len(x) < 3 or np.sum(np.isfinite(x)) < 3:
        return result
    peak_idx_all = find_local_peaks(x)
    peak_idx = [i for i in peak_idx_all if np.isfinite(x[i]) and x[i] >= min_peak_mm]
    if len(peak_idx) < 2:
        return result
    for p1, p2 in zip(peak_idx[:-1], peak_idx[1:]):
        if p2 <= p1 + 1:
            continue
        peak1 = float(x[p1])
        peak2 = float(x[p2])
        valley = float(np.nanmin(x[p1:p2 + 1]))
        lower_peak = min(peak1, peak2)
        interpeak_drop = lower_peak - valley
        if interpeak_drop >= min_interpeak_drop_mm:
            result.update({'suspect_multi_press': True, 'multi_peak1_idx_local': p1, 'multi_peak2_idx_local': p2, 'multi_peak1_mm': peak1, 'multi_peak2_mm': peak2, 'multi_valley_between_peaks_mm': valley, 'multi_interpeak_drop_from_lower_peak_mm': interpeak_drop})
            return result
    return result

def detect_first_two_press_return_regions(df: pd.DataFrame, expected_count: int=EXPECTED_PRESS_COUNT, start_threshold_mm: float=START_THRESHOLD_MM, backoff_s: float=BACKOFF_S, min_excursion_mm: float=MIN_PRESS_EXCURSION_MM, min_duration_s: float=MIN_PRESS_DURATION_S, merge_gap_s: float=MERGE_GAP_S, smooth_window_s: float=SMOOTH_WINDOW_S) -> Tuple[List[Tuple[int, int]], Dict[str, object], List[Dict[str, object]]]:
    time_s = df['time[s]'].to_numpy(dtype=float)
    disp = df['x[mm]'].to_numpy(dtype=float)
    condi = df['Condi'].to_numpy(dtype=float)
    dt = safe_median_dt(time_s)
    if not np.isfinite(dt) or dt <= 0:
        baseline_info = {'baseline_mm': np.nan, 'baseline_method_mm': 'invalid_dt', 'condi_drop_start_idx': None, 'baseline_n_mm': 0}
        return ([], baseline_info, [])
    fs = 1.0 / dt
    win = max(3, int(round(fs * smooth_window_s)))
    disp_s = smooth_signal(disp, win)
    valid = np.isfinite(disp_s)
    if np.sum(valid) < 5:
        baseline_info = {'baseline_mm': np.nan, 'baseline_method_mm': 'not_enough_valid_disp', 'condi_drop_start_idx': None, 'baseline_n_mm': 0}
        return ([], baseline_info, [])
    baseline_mm, baseline_method_mm, condi_drop_start_idx, baseline_n_mm = compute_lower5_mean_baseline(signal=disp_s, condi=condi, percentile=BASELINE_PERCENTILE, fallback_to_global=FALLBACK_TO_GLOBAL_BASELINE)
    baseline_info = {'baseline_mm': baseline_mm, 'baseline_method_mm': baseline_method_mm, 'condi_drop_start_idx': condi_drop_start_idx, 'baseline_n_mm': baseline_n_mm}
    disp_rel = disp_s - baseline_mm
    active = disp_rel > start_threshold_mm
    regions = contiguous_regions(active)
    if not regions:
        return ([], baseline_info, [])
    gap_n = int(round(merge_gap_s / dt))
    merged = merge_close_regions(regions, gap_n)
    min_n = int(round(min_duration_s / dt))
    backoff_n = int(round(backoff_s / dt))
    valid_regions: List[Tuple[int, int, float]] = []
    suspect_info_list: List[Dict[str, object]] = []
    for s, e in merged:
        if e - s + 1 < min_n:
            continue
        seg = disp_rel[s:e + 1]
        if np.sum(np.isfinite(seg)) < 5:
            continue
        excursion = float(np.nanmax(seg) - np.nanmin(seg))
        if excursion < min_excursion_mm:
            continue
        s2 = max(0, s - backoff_n)
        valid_regions.append((s2, e, excursion))
        connected = detect_two_peaks_connected_in_region(seg)
        multi_press = detect_multi_press_in_region(seg)
        suspect_info_list.append({'RegionStartIndex_before_backoff': s, 'RegionStartIndex_after_backoff': s2, 'RegionEndIndex': e, 'RegionExcursion_mm': excursion, **connected, **multi_press, 'suspect_any': bool(connected.get('suspect_connected', False) or multi_press.get('suspect_multi_press', False))})
    if not valid_regions:
        return ([], baseline_info, suspect_info_list)
    valid_regions = sorted(valid_regions, key=lambda x: x[0])[:expected_count]
    return ([(s, e) for s, e, _ in valid_regions], baseline_info, suspect_info_list)
