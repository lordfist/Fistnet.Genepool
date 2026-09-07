"""Run the complete existing regression suite and retain its current R03 result."""
import argparse
import json
from pathlib import Path
from run_part1_checks import execute

parser = argparse.ArgumentParser()
parser.add_argument("--configuration", choices=["Debug", "Release"], required=True)
args = parser.parse_args()
result = execute(["--all"], args.configuration, 120)
last = result["records"][-1] if result["records"] else {}
result["ok"] = result["exit_code"] == 0 and not result.get("incomplete") and last.get("failed") == 0 and last.get("total", 0) >= 149
path = Path(__file__).resolve().parents[1] / "r03_step3_regressions.json"
report = json.loads(path.read_text(encoding="utf-8")) if path.exists() else {"actor": "Genepool Analyzer", "configurations": {}}
report["configurations"][args.configuration] = result
path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"configuration": args.configuration, "ok": result["ok"], "seconds": result["wall_seconds"], "total": last.get("total"), "passed": last.get("passed"), "failed": last.get("failed"), "messages": result["messages"], "stderr": result["stderr"]}))
raise SystemExit(0 if result["ok"] else 1)
