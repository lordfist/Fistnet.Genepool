"""Bounded current Part 4 checks; no automatic trials, retries, or history trees.

The four comparison configurations and two reserved seeds are fixed by
R02_PART4_CONTRACT.md. Calling --confirmation is the coordinator's explicit
dispatch after recording candidate selection; this tool never selects or launches
confirmation automatically. Existing child command validation remains unchanged.
"""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import time

from run_part1_checks import execute as execute_suite

ROOT = Path(__file__).resolve().parents[2]
KB = ROOT / "KnowledgeBase"
RESULT = KB / "r02_part4_results.json"
CONTRACT = KB / "R02_PART4_CONTRACT.md"
CANDIDATES = ("default", "constrained", "regrowth2", "capped")


def validate_ecology_arguments(arguments):
    """Exact independent allowlist; validation completes before any process starts."""
    if not isinstance(arguments, (list, tuple)) or len(arguments) != 6:
        raise ValueError("Exactly six ecology arguments are required")
    if any(not isinstance(a, str) or not a or a != a.strip() for a in arguments):
        raise ValueError("Ecology arguments must be nonempty strings without surrounding whitespace")
    if arguments[0] != "--ecology-scenario" or any(a.startswith("--") for a in arguments[1:]):
        raise ValueError("Only one ecology scenario command may be selected")
    if not re.fullmatch(r"[+-]?[0-9]+", arguments[1]) or not -2147483648 <= int(arguments[1]) <= 2147483647:
        raise ValueError("Seed must be a signed 32-bit integer")
    if not re.fullmatch(r"[0-9]+", arguments[2]) or not 1 <= int(arguments[2]) <= 2048:
        raise ValueError("Ecology horizon must be 1..2048 seasons")
    if arguments[3] not in ("production", "reference") or arguments[4] not in CANDIDATES or arguments[5] not in ("on", "off"):
        raise ValueError("Unknown mode, fixed candidate or observer selection")
    return list(arguments)


def execute_ecology(arguments, configuration, timeout):
    arguments = validate_ecology_arguments(arguments)
    if configuration not in ("Debug", "Release") or not 0 < timeout <= 120:
        raise ValueError("Use Debug/Release and a positive process limit no greater than 120 seconds")
    executable = ROOT / f"Fistnet.Genepool.Tests/bin/{configuration}/net9.0-windows7.0/Fistnet.Genepool.Tests.exe"
    row = {"arguments": arguments, "configuration": configuration,
           "at_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "budget_seconds": timeout,
           "records": [], "messages": [], "stderr": "", "exit_code": None, "process_started": False}
    started = time.monotonic()
    # Same proven disposable cache boundary as run_part1_checks; the new command
    # has its own allowlist instead of weakening the old wrapper's validator.
    with tempfile.TemporaryDirectory(prefix=".diagnostics-", dir=KB) as scratch:
        env = os.environ.copy()
        for key in ("TEMP", "TMP", "LOCALAPPDATA", "APPDATA"):
            env[key] = scratch
        try:
            process = subprocess.Popen([str(executable), *arguments], cwd=ROOT, env=env,
                                       stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                       creationflags=subprocess.CREATE_NO_WINDOW)
            row["process_started"] = True
        except OSError as error:
            row["launch_error"] = str(error)
            row["incomplete"] = "Process could not be started; no trial was executed."
        else:
            try:
                output, error = process.communicate(timeout=timeout)
            except subprocess.TimeoutExpired:
                # Kill only the exact child tree created above, never by app name.
                termination = subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"],
                                             capture_output=True, timeout=10,
                                             creationflags=subprocess.CREATE_NO_WINDOW)
                row["termination_exit_code"] = termination.returncode
                output, error = process.communicate(timeout=10)
                row["incomplete"] = "External process budget expired; only complete flushed JSONL records are retained."
            row["exit_code"] = process.returncode
            for line in output.decode("utf-8", errors="replace").splitlines():
                try:
                    record = json.loads(line)
                    if not isinstance(record, dict):
                        raise ValueError("Expected a JSON object")
                    row["records"].append(record)
                except (json.JSONDecodeError, ValueError):
                    row["messages"].append(line)
            row["stderr"] = error.decode("utf-8", errors="replace")
    row["scratch_removed"] = not Path(scratch).exists()
    row["wall_seconds"] = round(time.monotonic() - started, 6)
    return summarize_ecology_records(row)


def summarize_ecology_records(row):
    """Retain prefix violations even when no terminal line survives interruption."""
    finals = [r for r in row["records"] if r.get("kind") == "final"]
    row["terminal_records"] = len(finals)
    if len(finals) != 1 or row["records"][-1:] != finals[-1:]:
        row.setdefault("incomplete", "Exactly one complete terminal JSON record was not observed; an unflushed final state is unknown.")
    elif not finals[0].get("complete"):
        row.setdefault("incomplete", finals[0].get("reason") or "Requested horizon was not completed.")
    if finals:
        row["ecological_screen"] = finals[-1].get("ecologicalScreen", "inconclusive")
        row["completed_seasons"] = finals[-1].get("completedSeasons")
    retained = next((r for r in reversed(row["records"]) if r.get("criteria") is not None), None)
    row["last_flushed_criteria"] = retained.get("criteria") if retained else None
    row["criteria_evidence_season"] = retained.get("criteriaEvidenceSeason") if retained else None
    if retained and retained.get("ecologicalScreen") in ("observed_failure", "invalid_accounting"):
        row["ecological_screen"] = retained["ecologicalScreen"]
    elif not finals:
        row["ecological_screen"] = "inconclusive_horizon"
    row["criteria_coverage"] = "Criteria are detached cumulative evidence through criteria_evidence_season, not proof of a later unflushed final world. Incomplete horizon and observed violation are separate."
    return row


def save(data):
    """Replace one derived report atomically; retain each named attempted stage."""
    data["updated_at_utc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    descriptor, temporary = tempfile.mkstemp(prefix=".part4-report-", suffix=".tmp", dir=KB)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as stream:
            json.dump(data, stream, indent=2)
            stream.write("\n")
        os.replace(temporary, RESULT)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def terminal(row):
    return next((r for r in reversed(row["records"]) if r.get("kind") == "final"), {})


def reference_comparison(rows):
    comparisons = []
    for seed in (11, 29):
        pair = {r["arguments"][5]: r for r in rows if r["arguments"][1] == str(seed)}
        off, on = pair.get("off"), pair.get("on")
        left, right = terminal(off) if off else {}, terminal(on) if on else {}
        eligible = all(r and r.get("exit_code") == 0 and not r.get("incomplete") for r in (off, on))
        lrandom, rrandom = left.get("random", {}), right.get("random", {})
        matched = bool(eligible and lrandom.get("stateSha256") and lrandom == rrandom)
        comparisons.append({"seed": seed, "complete": bool(eligible), "state_and_rng_equal": matched,
                            "off_random": lrandom, "on_random": rrandom})
    return comparisons


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--stage", required=True)
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Release")
    selection = parser.add_mutually_exclusive_group(required=True)
    selection.add_argument("--suite", action="store_true")
    selection.add_argument("--reference", action="store_true")
    selection.add_argument("--comparison", choices=CANDIDATES)
    selection.add_argument("--confirmation", choices=CANDIDATES)
    arguments = list(sys.argv[1:] if argv is None else argv)
    for flag in ("--stage", "--configuration", "--suite", "--reference", "--comparison", "--confirmation"):
        if sum(argument == flag or argument.startswith(flag + "=") for argument in arguments) > 1:
            parser.error("Repeated options are ambiguous: " + flag)
    args = parser.parse_args(arguments)
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_.-]{0,99}", args.stage):
        parser.error("Stage must be a short nonempty name using letters, numbers, dash, underscore or dot")
    if not CONTRACT.is_file():
        parser.error("The prospective R02_PART4_CONTRACT.md must exist before dispatch")
    contract_sha = hashlib.sha256(CONTRACT.read_bytes()).hexdigest().upper()
    if args.suite:
        kind, budget = "suite", 120
        jobs = [(args.stage, 120, ["--all"])]
    elif args.reference:
        kind, budget = "reference-passivity", 300
        jobs = [(f"reference-{seed}-{observe}", 60,
                 ["--ecology-scenario", str(seed), "128", "reference", "default", observe])
                for seed in (11, 29) for observe in ("off", "on")]
    else:
        candidate = args.comparison or args.confirmation
        kind = "comparison" if args.comparison else "reserved-confirmation"
        budget = 600 if args.comparison else 300
        seeds = (11, 29, 47, 83) if args.comparison else (101, 211)
        jobs = [(f"{kind}-{candidate}-{seed}", 120,
                 ["--ecology-scenario", str(seed), "2048", "production", candidate, "on"])
                for seed in seeds]
    if not args.suite:
        for _, _, command in jobs:
            validate_ecology_arguments(command)
    data = json.loads(RESULT.read_text(encoding="utf-8")) if RESULT.exists() else {
        "actor": "Genepool Analyzer", "part": "R02 Part4", "checks": {}}
    if data.get("actor") != "Genepool Analyzer" or data.get("part") != "R02 Part4" or not isinstance(data.get("checks"), dict):
        parser.error("Existing current result file has an unexpected identity or schema")
    if args.stage in data["checks"]:
        parser.error("Stage already exists. Do not silently retry or erase an attempted stage.")
    batch = {"kind": kind, "configuration": args.configuration, "budget_seconds": budget,
             "contract_sha256": contract_sha, "planned": [j[0] for j in jobs],
             "runs": [], "unstarted": [], "pending": None, "complete": False}
    data["checks"][args.stage] = batch
    started = time.monotonic()
    save(data)
    for index, (name, limit, command) in enumerate(jobs):
        remaining = budget - (time.monotonic() - started)
        if remaining < 1:
            batch["unstarted"] = [j[0] for j in jobs[index:]]
            break
        timeout = min(limit, remaining)
        batch["pending"] = {"name": name, "arguments": command, "budget_seconds": timeout}
        save(data)  # A crash leaves explicit pending evidence, never a hidden retry.
        print(json.dumps({"stage": args.stage, "starting": name, "budget_seconds": timeout}), flush=True)
        try:
            row = execute_suite(command, args.configuration, timeout) if args.suite else execute_ecology(command, args.configuration, timeout)
        except Exception as error:
            # Cleanup/process termination uncertainty requires inspection before
            # any further job. Preserve the pending command and stop this batch.
            batch["execution_error"] = {"name": name, "type": type(error).__name__, "message": str(error)}
            batch["unstarted"] = [j[0] for j in jobs[index + 1:]]
            break
        row["name"] = name
        batch["runs"].append(row)
        batch["pending"] = None
        batch["wall_seconds"] = round(time.monotonic() - started, 6)
        save(data)
        last = row["records"][-1] if row["records"] else {}
        print(json.dumps({"finished": name, "exit_code": row["exit_code"], "seconds": row["wall_seconds"],
                          "completed_seasons": row.get("completed_seasons"),
                          "ecological_screen": row.get("ecological_screen"), "incomplete": row.get("incomplete"),
                          "suite": {k: last[k] for k in ("total", "passed", "failed") if k in last},
                          "messages": row["messages"][:6], "stderr": row["stderr"][:1200]}), flush=True)
    batch["wall_seconds"] = round(time.monotonic() - started, 6)
    batch["complete"] = not batch["unstarted"] and batch["pending"] is None and len(batch["runs"]) == len(jobs)
    batch["ok"] = batch["complete"] and all(r["exit_code"] == 0 and not r.get("incomplete") for r in batch["runs"])
    if args.reference:
        batch["passivity"] = reference_comparison(batch["runs"])
        batch["ok"] = batch["ok"] and all(p["state_and_rng_equal"] for p in batch["passivity"])
    elif not args.suite:
        batch["all_requested_runs_passed_ecological_screen"] = batch["ok"] and all(
            r.get("ecological_screen") == "passed" for r in batch["runs"])
        batch["interpretation"] = "ok describes completed process/horizon coverage; ecological success is separate. Timeouts never pass the requested horizon, even without an observed violation."
    save(data)
    print(json.dumps({"stage": args.stage, "complete": batch["complete"], "ok": batch["ok"],
                      "all_requested_runs_passed_ecological_screen": batch.get("all_requested_runs_passed_ecological_screen"),
                      "unstarted": batch["unstarted"], "pending": batch["pending"],
                      "execution_error": batch.get("execution_error")}), flush=True)
    return 0 if batch["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
