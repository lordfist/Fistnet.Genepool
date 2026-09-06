"""KB-local history checks in automatically removed synthetic repositories/KBs."""
import copy
import json
from pathlib import Path
import stat
import subprocess
import sys
import unittest
from unittest.mock import patch


TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
import source_versions
import test_source_versions as fixture
from test_source_versions import sha


class SourceSnapshotTests(unittest.TestCase):
    setUp = fixture.SourceVersionTests.setUp
    git = fixture.SourceVersionTests.git
    save_fixture = fixture.SourceVersionTests.save_fixture
    canonical_bytes = fixture.SourceVersionTests.canonical_bytes
    call = fixture.SourceVersionTests.call
    apply = fixture.SourceVersionTests.apply

    def snapshot(self):
        path = self.kb / "source-history" / "SRC-0001" / "evidence.txt"
        path.parent.mkdir(parents=True)
        path.write_bytes(self.old_bytes)
        return path

    def draft(self, path):
        self.file.write_bytes(b"new current source\n")
        result = self.call("revise-source", "SRC-0001", "SRC-0002", "--expect-revision", "1",
                           "--actor", "Synthetic snapshot test", "--reason", "Intentional source edit",
                           "--record-id", "DEC-0001", "--history-file", str(path))
        self.assertFalse(result["canonical_writes"])
        return result["transaction"]

    def test_exact_snapshot_preview_reviewed_apply_and_deep_verification(self):
        snapshot = self.snapshot()
        before = self.canonical_bytes()
        transaction = self.draft(snapshot)
        history = transaction["upsert_sources"][0]["supersession"]["history"]
        self.assertEqual(history, {"kind": "kb_snapshot", "path": "source-history/SRC-0001/evidence.txt"})
        preview = self.apply(transaction, dry=True)
        self.assertEqual(self.canonical_bytes(), before)
        self.call("apply", str(self.kb / "transaction.json"), "--expect-revision", "1",
                  "--expect-transaction-sha256", preview["transaction_sha256"])
        result = self.call("validate", "--deep")
        self.assertEqual(result["kb_revision"], 2)
        self.assertEqual(result["warnings"], [])
        self.assertEqual(snapshot.read_bytes(), self.old_bytes)
        self.assertEqual(json.loads((self.kb / "records.jsonl").read_text())["source_refs"], ["SRC-0001"])

    def test_draft_requires_matching_sha_and_catalog_size(self):
        snapshot = self.snapshot()
        before = self.canonical_bytes()
        for payload in (b"wrong bytes", self.old_bytes.replace(b"\r\n", b"\n")):
            snapshot.write_bytes(payload)
            with self.assertRaisesRegex(source_versions.VersionError, "exact bytes/size"):
                source_versions.snapshot_history(self.root, self.old, str(snapshot))
        snapshot.write_bytes(self.old_bytes)
        incorrect_size = dict(self.old, size_bytes=len(self.old_bytes) + 1)
        with self.assertRaisesRegex(source_versions.VersionError, "exact bytes/size"):
            source_versions.snapshot_history(self.root, incorrect_size, str(snapshot))
        self.assertEqual(self.canonical_bytes(), before)

    def test_missing_and_tampered_snapshot_fail_before_canonical_replacement(self):
        snapshot = self.snapshot()
        transaction = self.draft(snapshot)
        before = self.canonical_bytes()
        snapshot.write_bytes(b"tampered snapshot")
        self.apply(transaction, dry=True, ok=False)
        self.apply(transaction, ok=False)
        snapshot.unlink()
        self.apply(transaction, ok=False)
        self.assertEqual(self.canonical_bytes(), before)

    def test_missing_or_tampered_pinned_snapshot_is_deep_error_not_gap_warning(self):
        snapshot = self.snapshot()
        self.apply(self.draft(snapshot))
        before = self.canonical_bytes()
        snapshot.write_bytes(b"tampered snapshot")
        self.call("validate", "--deep", ok=False)
        snapshot.unlink()
        self.call("validate", "--deep", ok=False)
        self.assertEqual(self.canonical_bytes(), before)

    def test_metadata_rejects_absolute_traversal_aliases_and_other_kb_paths(self):
        transaction = self.draft(self.snapshot())
        before = self.canonical_bytes()
        paths = (None, [], str(self.file), "../evidence.txt", "source-history/../evidence.txt",
                 "source-history//evidence.txt", "source-history/./evidence.txt",
                 "source-history/a./evidence.txt", "source-history/a /evidence.txt",
                 "source-history/a:stream", "source-history\\evidence.txt", "prepared/evidence.txt")
        for path in paths:
            with self.subTest(path=path):
                invalid = copy.deepcopy(transaction)
                invalid["upsert_sources"][0]["supersession"]["history"]["path"] = path
                self.apply(invalid, ok=False)
                self.assertEqual(self.canonical_bytes(), before)

    def test_cli_input_is_absolute_and_inside_exact_snapshot_directory(self):
        snapshot = self.snapshot()
        for path in ("source-history/SRC-0001/evidence.txt", str(self.file), str(self.kb / "core.json"),
                     str(snapshot.parent / ".." / "SRC-0001" / "evidence.txt")):
            with self.subTest(path=path), self.assertRaises(source_versions.VersionError):
                source_versions.snapshot_history(self.root, self.old, path)

    def test_directories_and_oversized_files_rejected_exact_limit_allowed(self):
        snapshot = self.snapshot()
        relative = snapshot.relative_to(self.kb).as_posix()
        with self.assertRaisesRegex(source_versions.VersionError, "regular files"):
            source_versions.snapshot_bytes(self.root, snapshot.parent.relative_to(self.kb).as_posix())
        payload = b"x" * (16 * 1024 * 1024)
        snapshot.write_bytes(payload)
        self.assertEqual(source_versions.snapshot_bytes(self.root, relative), payload)
        with snapshot.open("ab") as handle:
            handle.write(b"x")
        with self.assertRaisesRegex(source_versions.VersionError, "16 MiB"):
            source_versions.snapshot_bytes(self.root, relative)

    def test_symlink_snapshot_or_parent_refused_before_open(self):
        snapshot = self.snapshot()
        link = snapshot.parent / "linked.txt"
        try:
            link.symlink_to(snapshot)
        except OSError as exc:
            self.skipTest(f"Host does not permit fixture symlinks: {type(exc).__name__}")
        with patch.object(source_versions.os, "open") as opened:
            with self.assertRaisesRegex(source_versions.VersionError, "symlinks or reparse"):
                source_versions.snapshot_bytes(self.root, link.relative_to(self.kb).as_posix())
            opened.assert_not_called()
        directory_link = self.kb / "source-history" / "linked-directory"
        directory_link.symlink_to(snapshot.parent, target_is_directory=True)
        with patch.object(source_versions.os, "open") as opened:
            with self.assertRaisesRegex(source_versions.VersionError, "symlinks or reparse"):
                source_versions.snapshot_bytes(self.root, "source-history/linked-directory/evidence.txt")
            opened.assert_not_called()

    def test_windows_reparse_attributes_refused_at_every_component_before_open(self):
        snapshot = self.snapshot()
        original_lstat = Path.lstat
        for target in (self.root, self.kb, self.kb / "source-history", snapshot.parent, snapshot):
            def lstat_with_reparse(path, *, follow_symlinks=False):
                real = original_lstat(path)
                if path == target:
                    class ReparseStat:
                        st_mode = real.st_mode
                        st_file_attributes = stat.FILE_ATTRIBUTE_REPARSE_POINT
                    return ReparseStat()
                return real
            with self.subTest(target=target), patch.object(Path, "lstat", lstat_with_reparse), \
                    patch.object(source_versions.os, "open") as opened:
                with self.assertRaisesRegex(source_versions.VersionError, "symlinks or reparse"):
                    source_versions.snapshot_bytes(self.root, snapshot.relative_to(self.kb).as_posix())
                opened.assert_not_called()

    def test_history_options_are_mutually_exclusive(self):
        snapshot = self.snapshot()
        before = self.canonical_bytes()
        for extra in (("--git-commit", self.commit), ("--history-unavailable-reason", "Synthetic gap")):
            result = subprocess.run([sys.executable, "-X", "utf8", "-B", str(self.tools / "kb.py"),
                "revise-source", "SRC-0001", "SRC-0002", "--expect-revision", "1",
                "--actor", "Synthetic test", "--reason", "Edit", "--record-id", "DEC-0001",
                "--history-file", str(snapshot), *extra], cwd=self.root, env=self.env,
                capture_output=True, timeout=30)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn(b"not allowed with argument", result.stderr + result.stdout)
        self.assertEqual(self.canonical_bytes(), before)

    def test_current_drift_and_old_identity_remain_strict(self):
        transaction = self.draft(self.snapshot())
        before = self.canonical_bytes()
        invalid = copy.deepcopy(transaction)
        invalid["upsert_sources"][0]["size_bytes"] += 1
        self.apply(invalid, ok=False)
        self.assertEqual(self.canonical_bytes(), before)
        self.apply(transaction)
        self.file.write_bytes(b"unexpected source drift")
        result = self.call("validate", "--deep", ok=False)
        self.assertTrue(any("SRC-0002 is stale" in error for error in result["errors"]))

    def test_prepared_historical_bytes_verify_against_snapshot(self):
        self.call("prepare", "SRC-0001", "--actor", "Synthetic test", tool="prepare.py")
        core = json.loads((self.kb / "core.json").read_text())
        core["kb_revision"] = 1
        (self.kb / "core.json").write_text(json.dumps(core), encoding="utf-8")
        prepared = self.kb / "prepared" / "SRC-0001.jsonl"
        before = prepared.read_bytes()
        self.apply(self.draft(self.snapshot()))
        verified = self.call("verify", "SRC-0001", tool="prepare.py")["verification"]
        self.assertTrue(verified["historical"] and verified["original_bytes_verified"])
        self.assertEqual(verified["source_sha256"], sha(self.old_bytes))
        self.assertEqual(prepared.read_bytes(), before)


if __name__ == "__main__":
    unittest.main(verbosity=2)
