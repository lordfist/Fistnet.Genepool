"""Disposable identity checks; no builds, applications or canonical KB changes."""
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import build_identity as identity
import run_part1_checks as runner


class BuildIdentityTests(unittest.TestCase):
    def setUp(self):
        self.scratch = tempfile.TemporaryDirectory(prefix=".identity-test-", dir=identity.KB)
        self.addCleanup(self.scratch.cleanup)
        self.root = Path(self.scratch.name)
        self.kb = self.root / "KnowledgeBase"
        self.kb.mkdir()
        self.project = self.root / "Fistnet.Genepool.Tests"
        self.project.mkdir()
        self.source = self.project / "Fistnet.Genepool.Tests.csproj"
        self.source.write_text('<Project><PropertyGroup><TargetFramework>net10.0-windows7.0</TargetFramework></PropertyGroup></Project>')
        self.binary = self.project / "bin" / "Release" / "net10.0-windows7.0" / "Fistnet.Genepool.Tests.dll"
        self.binary.parent.mkdir(parents=True)
        self.binary.write_bytes(b"synthetic freshly built assembly")
        self.report = self.kb / "build.json"
        self.build = {"ok": True, "configuration": "Release",
                      "source_sha256_after": {self.source.relative_to(self.root).as_posix(): identity.digest(self.source)},
                      "output_sha256": {self.binary.relative_to(self.root).as_posix(): identity.digest(self.binary)}}
        self.report.write_text(json.dumps({"stages": {"current": self.build}}))
        self.root_patch = patch.object(identity, "ROOT", self.root)
        self.kb_patch = patch.object(identity, "KB", self.kb)
        self.root_patch.start()
        self.kb_patch.start()
        self.addCleanup(self.root_patch.stop)
        self.addCleanup(self.kb_patch.stop)

    def verify(self):
        return identity.verify_build("build.json", "current", "Release", [self.binary])

    def test_target_is_read_from_project_and_exact_build_is_accepted(self):
        self.assertEqual("net10.0-windows7.0", identity.project_target("Fistnet.Genepool.Tests"))
        self.assertEqual(self.binary.parent, identity.project_output("Fistnet.Genepool.Tests", "Release"))
        self.assertEqual(1, self.verify()["outputs_verified"])

    def test_earlier_framework_directory_cannot_satisfy_current_output(self):
        old = self.binary.parent.parent / "net9.0-windows7.0" / self.binary.name
        old.parent.mkdir()
        old.write_bytes(self.binary.read_bytes())
        self.binary.unlink()
        with self.assertRaises(OSError):
            self.verify()

    def test_changed_source_or_binary_rejects_stale_success(self):
        for path in (self.source, self.binary):
            with self.subTest(path=path.name):
                original = path.read_bytes()
                path.write_bytes(original + b" changed")
                with self.assertRaisesRegex(ValueError, "changed|differs"):
                    self.verify()
                path.write_bytes(original)

    def test_failed_build_wrong_configuration_and_unrecorded_output_are_rejected(self):
        for edit in ({"ok": False}, {"configuration": "Debug"}, {"output_sha256": {"other.dll": "00"}}):
            with self.subTest(edit=edit):
                self.report.write_text(json.dumps({"stages": {"current": {**self.build, **edit}}}))
                with self.assertRaises(ValueError):
                    self.verify()

    def test_loaded_identity_mismatch_and_missing_report_fail(self):
        for actual in (None, {}, {"Example": "wrong"}):
            with self.subTest(actual=actual), self.assertRaises(ValueError):
                identity.verify_loaded({"Example": "abcd"}, actual)
        identity.verify_loaded({"Example": "abcd"}, {"Example": "ABCD"})

    def test_evidence_and_report_paths_cannot_escape_project(self):
        with self.assertRaises(ValueError):
            identity.report_path("../outside.json")
        with self.assertRaises(ValueError):
            identity.rooted_path("../outside.dll")

    def test_failed_preflight_dispatches_no_child_or_temporary_process_directory(self):
        with patch.object(runner, "verify_build", side_effect=ValueError("stale build")), \
                patch.object(runner, "project_output", return_value=self.binary.parent), \
                patch.object(runner, "project_target", return_value="net10.0-windows7.0"), \
                patch.object(runner.tempfile, "TemporaryDirectory") as scratch, \
                patch.object(runner.subprocess, "Popen") as process:
            with self.assertRaisesRegex(ValueError, "stale build"):
                runner.execute(["--all"], "Release", 30, build_report="build.json", build_stage="current")
            process.assert_not_called()
            scratch.assert_not_called()


if __name__ == "__main__":
    unittest.main(verbosity=2)
