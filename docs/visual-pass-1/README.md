# Ashen Crypt — Visual Pass 1

Code 24c4fdd13f896afd18c54bc342aa3ab8ae483937, baseline 193c1466e9256185b6980f9ee6a62c3853676813.
Unity 6000.6.8f1 and existing URP pins are preserved.

The outdoor sky projects the existing persisted Forge tick: 14,400 ticks make one
24-minute visual day. Sun, moon, moving clouds and stars are presentation only.
The one existing directional-light proxy switches to moonlight at night and retains
a minimum elevation at twilight for ground/actor readability. The visible solar disc
keeps its actual phase direction. Ambient SH state is restored at disposal; rebuilding
an area rebinds the new light, and interiors retain their authored light/fog.
Distant unlit landscape brightness follows the daylight phase instead of staying bright.

[Actual RED/GREEN acceptance and limits](../evidence/quality-sky-20261116/acceptance.json).
Six native PlayMode cases and one shader EditMode case passed on the same code SHA,
without skips. Four native test-camera phase images were inspected privately.
These are explicit test fixtures, not screenshots of an owner production session.
40 preflights pass; 19,739 Core assertions cover byte-identical Core/test sources.

[Seven selected material surfaces](materials-20261116.md) use fourteen original
textures from Terrain Sample, Lighting Optimisation and Viking Village. Private material
GUIDs are preserved; Android texture overrides use at most 1024 and ASTC 6x6.
Six Viking source prefabs have a real native import check (no missing mesh/material/script),
but their placement and artistic integration are still open. Source archives, raw Store
assets, private recipes and rendered evidence stay private.

## Next work

- Village hero composition: coherent Viking props/selected architecture, grounded
  placement and protected interaction routes, actual before/after views.
- Forest/mill terrain and vegetation polish, then cave combat readability.
- Reflection/light-probe authoring where it improves real scene images.
- Android build and physical Tab A9 profiling; no FPS, RAM or thermal claim yet.

The existing Fable HUD/camera, saves and 100-ms authority are retained. No new Unity
package, full sample-scene import, merge, release or complete Quality Epic #48 acceptance.
