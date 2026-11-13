import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from route_work import plan


class SpecWorkflowTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        (self.root / 'ProjectSettings').mkdir()
        (self.root / 'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 6000.6.8f1\n')
        (self.root / 'Assets').mkdir()
        self.names = ['spec-driven-develop', 'review-spd', 'speckit-bug-assess',
                      'speckit-bug-fix', 'speckit-bug-test', 'speckit-specify',
                      'speckit-plan', 'speckit-tasks', 'speckit-analyze',
                      'speckit-implement', 'speckit-converge']
        skills = []
        for name in self.names:
            p = self.root / 'skills' / name / 'SKILL.md'
            p.parent.mkdir(parents=True)
            p.write_text(name)
            skills.append(dict(name=name, path=str(p), sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
        (self.root / 'spec-workflow-catalog.json').write_text(json.dumps(dict(schema='ashen.skill-catalog.v1', skills=skills)))
        # Exercise the real Director entrypoint with a machine-local catalog root.
        self.patcher = patch('route_work.__file__', str(self.root / 'scripts/route_work.py'))
        self.patcher.start()
        self.addCleanup(self.patcher.stop)

    def test_hunter_repair_selects_both_workflows_without_writing(self):
        before = {str(p): p.read_bytes() for p in self.root.rglob('*') if p.is_file()}
        result = plan('Jäger Bogenschuss Animation reparieren', project=self.root)
        self.assertIn('spec_workflow', result)
        route = result['spec_workflow']
        self.assertEqual('bug', route['kind'])
        self.assertEqual(['spec-driven-develop', 'speckit-bug-assess', 'speckit-bug-fix',
                          'speckit-bug-test', 'review-spd'], [s['name'] for s in route['skills']])
        self.assertTrue(route['advisory_only'])
        self.assertEqual('NOT_EXECUTED', route['execution'])
        self.assertEqual(before, {str(p): p.read_bytes() for p in self.root.rglob('*') if p.is_file()})

    def test_feature_plan_has_specification_and_convergence(self):
        result = plan('Spec Kit Feature planen', project=self.root)
        self.assertIn('spec_workflow', result)
        route = result['spec_workflow']
        self.assertEqual('feature', route['kind'])
        self.assertIn('speckit-converge', [s['name'] for s in route['skills']])

    def test_changed_or_missing_skills_are_not_verified(self):
        (self.root / 'skills/speckit-bug-fix/SKILL.md').write_text('changed')
        (self.root / 'skills/speckit-bug-test/SKILL.md').unlink()
        result = plan('Bug fix', project=self.root)
        self.assertIn('spec_workflow', result)
        skills = {s['name']: s['status'] for s in result['spec_workflow']['skills']}
        self.assertEqual('CHANGED', skills['speckit-bug-fix'])
        self.assertEqual('MISSING', skills['speckit-bug-test'])

    def test_explicit_valid_project_is_required(self):
        with self.assertRaises(ValueError):
            plan('Bug fix', project=self.root / 'absent')

    def test_ordinary_work_does_not_require_a_spec(self):
        self.assertNotIn('spec_workflow', plan('Wald ansehen', project=self.root))
        self.assertNotIn('spec_workflow', plan('Bug fix'))

    def test_absent_catalog_keeps_requested_capabilities_visible_as_missing(self):
        (self.root / 'spec-workflow-catalog.json').unlink()
        result = plan('Bug fix', project=self.root)
        self.assertTrue(all(s['status'] == 'MISSING' for s in result['spec_workflow']['skills']))


if __name__ == '__main__':
    unittest.main()
