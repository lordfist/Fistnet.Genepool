"""Bounded hidden apphost startup evidence; no debugger or designer claim."""
import argparse
import datetime
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile

from build_identity import KB, ROOT, project_output, project_target, verify_build

REPORT = KB / "r04_step1_winforms_startup.json"
PROBE = r'''
param([string]$AppPath, [string]$WorkingDirectory)
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class GenepoolStartupWindow {
    private delegate bool Callback(IntPtr handle, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(Callback callback, IntPtr data);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint id);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetWindowText(IntPtr handle, StringBuilder text, int length);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr handle, int command);
    [DllImport("user32.dll", SetLastError=true)] private static extern IntPtr SendMessageTimeout(IntPtr handle, uint message, IntPtr wparam, IntPtr lparam, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr handle, uint message, IntPtr wparam, IntPtr lparam);
    public static IntPtr Find(int processId) {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr handle, IntPtr data) {
            uint owner; GetWindowThreadProcessId(handle, out owner);
            if (owner == (uint)processId) {
                var text = new StringBuilder(512); GetWindowText(handle, text, text.Capacity);
                if (text.ToString().StartsWith("Genepool", StringComparison.Ordinal)) { found = handle; return false; }
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }
    public static bool Responds(IntPtr handle) {
        IntPtr result;
        return SendMessageTimeout(handle, 0, IntPtr.Zero, IntPtr.Zero, 2, 2000, out result) != IntPtr.Zero;
    }
}
'@
$ownedProcess = $null
$result = [ordered]@{ ok=$false; windowFound=$false; responding=$false; modules=@(); gracefulClose=$false; forcedTermination=$false; cleanupVerified=$false }
try {
    $ownedProcess = Start-Process -FilePath $AppPath -WorkingDirectory $WorkingDirectory -WindowStyle Hidden -PassThru
    $result.pid = $ownedProcess.Id
    $result.processStartedAtUtc = $ownedProcess.StartTime.ToUniversalTime().ToString('o')
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    $handle = [IntPtr]::Zero
    while ([DateTime]::UtcNow -lt $deadline -and -not $ownedProcess.HasExited) {
        $handle = [GenepoolStartupWindow]::Find($ownedProcess.Id)
        if ($handle -ne [IntPtr]::Zero) { break }
        Start-Sleep -Milliseconds 100
        $ownedProcess.Refresh()
    }
    $result.windowFound = $handle -ne [IntPtr]::Zero
    if ($result.windowFound) {
        [void][GenepoolStartupWindow]::ShowWindow($handle, 0)
        $result.windowHidden = -not [GenepoolStartupWindow]::IsWindowVisible($handle)
        $result.responding = [GenepoolStartupWindow]::Responds($handle)
        $result.windowHandle = $handle.ToInt64()
    }
    if (-not $ownedProcess.HasExited) {
        try {
            $ownedProcess.Refresh()
            $result.executable = $ownedProcess.MainModule.FileName
            $result.modules = @($ownedProcess.Modules | Where-Object { $_.ModuleName -in @('coreclr.dll', 'System.Private.CoreLib.dll', 'mscorlib.dll') } | ForEach-Object {
                [ordered]@{ name=$_.ModuleName; path=$_.FileName; productVersion=$_.FileVersionInfo.ProductVersion }
            })
        } catch { $result.moduleInspectionError = $_.Exception.Message }
        $result.closeMainWindowAccepted = $ownedProcess.CloseMainWindow()
        if (-not $result.closeMainWindowAccepted -and $handle -ne [IntPtr]::Zero) {
            $result.hiddenWindowClosePosted = [GenepoolStartupWindow]::PostMessage($handle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)
        }
        $result.gracefulClose = $ownedProcess.WaitForExit(10000)
    }
    $result.ok = $result.windowFound -and $result.responding -and $result.gracefulClose
} catch { $result.error = $_.Exception.Message }
finally {
    if ($null -ne $ownedProcess) {
        if (-not $ownedProcess.HasExited) {
            $result.forcedTermination = $true
            $ownedProcess.Kill()
            [void]$ownedProcess.WaitForExit(5000)
        }
        $result.cleanupVerified = $ownedProcess.HasExited
        if ($ownedProcess.HasExited) { $result.exitCode = $ownedProcess.ExitCode }
        $ownedProcess.Dispose()
    }
    $result.ok = $result.ok -and $result.cleanupVerified -and -not $result.forcedTermination -and $result.exitCode -eq 0
}
$result | ConvertTo-Json -Depth 6 -Compress
'''


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("--configuration", choices=["Debug", "Release"], default="Debug")
    parser.add_argument("--build-report", required=True)
    parser.add_argument("--build-stage", required=True)
    args = parser.parse_args(argv)
    project = "Fistnet.Genepool.App"
    target = project_target(project)
    directory = project_output(project, args.configuration)
    executable = directory / (project + ".exe")
    required = [directory / (project + suffix) for suffix in (".exe", ".dll", ".deps.json", ".runtimeconfig.json")]
    required += [directory / ("Fistnet.Genepool." + name + ".dll") for name in ("Control", "Dna", "Visualization")]
    result = {"ok": False, "status": "running", "atUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "configuration": args.configuration, "targetFramework": target, "executable": str(executable),
              "limits": "Actual apphost and hidden message-loop startup/shutdown only. No debugger, designer or physical UI-interaction claim."}
    REPORT.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    scratch_path = None
    try:
        result["buildIdentity"] = verify_build(args.build_report, args.build_stage, args.configuration, required)
        powershell = shutil.which("powershell.exe")
        if not powershell:
            raise OSError("Windows PowerShell is unavailable for the bounded process-module/window probe")
        with tempfile.TemporaryDirectory(prefix=".winforms-startup-", dir=KB) as scratch:
            scratch_path = Path(scratch)
            script = scratch_path / "probe.ps1"
            script.write_text(PROBE, encoding="utf-8-sig")
            env = os.environ.copy()
            for key in ("TEMP", "TMP", "APPDATA", "LOCALAPPDATA"):
                env[key] = scratch
            process = subprocess.Popen([powershell, "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", str(script),
                                        "-AppPath", str(executable), "-WorkingDirectory", str(ROOT)],
                                       cwd=ROOT, env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                       creationflags=subprocess.CREATE_NO_WINDOW)
            try:
                stdout, stderr = process.communicate(timeout=45)
            except subprocess.TimeoutExpired:
                killed = subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"], capture_output=True, timeout=10,
                                        creationflags=subprocess.CREATE_NO_WINDOW)
                stdout, stderr = process.communicate(timeout=10)
                result["terminationExitCode"] = killed.returncode
                raise TimeoutError("Startup probe exceeded 45 seconds; task-owned process tree termination was requested")
            result["probeExitCode"] = process.returncode
            result["stderr"] = stderr.decode("utf-8", errors="replace")
            result["probe"] = json.loads(stdout.decode("utf-8-sig").strip())
        verify_build(args.build_report, args.build_stage, args.configuration, required)
        runtime = next((entry for entry in result["probe"].get("modules", []) if entry["name"].lower() == "coreclr.dll"), None)
        result["runtimeModuleObserved"] = runtime is not None
        # The native coreclr ProductVersion uses comma-separated Windows build
        # components. CoreLib exposes the managed runtime's servicing version.
        corelib = next((entry for entry in result["probe"].get("modules", [])
                        if entry["name"].lower() == "system.private.corelib.dll"), None)
        version = re.match(r"(\d+\.\d+\.\d+)", corelib["productVersion"]) if corelib else None
        match = re.match(r"net(\d+)\.(\d+)", target)
        if runtime and version and match:
            result["runtimeVersion"] = version[1]
            result["runtimeVersionEvidence"] = "Loaded System.Private.CoreLib ProductVersion; coreclr module path also recorded"
            result["runtimeMatchesTarget"] = version[1].startswith(match[1] + "." + match[2] + ".")
        result["ok"] = process.returncode == 0 and result["probe"].get("ok") is True and result.get("runtimeMatchesTarget", True)
    except (OSError, ValueError, TimeoutError, subprocess.SubprocessError) as error:
        result["error"] = str(error)
    result["scratchRemoved"] = scratch_path is None or not scratch_path.exists()
    result["ok"] = bool(result["ok"]) and result["scratchRemoved"]
    result["status"] = "completed" if result["ok"] else "failed"
    REPORT.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result))
    return 0 if result["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
