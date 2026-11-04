"""再生成用の静的PNG/SVG出力。"""
from pathlib import Path
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt


def save_figure(fig, output: Path, stem: str):
    output.mkdir(parents=True, exist_ok=True)
    fig.savefig(output / f"{stem}.png", dpi=180, bbox_inches="tight")
    fig.savefig(output / f"{stem}.svg", bbox_inches="tight")
    plt.close(fig)
