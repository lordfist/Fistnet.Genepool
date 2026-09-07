"""Bounded Godot verification with current reports and process-local data."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import time

ROOT = Path(__file__).resolve().parents[2]
KB = ROOT / "KnowledgeBase"
PROJECT = ROOT / "Fistnet.Genepool.Godot"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--mode", choices=["smoke", "benchmark", "preview", "headless"], default="smoke")
    args = parser.parse_args()
    mode = "smoke" if args.mode == "headless" else args.mode
    name = {"headless": "r03_godot_headless.json", "benchmark": "r03_godot_benchmark.json"}.get(args.mode, "r03_godot_verification.json")
    report = KB / name
    profile = json.loads((PROJECT / "Properties" / "launchSettings.json").read_text(encoding="utf-8-sig"))["profiles"]["Genepool (Godot)"]
    env = os.environ.copy()
    env.update(profile["environmentVariables"])
    engine = ROOT / ".tools" / "godot" / "4.7.2" / "Godot_v4.7.2-stable_mono_win64_console.exe"
    command = [str(engine), "--path", str(PROJECT), "--rendering-method", "gl_compatibility"]
    if args.mode == "headless":
        command += ["--headless"]
    command += ["res://Scenes/Verification.tscn", "--", "--mode=" + mode, "--output=" + str(report)]
    # Invalidate only this runner's current report before starting. A killed process
    # cannot leave an earlier successful result looking like the current attempt.
    report.write_text(json.dumps({"ok": False, "status": "running", "mode": args.mode}) + "\n", encoding="utf-8")
    started = time.monotonic()
    timeout = 100 if mode == "benchmark" else 90
    try:
        result = subprocess.run(command, cwd=PROJECT, env=env, capture_output=True, timeout=timeout)
        output = (result.stdout + result.stderr).decode("utf-8", errors="replace")
        data = json.loads(report.read_text(encoding="utf-8"))
        errors = [line for line in output.splitlines() if line.startswith(("ERROR:", "SCRIPT ERROR:"))]
        data["process"] = {"exitCode": result.returncode, "elapsedSeconds": round(time.monotonic() - started, 3), "deadlineSeconds": timeout, "engineErrors": errors}
        data["ok"] = bool(data.get("ok")) and result.returncode == 0 and not errors
    except subprocess.TimeoutExpired as failure:
        output = ((failure.stdout or b"") + (failure.stderr or b"")).decode("utf-8", errors="replace")
        data = {"ok": False, "mode": args.mode, "incomplete": "External process deadline exceeded; owned process terminated", "deadlineSeconds": timeout}
    report.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    (ROOT / ".tools" / "godot-process" / (args.mode + ".log")).write_text(output, encoding="utf-8")
    print(json.dumps({"report": str(report), "ok": data["ok"], "process": data.get("process"), "incomplete": data.get("incomplete"), "tail": output.splitlines()[-15:]}))
    return 0 if data["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
