"""Version-bound advisory skill selection for the existing Ashen Crypt Director.

The cortex does not connect to MCP, open a socket, execute tools, grant a
capability, install a package, mutate Unity, or claim runtime evidence.
"""
from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path

SCHEMA = 'ashen.director.neural-catalog.v1'
ROUTE_SCHEMA = 'ashen.director.neural-route.v1'
MAX_CATALOG_BYTES = 131_072
MAX_DEPARTMENTS = 48
MAX_KEYWORDS = 48
MAX_SKILLS = 10
ALLOWED_DEPARTMENT_KEYS = {'id', 'title', 'triggers', 'skills'}
SKILL_NAME = re.compile(r'^[a-z][a-z0-9_.-]{1,79}$')
ID = re.compile(r'^[a-z][a-z0-9_]{2,48}$')


def _unique_pairs(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError('INVALID_CATALOG_DUPLICATE_JSON_KEY')
        result[key] = value
    return result


def _text_normalized(value):
    text = value.casefold()
    for source, target in (
        ('\u00e4', 'ae'), ('\u00f6', 'oe'), ('\u00fc', 'ue'), ('\u00df', 'ss'),
    ):
        text = text.replace(source, target)
    return text


def load_catalog(path):
    """Read exactly one complete byteset and validate before exposing hints."""
    data_path = Path(path)
    if not data_path.is_file() or data_path.stat().st_size > MAX_CATALOG_BYTES:
        raise ValueError('INVALID_CATALOG_FILE')
    raw = data_path.read_bytes()
    try:
        data = json.loads(raw.decode('utf-8'), object_pairs_hook=_unique_pairs)
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ValueError('INVALID_CATALOG_JSON') from exc
    if not isinstance(data, dict) or set(data) != {'schema', 'unity', 'departments'}:
        raise ValueError('INVALID_CATALOG')
    if data['schema'] != SCHEMA or data['unity'] != '6000.6.8f1':
        raise ValueError('INVALID_CATALOG_VERSION')
    departments = data['departments']
    if not isinstance(departments, list) or not 1 <= len(departments) <= MAX_DEPARTMENTS:
        raise ValueError('INVALID_CATALOG_DEPARTMENTS')
    ids = set()
    for item in departments:
        if not isinstance(item, dict) or set(item) != ALLOWED_DEPARTMENT_KEYS:
            raise ValueError('INVALID_CATALOG')
        identifier = item['id']
        if not isinstance(identifier, str) or not ID.fullmatch(identifier):
            raise ValueError('INVALID_DEPARTMENT_ID')
        if identifier in ids:
            raise ValueError('DUPLICATE_DEPARTMENT')
        ids.add(identifier)
        if not isinstance(item['title'], str) or not 3 <= len(item['title']) <= 90:
            raise ValueError('INVALID_DEPARTMENT_TITLE')
        triggers = item['triggers']
        skills = item['skills']
        if (not isinstance(triggers, list) or not 1 <= len(triggers) <= MAX_KEYWORDS
                or any(not isinstance(s, str) or not 2 <= len(s) <= 64 or
                       _text_normalized(s) != s or not s.strip() for s in triggers)
                or len(triggers) != len(set(triggers))):
            raise ValueError('INVALID_DEPARTMENT_TRIGGERS')
        if (not isinstance(skills, list) or not 1 <= len(skills) <= MAX_SKILLS
                or any(not isinstance(s, str) or not SKILL_NAME.fullmatch(s)
                       for s in skills) or len(skills) != len(set(skills))):
            raise ValueError('INVALID_DEPARTMENT_SKILLS')
    return data, hashlib.sha256(raw).hexdigest()


def _verified_project_version(project, expected_version):
    path = Path(project)
    version_file = path / 'ProjectSettings' / 'ProjectVersion.txt'
    if not (path / 'Assets').is_dir() or not version_file.is_file():
        raise ValueError('UNITY_PROJECT_NOT_VERIFIED')
    contents = version_file.read_text(encoding='utf-8', errors='strict')
    match = re.search(r'^m_EditorVersion:\s*(\S+)', contents, re.MULTILINE)
    if match is None or match.group(1) != expected_version:
        raise ValueError('UNITY_VERSION_MISMATCH')
    return match.group(1)


def _matched_triggers(task, triggers):
    return [keyword for keyword in triggers
            if re.search(r'(?<!\w)' + re.escape(keyword) + r'(?!\w)', task)]


def route(task, catalog, project=None, observed_skill_states=None, max_departments=3):
    """Choose only explicit departments; return suggestions, never call tools.

    A project is required: only the pin read from ProjectSettings is accepted, so a
    project-less call is refused before any department is selected (contract, no
    NOT_VERIFIED candidates).
    """
    if not isinstance(task, str) or not 1 <= len(task.strip()) <= 4096:
        raise ValueError('INVALID_TASK')
    if project is None:
        raise ValueError('PROJECT_REQUIRED')
    if not isinstance(max_departments, int) or not 1 <= max_departments <= 3:
        raise ValueError('INVALID_SELECTION_BUDGET')
    data, digest = load_catalog(catalog)
    unity = _verified_project_version(project, data['unity'])
    normalized_task = _text_normalized(task)
    states = observed_skill_states if isinstance(observed_skill_states, dict) else {}
    ranked = []
    for item in data['departments']:
        matches = _matched_triggers(normalized_task, item['triggers'])
        if not matches:
            continue
        rank = (max(len(x) for x in matches), len(matches))
        ranked.append((rank, item['id'], item, matches))
    ranked.sort(key=lambda x: (-x[0][0], -x[0][1], x[1]))
    selected = []
    for _, identifier, item, matches in ranked[:max_departments]:
        skills = []
        for name in item['skills']:
            status = states.get(name)
            # A locally verified file hash is NOT an authorization or runtime proof.
            status = {'INSTALLED': 'INSTALLED_FILE_HASH_ONLY',
                      'CHANGED': 'CHANGED_REVIEW_REQUIRED',
                      'MISSING': 'MISSING'}.get(status, 'NOT_VERIFIED')
            skills.append({'name': name, 'status': status})
        selected.append({
            'id': identifier,
            'title': item['title'],
            'matches': sorted(matches),
            'skills': skills,
            'effect': 'PROPOSAL_ONLY',
            'authorized': False,
        })
    return {
        'schema': ROUTE_SCHEMA,
        'task': task,
        'unity': unity,
        'authority': 'Unity Game Director',
        'catalog_sha256': digest,
        'status': 'CANDIDATES_ONLY' if selected else 'NO_DOMAIN_MATCH',
        'advisory_only': True,
        'broadcast': False,
        'execution': 'NONE',
        'runtime_evidence': 'NOT_EXECUTED',
        'selected': selected,
        'deferred': [identifier for _, identifier, _, _ in ranked[max_departments:]],
        'instruction': 'Director selects relevant expertise only. Before invoking any tool, '
                       'verify source revision, installed guidance, capability policy and '
                       'explicit permission; execute through the existing Director or Sovott. '
                       'Read back real Unity/Core/CI outcomes separately.',
    }
