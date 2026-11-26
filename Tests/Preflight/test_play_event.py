"""Readback CLI contracts. All bundle and receipt bytes in this module are test fixtures."""
import hashlib
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[2] / "scripts/events/play.py"
PACKAGE = "com.ouroboroscollective.areashencrypt"
SHA = "a" * 40


class PlayEventTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.aab = self.root / "bundle.aab"
        self.aab.write_bytes(b"fixture")
        self.digest = hashlib.sha256(self.aab.read_bytes()).hexdigest()
        self.build_file = self.root / "build.json"
        self.track_file = self.root / "track.json"
        self.build = {"source": {"commit": SHA}, "build": {"status": "built",
                      "bundle": {"package": PACKAGE, "version_code": 7, "aab_sha256": self.digest}}}
        self.track = {"package": PACKAGE, "track": "internal", "status": "completed",
                      "versionCode": "7", "sha256": self.digest}

    def run_cli(self, sha=SHA, observation=None):
        self.build_file.write_text(json.dumps(self.build))
        self.track_file.write_text(json.dumps(self.track))
        return subprocess.run([sys.executable, str(SCRIPT), "--aab", str(self.aab),
                               "--source-sha", sha, "--build-receipt", str(self.build_file),
                               *(observation or ["--track-receipt", str(self.track_file)])],
                              capture_output=True, text=True)

    def test_reads_publisher_receipt_without_credentials(self):
        result = self.run_cli()
        self.assertEqual(result.returncode, 0, result.stderr)
        report = json.loads(result.stdout)
        self.assertEqual(report["observation"], "publisher-receipt")
        self.assertEqual(report["buildSourceSha"], SHA)
        self.assertEqual(report["releases"], [{"status": "completed", "versionCodes": ["7"]}])

    def test_rejects_wrong_revision_before_opening_google_session(self):
        result = self.run_cli("b" * 40, ["--edit-id", "existing-edit"])
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("source revision", result.stderr)
        self.assertEqual(result.stdout, "")

    def test_rejects_changed_bundle_before_opening_google_session(self):
        self.aab.write_bytes(b"different")
        result = self.run_cli(observation=["--edit-id", "existing-edit"])
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("AAB digest", result.stderr)
        self.assertEqual(result.stdout, "")

    def test_rejects_wrong_track_observation(self):
        for key, wrong in (("package", "other.app"), ("track", "alpha"),
                           ("sha256", "f" * 64), ("versionCode", "8"), ("status", "")):
            with self.subTest(key=key):
                original = self.track[key]
                self.track[key] = wrong
                result = self.run_cli()
                self.assertNotEqual(result.returncode, 0)
                self.assertIn("track receipt", result.stderr)
                self.assertEqual(result.stdout, "")
                self.track[key] = original

    def run_with_recorded_transport(self, options):
        """A transport spy for method/path assertions only. No Play credentials or network."""
        from contextlib import redirect_stderr, redirect_stdout
        from io import StringIO
        import runpy
        from unittest.mock import MagicMock, patch

        self.build_file.write_text(json.dumps(self.build))
        auth = MagicMock()
        http = MagicMock()
        calls = []

        def respond(method, url, **kwargs):
            calls.append((method, url.rsplit("/", 1)[-1]))
            response = MagicMock()
            if url.endswith("/bundles"):
                data = {"bundles": [{"sha256": self.digest, "versionCode": 7}]}
            elif url.endswith("/tracks/internal"):
                data = {"track": "internal", "releases": [{"status": "draft", "versionCodes": ["7"]}]}
            else:
                data = {"id": "fixture-edit"}
            response.json.return_value = data
            return response

        transport = http.AuthorizedSession.return_value.__enter__.return_value
        transport.request.side_effect = respond
        transport.get.side_effect = lambda url, **kw: respond("GET", url, **kw)
        modules = {"google.oauth2": auth, "google.auth.transport.requests": http}
        stdout = StringIO()
        with patch.dict(sys.modules, modules), patch.dict("os.environ", GOOGLE_APPLICATION_CREDENTIALS="fixture"), \
             patch.object(sys, "argv", [str(SCRIPT), "--aab", str(self.aab), *options]), \
             redirect_stdout(stdout), redirect_stderr(StringIO()):
            try:
                runpy.run_path(str(SCRIPT), run_name="__main__")
            except SystemExit as exc:
                return exc.code, calls, stdout.getvalue()
        return 0, calls, stdout.getvalue()

    def test_legacy_call_cannot_attribute_an_arbitrary_sha_or_replace_an_edit(self):
        code, calls, output = self.run_with_recorded_transport(["--source-sha", "b" * 40])
        self.assertEqual(calls, [])
        self.assertNotEqual(code, 0)
        self.assertEqual(output, "")

    def test_edit_snapshot_only_uses_two_reads(self):
        code, calls, output = self.run_with_recorded_transport([
            "--source-sha", SHA, "--build-receipt", str(self.build_file), "--edit-id", "fixture-edit"])
        self.assertEqual(code, 0)
        self.assertEqual(calls, [("GET", "bundles"), ("GET", "internal")])
        self.assertEqual(json.loads(output)["observation"], "edit-snapshot")
