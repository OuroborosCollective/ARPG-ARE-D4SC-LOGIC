# Weapon skills in RuneScape style (2026-11-17)

Owner order: one learn-by-doing skill per currently droppable weapon type; weapon types level individually through
active use; every 5 levels one freely spendable stat point and +2.5 % damage for that weapon type; skills, levels,
stats and their effects (damage share per type, crit chance) visible in the HUD menu.

Code under test: commit `60a664b4a4a23e91aaaa507ee271a0d8c43b3b63` (tree `f16065eca1146a2cd3fc5f430b1b43037ba90697`) on
main `c839b68aaace5e14288f9969b956c3705ec3c033`. Rules and saving are described in `docs/LEARN_BY_DOING.md`.

## Decisions taken from the order (no new owner decision)

- "RuneScape style": classic experience table, levels 1-99, experience cap 200,000,000.
- Training rate: the existing learn-by-doing rate of the disciplines (1 XP per started 10 HP actually removed), so
  weapon skills and disciplines advance at the same pace.
- Weapon types: the five droppable types that attack (sword, dagger, axe, staff, bow). Mace and spear are not
  droppable. Shields are droppable but sit in the off-hand and never strike, so "+2.5 % damage" has no meaning for
  them; their rule is an open owner decision.
- The +2.5 % per 5 levels is additive to the existing damage shares and applies only while that weapon type is equipped.

## Evidence

The changed runtime and test files in the validation project were compared byte for byte (SHA-256) with the commit
before the Unity runs. Unity runs went through the live Unity 6000.6.8f1 editor of the validation project; saved
outputs are private on the VPS under `.local/share/are-arpg/weapon-mastery-20261117/runs/`.

| Run | Result | Output SHA-256 |
| --- | --- | --- |
| RED: Core suite before `WeaponMastery.cs` existed | build fails, CS2001 source file missing | `899cbd6285ef27d3fdbbb109cc4d7553252e985b19e7fcc9f0cc853bf01fc76f` |
| Core verification | 19899/19899 (19866 before; 33 new weapon-skill assertions incl. 9 hostile save cases) | `a71e2f845d2117ac464ae9642b3836b537c168e796c8724e7b3f44920991b552` |
| Python preflight | 40/40 | `e82692dddf64b6b80e9726e5314491cb0bdc69e4206a4c48c9e56237dd046160` |
| PlayMode `SkillsTabShowsWeaponLevelsStatPointsAndExactDamage` | 1/1 | `ac4e6a390d4c1c7361743b8bbe27c81d9b0263da7a5104080f24d710f4ff9b74` |
| PlayMode `MenuButtonsOpenModalMenusThatPauseTheCore` (all tabs, desktop and phone captures) | 1/1 | `0d1c118a3a289989a39d20ff5821fb3b5b7246b6bc24f7ed8fa3a20e39a7df0f` |
| EditMode, full project suite | 107: 83 passed, 0 failed, 24 skipped (private dungeon/bow/village assets missing in the validation project) | `4bfaff272ffd718496a6091ddcc380eb18a950b2064586d480bd817f0af74506` |
| PlayMode, full suite | 165: 151 passed, 14 failed, 0 skipped | `76867c32466b10b80714f4c6ba918dbc7e82b47e54033b64644fc4871842e1fd` |
| Baseline on unchanged main `507c0f4`: `EveryOwnedWeaponBindsAndInventoryChangesTheActualHandModel` | 0/1, same message | `0792dd7fea9b729af064d6ede40d4824e2d36c0844c12e361b87aa0750cad6df` |
| Baseline on unchanged main `507c0f4`: `MeleeAnimationRequiresAnAcceptedCoreStrike` | 0/1, same message | `9ef6fae275e9cc94f08a9614bfb9905294215170ae312f5c4f2ef2a6c5e1f3ab` |

The 14 PlayMode failures are not caused by this change: 12 are the failures already recorded for the validation project
on 2026-11-16 (missing bear, rattler and audio assets, timing-sensitive presentation checks, the hero dipping into the
raised road paving); the two weapon-model failures above fail identically on unchanged main. Two cases from the earlier
list (`MillRefugeKeepsCluesAndTravelAccessible`, `OutdoorBackdropFollowsTravelWithoutChangingTheCore`) now pass.

The skills tab was inspected in real captures at 1920x1080 and 2400x1080; a first desktop version cut off text, which
was fixed (split columns, best-fit text) before the runs above.

## Not verified

Android build and Tab A9, a full PlayMode run with the complete private asset set, GitHub Unity CI (skipped there,
no licence).
