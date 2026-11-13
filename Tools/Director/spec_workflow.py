"""Advisory spec workflow selection; never executes agents, hooks or Unity."""
from pathlib import Path
import re

from official_unity import inventory


def route(task, catalog, project):
    text = task.casefold()
    if re.search(r'\b(bug|fix|fehler|repar\w*)\b', text):
        kind = 'bug'
        commands = ['speckit-bug-assess', 'speckit-bug-fix', 'speckit-bug-test']
        artifacts = '.specify/bugs/<slug>/{assessment,fix,test}.md'
    elif re.search(r'\b(spec|speckit|specification|spezifikation|feature)\b', text):
        kind = 'feature'
        commands = ['speckit-specify', 'speckit-plan', 'speckit-tasks',
                    'speckit-analyze', 'speckit-implement', 'speckit-converge']
        artifacts = 'specs/<feature>/{spec,plan,tasks}.md'
    else:
        return None
    project = Path(project)
    version = project / 'ProjectSettings/ProjectVersion.txt'
    if not (project / 'Assets').is_dir() or not version.is_file():
        raise ValueError('A real Unity project path is required')
    match = re.search(r'^m_EditorVersion:\s*(\S+)', version.read_text(), re.MULTILINE)
    if not match:
        raise ValueError('Unity project version is missing')
    installed = {s['name']: s for s in inventory(catalog)} if Path(catalog).is_file() else {}
    names = ['spec-driven-develop', *commands, 'review-spd']
    return dict(schema='ashen.director.spec-workflow.v1', kind=kind,
                advisory_only=True, execution='NOT_EXECUTED', unity=match.group(1),
                skills=[installed.get(n, dict(name=n, status='MISSING')) for n in names],
                artifacts=artifacts,
                authority=dict(rules='AGENTS.md', memory='Memory.md',
                               game_state='ProjectState.md', execution='Unity Game Director'),
                instruction='Read selected skills before use. Spec Kit owns the change contract; '
                            'SPD plans and reviews; Director executes the existing checks. '
                            'Use existing user decisions and ask only about unresolved product choices. '
                            'Preserve AreArpg.Core, 100-ms ticks and saves. '
                            'Original reproduction and affected regression checks remain mandatory. '
                            'Skill installation and routing do not prove a gameplay fix.')
