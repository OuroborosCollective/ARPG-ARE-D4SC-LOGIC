"""Verify an exact signed AAB against Google's internal track in a fresh edit."""
import argparse
import hashlib
import json
import os
import re
from pathlib import Path
from google.oauth2 import service_account
from google.auth.transport.requests import AuthorizedSession

parser = argparse.ArgumentParser()
parser.add_argument("--aab", type=Path, required=True)
parser.add_argument("--source-sha", required=True, help="Revision from the bundle build receipt")
args = parser.parse_args()
if not re.fullmatch("[0-9a-f]{40}", args.source_sha):
    parser.error("Expected a full build SHA")
with args.aab.open("rb") as stream:
    digest = hashlib.file_digest(stream, "sha256").hexdigest()
scope = "https://www.googleapis.com/auth/androidpublisher"
credentials = service_account.Credentials.from_service_account_file(os.environ["GOOGLE_APPLICATION_CREDENTIALS"], scopes=[scope])
base = "https://androidpublisher.googleapis.com/androidpublisher/v3/applications/com.ouroboroscollective.areashencrypt/edits"
with AuthorizedSession(credentials) as session:
    def request(method, url, **kwargs):
        response = session.request(method, url, timeout=30, **kwargs)
        response.raise_for_status()
        return response
    edit = request("POST", base, json={}).json()["id"]
    url = f"{base}/{edit}"
    try:
        bundles = request("GET", url + "/bundles").json().get("bundles", [])
        codes = {str(b["versionCode"]) for b in bundles if b.get("sha256", "").lower() == digest}
        track = request("GET", url + "/tracks/internal").json()
        if track.get("track") != "internal":
            raise ValueError("Unexpected Google Play track")
        releases = [dict(status=r["status"], versionCodes=sorted(codes.intersection(r.get("versionCodes", []))))
                    for r in track.get("releases", []) if codes.intersection(r.get("versionCodes", []))]
    finally:
        request("DELETE", url)
report = dict(name="ashen.play.track.readback", buildSourceSha=args.source_sha, bundleSha256=digest,
              track="internal", releases=releases, deviceTest="unverified")
print(json.dumps(report))
if not releases:
    raise SystemExit("Exact bundle not found on internal track")
