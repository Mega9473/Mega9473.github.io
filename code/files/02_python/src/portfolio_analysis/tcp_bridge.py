"""dSPACE/Unityの改行区切り2値通信。実機APIと通信形式を分離する。"""
from __future__ import annotations

import json
import socket
import time
from pathlib import Path
from typing import Protocol


class Device(Protocol):
    def read_pair(self) -> tuple[float, float]: ...
    def write_pair(self, pair: tuple[float, float]) -> None: ...
    def close(self) -> None: ...


def encode_measurement(pair: tuple[float, float]) -> bytes:
    """元コードと同じ小数4桁・カンマ・LFで送信する。"""
    return f"{pair[0]:.4f},{pair[1]:.4f}\n".encode("utf-8")


class CommandBuffer:
    """部分行を保持し、1回の受信で届いた最後の候補行を処理する。"""
    def __init__(self):
        self.pending = ""

    def feed(self, chunk: bytes) -> tuple[float, float] | None:
        self.pending += chunk.decode("utf-8")
        lines = self.pending.split("\n")
        if self.pending.endswith("\n"):
            self.pending = ""
        else:
            self.pending = lines[-1]
            lines = lines[:-1]
        # 元実装通り、最後のカンマ付き行が不正なら古い行へ戻らない。
        candidate = next((line.strip() for line in reversed(lines) if "," in line.strip()), None)
        if candidate is None:
            return None
        parts = candidate.split(",")
        if len(parts) != 2:
            return None
        try:
            return float(parts[0]), float(parts[1])
        except ValueError:
            return None


def exchange_once(sock: socket.socket, device: Device, parser: CommandBuffer):
    values = device.read_pair()
    sock.sendall(encode_measurement(values))
    command = None
    try:
        data = sock.recv(4096)
        if not data:
            raise ConnectionError("Peer closed the connection")
        command = parser.feed(data)
        if command is not None:
            device.write_pair(command)
    except socket.timeout:
        pass
    return values, command


class DSpaceDevice:
    """実機専用。pythonnetとdSPACE XIL 2.2の導入は別途必要。"""
    def __init__(self, config: dict):
        import clr
        clr.AddReference("ASAM.XIL.Implementation.TestbenchFactory, Version=2.2.0.0, Culture=neutral, PublicKeyToken=bf471dff114ae984")
        clr.AddReference("ASAM.XIL.Interfaces, Version=2.2.0.0, Culture=neutral, PublicKeyToken=bf471dff114ae984")
        from ASAM.XIL.Implementation.TestbenchFactory.Testbench import TestbenchFactory
        from ASAM.XIL.Interfaces.Testbench.Common.VariableRef.Enum import ValueRepresentation
        bench = TestbenchFactory().CreateVendorSpecificTestbench("dSPACE GmbH", "XIL API", config["release_version"])
        self.value_factory = bench.ValueFactory
        self.port = bench.MAPortFactory.CreateMAPort("DemoMAPort")
        self.port.Configure(self.port.LoadConfiguration(config["maport_config_file"]), False)
        make_ref = lambda p: bench.VariableRefFactory.CreateGenericVariableRef(p, ValueRepresentation.ePhysicalValue)
        self.read_refs = [make_ref(p) for p in config["read_paths"]]
        self.write_refs = [make_ref(p) for p in config["write_paths"]]
        if len(self.read_refs) != 2 or len(self.write_refs) != 2:
            raise ValueError("Two read paths and two write paths are required")

    def read_pair(self):
        return tuple(float(self.port.Read2(ref).Value) for ref in self.read_refs)

    def write_pair(self, pair):
        for ref, value in zip(self.write_refs, pair):
            self.port.Write2(ref, self.value_factory.CreateFloatValue(value))

    def close(self):
        self.port.Dispose()


def run_real(config_file: Path, duration: float):
    """実機接続は明示的なrealサブコマンドからのみ実行。未実機検証。"""
    config = json.loads(config_file.read_text(encoding="utf-8"))
    device = DSpaceDevice(config)
    try:
        with socket.create_connection((config["host"], int(config["port"])), timeout=3.0) as sock:
            sock.settimeout(0.01)
            parser = CommandBuffer()
            stop = time.monotonic() + duration
            while time.monotonic() < stop:
                exchange_once(sock, device, parser)
                time.sleep(0.001)
    finally:
        device.close()
