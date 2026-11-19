"""Sovott-only one-shot read bridge: the existing Director remains sole Unity owner.

This executable does not deploy a listener or grant Unity effects. The SSH caller
must already have broker authorization; root is used only for bounded Docker and
runuser calls. Every source, tool and output boundary fails closed.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import sys

REPOSITORY = "https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC"
SHA = re.compile(r"^[0-9a-f]{40}$")
SHA256 = re.compile(r"^[0-9a-f]{64}$")
DEPARTMENT = re.compile(r"^[a-z][a-z0-9_]{2,48}$")
ALLOWED_ACTION = "project_status"
CONFIG = Path("/var/lib/unityarpg/.local/share/unity-game-director/project.json")
DIRECTOR = Path("/var/lib/unityarpg/.local/share/unity-game-director/scripts/director.py")
MAX_ISSUE_BYTES = 65536
MAX_OUTPUT_BYTES = 262144
ALLOWED_IMAGE = re.compile(r"^sha256:[a-f0-9]{64}$")


def normalize_remote(value):
    if not isinstance(value, str):
        raise ValueError("INVALID_REMOTE")
    remote = value.strip()
    if remote.startswith("git@github.com:"):
        remote = "https://github.com/" + remote[len("git@github.com:"):]
    remote = remote.rstrip("/").removesuffix(".git").lower()
    if remote != REPOSITORY.lower():
        raise ValueError("UNTRUSTED_REPOSITORY")
    return remote


def validate_issues(obj):
    if not isinstance(obj, dict) or set(obj) != {"issues"} or not isinstance(obj["issues"], list):
        raise ValueError("INVALID_ISSUE_ROOT")
    if len(obj["issues"]) > 100:
        raise ValueError("TOO_MANY_ISSUES")
    seen = set()
    for row in obj["issues"]:
        if not isinstance(row, dict) or set(row) != {"number", "state", "title", "url"}:
            raise ValueError("INVALID_ISSUE")
        n = row["number"]
        if type(n) is not int or n < 1 or n > 10000000 or n in seen:
            raise ValueError("INVALID_ISSUE_NUMBER")
        seen.add(n)
        if row["state"] not in ("open", "closed") or not isinstance(row["title"], str) or not 1 <= len(row["title"]) <= 300:
            raise ValueError("INVALID_ISSUE_STATE")
        if row["url"] != REPOSITORY + "/issues/" + str(n):
            raise ValueError("ISSUE_ORIGIN_MISMATCH")
    return obj["issues"]


def assert_regular_issue_file(path):
    p = Path(path)
    st = p.lstat()
    if not stat.S_ISREG(st.st_mode) or stat.S_ISLNK(st.st_mode) or st.st_size > MAX_ISSUE_BYTES or st.st_size < 2:
        raise ValueError("INVALID_ISSUE_FILE")
    if st.st_mode & 0o022 or not st.st_mode & stat.S_IROTH:
        raise ValueError("ISSUE_FILE_PERMISSIONS")
    # Open without symlink following; verify same inode as lstat.
    fd = os.open(str(p), os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
    try:
        fdstat = os.fstat(fd)
        if (fdstat.st_dev, fdstat.st_ino) != (st.st_dev, st.st_ino):
            raise ValueError("ISSUE_FILE_CHANGED")
        raw = os.read(fd, MAX_ISSUE_BYTES + 1)
    finally:
        os.close(fd)
    if len(raw) > MAX_ISSUE_BYTES:
        raise ValueError("ISSUE_FILE_TOO_LARGE")
    data = json.loads(raw.decode("utf-8", "strict"))
    validate_issues(data)
    return raw


def verify_workspace_readback(data, expected_sha, expected_root):
    if not SHA.fullmatch(expected_sha) or not isinstance(data, dict) or data.get("ok") is not True or data.get("action") != ALLOWED_ACTION:
        raise ValueError("DIRECTOR_NOT_VERIFIED")
    row = data.get("data")
    if not isinstance(row, dict) or row.get("source_commit_sha") != expected_sha:
        raise ValueError("STALE_WORKTREE")
    if row.get("unity_version") != "6000.6.8f1" or row.get("project_root") != str(expected_root):
        raise ValueError("DIRECTOR_IDENTITY_DRIFT")
    if row.get("tracked_changes") != [] or row.get("untracked_game_inputs") != []:
        raise ValueError("DIRTY_UNITY_PROJECT")
    return expected_sha


def parse_probe_output(stdout, expected_sha, department, issue_digest):
    if not SHA.fullmatch(expected_sha) or not DEPARTMENT.fullmatch(department) or not SHA256.fullmatch(issue_digest):
        raise ValueError("INVALID_EXPECTATION")
    if not isinstance(stdout, str) or len(stdout.encode("utf-8")) > MAX_OUTPUT_BYTES:
        raise ValueError("INVALID_PROBE_OUTPUT")
    lines = [s for s in stdout.splitlines() if s.startswith("{")]
    if len(lines) != 1:
        raise ValueError("AMBIGUOUS_PROBE")
    data = json.loads(lines[0])
    required = {"schema": "ashen.director.mcp-probe.v1", "status": "ADVISORY_ONLY",
                "execution": "NONE", "sourceRevisionVerified": False,
                "runtimeVerified": False, "sourceRevisionClaim": expected_sha,
                "department": department, "issueDigest": issue_digest}
    if not isinstance(data, dict) or any(data.get(k) != v for k, v in required.items()):
        raise ValueError("PROBE_CONTRACT_MISMATCH")
    if not SHA256.fullmatch(data.get("snapshotId", "")) or data.get("advisorStatus") not in ("PROPOSE", "ABSTAIN"):
        raise ValueError("INVALID_PROBE_SNAPSHOT")
    if data.get("advisorStatus") == "ABSTAIN" and data.get("proposal") is not None:
        raise ValueError("UNSUPPORTED_PROPOSAL")
    return data


def checked(command, timeout=30, max_bytes=MAX_OUTPUT_BYTES):
    p = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                       timeout=timeout, check=False)
    if p.returncode or len(p.stdout) > max_bytes or len(p.stderr) > max_bytes:
        raise RuntimeError("COMMAND_FAILED:" + command[0] + ":" + str(p.returncode))
    return p.stdout.decode("utf-8", "strict").strip()


def inspect_git_checkout(path, expected_sha):
    if not SHA.fullmatch(expected_sha):
        raise ValueError("INVALID_SOURCE_SHA")
    path = Path(path).resolve(strict=True)
    actual = checked(["git", "-C", str(path), "rev-parse", "HEAD"])
    if actual != expected_sha or checked(["git", "-C", str(path), "status", "--porcelain"]) != "":
        raise ValueError("BRIDGE_SOURCE_DRIFT")
    normalize_remote(checked(["git", "-C", str(path), "remote", "get-url", "origin"]))
    return path


def main(argv=None):
    p = argparse.ArgumentParser(description="Sovott-delegated read-only Neural-to-Director preflight")
    p.add_argument("--source", required=True)
    p.add_argument("--source-sha", required=True)
    p.add_argument("--unity-sha", required=True)
    p.add_argument("--issues-file", required=True)
    p.add_argument("--image-id", required=True)
    p.add_argument("--department", required=True)
    a = p.parse_args(argv)
    if os.geteuid() != 0:
        raise ValueError("BROKER_OWNER_REQUIRED")
    if not DEPARTMENT.fullmatch(a.department):
        raise ValueError("INVALID_DEPARTMENT")
    if not ALLOWED_IMAGE.fullmatch(a.image_id) or not SHA.fullmatch(a.unity_sha):
        raise ValueError("INVALID_IMAGE_OR_UNITY_SHA")
    path = inspect_git_checkout(a.source, a.source_sha)
    issue_bytes = assert_regular_issue_file(a.issues_file)
    digest = hashlib.sha256(issue_bytes).hexdigest()
    issue_path = Path(a.issues_file).resolve(strict=True)
    # Never derive the Unity worktree path or executable from the model.
    config = json.loads(CONFIG.read_text(encoding="utf-8"))
    root = config.get("project_root")
    if not isinstance(root, str) or not root.startswith("/var/lib/unityarpg/workspaces/"):
        raise ValueError("INVALID_DIRECTOR_ROOT")
    status = checked(["runuser", "-u", "unityarpg", "--", sys.executable, str(DIRECTOR),
                      "--config", str(CONFIG), ALLOWED_ACTION], timeout=25)
    verify_workspace_readback(json.loads(status), a.unity_sha, root)
    if checked(["runuser", "-u", "unityarpg", "--", "git", "-C", root, "rev-parse", "HEAD"]) != a.unity_sha:
        raise ValueError("GIT_IDENTITY_CHANGED")
    normalize_remote(checked(["runuser", "-u", "unityarpg", "--", "git", "-C", root,
                               "remote", "get-url", "origin"]))
    actual_image = checked(["docker", "image", "inspect", a.image_id, "--format", "{{.Id}}"])
    if actual_image != a.image_id:
        raise ValueError("DOCKER_IMAGE_DRIFT")
    source_hashes = {
        "ASHEN_ARCHITECTURE_SHA256": hashlib.sha256((path / "docs/ARCHITECTURE.md").read_bytes()).hexdigest(),
        "ASHEN_CATALOG_SHA256": hashlib.sha256((path / "Tools/Director/neural_catalog.json").read_bytes()).hexdigest(),
        "ASHEN_SOURCES_SHA256": hashlib.sha256((path / "Tools/Director/neural_hub/upstream_sources.json").read_bytes()).hexdigest(),
    }
    cmd = ["docker", "run", "--rm", "--network", "none", "--read-only",
           "--cap-drop", "ALL", "--security-opt", "no-new-privileges",
           "--memory", "256m", "--cpus", "1", "--pids-limit", "64",
           "-e", "NEURAL_HUB_LOCAL_ONLY=1",
           "-e", "ASHEN_REPOSITORY_SHA=" + a.source_sha,
           "-e", "ASHEN_ISSUES_FILE=/tmp/ashen-public-issues.json"]
    for key, value in sorted(source_hashes.items()):
        cmd.extend(["-e", key + "=" + value])
    cmd.extend(["-v", str(issue_path) + ":/tmp/ashen-public-issues.json:ro",
                a.image_id, "node", "mcp_probe.mjs", a.department])
    output = checked(cmd, timeout=45)
    result = parse_probe_output(output, a.source_sha, a.department, digest)
    # Independent second read after the tool call; no source or Unity mutation.
    reread = checked(["runuser", "-u", "unityarpg", "--", sys.executable, str(DIRECTOR),
                      "--config", str(CONFIG), ALLOWED_ACTION], timeout=25)
    verify_workspace_readback(json.loads(reread), a.unity_sha, root)
    if hashlib.sha256(assert_regular_issue_file(a.issues_file)).hexdigest() != digest:
        raise ValueError("ISSUES_CHANGED_DURING_RUN")
    result["unityWorktreeShaVerified"] = a.unity_sha
    result["unityRuntimeVerified"] = False
    result["owner"] = "unityarpg"
    print(json.dumps(result, ensure_ascii=False, sort_keys=True))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, OSError, RuntimeError, subprocess.TimeoutExpired, json.JSONDecodeError) as exc:
        print(json.dumps({"ok": False, "status": "BLOCKED",
                          "reason": str(exc)[:160],
                          "execution": "NONE", "unityRuntimeVerified": False}))
        sys.exit(2)
