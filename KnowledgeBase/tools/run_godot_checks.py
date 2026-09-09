"""Bounded Godot verification with current reports and process-local data."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import time
from build_identity import capture_outputs, project_output, project_target, report_path, verify_build, verify_loaded

ROOT = Path(__file__).resolve().parents[2]
KB = ROOT / "KnowledgeBase"
PROJECT = ROOT / "Fistnet.Genepool.Godot"


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("--mode", choices=["smoke", "benchmark", "preview", "headless", "step4", "step4-headless", "step4-benchmark"], default="smoke")
    parser.add_argument("--report", help="Approved current report filename directly inside KnowledgeBase")
    parser.add_argument("--build-report", help="Build evidence report directly inside KnowledgeBase")
    parser.add_argument("--build-stage", help="Exact successful Debug build stage; Release maps to ExportRelease compilation only")
    args = parser.parse_args(argv)
    if bool(args.build_report) != bool(args.build_stage):
        parser.error("--build-report and --build-stage must be supplied together")
    mode = {"headless": "smoke", "step4-headless": "step4"}.get(args.mode, args.mode)
    name = {"headless": "r03_godot_headless.json", "benchmark": "r03_godot_benchmark.json",
            "step4": "r03_step4_verification.json", "step4-headless": "r03_step4_headless.json",
            "step4-benchmark": "r03_step4_benchmark.json"}.get(args.mode, "r03_godot_verification.json")
    try:
        report = report_path(args.report or name)
    except ValueError as error:
        parser.error(str(error))
    allowed = {"r03_godot_verification.json", "r03_godot_headless.json", "r03_godot_benchmark.json",
               "r03_step4_verification.json", "r03_step4_headless.json", "r03_step4_benchmark.json",
               "r04_step1_godot_verification.json", "r04_step1_godot_headless.json", "r04_step1_godot_benchmark.json",
               "r04_step1_godot_step4_verification.json", "r04_step1_godot_step4_headless.json", "r04_step1_godot_step4_benchmark.json"}
    if report.name not in allowed:
        parser.error("--report is not an approved current verification report filename")
    profile = json.loads((PROJECT / "Properties" / "launchSettings.json").read_text(encoding="utf-8-sig"))["profiles"]["Genepool (Godot)"]
    env = os.environ.copy()
    env.update(profile["environmentVariables"])
    engine = ROOT / ".tools" / "godot" / "4.7.2" / "Godot_v4.7.2-stable_mono_win64_console.exe"
    command = [str(engine), "--path", str(PROJECT), "--rendering-method", "gl_compatibility"]
    if args.mode in ("headless", "step4-headless"):
        command += ["--headless"]
    command += ["res://Scenes/Verification.tscn", "--", "--mode=" + mode, "--output=" + str(report)]
    # Invalidate only this runner's current report before starting. A killed process
    # cannot leave an earlier successful result looking like the current attempt.
    report.write_text(json.dumps({"ok": False, "status": "running", "mode": args.mode}) + "\n", encoding="utf-8")
    started = time.monotonic()
    timeout = 100 if mode in ("benchmark", "step4-benchmark") else 90
    expected = None
    identity = {"verified": False, "reason": "Legacy invocation supplied no build report/stage; fresh-build identity is unproven."}
    output = ""
    try:
        project = "Fistnet.Genepool.Godot"
        directory = project_output(project, "Debug")
        assemblies = ["Fistnet.Genepool." + suffix for suffix in ("Godot", "Control", "Dna")]
        if args.build_report:
            required = [directory / (assembly + ".dll") for assembly in assemblies]
            required += [directory / (project + ".runtimeconfig.json"), directory / (project + ".deps.json")]
            identity = verify_build(args.build_report, args.build_stage, "Debug", required)
            expected = capture_outputs(directory, assemblies)
        result = subprocess.run(command, cwd=PROJECT, env=env, capture_output=True, timeout=timeout,
                                creationflags=subprocess.CREATE_NO_WINDOW)
        output = (result.stdout + result.stderr).decode("utf-8", errors="replace")
        data = json.loads(report.read_text(encoding="utf-8"))
        errors = [line for line in output.splitlines() if line.startswith(("ERROR:", "SCRIPT ERROR:"))]
        data["process"] = {"exitCode": result.returncode, "elapsedSeconds": round(time.monotonic() - started, 3), "deadlineSeconds": timeout, "engineErrors": errors}
        data["ok"] = bool(data.get("ok")) and result.returncode == 0 and not errors
        data["target_framework"] = project_target(project)
        data["executed_configuration"] = "Debug"
        data["configuration_limit"] = "Godot verification scene loads Debug; solution Release is a separate ExportRelease compilation check."
        if expected is not None:
            verify_loaded(expected, capture_outputs(directory, assemblies))
            verify_build(args.build_report, args.build_stage, "Debug", required)
            verify_loaded(expected, data.get("assemblySha256"))
            data["loaded_assembly_identity_verified"] = True
            data["expected_assembly_sha256"] = expected
    except subprocess.TimeoutExpired as failure:
        output = ((failure.stdout or b"") + (failure.stderr or b"")).decode("utf-8", errors="replace")
        data = {"ok": False, "mode": args.mode, "incomplete": "External process deadline exceeded; owned process terminated", "deadlineSeconds": timeout}
    except (OSError, ValueError) as failure:
        data = {"ok": False, "mode": args.mode, "identity_or_execution_error": str(failure)}
    data["build_identity"] = identity
    report.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    (ROOT / ".tools" / "godot-process" / (args.mode + ".log")).write_text(output, encoding="utf-8")
    print(json.dumps({"report": str(report), "ok": data["ok"], "process": data.get("process"), "incomplete": data.get("incomplete"), "tail": output.splitlines()[-15:]}))
    return 0 if data["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
