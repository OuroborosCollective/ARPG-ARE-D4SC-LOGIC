# Weapon types, shield and bow; review fixes; Unity tests red on main repaired (2026-11-17/18)

Owner decisions of 2026-11-17 (`docs/specs/weapon-families-20261117/owner-decisions.md`): every weapon type has its
own character, shields sit in the off-hand with their own block skill, bows can miss until their skill rises. The
owner then asked to fix every Unity test that was already red on main inside this PR.

Code under test, on main `c839b68aaace5e14288f9969b956c3705ec3c033` after `8906365` (first round of PR #59):

| Commit | Content |
| --- | --- |
| `0262d1ccb0618b8fc0f8d89feed3289654064e15` | weapon profiles, shield and bow rules; fixes for the Codex and CodeRabbit findings |
| `788c064` | merge of CodeRabbit's boundary tests `c29f0e7`, adapted to the sixth skill (shield) |
| `27e8c9b6bfa7b5ff5bde38e6b5996dfcfa178f02` | repairs of the Unity tests red on main |
| `c2364e43092a99c9f71e1a8ee066f1042638ebb1` | incomplete character art is never shown |
| `7f834a80ed8b716f2e79f2dcef181e54af1cec5c` (tree `a449fcd935906ad654b2e72d30a3cef7ed7f534c`) | camp and arrival flows at a fixed frame time |

Before every Unity run the validation project's files that differ from `c839b68` were compared with the commit
(SHA-256 per blob; 36 files at `7f834a8`, no mismatch). Runs went through the live Unity 6000.6.8f1 editor of the
validation project. Outputs are private on the VPS under `.local/share/are-arpg/weapon-families-20261117/runs/`.

## Review findings, each red before the fix

| Finding (reviewer) | RED before → GREEN after | Output SHA-256 RED / GREEN |
| --- | --- | --- |
| Rank total can wrap around (Codex, CodeRabbit) | Core `wm-rejects-rank-sum-that-wraps-around` → Core 19978/19978 | `810b9b6786216e97c8e34d512fdd15e2576f81ecfea37509a1a150986c1258b5` / `cf696648adf9776bf8eeb8d57dc9dc796351cec0e67344a037efe08535f4675b` |
| Saved health validated before weapon Vitality (Codex P1, CodeRabbit), woodland | Core `wm-healed-weapon-vitality-woodland-roundtrip` → same run | `ff052f2c70a0fd6ace81a6bcf5454086f00eb6adbe80be5f4688e56d4e0f5dfa` / as above |
| … road checkpoint (old loader restored for this run) | Core `…-road-roundtrip` | `b47847032141659fe2dcca4bb5e599230617876cb8cb3cbf4bad62e96d668da3` / as above |
| … journey checkpoint (old loader restored for this run) | Core `…-journey-roundtrip` | `fe9b86c63528e1d321b7edb41349ed10f0c4f54ca47d2b4d840d128fb1e26248` / as above |
| AC21 journey checkpoint fell back to an older backup (Codex) | PlayMode `CorruptedWeaponSkillJourneyCheckpointNeverFallsBackToAnOlderBackup` 0/1 → 1/1 | `d904edb5884486904c1a729b784f0b8361cb0df7b384c133465cd7201bc2f5d0` / `eeec4516209c8b475f491780678e491b9f2821fa0fce23757b04d78e16d5c49f` |
| Weapon level-up without a kill not saved (Codex) | PlayMode `WeaponLevelUpFromANonKillingHitIsSaved` 0/1 → 1/1 | `5c2c5a5a7e081238cecb2b72951cd88964bb8d1974971c0a1f66b94c94702164` / `2831947c8cf19fc4ec8feef94dcf1402665ceae9e7c8cf5e14dcbd23aac40a7b` |
| Shield level-up from an incoming attack not saved | PlayMode `ShieldLevelUpFromAnIncomingAttackIsSaved` 0/1 → 1/1 | `43fac768e7a11af024e51a518513f048d6aef84c3571a845396dca25a78fca10` / `45814f634e1f5d2d0d180ca9449eaf2f777b2c18285b25dc3a3c6700696059ef` |
| Armour note showed the raw armour value (Codex) | PlayMode skills tab 0/1 → 1/1 | `1030e0e0b6abf7b646f9b72a9d5613ea23cd8427b16e33a59a832901c8f77fb5` / `7a59e70c74576f5745aa306dea74e49e885f86dcc0a21f2b1b07a2cd67193a0d` |
| Neighbour: AC20 fallback | 1/1 | — / `3a312c3af1e5cfb2e3ead5a97a201f4c35b12f1548225720bd55eb11ff89f68b` |

CodeRabbit's documentation findings: the training rule now states the floor (1 XP per full 10 HP, at least 1), and
the baseline of both rounds is bound to `c839b68` (next section).

## Unity tests red on main

The first full run on `0262d1c` had 17 failures (PlayMode 169: 156/13,
`7e2bab1e32db85edb08fdb497d7b13d7e0d8fca991823ea334d558ae30c7a111`; EditMode 107: 102/5,
`18156cd3a79cf3815f73c9c0e3351f6c2b1a65192c3967d96d2b0cdacc3e1699`). Each was run alone on `c839b68` in the same
validation project and failed there with the same message (`8444113d2d3c55c8a187eeed60207ae8a93bd6dfbb87731c8f3ef793ffebc581`).
The hero-action test was flaky on the head (1 of 8 single runs red) and passed 5/5 on main. Then:

1. Missing private assets in the validation project: bear, rattler, audio library, starter-pack bow clips and the
   combat-art plan were copied from the authoring workspace (338 files, nothing overwritten, list
   `b7de72d89a674e783aa153fc8c876a6392babf9bfd1f7fa6aae5eb5604e2b56b`). 11 failures pass with them
   (`6995149d2809101f0152d4d057debd244bf469f170d7fe3c67cc9e44117e30d9`). Not a repository change.
2. Hero inside the village paving: the Core village is flat, the private road paving reaches 0.111 m. Actors now stand
   on the visible walkable art. RED "Wanderer sinks 0.11 m below FloorPanel2x1_LOD00" → GREEN 1/1
   (`94046af0c4feb2590e89eb160fba75ec051c28ba25d7c8fbf56de4c70b47b208`); road surface 0.020 m above ground as required.
3. Authoring lost the 128 m pilot profile: the profile held its 4 x 64 m defaults right after `CreateProfile` because an
   import reloaded the unsaved fresh asset. RED "Pilot map dimensions differ" → GREEN 1/1
   (`8087ef15eca2c0915d486713cf8c04eacf39da318bf7bfe06cbb5197d6b4d6ad`).
4. Five presentation tests sampled 0.28–0.6 s effects once per rendered frame, while the validation editor renders at
   4.9 frames per second (measured with a temporary probe; worst frame 0.234 s). They now run at a fixed device frame
   time of 1/60 s per frame with unchanged thresholds. With the test change alone 4/5 passed; the arrival scene stayed
   red because its readable-line guard used real time. With that guard on the game clock: 5/5 in two runs
   (`1fa8cb475927ee8b1cc4d0520ac648c79867f44f68318bc31000934320bc63f4`).
5. The second full run (`27e8c9b`: EditMode 107/107, PlayMode 169: 168/1,
   `1e3fd398a6f81010f7ba2c7a7730b20e0e231709ca8f4944f4184ae90ba16ab1`) exposed a real art defect that the missing
   model had hidden: Dorn's private prefab references four body meshes that the imported vendor model does not contain.
   The binder now rejects a character with an unresolved skinned mesh and shows the complete fallback. RED EditMode
   `IncompleteCharacterArtTests` 1/2 (`95aae809fe8b337158b8a9447615bdcbc40dfd081300bd478ec759e54883adfa`) → 2/2
   (`958a0d29bfe69702b06dbb2aa059fff70251bbc329645fce855b81bb0f875e93`); PlayMode
   `PrivateVillageActorsAllContainSkinnedBodies` 1/1 (`efe7cb40737b4bc1ec20d0b5dfbe53683f50c320faa77571f4c70412098b2f30`).

6. The third full run (`c2364e4`: EditMode 109/109, PlayMode 169: 167/2,
   `cb33e83a070622ed7123dc2caa8946bd69684816f13351d6c0b56f2f2f987c35`) had heavy editor stalls (one test hung 990 s). Two more tests mixed clocks: the
   arrival-input test waited in real time while the readable-line guard counts game time, and the theft confirmation
   window (3 s, real time) sat close to the 12 frames between the two taps at 5 frames per second (the earlier
   "7 instead of 8" failure). Both tests now run at the fixed device frame time and wait in game time; the confirmation
   window uses the game clock. RED: the input test with the device clock but real-time waits fails with the same
   assertion as in the full run (`4a0c1aad61397729337e4019f69829dc7ecd7bce0b1e174025056d370744a3ac`); GREEN: the four camp and arrival tests in two
   rounds, 8/8 (`a314c8fabf4f0e91fa90b7bc43bb29377bd86f61aec6cd1050860e6a0a6946c7`). The scene walks wait in game time with
   0.25 s steps because the village renders at 2.0 frames per second here (measured; worst frame 2.29 s), where 1/60 s
   steps would exceed the test timeout. A modal "Scene(s) Have Been Modified" dialog for the test runner's scene,
   left by an interrupted run, was answered with "Don't Save".

## Final runs on `7f834a8`

| Run | Result | Output SHA-256 |
| --- | --- | --- |
| Core verification | 20903/20903 (19978 plus CodeRabbit's boundary tests) | `3113ea931239a5f0e2024972f8b5d296b8bdc978c44f8060279822104a08bfa5` |
| Python preflight | 40/40 | `e753e77a61fa2e714d9a5cb14371ad482a1a39757c5e6dc00479d297f8debf4e` |
| EditMode, full project suite | 109/109, 0 failed, 0 skipped | `2ca56ed9917838ee16313ea4e1f56f1967a539b42e0243514f3f1b8f4470bd58` |
| PlayMode, full suite | 169/169, 0 failed, 0 skipped | `d157f2e26cfd0beec4a8db8fb0c62ea4ef304a2062f7f55a23c32c2126726418` |

## Not verified

Android build and Tab A9 (optional per policy), GitHub Unity CI (skipped there, no licence). Dorn's private model
itself still lacks four body meshes; the game shows the complete fallback until that art is re-authored. The private
assets copied into the validation project are not part of the repository.
