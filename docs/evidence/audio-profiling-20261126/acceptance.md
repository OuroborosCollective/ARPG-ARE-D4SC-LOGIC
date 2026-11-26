# Audio and profiling · 2026-11-26

Owner requests of 2026-11-26: load and put to use the Memory Profiler, the Profile Analyzer, Footsteps – Essentials and
SwordSoundPack; then install ProBuilder, the Project Auditor's rules, Profiling Core and System Metrics Mali (target
tablet: Galaxy Tab A9, Mali-G57); then apply what fits from Unity 6000.6's "Optimization for Android". Declined after the
owner asked: Vivox (the game is offline and collects no data) and Terrain Tools (the ground is meshes with Core heights).

## What changed

### Packages (Unity's registry)

`com.unity.memoryprofiler` 1.2.1, `com.unity.performance.profile-analyzer` 1.5.0, `com.unity.probuilder` 6.1.4,
`com.unity.project-auditor-rules` 3.2.0 (the auditor itself is part of the editor), `com.unity.profiling.core` 1.0.3
(now direct) and `com.unity.profiling.systemmetrics.mali` 1.1.4; the lock adds mathematics, editor coroutines and the
settings manager as Unity resolved them.

### Profiling in the game

- Area memory snapshots (`MemorySnapshotPresentation`): in a development build or the editor, switched on by
  `-areMemorySnapshots`, `ARE_MEMORY_SNAPSHOTS=1` or a file `memory-snapshots.on` in persistentDataPath, the game takes
  one snapshot three seconds after every change of area into a folder per session (`MemorySnapshots/<time>/NN-<area>.snap`).
  With a profiler attached the snapshot goes to the connected editor. Release builds take none.
- Performance log (`PerformanceLog`, `PerformanceProbes`): development builds write one line a second to
  `PerformanceLog/perf-<time>.csv`: area, frame rate, mean and worst frame time, the Core's step time, enemies alive,
  loot on the ground, system/GC/graphics memory and, on a Mali GPU, active cycles and fragment, vertex, arithmetic,
  texture and load/store load. Development builds made for the owner's tests write it from the start; elsewhere it is
  switched on like the snapshots (`performance-log.on`).
- Profiler counters (Profiling Core): "Ashen Core Step" (nanoseconds per frame), "Ashen Enemies Alive", "Ashen Ground Loot".
- CPU route (`ProfiledVillageRoute`, explicit): from the village square a fixed loop of four legs of 40 simulation ticks,
  walked through the runtime's own input (`ArenaRuntime.ScriptMove`), recorded as a profiler capture
  `Temp/Profiling/village-route-<time>.raw` for the Profile Analyzer's Compare view.

### Android (Unity 6000.6, optimization for Android)

The game tells Android once that it is interruptible gameplay (game state hinting, Android 13+); in the battery game
mode it runs at 30 frames a second, otherwise 60, read again on every return to the game; "fully drawn" is reported on the
first frame the wanderer can play. Thread configuration is left for measurements on the tablet.

### Audio

- Recorded footsteps (Footsteps – Essentials): ten sand takes for the ash road and ten wood takes for the mill replace
  the two of each; every take of a surface is used in turn.
- Sword sounds (SwordSoundPack): swords swing and hit with six takes each (sorted by measured attack: slow, noisy rise
  for swings; hard onset for hits; leading silence trimmed); swings and hits cycle on their own. The staffs and every
  other weapon keep the existing swing and hit, so the two can be compared by switching weapons.
- Both packages held WAV recordings only, no scripts; nothing of them is in the repository.

## Checks

| Run | Code | Result |
|---|---|---|
| Core verification (GitHub `core`) | `158764d` | success |
| Gitar, Neural Hub contracts (GitHub) | `158764d` | success |
| Review comments (Gitar, Codex, CodeRabbit) | `158764d` | fixed in this commit |
| EditMode and PlayMode tests of this PR (`EditorToolPackagesTests`, `FittedArmourArtTests`, sword/step, performance log, `ProfiledVillageRoute`) | `158764d` | not run: the owner stopped all further tests on 2026-11-26 |
| Project Auditor over the project | – | cancelled: it scanned every pack and blocked the editor for over an hour counting shader variants; the owner had it aborted |
| Full EditMode and PlayMode suites | – | not run (owner stop) |

## Limits

- Snapshots, the performance log and the Mali counters are only meaningful on the tablet; the editor has no Mali GPU, and a
  snapshot of the editor process is not representative.
- The sword takes are sorted into swings and hits by measurement, not by ear; the owner may move any of them.
- ProBuilder is installed for later blockouts; nothing in the game uses it yet.
