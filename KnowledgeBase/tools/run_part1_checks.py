"""Run bounded local Part 1 checks into one current result file, without test archives.

This wrapper selects exact commands before dispatch. The frozen Tests.exe still
has legacy argument routing when invoked directly; this does not change its code.
"""
import argparse
import datetime
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time
from build_identity import capture_outputs, project_output, project_target, report_path, verify_build, verify_loaded

ROOT = Path(__file__).resolve().parents[2]
KB = ROOT / "KnowledgeBase"
RESULT = KB / "r02_part1_results.json"


def validate_child_arguments(arguments):
    """Reject ambiguous selection; existing child parsers own value/range checks.

    Preview export is intentionally not a check command: its file-write boundary
    belongs to a separately authorized direct invocation, not this wrapper.
    """
    if not isinstance(arguments, (list, tuple)) or not arguments:
        raise ValueError("Exactly one supported child command is required")
    if any(not isinstance(argument, str) or not argument for argument in arguments):
        raise ValueError("Child arguments must be nonempty strings")
    lengths = {
        "--all": (1,),
        "--group": (2,),
        "--scenario": (4,),
        "--diagnostic-scenario": (7, 8),
        "--diagnostic-fixture": (4, 5),
        "--baseline-smoke": (1,),
        "--runner-negative-control": (1,),
    }
    command = arguments[0]
    if command not in lengths:
        raise ValueError(f"Unsupported child command: {command}")
    if len(arguments) not in lengths[command]:
        raise ValueError(f"Wrong argument count for {command}; no missing or extra arguments are allowed")
    if any(argument.startswith("--") for argument in arguments[1:]):
        raise ValueError("Select one child command; embedded, repeated or conflicting flags are not allowed")
    return list(arguments)


def execute(arguments, configuration, timeout, *, build_report=None, build_stage=None):
    arguments = validate_child_arguments(arguments)
    project = "Fistnet.Genepool.Tests"
    directory = project_output(project, configuration)
    executable = directory / (project + ".exe")
    row = {"arguments": arguments, "configuration": configuration,
           "target_framework": project_target(project), "executable": str(executable),
           "at_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "budget_seconds": timeout}
    expected = None
    if build_report is not None or build_stage is not None:
        assemblies = ["Fistnet.Genepool." + name for name in ("Tests", "Control", "Dna", "Visualization", "App")]
        required = [directory / (name + ".dll") for name in assemblies]
        required += [executable, directory / (project + ".runtimeconfig.json"), directory / (project + ".deps.json")]
        row["build_identity"] = verify_build(build_report, build_stage, configuration, required)
        expected = capture_outputs(directory, assemblies)
        row["expected_assembly_sha256"] = expected
    else:
        row["build_identity"] = {"verified": False, "reason": "Legacy invocation supplied no build report/stage; fresh-build identity is unproven."}
    started = time.monotonic()
    with tempfile.TemporaryDirectory(prefix=".diagnostics-", dir=KB) as scratch:
        env = os.environ.copy()
        for key in ("TEMP", "TMP", "LOCALAPPDATA", "APPDATA"):
            env[key] = scratch
        process = subprocess.Popen([str(executable), *arguments], cwd=ROOT, env=env,
                                   stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                   creationflags=subprocess.CREATE_NO_WINDOW)
        try:
            output, error = process.communicate(timeout=timeout)
        except subprocess.TimeoutExpired:
            # This is only the process tree started immediately above, never an app-name kill.
            termination = subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"],
                                         capture_output=True, timeout=10, creationflags=subprocess.CREATE_NO_WINDOW)
            output, error = process.communicate(timeout=10)
            row["incomplete"] = "External process budget expired; last complete JSONL checkpoint retained."
            row["termination_exit_code"] = termination.returncode
        row["exit_code"] = process.returncode
        row["wall_seconds"] = round(time.monotonic() - started, 6)
        row["records"] = []
        non_json = []
        for line in output.decode("utf-8", errors="replace").splitlines():
            try:
                row["records"].append(json.loads(line))
            except json.JSONDecodeError:
                non_json.append(line)
        # Successful test names are already in the structured suite result.
        row["messages"] = [line for line in non_json if not line.startswith("PASS ")]
        row["stderr"] = error.decode("utf-8", errors="replace")
    row["scratch_removed"] = not Path(scratch).exists()
    if expected is not None:
        try:
            verify_loaded(expected, capture_outputs(directory, expected))
            verify_build(build_report, build_stage, configuration, required)
            if arguments[0] in ("--all", "--group"):
                last = row["records"][-1] if row["records"] else {}
                verify_loaded(expected, last.get("assemblySha256"))
                row["loaded_assembly_identity_verified"] = True
            else:
                row["loaded_assembly_identity_verified"] = False
                row["identity_limit"] = "Command has no loaded-assembly report; executed output matched the build before and after dispatch."
        except (OSError, ValueError) as error:
            row["identity_error"] = str(error)
            row["exit_code"] = row["exit_code"] or 1
    return row


def save(stage, value, destination=RESULT):
    data = json.loads(destination.read_text(encoding="utf-8")) if destination.exists() else {"actor": "Genepool Analyzer"}
    data.setdefault("checks", {})[stage] = value
    destination.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("--stage", required=True)
    parser.add_argument("--configuration", choices=["Debug", "Release"], default="Release")
    parser.add_argument("--timeout", type=int, default=120)
    parser.add_argument("--batch", choices=["reference", "production"])
    parser.add_argument("--report", help="Current JSON report directly inside KnowledgeBase; leaves prior reports untouched")
    parser.add_argument("--build-report", help="Build evidence report directly inside KnowledgeBase")
    parser.add_argument("--build-stage", help="Exact successful stage used to build the executed configuration")
    parser.add_argument("arguments", nargs=argparse.REMAINDER)
    args = parser.parse_args(argv)
    if not 1 <= args.timeout <= 120:
        parser.error("Process timeout must be 1..120 seconds")
    arguments = args.arguments[1:] if args.arguments[:1] == ["--"] else args.arguments
    if args.batch and arguments:
        parser.error("--batch cannot be combined with explicit child arguments")
    if not args.batch:
        try:
            arguments = validate_child_arguments(arguments or ["--all"])
        except ValueError as error:
            parser.error(str(error))
    if bool(args.build_report) != bool(args.build_stage):
        parser.error("--build-report and --build-stage must be supplied together")
    try:
        destination = report_path(args.report) if args.report else RESULT
    except ValueError as error:
        parser.error(str(error))
    if not args.report and not RESULT.exists():
        parser.error("Capture the unchanged pre-instrumentation baseline first")
    proof = {"build_report": args.build_report, "build_stage": args.build_stage} if args.build_report else {}
    if args.batch:
        budget = 300 if args.batch == "reference" else 600
        jobs = []
        if args.batch == "reference":
            for seed in (11, 29):
                for observe in ("off", "on"):
                    jobs.append((f"reference-{seed}-{observe}", 60,
                                 ["--diagnostic-scenario", str(seed), "128", "reference", observe, "1",
                                  "render" if observe == "on" else "no-render"]))
        else:
            for seed in (11, 29, 47, 83):
                jobs.append((f"production-{seed}", 120,
                             ["--diagnostic-scenario", str(seed), "2048", "production", "on", "8", "no-render"]))
            jobs.append(("production-single-season", 60,
                         ["--diagnostic-scenario", "11", "128", "production", "on", "1", "no-render"]))
            for density in (0, 10, 50, 100):
                for history in ("early", "large"):
                    for observe in ("off", "on"):
                        jobs.append((f"fixture-{density}-{history}-{observe}", 60,
                                     ["--diagnostic-fixture", str(density), history, observe, "8"]))
        # Preflight the complete batch before launching even its first child.
        for _, _, child_arguments in jobs:
            validate_child_arguments(child_arguments)
        started = time.monotonic()
        batch = {"budget_seconds": budget, "runs": [], "unstarted": []}
        for index, (name, timeout, arguments) in enumerate(jobs):
            remaining = budget - (time.monotonic() - started)
            if remaining < 1:
                batch["unstarted"] = [j[0] for j in jobs[index:]]
                break
            result = execute(arguments, args.configuration, min(timeout, remaining), **proof)
            result["name"] = name
            batch["runs"].append(result)
            batch["wall_seconds"] = time.monotonic() - started
            save(args.stage, batch, destination)
            print(json.dumps({"name": name, "exit_code": result["exit_code"],
                              "wall_seconds": result["wall_seconds"], "records": len(result["records"]),
                              "incomplete": result.get("incomplete")}), flush=True)
        batch["wall_seconds"] = time.monotonic() - started
        save(args.stage, batch, destination)
        return 0 if not batch["unstarted"] and all(r["exit_code"] == 0 for r in batch["runs"]) else 1
    result = execute(arguments, args.configuration, args.timeout, **proof)
    save(args.stage, result, destination)
    last = result["records"][-1] if result["records"] else {}
    print(json.dumps({"stage": args.stage, "exit_code": result["exit_code"], "wall_seconds": result["wall_seconds"],
                      "result": {k: v for k, v in last.items() if k in ("total", "passed", "failed", "seconds")},
                      "messages": result["messages"], "stderr": result["stderr"]}), flush=True)
    return 0 if result["exit_code"] == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
