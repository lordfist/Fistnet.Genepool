"""Focused reviewed-byte commit checks in automatically removed synthetic KBs."""
import importlib.util
import json
from pathlib import Path
import unittest
from unittest.mock import patch

from test_source_versions import SourceVersionTests, sha


class ReviewedCommitTests(unittest.TestCase):
    setUp = SourceVersionTests.setUp
    git = SourceVersionTests.git
    save_fixture = SourceVersionTests.save_fixture
    canonical_bytes = SourceVersionTests.canonical_bytes
    call = SourceVersionTests.call

    def transaction(self):
        path = self.kb / "reviewed.json"
        path.write_text(json.dumps({"actor": "Synthetic test", "note": "Reviewed bytes",
                                    "core_patch": {"synthetic_check": True}}), encoding="utf-8")
        return path

    def test_preview_hash_commits_exact_bytes(self):
        path = self.transaction()
        preview = self.call("apply", str(path), "--expect-revision", "1", "--dry-run")
        digest = preview["transaction_sha256"]
        self.assertEqual(digest, sha(path.read_bytes()))
        result = self.call("apply", str(path), "--expect-revision", "1",
                           "--expect-transaction-sha256", digest.lower())
        self.assertEqual(result["transaction_sha256"], digest)
        self.assertTrue(result["write_outcome"]["commit_completed"])
        self.assertTrue(result["write_outcome"]["verification_completed"])
        self.assertEqual(result["to_revision"], 2)

    def test_changed_reviewed_bytes_refused_without_replacement(self):
        path = self.transaction()
        digest = sha(path.read_bytes())
        before = self.canonical_bytes()
        path.write_bytes(path.read_bytes() + b"\n")  # Even same-meaning JSON has changed identity.
        result = self.call("apply", str(path), "--expect-revision", "1",
                           "--expect-transaction-sha256", digest, ok=False)
        self.assertIn("reviewed bytes", result["error"])
        self.assertEqual(result["write_outcome"]["replaced_files"], [])
        self.assertEqual(self.canonical_bytes(), before)

    def test_matching_hash_does_not_override_revision(self):
        path = self.transaction()
        before = self.canonical_bytes()
        result = self.call("apply", str(path), "--expect-revision", "2",
                           "--expect-transaction-sha256", sha(path.read_bytes()), ok=False)
        self.assertIn("revision conflict", result["error"])
        self.assertEqual(self.canonical_bytes(), before)

    def test_transaction_change_during_staging_refused_and_cleaned(self):
        path = self.transaction()
        before = self.canonical_bytes()
        digest = sha(path.read_bytes())
        spec = importlib.util.spec_from_file_location("synthetic_reviewed_kb", self.tools / "kb.py")
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        stage = module.atomic_stage
        changed = False

        def mutate_after_stage(destination, payload):
            nonlocal changed
            temporary = stage(destination, payload)
            if not changed:
                changed = True
                path.write_bytes(path.read_bytes() + b" ")
            return temporary

        with patch.object(module, "atomic_stage", side_effect=mutate_after_stage):
            with self.assertRaises(module.KBError) as caught:
                module.main(["apply", str(path), "--expect-revision", "1",
                             "--expect-transaction-sha256", digest, "--compact"])
        self.assertIn("changed before commit", str(caught.exception))
        outcome = caught.exception.details["write_outcome"]
        self.assertEqual(outcome["stage"], "before_replacement")
        self.assertFalse(outcome["recovery_required"])
        self.assertEqual(outcome["cleanup_errors"], [])
        self.assertEqual(self.canonical_bytes(), before)
        self.assertEqual(list(self.kb.glob(".*.tmp")), [])


if __name__ == "__main__":
    unittest.main(verbosity=2)
