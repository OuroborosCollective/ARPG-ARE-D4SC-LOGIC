"""Unit tests for static Unity/Android CI contracts, NOT Unity runtime tests."""
import os
from pathlib import Path
import shutil
import tempfile
import unittest
import zipfile

from scripts.ci.unity_contract import inspect_project, inspect_apk, create_apk_receipt

ROOT = Path(__file__).resolve().parents[2]


class UnityContractTests(unittest.TestCase):
    def test_pinned_unity_editor_has_no_nonexistent_input_package(self):
        # Verified against installed Unity 2022.3.62f1 BuiltInPackages and real UPM failure.
        # UnityEngine.Input exists in InputLegacyModule.dll; there is no com.unity.modules.input.
        import json
        dependencies = json.loads((ROOT / "Packages/manifest.json").read_text(encoding="utf-8"))["dependencies"]
        self.assertNotIn("com.unity.modules.input", dependencies,
                         "Unity 2022.3.62f1 package manager cannot resolve com.unity.modules.input@1.0.0")

    def test_repository_ready_for_licensed_unity_runner(self):
        self.assertEqual([], inspect_project(ROOT))

    def test_rejects_unpinned_unity_version(self):
        with tempfile.TemporaryDirectory() as folder:
            target = Path(folder)
            self._copy_contract_files(target)
            version = target / 'ProjectSettings/ProjectVersion.txt'
            version.write_text('m_EditorVersion: 2099.0.1f1\n', encoding='utf-8')
            self.assertTrue(any('unity version' in error.lower() for error in inspect_project(target)))

    def test_rejects_unreachable_pr_test_workflow(self):
        with tempfile.TemporaryDirectory() as folder:
            target = Path(folder)
            self._copy_contract_files(target)
            wf = target / '.github/workflows/unity-playmode-smoke.yml'
            wf.write_text(wf.read_text(encoding='utf-8').replace('  pull_request:', '  never_pr:'), encoding='utf-8')
            self.assertTrue(any('pull_request' in error for error in inspect_project(target)))

    def test_rejects_release_pipeline_missing_guard(self):
        with tempfile.TemporaryDirectory() as folder:
            target = Path(folder)
            self._copy_contract_files(target)
            path = target / '.github/workflows/unity-android-build.yml'
            content = path.read_text(encoding='utf-8')
            path.write_text(content.replace('Fail closed if Unity credentials are missing', 'No validation'), encoding='utf-8')
            self.assertTrue(any('credential guard' in error for error in inspect_project(target)))

    @staticmethod
    def _copy_contract_files(target):
        from scripts.ci.unity_contract import REQUIRED_FILES
        for relative in REQUIRED_FILES:
            dst = target / relative
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(ROOT / relative, dst)


class ApkReceiptUnitTests(unittest.TestCase):
    def test_rejects_non_apk_file(self):
        with tempfile.TemporaryDirectory() as folder:
            fake = Path(folder) / 'ARE-Ashen-Crypt.apk'
            fake.write_bytes(b'not an apk')
            with self.assertRaises(ValueError):
                inspect_apk(fake)

    def test_rejects_zip_missing_arm64_unity_runtime(self):
        with tempfile.TemporaryDirectory() as folder:
            invalid = Path(folder) / 'ARE-Ashen-Crypt.apk'
            with zipfile.ZipFile(invalid, 'w', zipfile.ZIP_STORED) as apk:
                apk.writestr('AndroidManifest.xml', b'fixture')
                apk.writestr('classes.dex', b'fixture')
                apk.writestr('padding.bin', b'0' * 1024 * 1024)
            with self.assertRaisesRegex(ValueError, 'arm64'):
                inspect_apk(invalid)

    def test_rejects_unbound_source_revision(self):
        with self.assertRaises(ValueError):
            create_apk_receipt(Path('missing.apk'), Path('receipt.json'), 'HEAD', '2022.3.62f1', '123')

    def test_writes_digest_for_explicit_test_fixture_only(self):
        # Synthetic zip fixture exercises validator, NEVER evidence of a real Unity build.
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            fixture = root / 'ARE-Ashen-Crypt.apk'
            with zipfile.ZipFile(fixture, 'w', zipfile.ZIP_STORED) as fake:
                fake.writestr('AndroidManifest.xml', b'test-fixture-not-runtime')
                fake.writestr('classes.dex', b'test-fixture-not-runtime')
                fake.writestr('lib/arm64-v8a/libil2cpp.so', b'test-fixture-not-runtime')
                fake.writestr('lib/arm64-v8a/libunity.so', b'test-fixture-not-runtime')
                fake.writestr('padding.bin', b'0' * 1024 * 1024)
            report = root / 'receipt.json'
            info = create_apk_receipt(fixture, report, 'a' * 40, '6000.6.8f1', '123456')
            self.assertEqual(64, len(info['apk_sha256']))
            self.assertEqual('a' * 40, info['source_commit_sha'])
            self.assertEqual('not-verified', info['signature_status'])
            self.assertTrue(report.is_file())


if __name__ == '__main__':
    unittest.main()