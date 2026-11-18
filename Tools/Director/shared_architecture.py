"""Version-bound read-only architecture council for the existing Unity Game Director.

No model calls, MCP execution, Unity mutations, Git updates or authority claims.
The caller must verify the claimed Git revision against the canonical repository.
"""
from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path

MARKER = '<!-- ashen.shared-architecture.v1 -->'
CATALOG_SCHEMA = 'ashen.director.neural-catalog.v1'
COUNCIL_SCHEMA = 'ashen.director.council.v1'
HEX40 = re.compile(r'^[0-9a-f]{40}$')
HEX64 = re.compile(r'^[0-9a-f]{64}$')
DEPARTMENT = re.compile(r'^[a-z][a-z0-9_]{2,48}$')
SKILL = re.compile(r'^[a-z][a-z0-9_.-]{1,79}$')
ISSUE = re.compile(
    r'^https://github\.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/[1-9][0-9]*$')
TRACKS = ('gameplay', 'story', 'world', 'quality', 'infrastructure')


class ArchitectureError(ValueError):
    pass


def _digest(data):
    return hashlib.sha256(data).hexdigest()


def _read(path, limit):
    target = Path(path)
    if not target.is_file() or target.stat().st_size > limit:
        raise ArchitectureError('INVALID_SNAPSHOT_SOURCE')
    data = target.read_bytes()
    if not data or len(data) > limit:
        raise ArchitectureError('INVALID_SNAPSHOT_SOURCE')
    return data


def _unique_pairs(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ArchitectureError('DUPLICATE_CATALOG_KEY')
        result[key] = value
    return result


def prepare_council(architecture_md, catalog_json, source_revision, unity_version,
                    *, expected_doc_sha256=None, expected_catalog_sha256=None):
    """Prepare exactly one immutable architecture snapshot for 28 advisors."""
    if not isinstance(source_revision, str) or not HEX40.fullmatch(source_revision):
        raise ArchitectureError('INVALID_SOURCE_REVISION')
    if unity_version != '6000.6.8f1':
        raise ArchitectureError('UNITY_PIN_MISMATCH')
    doc_bytes = _read(architecture_md, 96_000)
    try:
        doc = doc_bytes.decode('utf-8', errors='strict')
    except UnicodeDecodeError as exc:
        raise ArchitectureError('INVALID_ARCHITECTURE_UTF8') from exc
    if (not doc.startswith(('# Ashen Crypt', '# ARE / Ashen Crypt'))
            or doc.count(MARKER) != 1):
        raise ArchitectureError('INVALID_ARCHITECTURE_SCHEMA')
    doc_sha = _digest(doc_bytes)
    if expected_doc_sha256 is not None and doc_sha != expected_doc_sha256:
        raise ArchitectureError('ARCHITECTURE_DIGEST_MISMATCH')
    catalog_bytes = _read(catalog_json, 131_072)
    catalog_sha = _digest(catalog_bytes)
    if expected_catalog_sha256 is not None and catalog_sha != expected_catalog_sha256:
        raise ArchitectureError('CATALOG_DIGEST_MISMATCH')
    try:
        raw_catalog = catalog_bytes.decode('utf-8', errors='strict')
        catalog = json.loads(raw_catalog, object_pairs_hook=_unique_pairs)
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ArchitectureError('INVALID_CATALOG_JSON') from exc
    if (not isinstance(catalog, dict)
            or set(catalog) != {'schema', 'unity', 'departments'}
            or catalog['schema'] != CATALOG_SCHEMA
            or catalog['unity'] != unity_version
            or not isinstance(catalog['departments'], list)
            or len(catalog['departments']) != 28):
        raise ArchitectureError('INVALID_CATALOG')
    departments = {}
    for dept in catalog['departments']:
        if not isinstance(dept, dict) or set(dept) != {'id', 'title', 'triggers', 'skills'}:
            raise ArchitectureError('INVALID_DEPARTMENT_FIELDS')
        ident, skills = dept['id'], dept['skills']
        if (not isinstance(ident, str) or not DEPARTMENT.fullmatch(ident)
                or ident in departments or not isinstance(skills, list)
                or not 1 <= len(skills) <= 10
                or any(not isinstance(s, str) or not SKILL.fullmatch(s) for s in skills)
                or len(set(skills)) != len(skills)):
            raise ArchitectureError('INVALID_DEPARTMENT')
        departments[ident] = list(skills)
    fingerprint = _digest(json.dumps(
        [source_revision, doc_sha, catalog_sha, unity_version],
        separators=(',', ':')).encode('ascii'))
    packets = [{
        'department': ident,
        'snapshot_id': fingerprint,
        'source_revision_claim': source_revision,
        'architecture_markdown': doc,
        'catalog_sha256': catalog_sha,
        'skills': departments[ident],
        'instruction': ('Only suggest changes within your department; cite real issues or '
                        'abstain. Reading the design grants no effect permissions.'),
    } for ident in sorted(departments)]
    return {
        'schema': COUNCIL_SCHEMA,
        'snapshot_id': fingerprint,
        'source_revision_claim': source_revision,
        'source_revision_verified': False,
        'architecture_sha256': doc_sha,
        'catalog_sha256': catalog_sha,
        'catalog_json': raw_catalog,
        'unity': unity_version,
        'effect': 'PROPOSAL_ONLY',
        'runtime_evidence': 'NOT_EXECUTED',
        'dispatch': 'NOT_SENT',
        'packets': packets,
    }


def compile_council(council, responses):
    """Fail closed if any advisor is missing, stale or exceeds its skill boundary."""
    if not isinstance(council, dict) or council.get('schema') != COUNCIL_SCHEMA:
        raise ArchitectureError('INVALID_COUNCIL')
    packets = council.get('packets')
    if not isinstance(packets, list) or len(packets) != 28:
        raise ArchitectureError('INVALID_COUNCIL')
    try:
        original = council['catalog_json'].encode('utf-8')
        data = json.loads(council['catalog_json'], object_pairs_hook=_unique_pairs)
        allowed_skills = {d['id']: d['skills'] for d in data['departments']}
        doc = packets[0]['architecture_markdown']
        identity = _digest(json.dumps([
            council['source_revision_claim'], council['architecture_sha256'],
            council['catalog_sha256'], council['unity']
        ], separators=(',', ':')).encode('ascii'))
        same = (
            council['effect'] == 'PROPOSAL_ONLY'
            and council['runtime_evidence'] == 'NOT_EXECUTED'
            and council['dispatch'] == 'NOT_SENT'
            and council['source_revision_verified'] is False
            and council['unity'] == '6000.6.8f1'
            and data.get('unity') == council['unity']
            and _digest(original) == council['catalog_sha256']
            and _digest(doc.encode('utf-8')) == council['architecture_sha256']
            and council['snapshot_id'] == identity
            and set(allowed_skills) == {p['department'] for p in packets}
            and all(p['snapshot_id'] == identity
                    and p['architecture_markdown'] == doc
                    and p['catalog_sha256'] == council['catalog_sha256']
                    and p['source_revision_claim'] == council['source_revision_claim']
                    and p['skills'] == allowed_skills[p['department']]
                    for p in packets))
    except (KeyError, TypeError, ValueError, AttributeError):
        same = False
    if not same:
        raise ArchitectureError('TAMPERED_COUNCIL_PACKET')
    catalog = {p['department']: set(p['skills']) for p in packets}
    expected = council['snapshot_id']
    if (not isinstance(expected, str) or not HEX64.fullmatch(expected)
            or not isinstance(responses, list) or len(responses) != 28):
        raise ArchitectureError('INCOMPLETE_COUNCIL')
    seen = set()
    candidates = []
    for response in responses:
        if not isinstance(response, dict) or not {'department', 'snapshot_id', 'status'} <= set(response):
            raise ArchitectureError('INVALID_ADVISOR_FIELDS')
        if not set(response) <= {'department', 'snapshot_id', 'status', 'proposal'}:
            raise ArchitectureError('INVALID_ADVISOR_FIELDS')
        ident = response['department']
        if not isinstance(ident, str) or ident not in catalog or ident in seen:
            raise ArchitectureError('UNKNOWN_OR_DUPLICATE_ADVISOR')
        seen.add(ident)
        if response['snapshot_id'] != expected:
            raise ArchitectureError('STALE_ADVISOR_RESPONSE')
        if response['status'] == 'ABSTAIN':
            if 'proposal' in response:
                raise ArchitectureError('INVALID_ABSTENTION')
            continue
        if response['status'] != 'PROPOSE' or 'proposal' not in response:
            raise ArchitectureError('INVALID_ADVISOR_STATUS')
        proposal = response['proposal']
        if not isinstance(proposal, dict) or set(proposal) != {
                'track', 'title', 'reason', 'skills', 'sources'}:
            raise ArchitectureError('INVALID_PROPOSAL_FIELDS')
        if (not isinstance(proposal['track'], str) or proposal['track'] not in TRACKS
                or any(not isinstance(proposal[k], str)
                       or not 8 <= len(proposal[k]) <= 500 for k in ('title', 'reason'))):
            raise ArchitectureError('INVALID_PROPOSAL')
        skills, sources = proposal['skills'], proposal['sources']
        if not isinstance(skills, list) or not 1 <= len(skills) <= 10:
            raise ArchitectureError('INVALID_PROPOSAL_SKILLS')
        if any(not isinstance(s, str) or s not in catalog[ident] for s in skills):
            raise ArchitectureError('UNTRUSTED_SKILL')
        if (not isinstance(sources, list) or not 1 <= len(sources) <= 10
                or any(not isinstance(s, str) or not ISSUE.fullmatch(s) for s in sources)):
            raise ArchitectureError('INVALID_PROPOSAL_SOURCES')
        candidates.append({
            'department': ident,
            **proposal,
            'authorization': 'NONE',
            'source_verification': 'REFERENCES_UNVERIFIED',
        })
    if seen != set(catalog):
        raise ArchitectureError('INCOMPLETE_COUNCIL')
    selected_by_track = {}
    for item in sorted(candidates, key=lambda x: (x['track'], x['department'], x['title'])):
        selected_by_track.setdefault(item['track'], item)
    paths = [selected_by_track[track] for track in TRACKS if track in selected_by_track][:3]
    enough = len(paths) == 3
    return {
        'schema': 'ashen.director.council-result.v1',
        'snapshot_id': expected,
        'consulted': len(seen),
        'status': ('THREE_PATHS_READY_FOR_DIRECTOR_REVIEW'
                   if enough else 'INSUFFICIENT_DISTINCT_PATHS'),
        'paths': paths if enough else [],
        'candidate_count': len(candidates),
        'execution': 'NONE',
        'runtime_evidence': 'NOT_EXECUTED',
        'director_review_required': True,
        'source_revision_verified': False,
    }
