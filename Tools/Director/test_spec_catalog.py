import json
from pathlib import Path
import tempfile
import unittest

from official_unity import inventory
from spec_catalog import build


class SpecCatalogTests(unittest.TestCase):
    def test_bundled_reference_changes_remain_visible_after_catalog_creation(self):
        project = Path(__file__).resolve().parents[2]
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            for name in ['spec-driven-develop', 'deep-discuss', 'review-spd']:
                (root / name).mkdir()
                (root / name / 'SKILL.md').write_text(name)
            ref = root / 'spec-driven-develop/reference.md'
            ref.write_text('inspected baseline')
            catalog = root / 'catalog.json'
            catalog.write_text(json.dumps(build(project, root)))
            self.assertEqual(15, len(inventory(catalog)))
            self.assertTrue(all(s['status'] == 'INSTALLED' for s in inventory(catalog)))
            ref.write_text('changed after inspection')
            self.assertEqual('CHANGED', next(s['status'] for s in inventory(catalog)
                                          if s['name'] == 'spec-driven-develop'))

    def test_missing_install_is_not_blessed_as_a_complete_catalog(self):
        with tempfile.TemporaryDirectory() as temp:
            with self.assertRaises(FileNotFoundError):
                build(temp, temp)

    def test_project_codex_events_remain_disabled(self):
        project = Path(__file__).resolve().parents[2]
        integration = json.loads((project / '.specify/integration.json').read_text())
        self.assertEqual('false', integration['integration_settings']['codex']['parsed_options']['events'])


if __name__ == '__main__':
    unittest.main()
