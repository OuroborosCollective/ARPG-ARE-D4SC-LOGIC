"""Build a machine-local catalog of already installed spec workflow skills."""
import argparse
import hashlib
import json
from pathlib import Path


def build(project, spd_skills):
    roots = [(Path(project) / '.agents/skills', [
        'speckit-analyze', 'speckit-clarify', 'speckit-checklist', 'speckit-constitution',
        'speckit-specify', 'speckit-plan', 'speckit-tasks', 'speckit-implement',
        'speckit-converge', 'speckit-bug-assess', 'speckit-bug-fix', 'speckit-bug-test']),
        (Path(spd_skills), ['spec-driven-develop', 'deep-discuss', 'review-spd'])]
    skills = []
    for root, names in roots:
        for name in names:
            path = (root / name / 'SKILL.md').resolve(strict=True)
            files = {str(f.relative_to(path.parent)): hashlib.sha256(f.read_bytes()).hexdigest()
                     for f in sorted(path.parent.rglob('*')) if f.is_file()}
            skills.append(dict(name=name, path=str(path), sha256=files['SKILL.md'], files=files))
    return dict(schema='ashen.skill-catalog.v1', skills=skills)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', required=True)
    parser.add_argument('--spd-skills', required=True)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    args.output.write_text(json.dumps(build(args.project, args.spd_skills), indent=2) + '\n')
