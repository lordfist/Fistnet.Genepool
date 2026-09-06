"""Disposable integration tests; never transact against the working KB.

Run with the existing Python: python -X utf8 -B test_source_versions.py
All synthetic repositories, commits, and KBs are removed on completion.
"""
import copy
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest


TOOLS = Path(__file__).resolve().parent


def sha(data):
    return hashlib.sha256(data).hexdigest().upper()


class SourceVersionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix=".kb-version-test-", dir=TOOLS.parent)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "experiment"
        self.kb = self.root / "KnowledgeBase"
        self.tools = self.kb / "tools"
        self.tools.mkdir(parents=True)
        for name in ("kb.py", "prepare.py", "source_versions.py"):
            shutil.copyfile(TOOLS / name, self.tools / name)
        self.env = {k: v for k, v in os.environ.items() if not k.upper().startswith("GIT_")}
        self.env.update(GIT_CONFIG_NOSYSTEM="1", GIT_CONFIG_GLOBAL=os.devnull,
                        GIT_TERMINAL_PROMPT="0", GIT_NO_LAZY_FETCH="1")
        self.git("init", "--quiet")
        self.git("config", "user.name", "Synthetic KB test")
        self.git("config", "user.email", "synthetic@example.invalid")
        self.git("config", "commit.gpgsign", "false")
        self.git("config", "core.autocrlf", "false")
        (self.root / ".gitattributes").write_bytes(b"*.txt text eol=lf\n")
        self.file = self.root / "evidence.txt"
        self.old_bytes = b"\xef\xbb\xbfold source\r\nsecond line\r\n"
        self.file.write_bytes(self.old_bytes)
        self.git("add", "--", "evidence.txt", ".gitattributes")
        self.git("commit", "--quiet", "-m", "synthetic source")
        self.commit = self.git("rev-parse", "HEAD").decode().strip()
        self.old = {"source_id": "SRC-0001", "title": "Synthetic source",
                    "locator": {"type": "file", "path": str(self.file)},
                    "sha256": sha(self.old_bytes), "size_bytes": len(self.old_bytes)}
        self.records = [{"id": "DEC-0001", "kind": "owner_decision", "status": "active",
                         "statement": "Synthetic fixture authorization, not experiment knowledge.",
                         "source_refs": ["SRC-0001"]}]
        self.catalog = {"catalog_revision": 1, "sources": [self.old]}
        self.core = {"schema_version": 1, "kb_revision": 1,
                     "experiment": {"root": str(self.root)}}
        self.save_fixture()

    def git(self, *args):
        result = subprocess.run(["git", "-c", "protocol.allow=never", *args], cwd=self.root,
                                env=self.env, capture_output=True, timeout=15)
        self.assertEqual(result.returncode, 0, result.stderr.decode(errors="replace"))
        return result.stdout

    def save_fixture(self):
        records = b"".join((json.dumps(r) + "\n").encode() for r in self.records)
        sources = json.dumps(self.catalog).encode()
        self.core["state_files"] = {"records_jsonl_sha256": sha(records), "sources_json_sha256": sha(sources)}
        (self.kb / "records.jsonl").write_bytes(records)
        (self.kb / "sources.json").write_bytes(sources)
        (self.kb / "core.json").write_text(json.dumps(self.core), encoding="utf-8")

    def canonical_bytes(self):
        return [(self.kb / name).read_bytes() for name in ("core.json", "records.jsonl", "sources.json")]

    def call(self, *args, tool="kb.py", ok=True):
        result = subprocess.run([sys.executable, "-X", "utf8", "-B", str(self.tools / tool), *args, "--compact"],
                                cwd=self.root, env=self.env, capture_output=True, timeout=30)
        try:
            parsed = json.loads(result.stdout)
        except ValueError:
            self.fail(result.stderr.decode(errors="replace") + result.stdout.decode(errors="replace"))
        # Successful retrieval envelopes omit 'ok'; mutations/validation include it.
        self.assertIs(parsed.get("ok", result.returncode == 0), ok, parsed)
        if ok:
            self.assertEqual(result.returncode, 0, result.stderr.decode(errors="replace"))
        return parsed

    def draft(self, *, unavailable=False):
        self.file.write_bytes(b"new current source\n")
        history = ["--history-unavailable-reason", "Old uncommitted bytes were not retained."] if unavailable else ["--git-commit", self.commit]
        result = self.call("revise-source", "SRC-0001", "SRC-0002", "--expect-revision", "1",
                           "--actor", "Synthetic test", "--reason", "Intentional source edit",
                           "--record-id", "DEC-0001", *history)
        self.assertIs(result["canonical_writes"], False)
        return result["transaction"]

    def apply(self, transaction, *, dry=False, ok=True, revision=1):
        path = self.kb / "transaction.json"
        path.write_text(json.dumps(transaction), encoding="utf-8")
        return self.call("apply", str(path), "--expect-revision", str(revision),
                         *(["--dry-run", "--preview-values", "--budget", "20000"] if dry else []), ok=ok)

    def test_git_revision_preview_apply_and_exact_historical_identity(self):
        before = self.canonical_bytes()
        transaction = self.draft()
        old, new = transaction["upsert_sources"]
        self.assertEqual(old["sha256"], sha(self.old_bytes))
        self.assertEqual(old["supersession"]["history"]["transform"], "lf_to_crlf")
        self.assertEqual(new["sha256"], sha(self.file.read_bytes()))
        self.apply(transaction, dry=True)
        self.assertEqual(self.canonical_bytes(), before)
        self.apply(transaction)
        result = self.call("validate", "--deep")
        self.assertEqual(result["warnings"], [])
        self.assertEqual(result["kb_revision"], 2)
        self.assertEqual(json.loads((self.kb / "records.jsonl").read_text())["source_refs"], ["SRC-0001"])

    def test_uncommitted_history_is_explicit_warning(self):
        self.apply(self.draft(unavailable=True))
        result = self.call("validate", "--deep")
        self.assertEqual(len(result["warnings"]), 1)
        self.assertIn("historical bytes unavailable", result["warnings"][0])

    def test_current_drift_still_fails(self):
        self.apply(self.draft())
        self.file.write_bytes(b"unexpected third version")
        result = self.call("validate", "--deep", ok=False)
        self.assertTrue(any("SRC-0002 is stale" in error for error in result["errors"]))

    def test_bad_history_never_mutates_canonical_state(self):
        transaction = self.draft()
        before = self.canonical_bytes()
        for field, value in (("commit", "0" * 40), ("blob_oid", "0" * 40),
                             ("transform", "raw"), ("transform", []), ("path", "../outside.txt")):
            with self.subTest(field=field):
                invalid = copy.deepcopy(transaction)
                invalid["upsert_sources"][0]["supersession"]["history"][field] = value
                self.apply(invalid, ok=False)
                self.assertEqual(self.canonical_bytes(), before)

    def test_broken_lineage_and_provenance_rejected(self):
        transaction = self.draft()
        for change in ("successor", "predecessor", "record_ids", "history", "self", "locator"):
            with self.subTest(change=change):
                invalid = copy.deepcopy(transaction)
                old, new = invalid["upsert_sources"]
                if change == "successor": old["supersession"]["source_id"] = "SRC-9999"
                if change == "predecessor": new["supersedes"] = "SRC-9999"
                if change == "record_ids": old["supersession"]["record_ids"] = ["DEC-9999"]
                if change == "history": old["supersession"]["history"] = {"kind": "unavailable", "reason": ""}
                if change == "self": old["supersession"]["source_id"] = "SRC-0001"
                if change == "locator": new["locator"]["path"] = str(self.root / "different.txt")
                self.apply(invalid, ok=False)

    def test_immutable_source_hash_size_and_revision_conflict(self):
        transaction = self.draft()
        before = self.canonical_bytes()
        for field, value in (("sha256", "0" * 64), ("size_bytes", 1)):
            invalid = copy.deepcopy(transaction)
            invalid["upsert_sources"][0][field] = value
            self.apply(invalid, ok=False)
        self.apply(transaction, revision=999, ok=False)
        self.assertEqual(self.canonical_bytes(), before)

    def test_state_hash_corruption_is_recovery_required(self):
        (self.kb / "records.jsonl").write_bytes((self.kb / "records.jsonl").read_bytes() + b"\n")
        result = self.call("validate", "--deep", ok=False)
        self.assertIn("RECOVERY_REQUIRED", json.dumps(result))

    def test_prepared_history_preserved_verified_and_not_reextracted(self):
        self.call("prepare", "SRC-0001", "--actor", "Synthetic test", tool="prepare.py")
        # Preparation advanced the disposable KB to revision 2.
        self.core = json.loads((self.kb / "core.json").read_text())
        self.core["kb_revision"] = 1
        (self.kb / "core.json").write_text(json.dumps(self.core))
        transaction = self.draft()
        prepared = self.kb / "prepared" / "SRC-0001.jsonl"
        before = prepared.read_bytes()
        self.apply(transaction)
        result = self.call("verify", "SRC-0001", tool="prepare.py")["verification"]
        self.assertTrue(result["historical"] and result["original_bytes_verified"])
        source_result = self.call("get", "SRC-0001")
        self.assertTrue(source_result["historical_source"])
        hits = self.call("search", "old")["hits"]
        self.assertTrue(any(hit.get("historical_source") and hit["kind"] == "prepared_chunk" for hit in hits))
        self.call("prepare", "SRC-0001", "--actor", "Synthetic test", "--upgrade", tool="prepare.py", ok=False)
        self.assertEqual(prepared.read_bytes(), before)
        prepared.write_bytes(before + b"\n")
        self.call("verify", "SRC-0001", tool="prepare.py", ok=False)
        self.call("validate", "--deep", ok=False)

    def test_git_proof_requires_matching_commit_bytes(self):
        self.file.write_bytes(b"new current source\n")
        self.git("add", "--", "evidence.txt")
        self.git("commit", "--quiet", "-m", "synthetic changed source")
        self.commit = self.git("rev-parse", "HEAD").decode().strip()
        result = self.call("revise-source", "SRC-0001", "SRC-0002", "--expect-revision", "1",
                           "--actor", "Synthetic test", "--reason", "Edit", "--record-id", "DEC-0001",
                           "--git-commit", self.commit, ok=False)
        self.assertIn("exact bytes", result["error"])

    def test_shared_object_store_rejected_without_following_it(self):
        transaction = self.draft()
        (self.root / ".git" / "objects" / "info" / "alternates").write_text("../../../../outside")
        self.apply(transaction, ok=False)

    def test_successive_revisions_preserve_the_entire_chain(self):
        self.apply(self.draft())
        self.file.write_bytes(b"third intentional version\n")
        transaction = self.call("revise-source", "SRC-0002", "SRC-0003", "--expect-revision", "2",
            "--actor", "Synthetic test", "--reason", "Second edit", "--record-id", "DEC-0001",
            "--history-unavailable-reason", "Intermediate working version was not committed.")["transaction"]
        self.apply(transaction, revision=2)
        result = self.call("validate", "--deep")
        self.assertEqual(result["counts"]["historical_sources"], 2)
        self.assertEqual(len(result["warnings"]), 1)

    def test_lineage_cycle_is_rejected(self):
        transaction = self.draft()
        old, new = transaction["upsert_sources"]
        old["supersedes"] = "SRC-0002"
        new["supersession"] = copy.deepcopy(old["supersession"])
        new["supersession"]["source_id"] = "SRC-0001"
        # This is a deliberately malformed synthetic state with matching file hashes.
        self.catalog["sources"] = [old, new]
        self.save_fixture()
        result = self.call("validate", ok=False)
        self.assertTrue(any("cycle" in error for error in result["errors"]))

    def test_raw_git_bytes_need_no_line_ending_transform(self):
        self.old_bytes = b"old source\n"
        self.file.write_bytes(self.old_bytes)
        self.git("add", "--", "evidence.txt")
        self.git("commit", "--quiet", "-m", "synthetic LF source")
        self.commit = self.git("rev-parse", "HEAD").decode().strip()
        self.old.update(sha256=sha(self.old_bytes), size_bytes=len(self.old_bytes))
        self.save_fixture()
        transaction = self.draft()
        self.assertEqual(transaction["upsert_sources"][0]["supersession"]["history"]["transform"], "raw")
        self.apply(transaction)
        self.call("validate", "--deep")

    def test_unavailable_history_does_not_claim_prepared_original_verification(self):
        self.call("prepare", "SRC-0001", "--actor", "Synthetic test", tool="prepare.py")
        self.core = json.loads((self.kb / "core.json").read_text())
        self.core["kb_revision"] = 1
        (self.kb / "core.json").write_text(json.dumps(self.core))
        self.apply(self.draft(unavailable=True))
        result = self.call("verify", "SRC-0001", tool="prepare.py")["verification"]
        self.assertFalse(result["original_bytes_verified"])
        self.assertIsNone(result["source_sha256"])
        self.assertEqual(len(result["warnings"]), 1)


if __name__ == "__main__":
    unittest.main(verbosity=2)
