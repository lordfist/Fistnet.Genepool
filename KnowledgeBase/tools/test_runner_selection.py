"""Disposable mocked checks: no process dispatch, simulations, builds or KB writes."""
import contextlib
import io
from pathlib import Path
import unittest
from unittest.mock import patch

import run_part1_checks as runner


class RunnerSelectionTests(unittest.TestCase):
    valid_vectors = (
        ["--all"],
        ["--group", "mechanics"],
        ["--scenario", "11", "1", "reference"],
        ["--scenario", "-29", "128", "production"],
        ["--diagnostic-scenario", "11", "2048", "reference", "on", "1", "no-render"],
        ["--diagnostic-scenario", "29", "512", "production", "off", "8", "render", "0"],
        ["--diagnostic-fixture", "50", "large", "on"],
        ["--diagnostic-fixture", "0", "early", "off", "1"],
        ["--baseline-smoke"],
        ["--runner-negative-control"],
    )
    malformed_vectors = (
        [],
        ["--unknown"],
        ["--diagnostic-scenaro", "11", "128", "reference", "on", "1", "no-render"],
        ["--group"],
        ["--group", ""],
        ["--group", "mechanics", "extra"],
        ["--group", "--all"],
        ["--all", "extra"],
        ["--all", "--all"],
        ["--all", "--group", "mechanics"],
        ["--scenario", "11"],
        ["--scenario", "11", "1", "reference", "extra"],
        ["--scenario", "11", "1", "--all"],
        ["junk", "--baseline-smoke"],
        ["--baseline-smoke", "extra"],
        ["--runner-negative-control", "extra"],
        ["--diagnostic-scenario", "11", "128", "reference", "on", "1"],
        ["--diagnostic-scenario", "11", "128", "reference", "on", "1", "no-render", "10", "extra"],
        ["--diagnostic-fixture", "10", "early"],
        ["--diagnostic-fixture", "10", "early", "off", "1", "extra"],
        ["--render-preview", "unused.png"],
    )

    def test_valid_commands_dispatch_exactly_once_without_substitution(self):
        for arguments in self.valid_vectors:
            with self.subTest(arguments=arguments), \
                    patch.object(runner.tempfile, "TemporaryDirectory") as temporary, \
                    patch.object(runner.subprocess, "Popen") as popen, \
                    patch.object(runner.subprocess, "CREATE_NO_WINDOW", 0, create=True), \
                    patch.object(Path, "exists", return_value=False):
                temporary.return_value.__enter__.return_value = str(runner.KB / ".mock-only-scratch")
                popen.return_value.communicate.return_value = (b'{"passed":1}\n', b"")
                popen.return_value.returncode = 0
                result = runner.execute(arguments, "Release", 30)
                popen.assert_called_once()
                self.assertEqual(arguments, popen.call_args.args[0][1:])
                self.assertEqual(arguments, result["arguments"])
                self.assertEqual([{"passed": 1}], result["records"])
                self.assertTrue(result["scratch_removed"])
                temporary.return_value.__exit__.assert_called_once()

    def test_malformed_selection_rejected_before_scratch_or_process(self):
        for arguments in self.malformed_vectors:
            with self.subTest(arguments=arguments), \
                    patch.object(runner.tempfile, "TemporaryDirectory") as temporary, \
                    patch.object(runner.subprocess, "Popen") as popen:
                with self.assertRaises(ValueError):
                    runner.execute(arguments, "Release", 30)
                temporary.assert_not_called()
                popen.assert_not_called()

    def test_explicit_child_value_validation_remains_with_child(self):
        # These shapes enter explicit C# value validation; they cannot fall into a default suite.
        for arguments in (
            ["--scenario", "bad-seed", "999", "bad-mode"],
            ["--group", "nonexistent-group"],
            ["--diagnostic-scenario", "11", "0", "bad-mode", "bad-toggle", "3", "bad-render"],
            ["--diagnostic-fixture", "3", "bad-history", "bad-toggle", "65"],
        ):
            with self.subTest(arguments=arguments):
                self.assertEqual(arguments, runner.validate_child_arguments(arguments))

    def test_main_rejects_ambiguous_batch_before_dispatch_or_results(self):
        for batch in ("reference", "production"):
            with self.subTest(batch=batch), \
                    patch.object(runner, "execute") as execute, \
                    patch.object(runner, "save") as save, \
                    patch.object(Path, "exists") as exists, \
                    contextlib.redirect_stderr(io.StringIO()) as error:
                with self.assertRaises(SystemExit) as stopped:
                    runner.main(["--stage", "mock", "--batch", batch, "--", "--all"])
                self.assertEqual(2, stopped.exception.code)
                self.assertIn("cannot be combined", error.getvalue())
                execute.assert_not_called()
                save.assert_not_called()
                exists.assert_not_called()

    def test_main_rejects_malformed_child_before_dispatch_or_results(self):
        for arguments in self.malformed_vectors[1:]:
            with self.subTest(arguments=arguments), \
                    patch.object(runner, "execute") as execute, \
                    patch.object(runner, "save") as save, \
                    patch.object(Path, "exists") as exists, \
                    contextlib.redirect_stderr(io.StringIO()):
                with self.assertRaises(SystemExit) as stopped:
                    runner.main(["--stage", "mock", "--", *arguments])
                self.assertEqual(2, stopped.exception.code)
                execute.assert_not_called()
                save.assert_not_called()
                exists.assert_not_called()

    def test_wrapper_no_child_arguments_still_selects_all(self):
        for suffix in ([], ["--"]):
            with self.subTest(suffix=suffix), \
                    patch.object(runner, "execute") as execute, \
                    patch.object(runner, "save") as save, \
                    patch.object(Path, "exists", return_value=True), \
                    contextlib.redirect_stdout(io.StringIO()):
                execute.return_value = {"exit_code": 0, "wall_seconds": 0, "records": [], "messages": [], "stderr": ""}
                self.assertEqual(0, runner.main(["--stage", "mock", *suffix]))
                execute.assert_called_once_with(["--all"], "Release", 120)
                save.assert_called_once()

    def test_declared_batches_use_only_valid_exact_commands(self):
        for batch, expected_count in (("reference", 4), ("production", 21)):
            dispatched = []

            def mock_execute(arguments, configuration, timeout):
                runner.validate_child_arguments(arguments)
                dispatched.append(arguments)
                return {"exit_code": 0, "wall_seconds": 0, "records": [], "messages": [], "stderr": ""}

            with self.subTest(batch=batch), \
                    patch.object(runner, "execute", side_effect=mock_execute), \
                    patch.object(runner, "save"), \
                    patch.object(runner.time, "monotonic", return_value=0), \
                    patch.object(Path, "exists", return_value=True), \
                    contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(0, runner.main(["--stage", "mock", "--batch", batch]))
                self.assertEqual(expected_count, len(dispatched))


if __name__ == "__main__":
    unittest.main(verbosity=2)
