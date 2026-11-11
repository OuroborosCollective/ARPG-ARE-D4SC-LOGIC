"""Actual child-process lifecycle tests for the offline audit runner, not game evidence."""
import importlib.util
from pathlib import Path
import sys
import unittest


class CandidateCompilationTests(unittest.TestCase):
    def test_timeout_is_distinct_and_next_process_still_runs(self):
        """An actual timed-out subprocess must not prevent collecting the next result."""
        path = Path(__file__).resolve().parents[2]/'scripts/imports/compile_candidates.py'
        spec = importlib.util.spec_from_file_location('candidate_compiler', path)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        timed = module.run_bounded([sys.executable, '-c', 'import time; time.sleep(1)'], 0.01)
        self.assertEqual('timeout', timed['status'])
        self.assertIsNone(timed['exit_code'])
        record = {'classification': 'ausführbarer Quellcode'}
        module.apply_outcome(record, timed)
        self.assertEqual('unbestätigt: Compiler-Timeout', record['classification'])
        next_result = module.run_bounded([sys.executable, '-c', 'import sys; sys.stdout.buffer.write(bytes([0xc3, 0xa4]))'], 5)
        self.assertEqual('completed', next_result['status'])
        self.assertEqual(0, next_result['exit_code'])
        self.assertIn('ä', next_result['log'])
