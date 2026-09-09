"""Run the complete existing regression suite and retain its current R03 result."""
import argparse
import json
from pathlib import Path
from run_part1_checks import execute
from build_identity import report_path

def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("--configuration", choices=["Debug", "Release"], required=True)
    parser.add_argument("--report", default="r03_step3_regressions.json")
    parser.add_argument("--build-report")
    parser.add_argument("--build-stage")
    args = parser.parse_args(argv)
    if bool(args.build_report) != bool(args.build_stage):
        parser.error("--build-report and --build-stage must be supplied together")
    try:
        path = report_path(args.report)
    except ValueError as error:
        parser.error(str(error))
    report = json.loads(path.read_text(encoding="utf-8")) if path.exists() else {"actor": "Genepool Analyzer", "configurations": {}}
    report.setdefault("configurations", {})[args.configuration] = {"ok": False, "status": "running"}
    path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    try:
        result = execute(["--all"], args.configuration, 120,
                         build_report=args.build_report, build_stage=args.build_stage)
    except (OSError, ValueError) as error:
        result = {"exit_code": None, "records": [], "wall_seconds": 0,
                  "messages": [str(error)], "stderr": "", "incomplete": "Verification preflight failed"}
    last = result["records"][-1] if result["records"] else {}
    result["ok"] = result["exit_code"] == 0 and not result.get("incomplete") and last.get("failed") == 0 and last.get("total", 0) >= 149
    report["configurations"][args.configuration] = result
    path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"configuration": args.configuration, "report": str(path), "ok": result["ok"], "seconds": result["wall_seconds"], "total": last.get("total"), "passed": last.get("passed"), "failed": last.get("failed"), "messages": result["messages"], "stderr": result["stderr"], "identity_error": result.get("identity_error")}))
    return 0 if result["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
