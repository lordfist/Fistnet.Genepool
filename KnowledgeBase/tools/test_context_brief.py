"""Disposable public-contract checks for brief, current-state KB retrieval.

Run with the existing Python: python -X utf8 -B test_context_brief.py
Only kb.py and source_versions.py are copied into temporary fixture roots.
No working KB content, simulation source, builds, Git repositories, retained
test runs, or external evidence are used.
"""

import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest


TOOLS = Path(__file__).resolve().parent
CANONICAL_NAMES = ("core.json", "records.jsonl", "sources.json")


def sha(data):
    return hashlib.sha256(data).hexdigest().upper()


def json_bytes(value):
    return json.dumps(value, ensure_ascii=False, separators=(",", ":")).encode("utf-8")


def estimated_tokens(value):
    # The documented conservative compact-JSON budget, including its envelope.
    return max(1, (len(json_bytes(value)) + 2) // 3)


class ContextBriefTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix=".kb-context-test-", dir=TOOLS.parent)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "synthetic-experiment"
        self.kb = self.root / "KnowledgeBase"
        self.tools = self.kb / "tools"
        self.tools.mkdir(parents=True)
        for name in ("kb.py", "source_versions.py"):
            shutil.copyfile(TOOLS / name, self.tools / name)

        self.original = self.root / "synthetic-evidence.txt"
        self.original.write_bytes(b"Fixture evidence, never experiment content.\n")
        self.prepared = self.kb / "prepared" / "SRC-0001.jsonl"
        self.prepared.parent.mkdir()
        original_sha = sha(self.original.read_bytes())
        coverage = {"text": "complete", "limitations": ["Synthetic fixture only."]}
        prepared_rows = [
            {"type": "meta", "source_id": "SRC-0001", "source_sha256": original_sha,
             "coverage": coverage},
            {"type": "text", "locator": "line:1", "text": "preparedonlysentinel evidence"},
        ]
        self.prepared.write_bytes(b"".join(json_bytes(row) + b"\n" for row in prepared_rows))
        self.sources = {"catalog_revision": 1, "sources": [{
            "source_id": "SRC-0001", "title": "Synthetic source",
            "locator": {"type": "file", "path": str(self.original)},
            "sha256": original_sha, "size_bytes": self.original.stat().st_size,
            "preparation": {"status": "prepared", "path": "prepared/SRC-0001.jsonl",
                            "source_sha256": original_sha,
                            "prepared_sha256": sha(self.prepared.read_bytes()),
                            "coverage": coverage},
        }]}
        self.records = [self.record("FACT-0001", "A concise current statement.")]
        self.core = {
            "schema_version": 1, "kb_revision": 17,
            "experiment": {"root": str(self.root), "purpose": "Synthetic orientation.",
                           "status": "DESIGN_ACCEPTED_IMPLEMENTATION_UNSTARTED"},
            "proposal": {"id": "R03_Step4", "record_id": "FACT-0001",
                         "status": "DESIGN_ACCEPTED_IMPLEMENTATION_UNSTARTED",
                         "implementation_authorized": False},
            "current_iteration_part": {"id": "R03_Step4",
                                       "status": "DESIGN_ACCEPTED_IMPLEMENTATION_UNSTARTED",
                                       "record_ids": ["FACT-0001"]},
            "authority": {"actor": "Synthetic test", "current_stage_record": "FACT-0001",
                          "next_action": "Await owner start instruction."},
            "routes": {"orientation": {"record_ids": ["FACT-0001"]}},
        }
        self.save_fixture()

    @staticmethod
    def record(identifier, statement, **extra):
        return {"id": identifier, "kind": "observed_fact", "status": "active",
                "statement": statement, "source_refs": [], **extra}

    def save_fixture(self):
        records = b"".join(json_bytes(row) + b"\n" for row in self.records)
        sources = json_bytes(self.sources)
        self.core["state_files"] = {"records_jsonl_sha256": sha(records),
                                    "sources_json_sha256": sha(sources)}
        (self.kb / "records.jsonl").write_bytes(records)
        (self.kb / "sources.json").write_bytes(sources)
        (self.kb / "core.json").write_bytes(json_bytes(self.core))

    def canonical_bytes(self):
        return tuple((self.kb / name).read_bytes() for name in CANONICAL_NAMES)

    def call(self, *args, ok=True, audited=False):
        command = [sys.executable, "-X", "utf8", "-B"]
        if audited:
            # Deny payload reads using Python's file-open audit event, including
            # Path.open, builtins.open, and os.open. Metadata checks are allowed.
            # Validation must still run exactly once, without deep validation.
            wrapper = r'''
import json, os, pathlib, sys
sys.path.insert(0, sys.argv.pop(1))
import kb
protected = {os.path.normcase(str(kb.EXPERIMENT_ROOT / "synthetic-evidence.txt")),
             os.path.normcase(str(kb.KB_ROOT / "prepared" / "SRC-0001.jsonl"))}
def audit(event, args):
    if event == "open" and isinstance(args[0], (str, bytes, os.PathLike)):
        target = os.path.normcase(os.path.abspath(os.fsdecode(args[0])))
        if target in protected:
            raise AssertionError("brief opened an evidence payload: " + target)
sys.addaudithook(audit)
original_validate = kb.validate_state
calls = []
def validate(*args, **kwargs):
    calls.append(kwargs.get("deep", False))
    return original_validate(*args, **kwargs)
kb.validate_state = validate
result = kb.main(sys.argv[1:])
assert calls == [False], "expected one ordinary validation, got " + repr(calls)
raise SystemExit(result)
'''
            command += ["-c", wrapper, str(self.tools)]
        else:
            command += [str(self.tools / "kb.py")]
        result = subprocess.run(command + [*args, "--compact"], cwd=self.root,
                                capture_output=True, timeout=20)
        diagnostic = result.stderr.decode("utf-8", errors="replace")
        try:
            parsed = json.loads(result.stdout)
        except ValueError:
            self.fail(diagnostic + result.stdout.decode("utf-8", errors="replace"))
        if ok:
            self.assertEqual(result.returncode, 0, diagnostic or parsed)
            self.assertIsNot(parsed.get("ok"), False, parsed)
        else:
            self.assertNotEqual(result.returncode, 0, parsed)
            self.assertIs(parsed.get("ok"), False, parsed)
        return parsed

    def context(self, topic, *args, **kwargs):
        return self.call("context", "--topic", topic, "--brief", *args, **kwargs)

    @staticmethod
    def ids(result, key="records"):
        return [row["id"] for row in result[key]]

    def test_default_brief_preserves_current_orientation_within_total_budget(self):
        result = self.context("orientation")
        self.assertLessEqual(estimated_tokens(result), 2000)
        self.assertLessEqual(result["estimated_tokens"], 2000)
        self.assertEqual(self.ids(result), ["FACT-0001"])
        orientation = result.get("core", result.get("orientation", {}))
        self.assertEqual(orientation["experiment"]["root"], str(self.root))
        self.assertIn("DESIGN_ACCEPTED_IMPLEMENTATION_UNSTARTED", json.dumps(orientation))
        self.assertIn("Await owner start instruction.", json.dumps(orientation))
        self.assertIn('"implementation_authorized": false', json.dumps(orientation))
        self.assertTrue(result["projection"]["is_projection"])
        self.assertEqual(result["projection"]["mode"], "brief")

    def test_exact_record_ids_precede_routes_and_relevance(self):
        self.records += [self.record("FACT-0002", "needle " * 150),
                         self.record("FACT-0003", "Explicit identity wins.")]
        self.core["routes"]["needle"] = {"record_ids": ["FACT-0002", "FACT-0001"]}
        self.save_fixture()
        result = self.context("needle", "--topic", "FACT-0003", "--no-core", "--budget", "5000")
        self.assertEqual(self.ids(result)[:3], ["FACT-0003", "FACT-0002", "FACT-0001"])

    def test_exact_routes_keep_authored_order_and_topic_order(self):
        self.records += [self.record("FACT-0002", "Second authored item."),
                         self.record("FACT-0003", "First authored item."),
                         self.record("FACT-0004", "alpha " * 300, title="alpha alpha alpha")]
        self.core["routes"].update(alpha={"record_ids": ["FACT-0003", "FACT-0002"]},
                                    beta={"record_ids": ["FACT-0001", "FACT-0003"]})
        self.save_fixture()
        result = self.context("alpha", "--topic", "beta", "--no-core", "--budget", "6000")
        self.assertEqual(self.ids(result)[:3], ["FACT-0003", "FACT-0002", "FACT-0001"])
        self.assertEqual(len(self.ids(result)), len(set(self.ids(result))))

    def test_current_relevance_uses_uncertainty_and_derivation_without_history_noise(self):
        self.records = [
            self.record("FACT-0001", "Otherwise unrelated.",
                        prior_statement="weightedneedle " * 150,
                        derivation={"artifact_sha256": "artifacthashsentinel"}),
            self.record("FACT-0002", "A current result.", title="weightedneedle"),
            self.record("FACT-0003", "Uncertainty is current.", uncertainty="uncertaintyneedle"),
            self.record("FACT-0004", "Method is current.", derivation={"method": "derivationneedle"}),
            self.record("FACT-0005", "weightedneedle " * 100),
        ]
        self.save_fixture()
        result = self.call("search", "weightedneedle", "--records-only")
        self.assertEqual(self.ids(result, "hits"), ["FACT-0002", "FACT-0005"])
        for term, identifier in (("uncertaintyneedle", "FACT-0003"),
                                 ("derivationneedle", "FACT-0004")):
            with self.subTest(term=term):
                self.assertIn(identifier, self.ids(self.context(term, "--no-core")))
                self.assertIn(identifier, self.ids(self.call("search", term, "--records-only"), "hits"))
        self.assertEqual(self.ids(self.call("search", "artifacthashsentinel", "--records-only"), "hits"), [])

    def test_history_only_terms_require_opt_in_in_context_and_search(self):
        self.records[0].update(
            prior_statement="priorstatementsentinel",
            resolution_notes=[{"note": "Current corrected wording.",
                               "prior_fields": {"statement": "priorfieldssentinel"}}])
        self.save_fixture()
        for term in ("priorstatementsentinel", "priorfieldssentinel"):
            with self.subTest(term=term):
                self.assertEqual(self.ids(self.context(term, "--no-core")), [])
                self.assertEqual(self.ids(self.call("search", term, "--records-only"), "hits"), [])
                self.assertIn("FACT-0001", self.ids(self.context(term, "--history", "--no-core")))
                self.assertIn("FACT-0001", self.ids(self.call("search", term, "--records-only", "--history"), "hits"))

    def test_resolved_records_retain_correction_and_evidence_pointers(self):
        self.records = [
            self.record("FACT-0001", "Resolutionneedle is settled.", status="resolved",
                        source_refs=["SRC-0001"], related_record_ids=["FACT-0002"],
                        resolution_notes=[{"note": "Current correction: inspect FACT-0002.",
                                           "record_ids": ["FACT-0002"], "source_refs": ["SRC-0001"]}]),
            self.record("FACT-0002", "The correcting detail."),
        ]
        note = self.records[0]["resolution_notes"][0]
        for notes in ([note], note):
            with self.subTest(notes_type=type(notes).__name__):
                self.records[0]["resolution_notes"] = notes
                self.save_fixture()
                self.call("validate")
                result = self.context("resolutionneedle", "--no-core")
                row = result["records"][0]
                self.assertEqual(row["status"], "resolved")
                self.assertEqual(row["detail_id"], "FACT-0001")
                self.assertIs(row["detail_available"], True)
                self.assertIn("FACT-0002", json.dumps(row))
                self.assertIn("SRC-0001", json.dumps(row))
                search = self.call("search", "resolutionneedle", "--records-only")
                self.assertEqual(self.ids(search, "hits"), ["FACT-0001"])
                self.assertIn("FACT-0002", json.dumps(search))
                full = self.call("get", row["detail_id"])
                self.assertEqual(full["value"], self.records[0])

    def test_long_brief_marks_clipped_fields_and_keeps_full_detail_retrievable(self):
        self.records[0].update(statement="longstatementneedle " + "long explanation " * 700,
                               uncertainty="Current uncertainty " * 150)
        self.save_fixture()
        result = self.context("FACT-0001", "--no-core")
        self.assertLessEqual(estimated_tokens(result), 2000)
        row = result["records"][0]
        self.assertIs(row["detail_required"], True)
        self.assertTrue(row["truncated_fields"])
        self.assertTrue(any("statement" in field for field in row["truncated_fields"]))
        self.assertIs(row["detail_available"], True)
        self.assertEqual(self.call("get", row["detail_id"], "--budget", "20000")["value"], self.records[0])

    def test_pagination_covers_each_record_once_and_requires_matching_revision(self):
        self.records = [self.record(f"FACT-{i:04}", "Pagination fixture.") for i in range(1, 13)]
        expected = [row["id"] for row in reversed(self.records)]
        self.core["routes"]["pages"] = {"record_ids": expected}
        self.save_fixture()
        first = self.context("pages", "--no-core", "--limit", "2")
        self.assertIsNotNone(first["next_offset"])
        self.context("pages", "--no-core", "--offset", str(first["next_offset"]), ok=False)
        conflict = self.context("pages", "--no-core", "--offset", str(first["next_offset"]),
                                "--expect-revision", "16", ok=False)
        self.assertEqual(conflict["code"], "revision_conflict")
        seen = self.ids(first)
        offsets = {0}
        page = first
        while page["next_offset"] is not None:
            self.assertNotIn(page["next_offset"], offsets, "pagination made no progress")
            offsets.add(page["next_offset"])
            page = self.context("pages", "--no-core", "--limit", "2",
                                "--offset", str(page["next_offset"]), "--expect-revision", "17")
            self.assertLessEqual(estimated_tokens(page), 2000)
            seen.extend(self.ids(page))
        self.assertEqual(seen, expected)
        self.assertTrue(page["scan_complete"])

    def test_explicit_budget_and_nonbrief_default_remain_effective(self):
        self.records[0]["statement"] = "A large complete record " * 350
        self.save_fixture()
        full = self.call("context", "--topic", "FACT-0001", "--records-only", "--no-core")
        self.assertEqual(full["records"][0], self.records[0])
        self.assertGreater(estimated_tokens(full), 2000)
        self.assertLessEqual(estimated_tokens(full), 6000)
        brief = self.context("FACT-0001", "--no-core", "--budget", "1200")
        self.assertLessEqual(estimated_tokens(brief), 1200)
        for option in (("--fields", "id"), ("--summary",), ("--ids-only",)):
            with self.subTest(option=option):
                self.context("FACT-0001", *option, ok=False)

    def test_brief_validates_once_without_evidence_access_or_canonical_mutation(self):
        before = self.canonical_bytes()
        entries = set(self.root.rglob("*"))
        result = self.context("orientation", audited=True)
        self.assertEqual(self.ids(result), ["FACT-0001"])
        self.assertEqual(result.get("evidence_snippets", []), [])
        self.assertEqual(self.canonical_bytes(), before)
        self.assertEqual(set(self.root.rglob("*")), entries, "brief created an unexpected sidecar")

    def test_corrupt_hash_or_broken_reference_refuses_brief_without_repair(self):
        clean = self.canonical_bytes()
        path = self.kb / "records.jsonl"
        path.write_bytes(path.read_bytes() + b"\n")
        corrupt = self.canonical_bytes()
        failure = self.context("orientation", ok=False)
        self.assertIn("RECOVERY_REQUIRED", json.dumps(failure))
        self.assertEqual(self.canonical_bytes(), corrupt)
        self.save_fixture()
        self.assertEqual(self.canonical_bytes(), clean)
        self.records[0]["source_refs"] = ["SRC-9999"]
        self.save_fixture()
        invalid = self.canonical_bytes()
        failure = self.context("orientation", ok=False)
        self.assertIn("SRC-9999", json.dumps(failure))
        self.assertEqual(self.canonical_bytes(), invalid)

    def test_existing_projection_and_prepared_evidence_retrieval_stay_available(self):
        self.records[0].update(title="projectionneedle", extra_detail={"retained": True})
        self.save_fixture()
        projected = self.call("search", "projectionneedle", "--records-only", "--fields", "id", "kind")
        self.assertEqual(projected["hits"], [{"id": "FACT-0001", "kind": "record"}])
        self.assertEqual(self.call("get", "FACT-0001")["value"], self.records[0])
        evidence = self.call("search", "preparedonlysentinel")
        self.assertTrue(any(row["kind"] == "prepared_chunk" for row in evidence["hits"]))
        context = self.call("context", "--topic", "preparedonlysentinel", "--no-core")
        self.assertEqual(context["returned_evidence_count"], 1)
        source = self.call("get", "SRC-0001")
        self.assertTrue(source["prepared_content_accessed"])
        self.assertEqual(len(source["prepared_chunks"]), 1)


if __name__ == "__main__":
    unittest.main()
