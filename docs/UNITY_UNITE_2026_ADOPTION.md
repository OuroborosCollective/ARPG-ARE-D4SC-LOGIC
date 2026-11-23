# Unite 2026 Tokyo - Ashen Crypt adoption gates

Source: Owner-provided `Unite_2026_Tokyo_-_Roadmap_Final.pdf`, 68-page presentation, 2026-11-23. Roadmap slide 3 explicitly says release features and performance estimates can change. This file documents **candidate integrations**, not actual Unity runtime support or Android performance.

## Applied in this branch (tooling only)

- Keep pinned Unity Editor `6000.6.8f1`, URP `17.6.0`, game logic, saved data and the existing six-action Game Director unchanged.
- Pin official `com.unity.pipeline` to stable `1.0.0` in both `Packages/manifest.json` and `Packages/packages-lock.json`; Unity Registry version metadata was read separately and confirmed that the six declared package dependencies did not change from the previous `0.10.0-exp.1` lock.
- Preflight migration contract requires the stable version and checks lock-file parity, editor pin and CI pin.
- Do **not** add a second editor agent, MCP bridge, NPC/quest authority, network stack or ML inference source.

### Verified and unverified boundaries

- RED: the targeted migration test failed on unchanged `0.10.0-exp.1` because it required `1.0.0`.
- GREEN in an isolated VPS checkout: five targeted preflight assertions, 54 total Python preflights and `unity projects verify --strict --expect-editor 6000.6.8f1` (0 errors, 0 warnings; 1,254 scanned files).
- The official Unity CLI `unity pipeline install --project-path <isolated-project> --version 1.0.0` reported success and updated the manifest. The lock was changed separately **only** after comparison with actual Unity Registry metadata, because CLI install did not regenerate the lock without an Editor import.
- **NATIVE VERIFIED (source SHA `c64483baf42dbe69d6dc1267d0e83bf0668254a8`):** real Unity Editor `6000.6.8f1` imported and compiled the project with stable Pipeline `1.0.0` and genuine owner private assets; the lock stayed at `1.0.0`. EditMode 252 passed / 0 failed / 28 ignored, XML SHA256 `f396e61273c55724fa75fedaf560a82007ea6a1321155d1bca4d9850db5c13db`; PlayMode 177 passed / 0 failed / 28 ignored, XML SHA256 `0c2f7f717fbe1cfeed7e546f067b35171cf2edd7188a832690eee42b7a648a95`. Native tests used an isolated VPS checkout with imported local Unity-generated ProjectSettings; these were not committed. [GitHub Core CI run 45105548253](https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/actions/runs/45105548253) passed 22,930 deterministic assertions and 54 preflights on the same source SHA; separate Unity CI was **skipped**.
- **STILL UNVERIFIED:** Android APK/AAB for this PR, real Android graphics/FPS/RAM, tablet/device execution, and any of the deferred Unity 7 roadmap features. Do not count ignored tests as passed. The owner requested retirement of one invalid forced-overflow village-board test; this does not prove the UI's late-game scroll behavior.

## Deferred candidates, required gates (not installed)

| Roadmap pages | Candidate | Compatibility and acceptance |
| --- | --- | --- |
| 19-21 | NavWorld/jobs, ECS-compatible animation | Version/API compatibility first; preserve canonical 100-ms tick, Core NPC decisions, movement legality and replay. Benchmark actual NPC navigation and Unity visual animation separately. |
| 23 | Method Reload | Verify available API on pinned editor and IL2CPP; test on isolated project, rollback ability and no source/authority divergence. |
| 33-39 | Surface Cache GI, GTAO, fog, SSR, physical light and upscaling | Check current URP support and Android capabilities, including software fallback. Preserve the existing sky/ash look, reduced-effect profile and passiveness of visuals. Before/after native frame captures, render times and RAM on the target hardware; avoid enabling all effects simultaneously. |
| 40-43 | Shader Graph stencil, Graphics Kernel, neural texture compression | Only opt-in per supported platform and package; validate content size, shader compilation and visual diff. Unity demo benchmark figures are **not** Ashen Crypt measurements. |
| 10-14 | ECS-for-All, unified netcode, CloudBehaviours | Do not import as replacement simulation, save owner or network authority. Research a read-only projection adapter only if real evidence of scalability needs emerges. |
| 53-56 | UI Toolkit, fonts, localization | Protect current HUD/Fable ownership and Android SafeArea/touch input; do not replace UI without visual and input regression. |
| 58-66 | Android dependency, build profiles, NPU/Sentis | Evaluate only on current supported stable versions with licensing, APK/AAB signing, 16-KiB alignment, replay invariance, metadata and device profiling. Do not run NPU model inference in canonical gameplay. |
| 7-9, 67 | Unity 7/CoreCLR and upgrade auditor | Defer engine migration until stable release and separate reproducible compatibility branch; keep Unity 6000.6.8f1 as canonical pin. |

## Merge gates

1. Recheck exact PR HEAD, base SHA, director source SHA, repo state and independent current CI run.
2. Import and compile with Unity Editor `6000.6.8f1` in isolated workspace; re-resolve and verify `packages-lock.json` from actual Package Manager; compare to Unity Registry dependencies and committed lock.
3. Execute real Core verification, full preflight, Unity EditMode and PlayMode with no related failures; archive job IDs and XML hashes. Do not count skipped runs as pass.
4. Keep existing owner editor process and private licensed assets unchanged. Android build/check as separately required; optional physical tablet evidence must remain distinct.
5. Exactly one concise `Memory.md` entry and project evidence handoff *after* required native gates, then only merge a reviewed, green PR. No promotion based solely on this static checklist.
