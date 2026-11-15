# Unity VPS CPU budget

Owner reported a renewed hoster restriction to 30% on 2026-11-14. Two persistent
Unity editors were the largest measured consumers. Both and their descendant
workers were paused with SIGSTOP to preserve memory and stop CPU consumption.
This is not a disk save and is not evidence of a completed PlayMode test.

The initial recovery ceiling was half a core. After the owner's question about
using more capacity on 2026-11-15, a five-second host baseline with Unity paused
measured 13.13% busy CPU (about 1.05 of 8 cores), 18.56% I/O wait and 0.70% steal.
This is a sample, not a guaranteed bound on unrelated services.

`ashen-unity-budget.scope` now limits the combined Unity process trees to **one
CPU core** (`cpu.max` = `100000 100000`), not 100% of the whole VPS. On this
8-vCPU host the ceiling is 12.5% of nominal total CPU capacity. Other services and
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

## Software-renderer startup

The recovery inspection found eight busy `llvmpipe` threads while the main editor
thread waited on a futex. This observation does not prove a driver deadlock. The
protected close eventually completed; a later fresh editor also closed gracefully
with an empty unsaved-scenes list. No force termination was used.

On this host `/usr/local/bin/unity` delegates through `sudo`, which strips an
outer `LP_NUM_THREADS=1` setting. Supply that setting **after** the user switch,
using the same unityarpg home, PATH and XDG runtime settings as the wrapper.
Supply the actual protected display/Xauthority path there too. Run the official
`/var/lib/unityarpg/.local/bin/unity` as unityarpg, pass `-job-worker-count 1`,
and attach the launcher to the existing shared cgroup before it spawns children.
Never expose the Xauthority cookie, CLI authentication arguments or editor tokens.

Read back `/proc/<pid>/environ` only for the whitelisted renderer setting, count
`llvmpipe-*` task names, and verify `cpu.max` and membership. The verified fresh
main editor and its asset-import worker each had one llvmpipe thread and inherited
the one-core shared limit. Startup alone is not a completed game test or a
performance improvement measurement. Do not increase the limit to 2.5 cores
while the reported 30% host restriction and other workloads remain in effect.
