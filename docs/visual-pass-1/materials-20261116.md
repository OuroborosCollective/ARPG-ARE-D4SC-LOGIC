# Visual Pass 1: selected materials, 16 November 2026

Material-authoring code: c72b2859ec91a081b18c550b633fead0f3fc9e04.
Game baseline: 193c1466e9256185b6980f9ee6a62c3853676813.
The existing uncommitted sky implementation is preserved separately in the same worktree.

| Existing surface | Selected licensed source |
| --- | --- |
| VillageSoil | Terrain Sample: Soil_Rocks |
| OutdoorGround | Terrain Sample: Grass_Moss |
| GrassVerge | Terrain Sample: Grass_Dry |
| RoadStone | Terrain Sample: Pebbles_A |
| MillWood / MillBoard | Lighting Optimisation: WoodTiled02 |
| WeatheredPost | Viking Village: prop_fence_02 |

The existing WorldTextureAuthoring builder now accepts a narrowly scoped private
recipe through ApplyVisualPassOne. Existing Apply/Audit entry points remain.
The seven generated URP/Lit surfaces and VillageGround binding retain their
existing GUIDs. Fourteen original texture files retain their source GUIDs.
Android overrides: maximum 1024, ASTC 6x6, mipmaps enabled, CPU readability off.
Raw Store content and the private recipe remain ignored and were not committed.

## Actual evidence and limits

Private evidence root: /var/lib/unityarpg/.local/share/are-arpg/visual-pass1-20261116.
- native-selected-materials-02/result.json: actual Unity 6000.6.8f1 exit 0,
  seven material references checked, fourteen textures audited; original prefs
  and settings restored, no remaining owned editor process.
- selected-materials-native.png: real URP material swatches inspected. Initial
  immediate capture was black; delayed editor-update capture with synchronous
  shaders and explicit lighting displays textured surfaces. This is an intake
  image, not a village/GameView acceptance.
- native-viking-import-03/result.json: six actual Viking prefabs loaded; six meshes,
  six renderers, ten material slots, no missing meshes/materials/scripts or error shaders.
  Exit 0, prefs/settings restored. This proves import structure, not scene composition.
- material-install-receipt.json: 49 files copied and independently compared;
  SHA256 bfbca317408cc46418db83f15bda7da005e05b487bb0034f1d18114663d4482d.
  Originals remain under before-material-install.
- core-resume.log: 19739 actual Core assertions including eight new sky cases.
  This covers the recorded dirty source checkpoint, not an invented clean HEAD.
- preflight-installed.log: 40/40 after actual private overlay installation.
- source-checkpoint.json and working-source.patch bind the uncommitted source.

Classification: builder = production/editor/effect; material overlay = projection/effect;
Core simulation and persistence owners are unchanged by this material delivery.
No new Unity package, gameplay authority, save schema, HUD or camera change.

## Remaining work

The later [sky acceptance](../evidence/quality-sky-20261116/acceptance.json) covers
native day/dusk/night/morning views and outdoor/interior/rebuild regressions.
Viking prefab placement, full village/forest/mill composition and
physical Tab A9 profiling remain open. The downloaded packages and rendered
swatches alone do not close Quality Epic #48. No APK, merge or device pass.
