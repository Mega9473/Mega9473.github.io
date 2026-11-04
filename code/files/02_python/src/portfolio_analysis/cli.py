"""CLI: 解析と装置なしデモを明示的なコマンドで実行。"""
import argparse
import json
from pathlib import Path
from .tcp_bridge import run_real
from .workflows import run_envelopes, run_full_envelopes, run_segmentation, run_tcp_demo


def main(argv=None):
    parser = argparse.ArgumentParser(description="研究コードの実データ抜粋解析とTCP mockデモ")
    parser.add_argument("command", choices=["all", "segments", "envelopes", "envelopes-full", "tcp-demo", "tcp-real"])
    parser.add_argument("--data", type=Path, default=Path("data"))
    parser.add_argument("--output", type=Path, default=Path("results"))
    parser.add_argument("--config", type=Path)
    parser.add_argument("--duration", type=float, default=1.0)
    args = parser.parse_args(argv)
    if args.command == "tcp-real":
        if args.config is None or args.duration <= 0:
            parser.error("tcp-real requires --config and positive --duration")
        run_real(args.config, args.duration)
        return
    result = {}
    if args.command in ("all", "tcp-demo"):
        result["P1"] = run_tcp_demo(args.output / "P1_tcp_demo")
    if args.command in ("all", "segments"):
        result["P2"] = run_segmentation(args.data, args.output / "P2_segments")
    if args.command in ("all", "envelopes"):
        result["P3"] = run_envelopes(args.data, args.output / "P3_envelopes")
    if args.command in ("all", "envelopes-full"):
        result["P3_full"] = run_full_envelopes(args.data, args.output / "P3_full_research")
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / "run_report.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result, indent=2))
