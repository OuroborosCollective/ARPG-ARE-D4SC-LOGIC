"""Match a signed AAB and its trusted build receipt to an internal Play observation."""
import argparse
import hashlib
import json
import os
import re
from pathlib import Path

PACKAGE = "com.ouroboroscollective.areashencrypt"
SCOPE = "https://www.googleapis.com/auth/androidpublisher"
EDITS = f"https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{PACKAGE}/edits"
RELEASE_STATES = {"draft", "inProgress", "halted", "completed"}


def verified_build(path, receipt_path, source_sha):
    receipt = json.loads(receipt_path.read_bytes())
    if not re.fullmatch(r"[0-9a-f]{40}", source_sha) or receipt["source"]["commit"] != source_sha:
        raise ValueError("Build receipt source revision does not match --source-sha")
    build = receipt["build"]
    bundle = build["bundle"]
    if build["status"] != "built" or bundle["package"] != PACKAGE:
        raise ValueError("Expected a successful Ashen Crypt bundle build")
    version = str(bundle["version_code"])
    if not re.fullmatch(r"[1-9][0-9]*", version):
        raise ValueError("Invalid version code in the build receipt")
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    if digest.hexdigest() != bundle["aab_sha256"]:
        raise ValueError("AAB digest does not match the build receipt")
    return digest.hexdigest(), version


def publisher_releases(path, digest, version):
    receipt = json.loads(path.read_bytes())
    if (receipt["package"] != PACKAGE or receipt["track"] != "internal"
            or receipt["sha256"] != digest or str(receipt["versionCode"]) != version
            or receipt["status"] not in RELEASE_STATES):
        raise ValueError("Publisher track receipt does not match the exact internal release")
    return [{"status": receipt["status"], "versionCodes": [version]}]


def edit_releases(edit_id, digest, version):
    if not re.fullmatch(r"[A-Za-z0-9_-]+", edit_id):
        raise ValueError("Invalid Google Play edit ID")

    from google.auth.transport.requests import AuthorizedSession
    from google.oauth2 import service_account

    credentials = service_account.Credentials.from_service_account_file(
        os.environ["GOOGLE_APPLICATION_CREDENTIALS"], scopes=[SCOPE])
    url = f"{EDITS}/{edit_id}"
    with AuthorizedSession(credentials) as session:
        bundles = session.get(f"{url}/bundles", timeout=30)
        bundles.raise_for_status()
        if not any(b.get("sha256", "").lower() == digest and str(b["versionCode"]) == version
                   for b in bundles.json().get("bundles", [])):
            raise ValueError("Exact bundle not found in Google Play edit")
        response = session.get(f"{url}/tracks/internal", timeout=30)
        response.raise_for_status()
        track = response.json()

    if track.get("track") != "internal":
        raise ValueError("Unexpected Google Play track")
    releases = [{"status": r["status"], "versionCodes": [version]} for r in track.get("releases", [])
                if version in r.get("versionCodes", []) and r["status"] in RELEASE_STATES]
    if not releases:
        raise ValueError("Exact bundle not found on internal track in the supplied edit")
    return releases


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--aab", type=Path, required=True)
    parser.add_argument("--source-sha", required=True, help="Expected full build revision")
    parser.add_argument("--build-receipt", type=Path, required=True,
                        help="Trusted build report with source.commit and build.bundle.aab_sha256")
    observed = parser.add_mutually_exclusive_group(required=True)
    observed.add_argument("--track-receipt", type=Path,
                          help="Read the authenticated publisher's archived play-receipt.json")
    observed.add_argument("--edit-id", help="Read an existing caller-owned edit, including staged changes")
    args = parser.parse_args()
    try:
        digest, version = verified_build(args.aab, args.build_receipt, args.source_sha)
        if args.track_receipt:
            observation = "publisher-receipt"
            releases = publisher_releases(args.track_receipt, digest, version)
        else:
            observation = "edit-snapshot"
            releases = edit_releases(args.edit_id, digest, version)
    except (ValueError, KeyError, OSError, TypeError) as error:
        parser.exit(1, f"Play readback failed: {error}\n")
    print(json.dumps(dict(name="ashen.play.track.readback", buildSourceSha=args.source_sha,
                          bundleSha256=digest, track="internal", releases=releases,
                          observation=observation, deviceTest="unverified")))


if __name__ == "__main__":
    main()
