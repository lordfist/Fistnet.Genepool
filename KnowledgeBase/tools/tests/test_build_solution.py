"""Disposable build-helper contract tests; never invoke .NET, MSBuild or restore."""
import contextlib
import io
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import Mock, patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import build_solution as helper


class BuildHelperTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix=".build-helper-test-", dir=helper.KB)
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.kb = self.root / "KnowledgeBase"
        self.kb.mkdir()
        (self.root / "Fistnet.Genepool.sln").write_text("synthetic solution")
        (self.root / "global.json").write_text('{"sdk":{"version":"10.0.400","rollForward":"latestPatch"}}')
        (self.root / "NuGet.Config").write_text('<configuration><packageSources><clear /></packageSources></configuration>')

    def project(self, name, configuration="Debug", framework="net10.0-windows7.0"):
        folder = self.root / name
        folder.mkdir(exist_ok=True)
        (folder / (name + ".csproj")).write_text(f'<Project><PropertyGroup><TargetFramework>{framework}</TargetFramework></PropertyGroup></Project>')
        if name.endswith("Godot"):
            output = folder / ".godot" / "mono" / "temp" / "bin" / configuration
        else:
            output = folder / "bin" / configuration / framework
        output.mkdir(parents=True, exist_ok=True)
        (output / (name + ".dll")).write_bytes(b"synthetic current assembly")
        return output

    def test_solution_selection_and_boundary(self):
        self.assertEqual(helper.resolve_solution(self.root).suffix, ".sln")
        (self.root / "Fistnet.Genepool.slnx").write_text("<Solution />")
        self.assertEqual(helper.resolve_solution(self.root).suffix, ".slnx")
        self.assertEqual(helper.resolve_solution(self.root, "Fistnet.Genepool.sln").suffix, ".sln")
        with self.assertRaises(ValueError):
            helper.resolve_solution(self.root, "../outside.sln")

    def test_isolation_does_not_shadow_root_policy(self):
        scratch = self.root / "scratch"
        scratch.mkdir()
        with patch.dict(os.environ, {"MSBuildSDKsPath": "obsolete", "DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR": "obsolete"}):
            env, removed = helper.isolated_environment(self.root, scratch)
        self.assertIn("MSBUILDSDKSPATH", [key.upper() for key in removed])
        self.assertFalse({key.upper() for key in env} & {key.upper() for key in helper.RESOLVER_OVERRIDES})
        self.assertFalse((scratch / "global.json").exists())
        self.assertFalse((scratch / "NuGet.Config").exists())
        self.assertEqual(Path(env["NUGET_PACKAGES"]), self.root / ".tools" / "nuget" / "packages")

    def test_locked_restore_is_default_and_refresh_explicit(self):
        solution = helper.resolve_solution(self.root)
        locked = helper.build_commands(self.root, solution, "dotnet", "msbuild", "Release", "Any CPU", False)
        self.assertIn("--locked-mode", locked[0])
        self.assertIn(str(self.root / "NuGet.Config"), locked[0])
        self.assertIn("-p:Configuration=Release", locked[0])
        self.assertIn("-t:Rebuild", locked[1])
        refresh = helper.build_commands(self.root, solution, "dotnet", "msbuild", "Debug", "Any CPU", True)
        self.assertIn("--force-evaluate", refresh[0])
        self.assertIn("-p:RestoreLockedMode=false", refresh[0])
        self.assertNotIn("--locked-mode", refresh[0])

    def test_windows_sdk_is_discovered_and_only_platform_discovery_is_overridden(self):
        kit = self.root / "Synthetic Windows Kits" / "10"
        (kit / "Include").mkdir(parents=True)
        (kit / "Lib").mkdir()
        with patch.object(helper, "windows_sdk_candidates", return_value=[(str(self.root / "absent"), "stale"), (str(kit), "synthetic registry")]):
            sdk = helper.discover_windows_sdk()
        self.assertEqual(sdk["path"], str(kit.resolve()))
        self.assertEqual(sdk["discovery_source"], "synthetic registry")
        commands = helper.build_commands(self.root, helper.resolve_solution(self.root), "dotnet", "msbuild", "Debug", "Any CPU", False, sdk["path"])
        for command in commands:
            self.assertIn("-p:TargetPlatformSdkRootOverride=" + str(kit), command)
            self.assertIn("-p:TargetPlatformDisplayName=Windows", command)
            self.assertFalse(any("MSBuildSDKsPath=" in argument or "NETCoreSdkVersion=" in argument for argument in command))
        with patch.object(helper, "windows_sdk_candidates", return_value=[(str(self.root / "absent"), "stale")]):
            with self.assertRaises(RuntimeError):
                helper.discover_windows_sdk()

    def test_output_identity_excludes_old_target_and_maps_exportrelease(self):
        output = self.project("Fistnet.Genepool.Tests", "Release")
        old = output.parent / "net9.0-windows7.0"
        old.mkdir()
        (old / "Fistnet.Genepool.Tests.dll").write_bytes(b"old assembly")
        self.project("Fistnet.Genepool.Godot", "ExportRelease")
        identities, missing, targets = helper.output_identities(self.root, "Release")
        self.assertFalse(missing)
        self.assertTrue(any("ExportRelease" in path for path in identities))
        self.assertFalse(any("net9.0" in path for path in identities))
        self.assertEqual(targets["Fistnet.Genepool.Tests"], "net10.0-windows7.0")

    def test_discovery_checks_godot_sdk_resolver_before_build(self):
        installation = self.root / "VS"
        msbuild = installation / "MSBuild" / "Current" / "Bin" / "amd64" / "MSBuild.exe"
        msbuild.parent.mkdir(parents=True)
        msbuild.write_bytes(b"synthetic")
        answers = [json.dumps([{"installationPath": str(installation), "installationVersion": "18.9"}]),
                   "10.0.401", "18.9", "10.0.401", "10.0.401", "synthetic sources"]
        with patch.object(helper.shutil, "which", side_effect=lambda name, **kwargs: name), \
             patch.object(helper, "discover_windows_sdk", return_value={"path": str(self.root / "Windows SDK"), "discovery_source": "synthetic"}), \
             patch.object(helper, "checked_query", side_effect=answers) as query:
            report = {}
            helper.discover_tools(self.root, {"NUGET_PACKAGES": str(self.root / "packages")}, report)
        godot_call = query.call_args_list[4].args
        self.assertIn("Fistnet.Genepool.Godot.csproj", godot_call[0][1])
        self.assertEqual(godot_call[1], self.root)
        self.assertEqual(report["toolchain"]["godot_msbuild_sdk_version"], "10.0.401")
        self.assertIn("standard inherited", report["toolchain"]["sdk_resolver_settings"])

    def test_main_records_identity_and_invalidates_previous_pass_before_dispatch(self):
        self.project("Fistnet.Genepool.Tests")
        report = self.kb / "result.json"
        report.write_text('{"stages":{"candidate":{"ok":true},"other":{"ok":true}}}')

        def simulated_command(command, root, env, timeout):
            running = json.loads(report.read_text())["stages"]["candidate"]
            self.assertFalse(running["ok"])
            self.assertEqual(running["status"], "running")
            self.assertEqual(root, self.root)
            return {"exit_code": 0}, ""

        with patch.object(helper, "ROOT", self.root), patch.object(helper, "KB", self.kb), \
             patch.object(helper, "discover_tools", return_value=("dotnet", "msbuild")), \
             patch.object(helper, "run_command", side_effect=simulated_command), contextlib.redirect_stdout(io.StringIO()):
            result = helper.main(["--stage", "candidate", "--report", "result.json"])
        self.assertEqual(result, 0)
        saved = json.loads(report.read_text())
        self.assertTrue(saved["stages"]["other"]["ok"])
        stage = saved["stages"]["candidate"]
        self.assertTrue(stage["ok"])
        self.assertTrue(stage["scratch_removed"])
        self.assertTrue(stage["output_sha256"])
        self.assertEqual(stage["source_sha256"], stage["source_sha256_after"])
        self.assertFalse(list(self.kb.glob(".build-*")))

    def test_failed_restore_stops_before_build(self):
        self.project("Fistnet.Genepool.Tests")
        with patch.object(helper, "ROOT", self.root), patch.object(helper, "KB", self.kb), \
             patch.object(helper, "discover_tools", return_value=("dotnet", "msbuild")), \
             patch.object(helper, "run_command", return_value=({"exit_code": 1}, "restore rejected")) as run, \
             contextlib.redirect_stdout(io.StringIO()):
            result = helper.main(["--stage", "rejected", "--report", "result.json"])
        self.assertEqual(result, 1)
        self.assertEqual(run.call_count, 1)
        self.assertFalse(json.loads((self.kb / "result.json").read_text())["stages"]["rejected"]["ok"])

    def test_timeout_terminates_owned_tree_and_cannot_pass(self):
        process = Mock(pid=123)
        process.communicate.side_effect = [helper.subprocess.TimeoutExpired("synthetic", 1), (b"", b"")]
        process.poll.return_value = 0
        with patch.object(helper.subprocess, "Popen", return_value=process), patch.object(helper, "terminate_tree", return_value=0) as terminate:
            row, _ = helper.run_command(["synthetic"], self.root, {}, 1)
        terminate.assert_called_once_with(process)
        self.assertIn("incomplete", row)


if __name__ == "__main__":
    unittest.main()
