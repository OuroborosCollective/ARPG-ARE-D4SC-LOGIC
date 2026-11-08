"""Candidate selection tests using user-excerpt fixtures, never Unity account API evidence."""
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from scripts.assets import plan_asset_choices

class AssetSelectionTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.catalog = Path(self.tmp.name) / 'catalog.json'
        self.catalog.write_text(json.dumps({
            'scope': 'partial',
            'items': [
                {'name':'Lite Dungeon Pack - Low Poly 3D Art by Gridness', 'publisher':'Gridness Studio', 'version':'1.1'},
                {'name':'Skeleton Ghost (FREE)', 'publisher':'SR Studios Kerala', 'version':'1.0'},
                {'name':'Low Poly Fantasy Sword Pack', 'publisher':'Kindle Tech Co.', 'version':'1.0'},
            ]}), encoding='utf-8')
    def test_ashen_crypt_prioritizes_dungeon_enemy_and_loot(self):
        with patch.object(plan_asset_choices, 'CATALOG', self.catalog):
            data = plan_asset_choices.plan('ashen-crypt')
        self.assertEqual(['Lite Dungeon Pack - Low Poly 3D Art by Gridness',
                          'Skeleton Ghost (FREE)', 'Low Poly Fantasy Sword Pack'],
                         [x['title'] for x in data['candidates'][:3]])
        self.assertTrue(all(not x['entitlement_verified'] and not x['download_verified'] for x in data['candidates']))
    def test_missing_asset_not_claimed_as_owned(self):
        with patch.object(plan_asset_choices, 'CATALOG', self.catalog):
            data = plan_asset_choices.plan('ember-rift')
        self.assertTrue(all(not x['catalogued_in_owner_excerpt'] for x in data['candidates']))
    def test_invalid_profile_rejected(self):
        with self.assertRaises(ValueError):
            plan_asset_choices.plan('delete-season-progress')
    def test_catalog_must_be_explicit_partial(self):
        self.catalog.write_text(json.dumps({'scope':'full','items':[]}),encoding='utf-8')
        with patch.object(plan_asset_choices, 'CATALOG', self.catalog):
            with self.assertRaises(ValueError):
                plan_asset_choices.plan('ashen-crypt')
    def test_missing_private_catalog_fails_closed(self):
        with patch.object(plan_asset_choices, 'CATALOG', self.catalog.parent/'missing.json'):
            with self.assertRaises(ValueError):
                plan_asset_choices.plan('ashen-crypt')
if __name__ == '__main__':
    unittest.main()
