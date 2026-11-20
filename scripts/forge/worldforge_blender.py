#!/usr/bin/env python3
"""Private, local World Forge batch export queue. Only a completed directory is published."""
import argparse
import contextlib
import fcntl
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import pwd
import re
import shutil
import stat
import struct
import subprocess
import sys
import tempfile
from urllib.parse import unquote, urlsplit


VERSION = "4.5.16"
SCHEMA = "worldforge.blender-export.v1"
ROOT = Path("/var/lib/unityarpg/.local/share/are-arpg/worldforge-blender")
EXPORTER = Path(__file__).resolve().with_name("blender_export.py")
RECIPES = EXPORTER.parent / "recipes"
FORMATS = {".blend", ".fbx", ".glb"}
MAX_SOURCE = 1024 ** 3
ATTEMPTS = (".attempt-1", ".attempt-2")


def prepare(root):
    for name in ("queue", "results", "failed", "damaged", "staging", "work"):
        (root / name).mkdir(mode=0o700, parents=True, exist_ok=True)


@contextlib.contextmanager
def lock(root, name):
    with (root / name).open("a") as handle:
        fcntl.flock(handle, fcntl.LOCK_EX)
        yield


def fingerprint(path, copy_to=None):
    digest = hashlib.sha256()
    size = 0
    with path.open("rb") as handle:
        while chunk := handle.read(1024 * 1024):
            size += len(chunk)
            if copy_to is not None:
                if size > MAX_SOURCE:
                    raise ValueError("Input exceeds the 1 GiB queue limit")
                copy_to.write(chunk)
            digest.update(chunk)
    return digest.hexdigest(), size


def job_key(extension, source_hash, version, recipe_hash):
    return hashlib.sha256(f"{SCHEMA}/{version}/{recipe_hash}/{extension}/{source_hash}".encode()).hexdigest()


def select_recipe(extension, source_hash, key, archive=RECIPES):
    # The installer retains root-owned recipe files and matching Blender bundles. A queued
    # input carries no executable code path: its content key identifies the trusted recipe.
    for path in sorted(archive.glob("*/*.py")):
        version = path.parent.name
        if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", version) or not re.fullmatch("[0-9a-f]{64}", path.stem):
            continue
        if job_key(extension, source_hash, version, path.stem) == key:
            if path.is_symlink() or fingerprint(path)[0] != path.stem:
                raise ValueError("Installed export recipe checksum mismatch")
            return version, path
    if job_key(extension, source_hash, VERSION, fingerprint(EXPORTER)[0]) == key:
        return VERSION, EXPORTER
    raise ValueError("No installed export recipe matches this input and job ID")


def valid_id(key):
    if not re.fullmatch("[0-9a-f]{64}", key):
        raise ValueError("Expected a 64-character lowercase hexadecimal job ID")
    return key


def submit(root, source):
    ext = source.suffix.lower()
    if ext not in FORMATS or not source.is_file():
        raise ValueError("Select a packed .blend or a self-contained .fbx or .glb model")
    with tempfile.TemporaryDirectory(dir=root / "staging") as staging:
        job = Path(staging) / "job"
        job.mkdir()
        with (job / ("source" + ext)).open("xb") as out:
            digest, size = fingerprint(source, out)
        if not size:
            raise ValueError("Empty source")
        key = job_key(ext, digest, VERSION, fingerprint(EXPORTER)[0])
        with lock(root, "publish.lock"):
            result = root / "results" / key
            if result.exists() and not result_intact(result):
                preserved = Path(tempfile.mkdtemp(prefix=key + "-", dir=root / "damaged"))
                result.rename(preserved / "previous")
            if not any((root / name / key).exists() for name in ("queue", "results", "failed")):
                job.rename(root / "queue" / key)
        return key


def status(root, key):
    valid_id(key)
    result = root / "results" / key
    if result.exists():
        return {"state": "ready" if result_intact(result) else "damaged", "directory": str(result)}
    error = root / "failed" / key / "error.txt"
    if error.is_file():
        return {"state": "failed", "error": error.read_text(encoding="utf-8")}
    if (root / "queue" / key).is_dir():
        return {"state": "queued"}
    raise ValueError("Unknown job ID")


def retry(root, key):
    valid_id(key)
    with lock(root, "publish.lock"):
        failed = root / "failed" / key
        if not (failed / "error.txt").is_file():
            raise ValueError("Only a failed job can be retried")
        for marker in ATTEMPTS:
            (failed / marker).unlink(missing_ok=True)
        failed.rename(root / "queue" / key)
        (root / "queue" / key / "error.txt").unlink()


def check_gltf(output):
    doc = json.loads((output / "model.gltf").read_text(encoding="utf-8"))
    if doc["asset"]["version"] != "2.0" or not doc.get("buffers"):
        raise ValueError("Missing glTF 2.0 geometry buffer")
    for item in [*doc["buffers"], *doc.get("images", [])]:
        uri = item.get("uri")
        if uri is None and "bufferView" in item:
            continue
        if not isinstance(uri, str):
            raise ValueError("Missing glTF resource URI")
        location = urlsplit(uri)
        path = PurePosixPath(unquote(location.path))
        if (location.scheme or location.netloc or location.query or location.fragment
                or path.is_absolute() or ".." in path.parts or "\\" in str(path)
                or not (output / path).is_file()):
            raise ValueError(f"Missing or non-local glTF resource: {uri}")
    for buffer in doc["buffers"]:
        size = buffer.get("byteLength")
        if type(size) is not int or size <= 0 or (output / unquote(buffer["uri"])).stat().st_size != size:
            raise ValueError("glTF buffer length does not match its sidecar")


def result_intact(output):
    try:
        if output.is_symlink() or not stat.S_ISREG((output / "receipt.json").lstat().st_mode):
            return False
        record = json.loads((output / "receipt.json").read_text(encoding="utf-8"))
        if record["schema"] != SCHEMA or record["jobId"] != output.name:
            return False
        files = set()
        for item in record["files"]:
            path = PurePosixPath(item["path"])
            file = output / path
            if (path.is_absolute() or ".." in path.parts or path in files
                    or file.resolve() != file or not stat.S_ISREG(file.lstat().st_mode)
                    or fingerprint(file) != (item["sha256"], item["size"])):
                return False
            files.add(path)
        expected = {p.relative_to(output) for p in output.rglob("*") if p.is_file() and p != output / "receipt.json"}
        if files != expected or not all(PurePosixPath(n) in files for n in ("model.fbx", "model.glb", "model.gltf")):
            return False
        check_gltf(output)
        return True
    except (OSError, ValueError, TypeError, KeyError, RuntimeError):
        return False


def receipt(output, key, source_hash, version, recipe):
    files = []
    for path in sorted(output.rglob("*")):
        mode = path.lstat().st_mode
        if stat.S_ISDIR(mode):
            continue
        if not stat.S_ISREG(mode):
            raise ValueError(f"Unexpected output type: {path.name}")
        digest, size = fingerprint(path)
        if not size:
            raise ValueError(f"Empty output: {path.name}")
        files.append({"path": path.relative_to(output).as_posix(), "size": size, "sha256": digest})
    with (output / "model.fbx").open("rb") as fbx:
        if not fbx.read(23).startswith(b"Kaydara FBX Binary  \x00\x1a\x00"):
            raise ValueError("Invalid binary FBX")
    glb = output / "model.glb"
    with glb.open("rb") as handle:
        if handle.read(12) != struct.pack("<4sII", b"glTF", 2, glb.stat().st_size):
            raise ValueError("Invalid GLB 2.0 header or length")
    check_gltf(output)
    with (output / "receipt.json").open("x", encoding="utf-8") as handle:
        json.dump({"schema": SCHEMA, "jobId": key, "blenderVersion": version,
                   "exporterSha256": fingerprint(recipe)[0], "sourceSha256": source_hash,
                   "files": files}, handle, indent=2)
        handle.write("\n")


def sandbox(source, output, version, recipe):
    args = ["/usr/bin/bwrap", "--unshare-all", "--die-with-parent", "--new-session", "--cap-drop", "ALL",
            "--clearenv", "--setenv", "HOME", "/tmp", "--setenv", "TMPDIR", "/tmp",
            "--setenv", "PATH", "/blender", "--setenv", "LANG", "C.UTF-8",
            "--setenv", "OPENBLAS_NUM_THREADS", "1", "--proc", "/proc", "--dev", "/dev",
            "--tmpfs", "/tmp", "--dir", "/etc", "--dir", "/input", "--dir", "/tool",
            "--ro-bind", "/usr/lib", "/usr/lib", "--symlink", "usr/lib", "/lib",
            "--ro-bind", "/usr/lib64", "/usr/lib64", "--symlink", "usr/lib64", "/lib64"]
    for common in ("/etc/ld.so.cache", "/etc/fonts", "/usr/share/fonts", "/usr/share/fontconfig",
                   "/usr/share/locale", "/usr/share/color"):
        if Path(common).exists():
            args.extend(["--ro-bind", common, common])
    bundle = f"/opt/worldforge-blender/blender-{version}-linux-x64"
    return [*args, "--ro-bind", bundle, "/blender", "--ro-bind", str(recipe), "/tool/export.py",
            "--ro-bind", str(source), "/input/" + source.name, "--bind", str(output), "/output",
            "--chdir", "/output", "/blender/blender", "--background", "--factory-startup", "--disable-autoexec",
            "--threads", "2", "--python-exit-code", "1", "--python", "/tool/export.py", "--",
            "/input/" + source.name, "/output"]


def process(root, job):
    key = valid_id(job.name)
    working = root / "work" / key
    # A killed process leaves the immutable input queued. Restart from a clean output.
    if working.exists():
        shutil.rmtree(working)
    if (root / "results" / key / "receipt.json").is_file() and result_intact(root / "results" / key):
        shutil.rmtree(job)
        return
    working.mkdir()
    output = working / "output"
    output.mkdir()
    log_path = working / "blender.log"
    try:
        sources = [p for p in job.iterdir() if p.name in {"source" + ext for ext in FORMATS}]
        if len(sources) != 1 or not stat.S_ISREG(sources[0].lstat().st_mode):
            raise ValueError("Expected exactly one regular model source")
        source = sources[0]
        source_hash = fingerprint(source)[0]
        version, recipe = select_recipe(source.suffix, source_hash, key)
        # A hard-killed worker cannot quarantine its input. Limit restarts across service invocations.
        for marker in ATTEMPTS:
            try:
                (job / marker).touch(exist_ok=False)
                break
            except FileExistsError:
                continue
        else:
            raise ValueError("Worker interrupted twice; inspect the source and retry this job explicitly")
        with log_path.open("wb") as log:
            subprocess.run(sandbox(source, output, version, recipe), stdin=subprocess.DEVNULL, stdout=log,
                           stderr=subprocess.STDOUT, check=True, timeout=900)
        receipt(output, key, source_hash, version, recipe)
        with lock(root, "publish.lock"):
            output.rename(root / "results" / key)
    except (OSError, ValueError, KeyError, subprocess.SubprocessError) as error:
        tail = ""
        if log_path.exists():
            with log_path.open("rb") as handle:
                handle.seek(max(0, log_path.stat().st_size - 3200))
                tail = handle.read().decode("utf-8", errors="replace")
        reason = f"Blender exit {error.returncode}" if isinstance(error, subprocess.CalledProcessError) else str(error)
        (job / "error.txt").write_text(reason + "\n" + tail, encoding="utf-8")
        with lock(root, "publish.lock"):
            job.rename(root / "failed" / key)
        print(f"failed {key}", flush=True)
    else:
        shutil.rmtree(job)
        print(f"ready {key}", flush=True)
    finally:
        shutil.rmtree(working)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("submit").add_argument("source", type=Path)
    for name in ("status", "retry"):
        commands.add_parser(name).add_argument("job_id")
    commands.add_parser("drain")
    args = parser.parse_args()
    os.umask(0o077)
    if os.geteuid() == 0:
        user = pwd.getpwnam("unityarpg")
        os.initgroups(user.pw_name, user.pw_gid)
        os.setgid(user.pw_gid)
        os.setuid(user.pw_uid)
    prepare(ROOT)
    if args.command == "submit":
        print(submit(ROOT, args.source))
    elif args.command == "status":
        print(json.dumps(status(ROOT, args.job_id), ensure_ascii=False))
    elif args.command == "retry":
        retry(ROOT, args.job_id)
    else:
        with lock(ROOT, "worker.lock"):
            for job in sorted((ROOT / "queue").iterdir()):
                if re.fullmatch("[0-9a-f]{64}", job.name) and job.is_dir() and not job.is_symlink():
                    process(ROOT, job)


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError) as exc:
        sys.exit(str(exc))
