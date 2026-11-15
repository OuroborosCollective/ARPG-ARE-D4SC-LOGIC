#!/usr/bin/env python3
"""Bound this owner's Unity process trees, never unrelated VPS services.

Root-only VPS operator tool. Pausing preserves memory; it is not an on-disk save.
The timer enforces CPU limits only and never resumes a paused editor.
"""
import argparse
import json
import os
from pathlib import Path
import pwd
import signal
import subprocess

UNIT = 'ashen-unity-budget.scope'
CGROUP = Path('/sys/fs/cgroup/system.slice') / UNIT
EDITOR_ROOT = '/var/lib/unityarpg/Unity/Hub/Editor/'
CPU_QUOTA_PERCENT = 100  # One core shared by every selected Unity process tree.


def select_family(processes, uid):
    editors = {pid for pid, p in processes.items() if p['uid'] == uid
               and p['exe'].startswith(EDITOR_ROOT) and p['exe'].endswith('/Editor/Unity')}
    family = set(editors)
    while True:
        children = {pid for pid, p in processes.items()
                    if p['uid'] == uid and p['ppid'] in family} - family
        if not children:
            return family, editors
        family.update(children)


def snapshot():
    result = {}
    for path in Path('/proc').iterdir():
        if not path.name.isdigit():
            continue
        try:
            stat = (path / 'stat').read_text()
            fields = stat[stat.rfind(')') + 2:].split()
            result[int(path.name)] = dict(uid=path.stat().st_uid, exe=os.readlink(path / 'exe'),
                                          ppid=int(fields[1]), state=fields[0], start=fields[19])
        except (OSError, ValueError, IndexError):
            continue
    return result


def still_same(pid, expected):
    try:
        path = Path('/proc') / str(pid)
        text = (path / 'stat').read_text()
        fields = text[text.rfind(')') + 2:].split()
        return fields[19] == expected['start'] and path.stat().st_uid == expected['uid']
    except (OSError, IndexError):
        return False


def enforce(processes, family):
    if not family:
        return 0
    if not CGROUP.exists():
        ids = [str(p) for p in sorted(family) if still_same(p, processes[p])]
        if not ids:
            return 0
        subprocess.run(['busctl', 'call', 'org.freedesktop.systemd1', '/org/freedesktop/systemd1',
                        'org.freedesktop.systemd1.Manager', 'StartTransientUnit', 'ssa(sv)a(sa(sv))',
                        UNIT, 'fail', '5', 'Description', 's', 'Bounded Ashen Unity editors',
                        'PIDs', 'au', str(len(ids)), *ids, 'CPUAccounting', 'b', 'true',
                        'CPUQuotaPerSecUSec', 't', str(CPU_QUOTA_PERCENT * 10000), 'CPUWeight', 't', '10', '0'],
                       check=True, capture_output=True, text=True)
    quota, period = (CGROUP / 'cpu.max').read_text().split()
    if quota == 'max' or int(quota) * 100 != int(period) * CPU_QUOTA_PERCENT:
        subprocess.run(['systemctl', 'set-property', '--runtime', UNIT,
                        f'CPUQuota={CPU_QUOTA_PERCENT}%', 'CPUWeight=10'], check=True)
    moved = 0
    for pid in sorted(family):
        if not still_same(pid, processes[pid]):
            continue
        try:
            group = (Path('/proc') / str(pid) / 'cgroup').read_text().strip()
            if group != '0::/system.slice/' + UNIT:
                (CGROUP / 'cgroup.procs').write_text(str(pid))
                moved += 1
        except FileNotFoundError:
            pass  # A verified process may exit between the identity check and attachment.
    return moved


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('operation', choices=['enforce', 'status', 'pause'])
    args = parser.parse_args()
    processes = snapshot()
    family, editors = select_family(processes, pwd.getpwnam('unityarpg').pw_uid)
    if args.operation != 'status' and os.geteuid() != 0:
        parser.error('Root required to manage the existing system scope.')
    if args.operation == 'pause':
        for pid in sorted(family):
            if still_same(pid, processes[pid]):
                try:
                    os.kill(pid, signal.SIGSTOP)
                except ProcessLookupError:
                    pass
    moved = enforce(processes, family) if args.operation != 'status' else 0
    if args.operation != 'enforce' or moved:
        print(json.dumps(dict(operation=args.operation, unit=UNIT, processes=len(family),
                              unityExecutables=sorted(editors), moved=moved,
                              quota=(CGROUP / 'cpu.max').read_text().strip() if CGROUP.exists() else None)))


if __name__ == '__main__':
    main()
