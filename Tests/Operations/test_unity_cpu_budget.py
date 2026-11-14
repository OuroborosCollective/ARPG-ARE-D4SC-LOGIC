import importlib.util
from pathlib import Path
import unittest

path = Path(__file__).resolve().parents[2] / 'Tools/Operations/unity_cpu_budget.py'
spec = importlib.util.spec_from_file_location('budget', path)
budget = importlib.util.module_from_spec(spec)
spec.loader.exec_module(budget)


class UnitySelectionTests(unittest.TestCase):
    def test_selects_only_owner_editor_and_same_owner_descendants(self):
        def p(uid, ppid, exe):
            return dict(uid=uid, ppid=ppid, exe=exe)
        processes = {
            1: p(997, 0, budget.EDITOR_ROOT + '6000.6.8f1/Editor/Unity'),
            2: p(997, 1, '/tools/UnityShaderCompiler'),
            3: p(997, 2, '/tools/worker'),
            4: p(0, 1, '/usr/bin/node'),
            5: p(997, 0, '/usr/bin/node'),
            6: p(1000, 0, budget.EDITOR_ROOT + '6000.6.8f1/Editor/Unity'),
            7: p(997, 0, '/another/Editor/Unity'),
            8: p(997, 0, budget.EDITOR_ROOT + '6000.6.8f1/Editor/Unity.backup'),
        }
        family, editors = budget.select_family(processes, 997)
        self.assertEqual({1, 2, 3}, family)
        self.assertEqual({1}, editors)

    def test_empty_inventory_cannot_target_anything(self):
        self.assertEqual((set(), set()), budget.select_family({}, 997))


if __name__ == '__main__':
    unittest.main()
