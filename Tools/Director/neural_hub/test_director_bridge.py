"""Read-only host bridge contract tests. No fake Unity runtime is accepted."""
import json
import os
import tempfile
import unittest
from pathlib import Path

from director_bridge import (
    validate_issues, normalize_remote, verify_workspace_readback, parse_probe_output,
    assert_regular_issue_file,
)

SHA = "a" * 40
REPO = "https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC"


class BridgeContractTests(unittest.TestCase):
    def test_only_exact_canonical_remote_is_allowed(self):
        self.assertEqual(REPO.lower(), normalize_remote(REPO + ".git"))
        self.assertEqual(REPO.lower(), normalize_remote(
            "git@github.com:OuroborosCollective/ARPG-ARE-D4SC-LOGIC.git"))
        for invalid in ["https://example.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC",
                        "https://github.com/OuroborosCollective/Other",
                        "https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC.evil.com",
                        "", "file:///etc/passwd"]:
            with self.subTest(invalid=invalid), self.assertRaises(ValueError):
                normalize_remote(invalid)

    def test_issue_record_is_bounded_and_exactly_one_source_namespace(self):
        issues = [{"number": 62, "state": "open", "title": "Asset-Eingang",
                   "url": REPO + "/issues/62"}]
        self.assertEqual(issues, validate_issues({"issues": issues}))
        self.assertEqual([], validate_issues({"issues": []}))
        for invalid in [
            {"issues": [dict(issues[0], state="draft")]},
            {"issues": [dict(issues[0], url=REPO + "/issues/63")]},
            {"issues": issues + issues},
            {"issues": [dict(issues[0], command="rm -rf /")]},
            {"issues": issues, "authorization": "granted"},
            {"issues": [dict(issues[0], title="x" * 301)]},
            {"issues": [{"number": True, "state": "open", "title": "foo",
                         "url": REPO + "/issues/1"}]},
        ]:
            with self.subTest(invalid=invalid), self.assertRaises(ValueError):
                validate_issues(invalid)

    def test_revision_readback_fails_on_stale_dirty_or_missing_inputs(self):
        clean = {"ok": True, "action": "project_status",
                 "data": {"source_commit_sha": SHA, "unity_version": "6000.6.8f1",
                          "tracked_changes": [], "untracked_game_inputs": [],
                          "project_root": "/var/lib/unityarpg/workspaces/ashen"}}
        self.assertEqual(SHA, verify_workspace_readback(
            clean, SHA, "/var/lib/unityarpg/workspaces/ashen"))
        for data in [
            {"source_commit_sha": "b" * 40},
            {"tracked_changes": ["Assets/A.cs"]},
            {"untracked_game_inputs": ["Assets/PrivateArt/Custom.prefab"]},
            {"unity_version": "2022.3.62f1"},
            {"project_root": "/var/lib/unityarpg/workspaces/other"},
        ]:
            row = json.loads(json.dumps(clean))
            row["data"].update(data)
            with self.subTest(data=data), self.assertRaises(ValueError):
                verify_workspace_readback(
                    row, SHA, "/var/lib/unityarpg/workspaces/ashen")
        with self.assertRaises(ValueError):
            verify_workspace_readback(
                {"ok": False, "data": clean["data"]}, SHA,
                "/var/lib/unityarpg/workspaces/ashen")

    def test_untrusted_probe_output_does_not_grant_effects_or_source_truth(self):
        good = {"schema": "ashen.director.mcp-probe.v1",
                "status": "ADVISORY_ONLY", "execution": "NONE",
                "sourceRevisionVerified": False, "runtimeVerified": False,
                "snapshotId": "b" * 64, "sourceRevisionClaim": SHA,
                "department": "world_design", "advisorStatus": "ABSTAIN",
                "issueDigest": "c" * 64}
        stdout = "tools/list 200\n" + json.dumps(good) + "\n"
        self.assertEqual(good, parse_probe_output(
            stdout, SHA, "world_design", "c" * 64))
        for bad in [
            dict(good, runtimeVerified=True),
            dict(good, sourceRevisionVerified=True),
            dict(good, execution="WRITE"),
            dict(good, status="DEPLOYED"),
            dict(good, department="shaders"),
            dict(good, sourceRevisionClaim="d" * 40),
            dict(good, issueDigest="d" * 64),
        ]:
            with self.subTest(bad=bad), self.assertRaises(ValueError):
                parse_probe_output(json.dumps(bad), SHA, "world_design", "c" * 64)

    def test_issue_file_requires_regular_non_symlink_bounded_no_secrets(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "issues.json"
            p.write_text('{"issues":[]}', encoding="utf-8")
            os.chmod(p, 0o644)
            self.assertEqual(b'{"issues":[]}', assert_regular_issue_file(p))
            os.chmod(p, 0o600)
            with self.assertRaises(ValueError):
                assert_regular_issue_file(p)
            os.chmod(p, 0o644)
            link = Path(d) / "link.json"
            link.symlink_to(p)
            with self.assertRaises(ValueError):
                assert_regular_issue_file(link)


if __name__ == "__main__":
    unittest.main()
