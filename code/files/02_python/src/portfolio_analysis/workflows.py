"""匿名入力を読み、元の計算条件で結果表と図を作る。"""
from __future__ import annotations

import json
import math
import socket
import threading
from pathlib import Path

import numpy as np
import pandas as pd

from .config import SMOOTH_WINDOW_S, START_THRESHOLD_MM
from .data_io import read_float_csv
from .envelope_math import compute_hilbert_envelope_mirror, interp_envelope_vs_displacement
from .plots import plt, save_figure
from .segmentation import detect_first_two_press_return_regions
from .signal_utils import safe_median_dt, smooth_signal
from .tcp_bridge import CommandBuffer, exchange_once

CONDITIONS = {9: "Const-Low", 10: "Const-High", 13: "Env-F", 14: "Env-xF"}
COLORS = {"Const-Low": "#4E9F3D", "Const-High": "#E69F00", "Env-F": "#D55E5E", "Env-xF": "#4C78A8"}
GRID_RAW = np.arange(5.0, 85.0 + 0.5 / 2, 0.5)


def read_numeric(path: Path, columns: list[str]) -> pd.DataFrame:
    return read_float_csv(path, columns)[columns]


def run_segmentation(data: Path, output: Path):
    files = sorted((data / "segmentation").glob("*.csv"))
    if not files:
        raise FileNotFoundError("No segmentation CSV files")
    output.mkdir(parents=True, exist_ok=True)
    fig, axes = plt.subplots(len(files), 1, figsize=(10, 3.0 * len(files)), squeeze=False, layout="constrained")
    rows = []
    for ax, path in zip(axes[:, 0], files):
        df = read_numeric(path, ["time[s]", "x[mm]", "Condi"]).dropna(subset=["time[s]", "x[mm]"]).reset_index(drop=True)
        regions, base, suspects = detect_first_two_press_return_regions(df)
        dt = safe_median_dt(df["time[s]"].to_numpy())
        smooth = smooth_signal(df["x[mm]"].to_numpy(), max(3, int(round(SMOOTH_WINDOW_S / dt))))
        relative = smooth - base["baseline_mm"]
        t = df["time[s]"].to_numpy()
        ax.plot(t, relative, color="#1D4E89", lw=1.2, label="Smoothed displacement - baseline")
        ax.axhline(START_THRESHOLD_MM, color="#666666", ls="--", lw=0.8, label="5 mm threshold")
        for number, (start, end) in enumerate(regions, 1):
            ax.axvspan(t[start], t[end], color="#e6a23c", alpha=0.28, label="Detected region" if number == 1 else None)
            suspect = bool(suspects[number - 1]["suspect_any"]) if number <= len(suspects) else False
            rows.append({"trial": path.stem, "region": number, "start_index": int(start), "end_index": int(end), "start_time_s": t[start], "end_time_s": t[end], "baseline_mm": base["baseline_mm"], "baseline_method": base["baseline_method_mm"], "suspect": suspect})
        ax.set(title=f"{path.stem}: {len(regions)} detected regions", xlabel="Measurement time [s]", ylabel="Displacement [mm]")
        ax.legend(loc="upper right", fontsize=8)
        ax.grid(alpha=0.2)
    fig.suptitle("Real-data excerpt: three trials / one anonymized participant\nOriginal thresholds and smoothing retained; not the full study", fontsize=12)
    save_figure(fig, output, "detected_press_return_regions")
    pd.DataFrame(rows).to_csv(output / "regions.csv", index=False)
    return {"kind": "real_data_excerpt", "trials": len(files), "regions": len(rows), "output": str(output)}


def process_envelope(path: Path):
    frame = read_numeric(path, ["time[s]", "x[mm]", "acc_high(80-120Hz)", "Condi"])
    code = frame["Condi"].dropna().astype(int)
    code = code[code != 0]
    condition = CONDITIONS.get(int(code.mode().iloc[0])) if len(code) else None
    if condition is None:
        return None
    disp = frame["x[mm]"].to_numpy(dtype=float)
    # 原コードの補正係数。既存の列名はm/s^2を示すが値は元処理のまま。
    acc = frame["acc_high(80-120Hz)"].to_numpy(dtype=float) / 0.102
    valid = np.isfinite(disp) & np.isfinite(acc)
    disp, acc = disp[valid], acc[valid]
    time_s = frame["time[s]"].to_numpy(dtype=float)[valid]
    if len(disp) < 3 or float(np.nanmax(disp)) < 84.0:
        return None
    env = compute_hilbert_envelope_mirror(acc, pad=100)
    mask = (disp >= 5.0) & (disp <= 85.0) & np.isfinite(env)
    if np.sum(mask) < 3:
        return None
    curve = interp_envelope_vs_displacement(disp[mask], env[mask], GRID_RAW)
    return {"participant": path.parent.name, "condition": condition, "curve": curve, "time_s": time_s, "acc": acc, "envelope": env}


def run_envelopes(data: Path, output: Path):
    files = sorted((data / "envelope").glob("*/*.csv"))
    records = [r for p in files if (r := process_envelope(p)) is not None]
    if not records:
        raise ValueError("No eligible envelope trials")
    output.mkdir(parents=True, exist_ok=True)
    columns = [f"disp_{d:.1f}" for d in GRID_RAW - 5.0]
    rows = [{"participant": r["participant"], "condition": r["condition"], **dict(zip(columns, r["curve"]))} for r in records]
    trial = pd.DataFrame(rows)
    person = trial.groupby(["participant", "condition"], as_index=False)[columns].mean()
    group = person.groupby("condition", as_index=False)[columns].mean()
    counts = person.groupby("condition", as_index=False)["participant"].nunique()
    trial.to_csv(output / "trial_curves.csv", index=False)
    person.to_csv(output / "participant_means.csv", index=False)
    group.to_csv(output / "condition_means.csv", index=False)
    counts.to_csv(output / "participant_counts.csv", index=False)
    fig, ax = plt.subplots(figsize=(9, 5.2), layout="constrained")
    for condition in CONDITIONS.values():
        row = group[group["condition"] == condition]
        if row.empty:
            continue
        n = int(counts.loc[counts["condition"] == condition, "participant"].iloc[0])
        ax.plot(GRID_RAW - 5.0, row[columns].iloc[0].to_numpy(), color=COLORS[condition], lw=2.2, label=f"{condition} (n={n})")
    ax.set(title="Real-data excerpt: Hilbert envelopes by condition\nParticipant means, then condition means; not the full study", xlabel="Corrected displacement [mm]", ylabel="Envelope amplitude [original scale / 0.102]", xlim=(0, 80))
    ax.legend(frameon=False)
    ax.grid(alpha=0.2)
    save_figure(fig, output, "acceleration_envelope_comparison")
    example = records[0]
    fig, ax = plt.subplots(figsize=(9, 4.2), layout="constrained")
    t = example["time_s"] - example["time_s"][0]
    ax.plot(t, example["acc"], color="#9bb5ca", lw=0.8, label="Band-passed acceleration / 0.102")
    ax.plot(t, example["envelope"], color="#D55E5E", lw=1.5, label="Recomputed Hilbert envelope")
    ax.set(title=f"Real measured excerpt: {example['participant']} / {example['condition']}", xlabel="Elapsed time within supplied phase [s]", ylabel="Amplitude [original scale / 0.102]")
    ax.legend(fontsize=8)
    save_figure(fig, output, "signal_and_recomputed_envelope")
    return {"kind": "real_data_excerpt", "trials": len(records), "participants": int(person["participant"].nunique()), "output": str(output)}


def run_full_envelopes(data: Path, output: Path):
    """元対象全体の再計算済み匿名中間値から、条件平均を再現する。"""
    source = data / "envelope_full"
    metadata = json.loads((source / "metadata.json").read_text(encoding="utf-8"))
    columns = [f"disp_{d:.1f}" for d in GRID_RAW - 5.0]
    trial = read_float_csv(source / "trial_curves.csv.gz", columns)
    # 参加者ごとの試行数に偏りがあっても等重みになる元の二段階平均。
    person = trial.groupby(["Participant", "Condition"])[columns].mean().reset_index()
    group = person.groupby("Condition")[columns].mean().reset_index()
    counts = person.groupby("Condition")["Participant"].nunique()
    output.mkdir(parents=True, exist_ok=True)
    group.to_csv(output / "full_condition_means.csv", index=False)
    person.to_csv(output / "full_participant_means.csv", index=False)
    fig, ax = plt.subplots(figsize=(9, 5.7), layout="constrained")
    for condition in CONDITIONS.values():
        values = group.loc[group["Condition"] == condition, columns].iloc[0].to_numpy(float)
        ax.plot(GRID_RAW - 5.0, values, lw=2.5, color=COLORS[condition], label=f"{condition} (n={int(counts[condition])})")
    ax.set(title=f"Full original analysis set: {metadata['unique_participants']} participants / {metadata['included_phases']} press phases\nHilbert envelope, averaged within participant then across participants", xlabel="Corrected displacement [mm]", ylabel="Envelope amplitude [original scale / 0.102]", xlim=(0, 80))
    ax.legend(frameon=False)
    ax.grid(alpha=0.2)
    save_figure(fig, output, "full_acceleration_envelope_comparison")
    return {"kind": "full_original_analysis", "included_phases": metadata["included_phases"], "unique_participants": metadata["unique_participants"], "participants_by_condition": metadata["participants_by_condition"], "aggregation_input": "anonymized trial-level interpolated curves recomputed from original measured signals"}


class MockDevice:
    def __init__(self):
        self.index = 0
        self.command = (0.0, 0.0)

    def read_pair(self):
        t = self.index * 0.01
        self.index += 1
        return 40 + 30 * math.sin(t * 3), 35 + 20 * math.cos(t * 3)

    def write_pair(self, pair):
        self.command = pair

    def close(self):
        pass


def run_tcp_demo(output: Path, samples: int = 120):
    """localhost上で本物のTCPを使うが、センサ・指令は合成値。"""
    output.mkdir(parents=True, exist_ok=True)
    server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server.bind(("127.0.0.1", 0))
    server.listen(1)
    errors = []

    def mock_unity():
        try:
            with server.accept()[0] as peer:
                pending = ""
                count = 0
                while count < samples:
                    chunk = peer.recv(4096)
                    if not chunk:
                        break
                    pending += chunk.decode("utf-8")
                    while "\n" in pending:
                        line, pending = pending.split("\n", 1)
                        a, b = map(float, line.split(","))
                        response = f"{a / 80:.6f},{b / 80:.6f}\n".encode()
                        midpoint = len(response) // 2
                        peer.sendall(response[:midpoint])
                        peer.sendall(response[midpoint:])
                        count += 1
        except Exception as exc:
            errors.append(str(exc))
        finally:
            server.close()

    thread = threading.Thread(target=mock_unity, daemon=True)
    thread.start()
    rows = []
    device = MockDevice()
    with socket.create_connection(server.getsockname(), timeout=2.0) as client:
        client.settimeout(0.1)
        parser = CommandBuffer()
        for i in range(samples):
            values, command = exchange_once(client, device, parser)
            # デモは各要求の応答を読み切る。実機ループの周期を模倣しない。
            while command is None:
                chunk = client.recv(4096)
                if not chunk:
                    raise ConnectionError("Mock peer closed before a complete response")
                command = parser.feed(chunk)
                if command is not None:
                    device.write_pair(command)
            rows.append({"sample": i, "synthetic_time_s": i * 0.01, "mock_position_1": values[0], "mock_position_2": values[1], "mock_command_1": device.command[0], "mock_command_2": device.command[1], "command_received_this_step": command is not None})
    thread.join(timeout=3.0)
    if thread.is_alive() or errors:
        raise RuntimeError(f"Loopback server failed: {errors}")
    df = pd.DataFrame(rows)
    df.to_csv(output / "mock_tcp_trace.csv", index=False)
    fig, axes = plt.subplots(2, 1, figsize=(9, 6), sharex=True, layout="constrained")
    axes[0].plot(df["sample"], df["mock_position_1"], label="Mock sensor 1")
    axes[0].plot(df["sample"], df["mock_position_2"], label="Mock sensor 2")
    axes[0].set(ylabel="Synthetic displacement", title="MOCK ONLY: local TCP exchange, no dSPACE hardware")
    axes[1].plot(df["sample"], df["mock_command_1"], label="Mock command 1")
    axes[1].plot(df["sample"], df["mock_command_2"], label="Mock command 2")
    axes[1].set(xlabel="Exchange step (not measured real-time rate)", ylabel="Synthetic command")
    for ax in axes:
        ax.legend()
        ax.grid(alpha=0.2)
    save_figure(fig, output, "mock_tcp_exchange")
    return {"kind": "synthetic_device_with_real_loopback_tcp", "exchanges": samples, "received_commands": int(df["command_received_this_step"].sum()), "hardware_verified": False}
