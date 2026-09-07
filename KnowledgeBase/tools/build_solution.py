"""Local VS2022 builds with an explicit feed, shared package cache and disposable process data."""
import argparse
import collections
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile
import time

ROOT = Path(__file__).resolve().parents[2]
KB = ROOT / "KnowledgeBase"
DOTNET = r"C:\Program Files\dotnet\dotnet.exe"
MSBUILD = r"D:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe"
SDK = Path(r"C:\Program Files\dotnet\sdk\9.0.315")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--stage", required=True)
    parser.add_argument("--configuration", default="Debug", choices=["Debug", "Release"])
    parser.add_argument("--platform", default="Any CPU", choices=["Any CPU", "Mixed Platforms", "x86"])
    parser.add_argument("--report", default="implementation_status.json",
                        help="Current result file inside KnowledgeBase")
    args = parser.parse_args()
    destination = (KB / args.report).resolve()
    if destination.parent != KB.resolve() or destination.suffix != ".json":
        parser.error("--report must name a JSON file directly inside KnowledgeBase")
    report = {"at_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "stage": args.stage, "configuration": args.configuration, "platform": args.platform, "builds": [],
              "source_sha256": {}}
    for project in ROOT.glob("Fistnet.Genepool.*"):
        if project.is_dir():
            for path in project.rglob("*"):
                if path.suffix in {".cs", ".csproj", ".godot", ".tscn", ".tres", ".gdshader", ".uid"} and not set(path.parts) & {"bin", "obj", ".vs", ".godot"}:
                    report["source_sha256"][path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    report["source_sha256"]["Fistnet.Genepool.sln"] = hashlib.sha256((ROOT / "Fistnet.Genepool.sln").read_bytes()).hexdigest()
    if (ROOT / "global.json").exists():
        report["source_sha256"]["global.json"] = hashlib.sha256((ROOT / "global.json").read_bytes()).hexdigest()
    if (ROOT / "NuGet.Config").exists():
        report["source_sha256"]["NuGet.Config"] = hashlib.sha256((ROOT / "NuGet.Config").read_bytes()).hexdigest()
    with tempfile.TemporaryDirectory(prefix=".build-", dir=KB) as temp:
        scratch = Path(temp)
        env = os.environ.copy()
        for key, folder in {"LOCALAPPDATA":"local", "APPDATA":"roaming", "TEMP":"tmp", "TMP":"tmp",
                            "DOTNET_CLI_HOME":"cli", "NUGET_HTTP_CACHE_PATH":"http",
                            "NUGET_PLUGINS_CACHE_PATH":"plugins"}.items():
            path = scratch / folder
            path.mkdir(exist_ok=True)
            env[key] = str(path)
        packages = ROOT / ".tools" / "nuget" / "packages"
        packages.mkdir(parents=True, exist_ok=True)
        env["NUGET_PACKAGES"] = str(packages)
        env.update(DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1", DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1", DOTNET_ADD_GLOBAL_TOOLS_TO_PATH="false", DOTNET_GENERATE_ASPNET_CERTIFICATE="false",
                   DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE="true", MSBuildEnableWorkloadResolver="false",
                   DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER="1", MSBUILDDISABLENODEREUSE="1",
                   MSBuildSDKsPath=str(SDK / "Sdks"), DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR=str(SDK / "Sdks"))
        (scratch / "global.json").write_text('{"sdk":{"version":"9.0.315","rollForward":"disable"}}')
        config = scratch / "NuGet.Config"
        # SDK resolution happens during project evaluation, before restore. Give
        # both the scratch working directory and explicit restore the same feed.
        config.write_text('<configuration><packageSources><clear />'
                          '<add key="nuget.org" value="https://api.nuget.org/v3/index.json" />'
                          '</packageSources></configuration>')
        common = ["-p:NuGetAudit=false", "-p:UseSharedCompilation=false", "-p:TargetPlatformSdkPath=D:\\Windows Kits\\10",
                  "-p:TargetPlatformDisplayName=Windows", "-nr:false", "-nologo", "-verbosity:minimal"]
        solution = str(ROOT / "Fistnet.Genepool.sln")
        commands = [
            [DOTNET, "restore", solution, "--configfile", str(config), "--disable-parallel",
             "-p:Configuration=" + args.configuration, "-p:Platform=" + args.platform, *common],
            [MSBUILD, solution, "-t:Rebuild", "-p:Configuration=" + args.configuration, "-p:Platform=" + args.platform, *common],
        ]
        for command in commands:
            start = time.monotonic()
            try:
                result = subprocess.run(command, cwd=scratch, env=env, capture_output=True, timeout=120)
                output = (result.stdout + result.stderr).decode("utf-8", errors="replace")
                codes = collections.Counter(re.findall(r"warning (\w+)\s*:", output))
                errors = [line for line in output.splitlines() if re.search(r"\berror\b", line, re.I)]
                item = {"tool": Path(command[0]).name, "exit_code": result.returncode,
                        "seconds": round(time.monotonic() - start, 3), "warning_codes": dict(codes),
                        "errors": errors[:20], "output_tail": output.splitlines()[-5:]}
            except subprocess.TimeoutExpired:
                item = {"tool": Path(command[0]).name, "exit_code": None, "incomplete": "120-second timeout"}
            report["builds"].append(item)
            print(json.dumps(item), flush=True)
            if item["exit_code"] != 0:
                break
    report["scratch_removed"] = not scratch.exists()
    report["ok"] = len(report["builds"]) == 2 and all(row["exit_code"] == 0 for row in report["builds"])
    status = json.loads(destination.read_text()) if destination.exists() else {"actor": "Seed Analyzer", "stages": {}}
    status["stages"][args.stage] = report
    destination.write_text(json.dumps(status, indent=2) + "\n", encoding="utf-8")
    return 0 if report["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
