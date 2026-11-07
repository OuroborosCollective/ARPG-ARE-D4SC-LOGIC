"""Fail-closed *static* checks for a Unity project and Android APK artifacts.

Neither this module nor its tests execute the Unity Editor or prove that an
APK launches. They validate wiring and produce a SHA-bound artifact receipt.
"""

import argparse
import hashlib
import json
from pathlib import Path
import re
import zipfile

VERSION = '2022.3.62f1'
REQUIRED_FILES = (
    'ProjectSettings/ProjectVersion.txt',
    'Packages/manifest.json',
    'Assets/Editor/ArpgBuildTools.cs',
    'Assets/Scripts/Runtime/ArenaRuntime.cs',
    'Assets/Scripts/Core/ArpgSimulation.cs',
    'Assets/Tests/PlayMode/ArenaBootSmokeTests.cs',
    '.github/workflows/unity-playmode-smoke.yml',
    '.github/workflows/unity-android-build.yml',
)


def inspect_project(root):
    """Report static wiring errors; empty result does not imply successful build."""
    root = Path(root)
    errors = []
    files = {}
    for relative in REQUIRED_FILES:
        p = root / relative
        if not p.is_file():
            errors.append('Missing source file: ' + relative)
        else:
            files[relative] = p.read_text(encoding='utf-8')
    ver = files.get('ProjectSettings/ProjectVersion.txt', '')
    if 'm_EditorVersion: ' + VERSION not in ver:
        errors.append('Pinned Unity version does not match ' + VERSION)
    try:
        manifest = json.loads(files.get('Packages/manifest.json', '{}'))
        if 'com.unity.test-framework' not in manifest.get('dependencies', {}):
            errors.append('Unity Test Framework package is missing')
    except ValueError:
        errors.append('Invalid Unity packages manifest')
    ed = files.get('Assets/Editor/ArpgBuildTools.cs', '')
    for guard in ('BuildAndroidApk', 'BuildReportGuard', 'BuildPipeline.BuildPlayer',
                  'BuildResult.Succeeded', 'BuildTarget.Android', 'AndroidArchitecture.ARM64',
                  'ScriptingImplementation.IL2CPP', 'Builds/ARE-Ashen-Crypt.apk'):
        if guard not in ed:
            errors.append('Missing Android build method requirement: ' + guard)
    play = files.get('.github/workflows/unity-playmode-smoke.yml', '')
    if not re.search(r'^\s*pull_request\s*:', play, re.MULTILINE):
        errors.append('Unity playmode smoke must have pull_request trigger')
    for wfname in ('.github/workflows/unity-playmode-smoke.yml',
                  '.github/workflows/unity-android-build.yml'):
        wf = files.get(wfname, '')
        if 'Fail closed if Unity credentials are missing' not in wf:
            errors.append('Missing credential guard in ' + wfname)
        if 'UNITY_LICENSE' not in wf or 'UNITY_EMAIL' not in wf or 'UNITY_PASSWORD' not in wf:
            errors.append('Missing explicit license env in ' + wfname)
    android = files.get('.github/workflows/unity-android-build.yml', '')
    if 'apk-receipt' not in android or 'apk-receipt.json' not in android:
        errors.append('Missing SHA-bound APK artifact receipt verification')
    if 'if-no-files-found: error' not in android:
        errors.append('Missing fail-closed artifact upload check')
    if 'github.sha' not in android:
        errors.append('Missing source SHA binding')
    return errors


def inspect_apk(path):
    """Inspect an APK ZIP's minimum physical features; NOT functional verification."""
    path = Path(path)
    if not path.is_file() or path.stat().st_size < 1024:
        raise ValueError('APK missing or suspiciously small')
    if not zipfile.is_zipfile(path):
        raise ValueError('APK does not have a valid ZIP container')
    with zipfile.ZipFile(path) as zf:
        bad = zf.testzip()
        if bad is not None:
            raise ValueError('APK corrupt ZIP entry: ' + bad)
        names = set(zf.namelist())
        if 'AndroidManifest.xml' not in names:
            raise ValueError('APK missing Android manifest')
        if not any(re.fullmatch(r'classes\d*\.dex', n) for n in names):
            raise ValueError('APK missing DEX bytecode')
        if 'lib/arm64-v8a/libil2cpp.so' not in names or 'lib/arm64-v8a/libunity.so' not in names:
            raise ValueError('APK missing arm64 Unity IL2CPP runtime')
    return {'size_bytes': path.stat().st_size, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}


def create_apk_receipt(apk_path, receipt_path, source_sha, unity_version, run_id):
    """Bind an inspected artifact to a known full Git commit SHA and a CI run ID."""
    if re.fullmatch('[0-9a-f]{40}', source_sha) is None:
        raise ValueError('source revision must be an exact 40-digit hexadecimal commit SHA')
    if unity_version != VERSION:
        raise ValueError('Unity Editor version mismatch')
    if not str(run_id).isdigit():
        raise ValueError('GitHub Actions run ID must be decimal')
    result = inspect_apk(apk_path)
    receipt = {
        'schema': 'are.arpg.android-build-receipt.v1',
        'artifact_name': Path(apk_path).name,
        'source_commit_sha': source_sha,
        'unity_editor_version': unity_version,
        'github_actions_run_id': str(run_id),
        'apk_sha256': result['sha256'],
        'apk_size_bytes': result['size_bytes'],
        'signature_status': 'not-verified',
        'android_install_status': 'not-verified',
        'android_runtime_status': 'not-verified',
    }
    target = Path(receipt_path)
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(receipt, sort_keys=True, indent=2) + '\n', encoding='utf-8')
    return receipt


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('command', choices=('preflight', 'apk-receipt'))
    parser.add_argument('--project', type=Path, default=Path('.'))
    parser.add_argument('--apk', type=Path)
    parser.add_argument('--receipt', type=Path)
    parser.add_argument('--source-sha')
    parser.add_argument('--unity-version', default=VERSION)
    parser.add_argument('--run-id')
    args = parser.parse_args()
    if args.command == 'preflight':
        problems = inspect_project(args.project)
        if problems:
            for p in problems:
                print('PREFLIGHT ERROR: ' + p)
            return 1
        print('Static Unity/Android configuration contract PASS; actual Unity runtime UNVERIFIED')
        return 0
    if not all((args.apk, args.receipt, args.source_sha, args.run_id)):
        parser.error('apk-receipt requires --apk --receipt --source-sha --run-id')
    print(json.dumps(create_apk_receipt(args.apk, args.receipt,
                                        args.source_sha, args.unity_version, args.run_id), indent=2))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
