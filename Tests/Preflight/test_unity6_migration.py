"""Unity 6 upgrade contract (static gate; not a substitute for Editor execution)."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
TARGET = '6000.6.8f1'


class Unity6MigrationContract(unittest.TestCase):
    """Keep the project version, CI and official MCP package in sync."""

    def test_pinned_editor_matches_unity_six(self):
        version = (ROOT / 'ProjectSettings/ProjectVersion.txt').read_text()
        self.assertIn('m_EditorVersion: ' + TARGET + '\n', version)

    def test_ci_jobs_use_same_unity_six_editor(self):
        for name in ('unity-playmode-smoke.yml', 'unity-android-build.yml'):
            with self.subTest(workflow=name):
                workflow = (ROOT / '.github/workflows' / name).read_text()
                self.assertIn("unityVersion: '" + TARGET + "'", workflow)

    def test_preflight_contract_uses_unity_six(self):
        from scripts.ci import unity_contract
        self.assertEqual(TARGET, unity_contract.VERSION)

    def test_manifest_and_lock_pin_same_direct_package_versions(self):
        manifest = json.loads((ROOT / 'Packages/manifest.json').read_text())['dependencies']
        locked = json.loads((ROOT / 'Packages/packages-lock.json').read_text())['dependencies']
        for name, version in manifest.items():
            with self.subTest(package=name):
                self.assertEqual(version, locked[name]['version'])

    def test_official_pipeline_package_declared(self):
        data = json.loads((ROOT / 'Packages/manifest.json').read_text())
        version = data['dependencies'].get('com.unity.pipeline')
        self.assertIsNotNone(version, 'Unity CLI Pipeline package required for MCP')
        self.assertRegex(version, r'^0\.\d+\.\d+-exp\.\d+$')


if __name__ == '__main__':
    unittest.main()
