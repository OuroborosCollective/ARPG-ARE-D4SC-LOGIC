# Unity VPS CPU budget

Owner reported a renewed hoster restriction to 30% on 2026-11-14. Two persistent
Unity editors were the largest measured consumers. Both and their descendant
workers were paused with SIGSTOP to preserve memory and stop CPU consumption.
This is not a disk save and is not evidence of a completed PlayMode test.

`ashen-unity-budget.scope` limits the combined Unity process trees to **half of
one CPU core** (`cpu.max` = `50000 100000`), not 50% of the whole VPS. On this
8-vCPU host the ceiling is 6.25% of nominal total CPU capacity. Other services and
the hoster's own restriction are separate; this does not promise the host will
remove its restriction or that total server use stays below a fixed percentage.

The timer reapplies this shared limit to newly launched owner Unity editors and
their descendants every 30 seconds. It does not resume paused processes, restart
services, touch Node/MariaDB/Docker workloads, or delete files. It verifies the
owner UID, executable location and process start identity. Existing workers
inherit the cgroup limit, and newly found trees join the same aggregate budget.

Installed operator files:

- `/opt/ashen-unity/unity_cpu_budget.py`
- `/etc/systemd/system/ashen-unity-budget-guard.service`
- `/etc/systemd/system/ashen-unity-budget-guard.timer`

Use `python3 /opt/ashen-unity/unity_cpu_budget.py status` for inventory and
`pause` for a memory-preserving pause. `enforce` applies the budget immediately.
The timer is enabled across reboot, but neither this service nor its timer starts
Unity. Never stop the live **scope** casually: systemd can terminate its members.
Disabling the **guard timer** leaves the current scope limit in place.

Development policy: keep at most one main editor running, start expensive tests
only for a concrete verification block, and save/close or pause afterward. Do
source work, Core checks and planning outside the VPS where available. A paused
editor must not be blindly resumed while a stale test is waiting: first preserve
its test record, use the shared cap, cancel/reconcile the interrupted run and
restore its isolated PlayerPrefs/settings. The asset-intake editor remains paused
until its work can be saved deliberately. No force-kill of unsaved editor work.

The browser Fadenwerk gateway remains protected, but the editor-backed UI is
unavailable while its Unity process is paused. Do not claim an interrupted run
passed or silently merge pending gameplay changes on the strength of Core tests.
