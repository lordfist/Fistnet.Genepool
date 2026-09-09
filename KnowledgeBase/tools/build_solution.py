"""Bounded VS2026 builds using root SDK/NuGet policy and disposable process data."""
import argparse
import collections
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import time
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
KB = ROOT / "KnowledgeBase"
RESOLVER_OVERRIDES = (
    "MSBuildSDKsPath", "DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR",
    "DOTNET_MSBUILD_SDK_RESOLVER_SDKS_VER", "DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR",
)


def resolve_solution(root, supplied=None):
    path = Path(supplied) if supplied else Path(
        "Fistnet.Genepool.slnx" if (root / "Fistnet.Genepool.slnx").is_file()
        else "Fistnet.Genepool.sln")
    path = (root / path).resolve()
    if path.parent != root.resolve() or path.suffix.lower() not in (".sln", ".slnx") or not path.is_file():
        raise ValueError("--solution must name an existing .sln or .slnx directly inside the project root")
    return path


def isolated_environment(root, scratch):
    env = os.environ.copy()
    override_names = {key.upper() for key in RESOLVER_OVERRIDES}
    removed = {key: env.pop(key) for key in list(env) if key.upper() in override_names}
    for key, folder in {"LOCALAPPDATA": "local", "APPDATA": "roaming", "TEMP": "tmp", "TMP": "tmp",
                        "DOTNET_CLI_HOME": "cli", "NUGET_HTTP_CACHE_PATH": "http",
                        "NUGET_PLUGINS_CACHE_PATH": "plugins"}.items():
        path = scratch / folder
        path.mkdir(exist_ok=True)
        env[key] = str(path)
    packages = root / ".tools" / "nuget" / "packages"
    packages.mkdir(parents=True, exist_ok=True)
    env["NUGET_PACKAGES"] = str(packages)
    env.update(DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1", DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1",
               DOTNET_ADD_GLOBAL_TOOLS_TO_PATH="false", DOTNET_GENERATE_ASPNET_CERTIFICATE="false",
               DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE="true", MSBuildEnableWorkloadResolver="false",
               DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER="1", MSBUILDDISABLENODEREUSE="1")
    return env, sorted(removed)


def terminate_tree(process):
    """Terminate only the child tree this helper owns, including timed-out builds."""
    if os.name == "nt":
        result = subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"],
                                capture_output=True, timeout=10, creationflags=subprocess.CREATE_NO_WINDOW)
        return result.returncode
    process.kill()
    return 0


def run_command(command, root, env, timeout):
    started = time.monotonic()
    options = {"cwd": root, "env": env, "stdout": subprocess.PIPE, "stderr": subprocess.PIPE}
    if os.name == "nt":
        options["creationflags"] = subprocess.CREATE_NO_WINDOW
    process = subprocess.Popen([str(part) for part in command], **options)
    row = {"command": [str(part) for part in command], "tool": Path(command[0]).name}
    try:
        output, error = process.communicate(timeout=timeout)
    except subprocess.TimeoutExpired:
        row["incomplete"] = f"{timeout}-second timeout; owned process tree termination requested"
        row["termination_exit_code"] = terminate_tree(process)
        try:
            output, error = process.communicate(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            row["cleanup_error"] = "Owned process did not settle within the termination deadline"
            output, error = b"", b""
    except BaseException:
        terminate_tree(process)
        process.wait(timeout=10)
        raise
    text = (output + error).decode("utf-8", errors="replace")
    row.update(exit_code=process.poll(), seconds=round(time.monotonic() - started, 3),
               warning_codes=dict(collections.Counter(re.findall(r"warning (\w+)\s*:", text))),
               errors=[line for line in text.splitlines() if re.search(r"\berror\b", line, re.I)][:20],
               output_tail=text.splitlines()[-12:])
    return row, text


def checked_query(command, root, env, report):
    row, output = run_command(command, root, env, 30)
    report.setdefault("tool_queries", []).append(row)
    if row["exit_code"] != 0 or row.get("incomplete") or row.get("cleanup_error"):
        raise RuntimeError(f"Tool discovery failed: {Path(command[0]).name}")
    return output.strip()


def windows_sdk_candidates():
    """Read installed Windows SDK metadata, independently of the root .NET SDK policy."""
    import winreg
    key_name = r"SOFTWARE\Microsoft\Windows Kits\Installed Roots"
    candidates = []
    for view in (winreg.KEY_WOW64_64KEY, winreg.KEY_WOW64_32KEY):
        try:
            with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, key_name, 0, winreg.KEY_READ | view) as key:
                path, _ = winreg.QueryValueEx(key, "KitsRoot10")
                candidates.append((path, f"HKLM\\{key_name}\\KitsRoot10 (registry view {view})"))
        except OSError:
            continue
    return candidates


def discover_windows_sdk():
    for value, origin in windows_sdk_candidates():
        path = Path(value).resolve()
        if path.is_dir() and (path / "Include").is_dir() and (path / "Lib").is_dir():
            return {"path": str(path), "discovery_source": origin}
    raise RuntimeError("No installed Windows SDK root with Include/Lib directories was found in Windows Kits registry metadata")


def windows_sdk_properties(sdk_path):
    # Microsoft.Common.CurrentVersion.targets supports this root override before
    # GetPlatformSDKLocation; its display-name fallback also probes user SDK folders.
    # These Windows platform settings do not override .NET SDK/framework resolution.
    return ["-p:TargetPlatformSdkRootOverride=" + str(sdk_path), "-p:TargetPlatformDisplayName=Windows"]


def discover_tools(root, env, report):
    dotnet = shutil.which("dotnet", path=env.get("PATH"))
    if not dotnet:
        candidate = Path(env.get("ProgramFiles", r"C:\Program Files")) / "dotnet" / "dotnet.exe"
        dotnet = str(candidate) if candidate.is_file() else None
    if not dotnet:
        raise RuntimeError("The installed dotnet host was not found")
    vswhere = shutil.which("vswhere", path=env.get("PATH"))
    if not vswhere:
        candidate = Path(env.get("ProgramFiles(x86)", r"C:\Program Files (x86)")) / "Microsoft Visual Studio" / "Installer" / "vswhere.exe"
        vswhere = str(candidate) if candidate.is_file() else None
    if not vswhere:
        raise RuntimeError("Visual Studio's installed vswhere discovery tool was not found")
    instances = json.loads(checked_query(
        [vswhere, "-latest", "-products", "*", "-version", "[18.0,19.0)",
         "-requires", "Microsoft.Component.MSBuild", "-format", "json", "-utf8"], root, env, report))
    if not isinstance(instances, list) or not instances:
        raise RuntimeError("No Visual Studio 2026 installation with MSBuild was found")
    instance = instances[0]
    msbuild = Path(instance["installationPath"]) / "MSBuild" / "Current" / "Bin" / "amd64" / "MSBuild.exe"
    if not msbuild.is_file():
        raise RuntimeError(f"Discovered Visual Studio MSBuild is missing: {msbuild}")
    sdk = checked_query([dotnet, "--version"], root, env, report)
    if not re.fullmatch(r"\d+\.\d+\.\d+(?:[-+][\w.-]+)?", sdk):
        raise RuntimeError("dotnet --version did not return one SDK version")
    msbuild_version = checked_query([str(msbuild), "-version", "-nologo"], root, env, report)
    windows_sdk = discover_windows_sdk()
    report["windows_sdk"] = windows_sdk
    sdk_project = root / "Fistnet.Genepool.Dna" / "Fistnet.Genepool.Dna.csproj"
    build_sdk = checked_query([str(msbuild), str(sdk_project), "-getProperty:NETCoreSdkVersion", "-nologo",
                               *windows_sdk_properties(windows_sdk["path"])], root, env, report)
    if build_sdk != sdk:
        raise RuntimeError(f"CLI SDK {sdk} and Visual Studio MSBuild SDK {build_sdk} differ")
    # Unlike Dna, this evaluation also exercises the installed NuGet SDK resolver.
    # VS's desktop resolver uses the normal NuGet settings chain and may require
    # host permission to parse user configuration; APPDATA does not redirect it.
    godot_project = root / "Fistnet.Genepool.Godot" / "Fistnet.Genepool.Godot.csproj"
    godot_sdk = checked_query([str(msbuild), str(godot_project), "-getProperty:NETCoreSdkVersion", "-nologo",
                               *windows_sdk_properties(windows_sdk["path"])], root, env, report)
    if godot_sdk != sdk:
        raise RuntimeError(f"CLI SDK {sdk} and Godot's Visual Studio MSBuild SDK {godot_sdk} differ")
    sources = checked_query([dotnet, "nuget", "list", "source", "--configfile", str(root / "NuGet.Config")], root, env, report)
    report["toolchain"] = {"dotnet": dotnet, "sdk_version": sdk, "msbuild": str(msbuild),
                           "msbuild_version": msbuild_version, "msbuild_sdk_version": build_sdk,
                           "godot_msbuild_sdk_version": godot_sdk,
                           "visual_studio_version": instance.get("installationVersion"),
                           "global_json": json.loads((root / "global.json").read_text(encoding="utf-8-sig")),
                           "nuget_config": str(root / "NuGet.Config"), "package_sources": sources,
                           "package_sources_scope": "Explicit restore uses only root NuGet.Config",
                           "sdk_resolver_settings": "Visual Studio NuGet SDK resolver uses the standard inherited NuGet settings chain including root NuGet.Config; user configuration is not redirected by APPDATA",
                           "package_cache": env["NUGET_PACKAGES"], "windows_sdk": windows_sdk}
    return dotnet, str(msbuild)


def build_commands(root, solution, dotnet, msbuild, configuration, platform, refresh_locks, windows_sdk_path=None):
    common = ["-p:UseSharedCompilation=false", "-nr:false", "-nologo", "-verbosity:minimal",
              "-p:Configuration=" + configuration, "-p:Platform=" + platform]
    if windows_sdk_path:
        common += windows_sdk_properties(windows_sdk_path)
    restore = [dotnet, "restore", str(solution), "--configfile", str(root / "NuGet.Config"), "--disable-parallel"]
    restore += ["--force-evaluate", "-p:RestoreLockedMode=false"] if refresh_locks else ["--locked-mode"]
    return [restore + common, [msbuild, str(solution), "-t:Rebuild", *common]]


def source_identities(root, solution):
    paths = [solution, root / "global.json", root / "NuGet.Config"]
    paths += [path for pattern in ("Directory.*.props", "Directory.*.targets") for path in root.glob(pattern)]
    for project in root.glob("Fistnet.Genepool.*"):
        if project.is_dir():
            for current, directories, files in os.walk(project):
                directories[:] = [name for name in directories if name not in {"bin", "obj", ".vs", ".godot"}]
                for name in files:
                    path = Path(current) / name
                    if path.suffix in {".cs", ".csproj", ".godot", ".tscn", ".tres", ".gdshader", ".uid", ".props", ".targets"} or name.endswith(".lock.json"):
                        paths.append(path)
    return {path.relative_to(root).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest() for path in sorted(set(paths)) if path.is_file()}


def output_identities(root, configuration):
    """Restrict identities to the declared target/configuration, excluding stale TFM directories."""
    outputs, missing, targets = {}, [], {}
    for project in sorted(root.glob("Fistnet.Genepool.*/*.csproj")):
        xml = ET.parse(project).getroot()
        framework = xml.findtext(".//TargetFramework")
        assembly = xml.findtext(".//AssemblyName") or project.stem
        targets[project.stem] = framework
        if project.parent.name == "Fistnet.Genepool.Godot":
            folder = project.parent / ".godot" / "mono" / "temp" / "bin" / ("Debug" if configuration == "Debug" else "ExportRelease")
        elif framework:
            folder = project.parent / "bin" / configuration / framework
        else:
            raise ValueError(f"Cannot identify declared output framework for {project.name}")
        expected = folder / (assembly + ".dll")
        if not expected.is_file():
            missing.append(expected.relative_to(root).as_posix())
        if folder.is_dir():
            for path in sorted(folder.iterdir()):
                if path.is_file() and path.suffix in {".dll", ".exe", ".pdb", ".json"}:
                    stat = path.stat()
                    outputs[path.relative_to(root).as_posix()] = {
                        "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                        "size_bytes": stat.st_size, "modified_at_ns": stat.st_mtime_ns,
                    }
    return outputs, missing, targets


def save_report(destination, stage, report):
    status = json.loads(destination.read_text(encoding="utf-8")) if destination.exists() else {"actor": "Genepool Analyzer", "stages": {}}
    status.setdefault("stages", {})[stage] = report
    destination.write_text(json.dumps(status, indent=2) + "\n", encoding="utf-8")


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("--stage", required=True)
    parser.add_argument("--configuration", default="Debug", choices=["Debug", "Release"])
    parser.add_argument("--platform", default="Any CPU", choices=["Any CPU", "Mixed Platforms", "x86"])
    parser.add_argument("--report", default="implementation_status.json", help="Current result file inside KnowledgeBase")
    parser.add_argument("--solution", help="Root .sln or .slnx; default prefers Fistnet.Genepool.slnx when present")
    parser.add_argument("--refresh-locks", action="store_true", help="Explicitly allow reviewed lockfile regeneration; default restore is locked")
    args = parser.parse_args(argv)
    destination = (KB / args.report).resolve()
    if destination.parent != KB.resolve() or destination.suffix != ".json":
        parser.error("--report must name a JSON file directly inside KnowledgeBase")
    try:
        solution = resolve_solution(ROOT, args.solution)
    except ValueError as error:
        parser.error(str(error))
    report = {"at_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "stage": args.stage,
              "configuration": args.configuration, "platform": args.platform, "solution": str(solution),
              "restore_mode": "refresh_locks" if args.refresh_locks else "locked", "builds": [], "ok": False,
              "status": "running"}
    # Invalidate this stage before launching; interrupted attempts cannot reuse an old pass.
    save_report(destination, args.stage, report)
    scratch = None
    try:
        report["source_sha256"] = source_identities(ROOT, solution)
        with tempfile.TemporaryDirectory(prefix=".build-", dir=KB) as temp:
            scratch = Path(temp)
            env, removed = isolated_environment(ROOT, scratch)
            report["removed_sdk_resolver_overrides"] = removed
            dotnet, msbuild = discover_tools(ROOT, env, report)
            for command in build_commands(ROOT, solution, dotnet, msbuild, args.configuration, args.platform,
                                           args.refresh_locks, report.get("windows_sdk", {}).get("path")):
                item, _ = run_command(command, ROOT, env, 120)
                report["builds"].append(item)
                print(json.dumps(item), flush=True)
                if item["exit_code"] != 0 or item.get("incomplete") or item.get("cleanup_error"):
                    break
            report["source_sha256_after"] = source_identities(ROOT, solution)
            report["changed_inputs"] = sorted(path for path in set(report["source_sha256"]) | set(report["source_sha256_after"])
                                               if report["source_sha256"].get(path) != report["source_sha256_after"].get(path))
            report["output_identities"], report["missing_outputs"], report["project_targets"] = output_identities(ROOT, args.configuration)
            report["output_sha256"] = {path: identity["sha256"] for path, identity in report["output_identities"].items()}
            report["ok"] = (len(report["builds"]) == 2 and not report["missing_outputs"]
                            and all(row["exit_code"] == 0 and not row.get("incomplete") and not row.get("cleanup_error") for row in report["builds"]))
            if report["changed_inputs"] and (not args.refresh_locks or any(not path.endswith(".lock.json") for path in report["changed_inputs"])):
                report["ok"] = False
                report["input_change_error"] = "Build inputs changed outside an explicit lockfile refresh"
    except Exception as error:
        report["error"] = f"{type(error).__name__}: {error}"
        report["ok"] = False
    report["scratch_removed"] = scratch is None or not scratch.exists()
    report["ok"] = report["ok"] and report["scratch_removed"]
    report["status"] = "complete" if report["ok"] else "failed"
    save_report(destination, args.stage, report)
    print(json.dumps({"report": str(destination), "ok": report["ok"], "error": report.get("error"), "scratch_removed": report["scratch_removed"]}), flush=True)
    return 0 if report["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
