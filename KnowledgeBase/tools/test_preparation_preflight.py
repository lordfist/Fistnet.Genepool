"""Disposable preparation tests: never load or modify the working KB.

Only copied tools and synthetic originals beneath an owned temporary KB are used.
No experiment source code, Git repository, network or dependency installer is run.
"""
import argparse
import contextlib
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest
from unittest import mock


TOOLS = Path(__file__).resolve().parent


def sha(payload):
    return hashlib.sha256(payload).hexdigest().upper()


class PreparationPreflightTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix=".kb-preflight-test-", dir=TOOLS.parent)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "experiment"
        self.kb_root = self.root / "KnowledgeBase"
        self.tools = self.kb_root / "tools"
        self.tools.mkdir(parents=True)
        for name in ("kb.py", "prepare.py", "source_versions.py"):
            shutil.copyfile(TOOLS / name, self.tools / name)
        self.originals = {}
        sources = []
        for number, text in ((1, "first synthetic source\n"), (2, "second synthetic source\n")):
            path = self.root / f"source-{number}.txt"
            path.write_bytes(text.encode())
            self.originals[number] = path
            sources.append({"source_id": f"SRC-{number:04}", "title": f"Synthetic source {number}",
                "locator": {"type": "file", "path": str(path)}, "sha256": sha(path.read_bytes()),
                "size_bytes": path.stat().st_size})
        source_bytes = json.dumps({"catalog_revision": 1, "sources": sources}).encode()
        (self.kb_root / "records.jsonl").write_bytes(b"")
        (self.kb_root / "sources.json").write_bytes(source_bytes)
        (self.kb_root / "core.json").write_text(json.dumps({"schema_version": 1, "kb_revision": 1,
            "experiment": {"root": str(self.root)}, "state_files": {
                "records_jsonl_sha256": sha(b""), "sources_json_sha256": sha(source_bytes)}}), encoding="utf-8")

        # Bind imported helpers to this synthetic installation, without replacing
        # any module another local test may have imported from its own fixture.
        saved_path = list(sys.path)
        saved_modules = {name: sys.modules.pop(name, None) for name in ("kb", "source_versions")}
        try:
            spec = importlib.util.spec_from_file_location("synthetic_prepare", self.tools / "prepare.py")
            self.prepare = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(self.prepare)
            self.kb = self.prepare.kb
        finally:
            sys.path[:] = saved_path
            for name, previous in saved_modules.items():
                sys.modules.pop(name, None)
                if previous is not None:
                    sys.modules[name] = previous
        self.assertEqual(self.kb.KB_ROOT, self.kb_root)

    def call_prepare(self, source_id="SRC-0002", *, upgrade=False):
        output = io.StringIO()
        args = argparse.Namespace(source_ids=[source_id], actor="Synthetic preparation test",
                                  upgrade=upgrade, compact=True)
        with contextlib.redirect_stdout(output):
            self.prepare.command_prepare(args)
        return json.loads(output.getvalue())

    def canonical_bytes(self):
        return {name: (self.kb_root / name).read_bytes()
                for name in ("core.json", "records.jsonl", "sources.json")}

    def assert_rejected_before_publication(self, action, expected):
        before = self.canonical_bytes()
        prepared_before = {path: path.read_bytes() for path in self.kb_root.glob("prepared/*.jsonl")}
        with self.assertRaises(self.kb.KBError) as caught:
            action()
        self.assertIn(expected, str(caught.exception))
        outcome = caught.exception.details["write_outcome"]
        self.assertEqual(outcome["stage"], "before_replacement")
        self.assertEqual(outcome["replaced_files"], [])
        self.assertFalse(outcome["commit_completed"])
        self.assertFalse(outcome["recovery_required"])
        self.assertEqual(self.canonical_bytes(), before)
        self.assertEqual({path: path.read_bytes() for path in self.kb_root.glob("prepared/*.jsonl")}, prepared_before)
        self.assertEqual(list(self.kb_root.rglob("*.tmp")), [])

    def test_unrelated_original_drift_is_rejected_before_parsing(self):
        self.originals[1].write_bytes(b"changed unrelated synthetic source\n")
        with mock.patch.object(self.prepare, "make_prepared", side_effect=AssertionError("Parser must not run")):
            self.assert_rejected_before_publication(self.call_prepare, "existing state deep validation failed")

    def test_stale_retained_preparation_is_rejected_before_parsing(self):
        self.call_prepare("SRC-0001")
        path = self.kb_root / "prepared" / "SRC-0001.jsonl"
        path.write_bytes(path.read_bytes() + b"\n")
        with mock.patch.object(self.prepare, "make_prepared", side_effect=AssertionError("Parser must not run")):
            self.assert_rejected_before_publication(self.call_prepare, "prepared representation hash mismatch")

    def test_upgrade_does_not_silently_repair_corrupt_existing_preparation(self):
        self.call_prepare("SRC-0001")
        path = self.kb_root / "prepared" / "SRC-0001.jsonl"
        path.write_bytes(path.read_bytes() + b"\n")
        self.assert_rejected_before_publication(lambda: self.call_prepare("SRC-0001", upgrade=True),
                                               "existing state deep validation failed")

    def test_malformed_staged_bytes_are_rejected_and_cleaned(self):
        original = self.prepare.atomic_write_candidate
        def stage(path, payload):
            return original(path, b"{malformed JSON\n" if path.suffix == ".jsonl" else payload)
        with mock.patch.object(self.prepare, "atomic_write_candidate", side_effect=stage):
            self.assert_rejected_before_publication(self.call_prepare, "candidate deep validation failed")

    def test_wrong_bound_candidate_meta_is_rejected_with_matching_payload_hash(self):
        original = self.prepare.make_prepared
        def wrong_meta(source, path, version):
            rows, _payload, result = original(source, path, version)
            rows[0]["source_id"] = "SRC-9999"
            return rows, self.kb.canonical_jsonl_bytes(rows), result
        with mock.patch.object(self.prepare, "make_prepared", side_effect=wrong_meta):
            self.assert_rejected_before_publication(self.call_prepare, "prepared meta record is invalid")

    def test_unrelated_drift_during_parsing_is_rejected_by_candidate_check(self):
        original = self.prepare.make_prepared
        def drift(source, path, version):
            result = original(source, path, version)
            self.originals[1].write_bytes(b"changed after initial preflight\n")
            return result
        with mock.patch.object(self.prepare, "make_prepared", side_effect=drift):
            self.assert_rejected_before_publication(self.call_prepare, "candidate deep validation failed")

    def test_normal_prepare_noop_and_explicit_upgrade(self):
        originals_before = {path: path.read_bytes() for path in self.originals.values()}
        first = self.call_prepare()
        self.assertEqual((first["from_revision"], first["to_revision"]), (1, 2))
        self.assertTrue(first["write_outcome"]["verification_completed"])
        before_noop = self.canonical_bytes()
        noop = self.call_prepare()
        self.assertEqual(noop["status"], "NO-OP")
        self.assertEqual(self.canonical_bytes(), before_noop)
        upgraded = self.call_prepare(upgrade=True)
        self.assertEqual((upgraded["from_revision"], upgraded["to_revision"]), (2, 3))
        core, records, sources = self.kb.load_state()
        self.assertEqual(sources["sources"][1]["preparation"]["version"], 2)
        self.assertTrue(self.kb.validate_state(core, records, sources, deep=True)["ok"])
        self.assertEqual({path: path.read_bytes() for path in self.originals.values()}, originals_before)
        self.assertEqual(list(self.kb_root.rglob("*.tmp")), [])

    def test_prepared_override_paths_cannot_escape_kb_or_be_unused(self):
        self.call_prepare("SRC-0001")
        core, records, sources = self.kb.load_state()
        prepared = self.kb_root / "prepared" / "SRC-0001.jsonl"
        for mapping in ({prepared: self.originals[1]}, {self.originals[1]: prepared},
                        {self.kb_root / "unused.jsonl": prepared}):
            with self.subTest(mapping=mapping):
                checked = self.kb.validate_state(core, records, sources, deep=True,
                                                prepared_file_overrides=mapping)
                self.assertFalse(checked["ok"])
                self.assertTrue(any("override" in error for error in checked["errors"]))


if __name__ == "__main__":
    unittest.main(verbosity=2)
