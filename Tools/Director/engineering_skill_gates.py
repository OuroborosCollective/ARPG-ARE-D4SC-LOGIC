"""Read-only, pinned engineering-skill routing and evidence consistency gates.

This module DOES NOT install skills, invoke models, run tests, grant permissions,
mutate the game, or certify external CI/Unity. All returned statuses concern
source bytes or supplied evidence metadata, never independent runtime truth.
"""
from __future__ import annotations

import hashlib
import heapq
import json
import re
from datetime import date
from pathlib import Path

SOURCE_REVISION = 'd1478fcb140db0b93c0c8df17bcfd311844749d0'
SOURCE_REPOSITORY = 'https://github.com/mattpocock/skills'
SOURCE_TAG = 'v1.3.2'
CATALOG_SCHEMA = 'ashen.engineering-skills.v1'
SKILL_PATHS = {
    'ask-matt': 'skills/engineering/ask-matt/SKILL.md',
    'code-review': 'skills/engineering/code-review/SKILL.md',
    'diagnosing-bugs': 'skills/engineering/diagnosing-bugs/SKILL.md',
    'tdd': 'skills/engineering/tdd/SKILL.md',
    'implement-spec': 'skills/engineering/implement-spec/SKILL.md',
    'chief-of-staff': 'skills/in-progress/chief-of-staff/SKILL.md',
}
SOURCE_SKILL_SHA256 = {
    'ask-matt': '1371f5ce005de0992a44b137748bfb960a56948aa9871b405fe9d90b24332e3c',
    'code-review': '9c921a8d88138a41401d8897ca1bb852088ab1b7b605cdd04edc13e3e8aab6dd',
    'diagnosing-bugs': '849198df88cdb65fd0d97236604c1fca7ff2f22d2f705f9724a800d0266b118d',
    'tdd': '5505d25bbaa8fc14a79e4d20163936958992a2995b7f1dd1d6197edc9ed722e0',
    'implement-spec': '8d8300f7f12325dbfc99cad124a4bcc96ac0d02d0b2d73dfb348e9262afacfc4',
    'chief-of-staff': 'd13f3a542a4664f27bf14977811ee9dcd83fae03655b20720b3441850a2008a4',
}

EXPERIMENTAL = frozenset({'chief-of-staff'})
HEX40 = re.compile(r'^[0-9a-f]{40}$')
HEX64 = re.compile(r'^[0-9a-f]{64}$')
CLAIM = re.compile(
    r'^- (?P<date>\d{4}-\d{2}-\d{2}) \| '
    r'(?P<repo>[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+)@(?P<sha>[^| ]+) \| '
    r'(?P<statement>[^|]+) \| source=(?P<source>[^ ]+)$'
)
DEFAULT_EDGES = (
    ('PinUpstream', 'CurateSkills'),
    ('RepoReadback', 'Compatibility'),
    ('CurateSkills', 'Compatibility'),
    ('Compatibility', 'Plan'),
    ('Plan', 'OwnerApproval'),
    ('OwnerApproval', 'DirectorApply'),
    ('DirectorApply', 'CoreTests'),
    ('DirectorApply', 'EditMode'),
    ('DirectorApply', 'PlayMode'),
    ('DirectorApply', 'OptionalDevice'),
    ('CoreTests', 'EvidenceReadback'),
    ('EditMode', 'EvidenceReadback'),
    ('PlayMode', 'EvidenceReadback'),
    ('EvidenceReadback', 'MemoryEntry'),
    ('MemoryEntry', 'Merge'),
)


def _sha256(data):
    return hashlib.sha256(data).hexdigest()


def _reachable(adjacency, start, goal, *, removed=None):
    if start == removed or goal == removed:
        return False
    queue = [start]
    seen = set()
    while queue:
        current = queue.pop()
        if current == goal:
            return True
        if current in seen or current == removed:
            continue
        seen.add(current)
        queue.extend(node for node in adjacency.get(current, ()) if node != removed)
    return False


def validate_graph(edges, *, graph_kind='task'):
    """Acyclicity is required for TASK dependencies, never for economy loops."""
    if graph_kind not in ('task', 'economy'):
        return {'status': 'INVALID_KIND'}
    if (not isinstance(edges, (tuple, list)) or
            any(not isinstance(edge, (list, tuple)) or len(edge) != 2 or
                any(not isinstance(v, str) or not v for v in edge) for edge in edges)):
        return {'status': 'INVALID_GRAPH'}
    vertices = {v for edge in edges for v in edge}
    successors = {v: set() for v in vertices}
    indegree = {v: 0 for v in vertices}
    for a, b in edges:
        if b not in successors[a]:
            successors[a].add(b)
            indegree[b] += 1
    ready = [v for v in vertices if indegree[v] == 0]
    heapq.heapify(ready)
    order = []
    while ready:
        v = heapq.heappop(ready)
        order.append(v)
        for w in sorted(successors[v]):
            indegree[w] -= 1
            if not indegree[w]:
                heapq.heappush(ready, w)
    cyclic = len(order) != len(vertices)
    if graph_kind == 'economy':
        return {'status': 'CYCLE_ALLOWED' if cyclic else 'VALID_ECONOMY',
                'has_cycle': cyclic, 'vertex_count': len(vertices)}
    if cyclic:
        return {'status': 'CYCLIC', 'vertex_count': len(vertices)}
    required = {'Plan', 'OwnerApproval', 'DirectorApply', 'EvidenceReadback', 'MemoryEntry', 'Merge'}
    if not required.issubset(vertices):
        return {'status': 'INVALID_GATES', 'missing': sorted(required - vertices)}
    approval = (_reachable(successors, 'Plan', 'DirectorApply') and
                not _reachable(successors, 'Plan', 'DirectorApply', removed='OwnerApproval') and
                not _reachable(successors, 'Plan', 'Merge', removed='OwnerApproval'))
    evidence = (_reachable(successors, 'DirectorApply', 'Merge') and
                not _reachable(successors, 'DirectorApply', 'Merge', removed='EvidenceReadback'))
    device_optional = ('OptionalDevice' in vertices and
                       _reachable(successors, 'DirectorApply', 'Merge', removed='OptionalDevice') and
                       not successors.get('OptionalDevice', set()))
    if not (approval and evidence and device_optional):
        return {'status': 'INVALID_GATES', 'approval_mandatory': approval,
                'evidence_mandatory': evidence, 'device_optional': device_optional}
    return {'status': 'VALID', 'topological_order': order, 'vertex_count': len(vertices),
            'edge_count': len(edges), 'approval_mandatory': approval,
            'evidence_mandatory': evidence, 'device_optional': device_optional}


def check_authorities(owners):
    """Detect competing canonical owners; do not instantiate any new authority."""
    if not isinstance(owners, dict) or not owners:
        return {'status': 'INVALID_AUTHORITY_REGISTRY'}
    duplicated = sorted(
        key for key, values in owners.items()
        if not isinstance(values, list) or len(values) != 1 or
        not isinstance(values[0], str) or not values[0].strip()
    )
    return {'status': 'DUPLICATE_AUTHORITY' if duplicated else 'VALID',
            'conflicting_areas': duplicated}


def audit_catalog(catalog_path):
    """Check installed SKILL.md bytes versus a LOCAL catalog, not remote identity."""
    states = {key: 'NOT_VERIFIED' for key in SKILL_PATHS}
    for key in EXPERIMENTAL:
        states[key] = 'EXPERIMENTAL_NOT_EXECUTABLE'
    path = Path(catalog_path)
    if not path.is_file():
        return {'status': 'CATALOG_MISSING', 'skills': states}
    if path.stat().st_size > 128_000:
        return {'status': 'INVALID_CATALOG', 'skills': states}
    try:
        obj = json.loads(path.read_text(encoding='utf-8'))
    except (ValueError, UnicodeError, OSError):
        return {'status': 'INVALID_CATALOG', 'skills': states}
    if not isinstance(obj, dict) or obj.get('schema') != CATALOG_SCHEMA:
        return {'status': 'INVALID_CATALOG', 'skills': states}
    if obj.get('source_revision') != SOURCE_REVISION:
        return {'status': 'SOURCE_PIN_MISMATCH', 'skills': states}
    skills = obj.get('skills')
    if not isinstance(skills, list):
        return {'status': 'INVALID_CATALOG', 'skills': states}
    found = set()
    for item in skills:
        if not isinstance(item, dict) or item.get('name') not in SKILL_PATHS or item['name'] in found:
            return {'status': 'INVALID_CATALOG', 'skills': states}
        name = item['name']
        found.add(name)
        if name in EXPERIMENTAL:
            continue
        declared = item.get('sha256')
        location = item.get('path')
        if not isinstance(declared, str) or not HEX64.fullmatch(declared):
            return {'status': 'INVALID_CATALOG', 'skills': states}
        if not isinstance(location, str):
            return {'status': 'INVALID_CATALOG', 'skills': states}
        skill = Path(location)
        if skill.name != 'SKILL.md' or skill.is_symlink() or not skill.is_file():
            states[name] = 'MISSING'
            continue
        if skill.stat().st_size > 96_000:
            states[name] = 'INVALID_SKILL'
            continue
        try:
            data = skill.read_bytes()
            data.decode('utf-8', errors='strict')
        except (OSError, UnicodeError):
            states[name] = 'INVALID_SKILL'
            continue
        actual = _sha256(data)
        states[name] = ('CHANGED' if actual != declared else
                        'UPSTREAM_PIN_MATCH' if actual == SOURCE_SKILL_SHA256[name] else
                        'LOCAL_BYTES_MATCH_OTHER_REVISION')
    for name in SKILL_PATHS:
        if name not in found and name not in EXPERIMENTAL:
            states[name] = 'MISSING'
    clean = all(states[name] == 'UPSTREAM_PIN_MATCH'
                for name in SKILL_PATHS if name not in EXPERIMENTAL)
    return {'status': 'UPSTREAM_PIN_MATCH' if clean else 'NEEDS_REVIEW',
            'skills': states, 'upstream_pin': SOURCE_REVISION,
            'provenance_limit': 'Exact upstream pinned hashes prove expected source bytes, not local runtime execution'}


def build_local_catalog(skills_root, output_path):
    """Create a machine-local inventory from exact v1.3.2 bytes only.

    No download, execution, overwrite, or installation occurs. Experimental
    chief-of-staff is excluded from the runnable host inventory.
    """
    root = Path(skills_root)
    dest = Path(output_path)
    entries = []
    for name in SKILL_PATHS:
        if name in EXPERIMENTAL:
            continue
        path = root / name / 'SKILL.md'
        if path.is_symlink() or not path.is_file():
            raise ValueError('MISSING_PINNED_SKILL:' + name)
        if path.stat().st_size > 96_000 or _sha256(path.read_bytes()) != SOURCE_SKILL_SHA256[name]:
            raise ValueError('PINNED_SKILL_HASH_MISMATCH:' + name)
        entries.append({'name': name, 'path': str(path.resolve()),
                        'sha256': SOURCE_SKILL_SHA256[name]})
    payload = {'schema': CATALOG_SCHEMA, 'source_revision': SOURCE_REVISION,
               'source_tag': SOURCE_TAG, 'skills': entries}
    encoded = (json.dumps(payload, indent=2, sort_keys=True) + '\n').encode('utf-8')
    if dest.exists():
        if not dest.is_file() or dest.read_bytes() != encoded:
            raise ValueError('CATALOG_EXISTS_AND_DIFFERS')
        return payload
    if not dest.parent.is_dir():
        raise ValueError('CATALOG_PARENT_MISSING')
    fd = None
    try:
        fd = dest.open('xb')
        fd.write(encoded)
    finally:
        if fd is not None:
            fd.close()
    return payload


def task_plan(task, catalog_path=None):
    """Router returns advice only; unread/missing skills are not promoted."""
    if not isinstance(task, str) or not task.strip():
        raise ValueError('TASK_REQUIRED')
    text = task.casefold()
    selected = ['ask-matt']
    def choose(name):
        if name not in selected:
            selected.append(name)
    bug = any(word in text for word in ('bug', 'fix', 'reparier', 'regress', 'debug', 'diagnos'))
    implementation = bug or any(word in text for word in ('implement', 'integrier', 'feature', 'code', 'entwickl'))
    if bug:
        choose('diagnosing-bugs')
    if implementation or 'test' in text:
        choose('tdd')
    if implementation or any(word in text for word in ('review', 'merge', 'pull request', ' pr ', 'audit')):
        choose('code-review')
    if ('spec' in text or 'ticket' in text) and implementation:
        choose('implement-spec')
    audited = audit_catalog(catalog_path) if catalog_path is not None else {
        'status': 'CATALOG_MISSING',
        'skills': {name: ('EXPERIMENTAL_NOT_EXECUTABLE' if name in EXPERIMENTAL else 'NOT_VERIFIED')
                   for name in SKILL_PATHS}
    }
    return {'schema': 'ashen.engineering-skill-route.v1', 'selected': selected,
            'skills': {name: audited['skills'][name] for name in selected},
            'catalog_status': audited['status'], 'advisory_only': True,
            'execution': 'NONE', 'new_authorities': 0,
            'pin': SOURCE_REVISION, 'experimental_knowledge_only': sorted(EXPERIMENTAL)}


def review_gate(standards, spec, revision):
    """Validate metadata from TWO independently produced reviews, not the reviews."""
    if not isinstance(revision, str) or not HEX40.fullmatch(revision):
        return {'status': 'REVIEW_BLOCKED', 'reason': 'INVALID_REVISION'}
    reports = (standards, spec)
    if not all(isinstance(report, dict) and report.get('status') == 'PASS' and
               report.get('revision') == revision and
               isinstance(report.get('report_sha256'), str) and
               HEX64.fullmatch(report['report_sha256']) and
               isinstance(report.get('sources'), list) and
               bool(report['sources']) and all(isinstance(src, str) and src.strip()
                                              for src in report['sources'])
               for report in reports):
        return {'status': 'REVIEW_BLOCKED', 'reason': 'MISSING_OR_INVALID_REPORT'}
    if standards['report_sha256'] == spec['report_sha256']:
        return {'status': 'REVIEW_BLOCKED', 'reason': 'REPORTS_NOT_DISTINCT'}
    return {'status': 'REVIEW_EVIDENCE_CONSISTENT', 'revision': revision,
            'runtime_verified': False, 'merge_authorized': False}


def mutation_gate(pristine, mutated, red_exit, green_exit):
    """Check a real byte delta and exit-code metadata; never claim a Unity test."""
    if not isinstance(pristine, bytes) or not isinstance(mutated, bytes):
        return {'status': 'INVALID_TEST_ARTIFACT'}
    if pristine == mutated:
        return {'status': 'MUTATION_NOT_APPLIED'}
    if not isinstance(red_exit, int) or isinstance(red_exit, bool) or red_exit == 0:
        return {'status': 'RED_NOT_OBSERVED'}
    if not isinstance(green_exit, int) or isinstance(green_exit, bool) or green_exit != 0:
        return {'status': 'GREEN_NOT_OBSERVED'}
    return {'status': 'METADATA_CONSISTENT_NOT_RUNTIME',
            'pristine_sha256': _sha256(pristine), 'mutated_sha256': _sha256(mutated),
            'runtime_verified': False}


def lint_wiki_page(markdown):
    """Validate historical wiki claims; source URLs are syntax-checked, not fetched."""
    if not isinstance(markdown, str):
        return ['INVALID_PAGE']
    errors = []
    entries = 0
    for line in markdown.splitlines():
        if not line.startswith('- '):
            continue
        entries += 1
        match = CLAIM.fullmatch(line.strip())
        if match is None:
            errors.append('INVALID_CLAIM_FORMAT')
            if not re.search(r'@[0-9a-f]{40}\b', line):
                errors.append('MISSING_COMMIT')
            if 'source=https://' not in line:
                errors.append('MISSING_SOURCE')
            continue
        val = match.groupdict()
        try:
            date.fromisoformat(val['date'])
        except ValueError:
            errors.append('INVALID_DATE')
        if not HEX40.fullmatch(val['sha']):
            errors.append('MISSING_COMMIT')
        if not val['source'].startswith('https://') or '://' not in val['source']:
            errors.append('MISSING_SOURCE')
        if not val['statement'].strip():
            errors.append('EMPTY_CLAIM')
    if entries == 0:
        errors.append('NO_ENTRIES')
    return sorted(set(errors))


def main():
    import argparse
    parser = argparse.ArgumentParser(description='Build exact v1.3.2 local Skill catalog')
    parser.add_argument('--skills-root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = build_local_catalog(args.skills_root, args.output)
    audited = audit_catalog(args.output)
    if audited['status'] != 'UPSTREAM_PIN_MATCH':
        raise SystemExit('Skill catalog readback failed: ' + audited['status'])
    print(json.dumps({'status': audited['status'],
                      'source_revision': result['source_revision'],
                      'skills': {key: value for key, value in audited['skills'].items()}},
                     sort_keys=True))


if __name__ == '__main__':
    main()
