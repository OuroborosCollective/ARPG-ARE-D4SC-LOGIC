# Real combat clips for the hero · 2026-11-24

Issue #48, block 3 (Kampfqualität). Until now the hero had real clips only for standing, running and striking. Guard
and cast were poses generated from the idle stance; dodge, flask, getting hit and defeat had no body motion at all,
only particles.

The issue asks for real, compatible clips from licensed assets that are already here (no purchases), with transitions
and timing coupled to the Core's accepted events, and without displacing the visual knockback, the level-up, the sound
or the camera.

## What changed

### Clips

All six come from the licensed animation starter pack that already supplies the hero's idle, run and strike.

| State | Take | Length | Used for |
|---|---|---|---|
| Guard | BlockingLoop | 0.67 s, loop | the guard, for as long as the Core holds it |
| Cast | SpellCast | 2.20 s | Wundschluss and the ruin storm |
| Hit | GetHit | 0.67 s | a blow that gets through |
| Death | Death | 1.67 s | defeat |
| Dodge | RollForward | 1.53 s | the dodge |
| Drink | Buff | 1.53 s | the flask |

- `EntryCombatArtBuilder.AddCombatClips` (menu "ARE ARPG/Art/Add Hero Combat Clips") copies the takes into the game's
  clips and wires their states into the existing hero controller. The full entry combat build uses the same step when
  the pack is installed.
- The builder accepts only a moving humanoid take of the exact name. Without the pack the generated guard and cast
  poses stay as the fallback, under their old names.
- The built clips and the controller are private derived art and are not part of the repository.

### Following the Core

`HeroClipPresentation` asks the Core once per frame and never waits for an animation:

- **Dodge, flask, Wundschluss:** a new Core action starts its clip. The clip plays at the speed that makes it span the
  action: the roll 0.8 s, the flask 0.9 s. Wundschluss lasts 0.9 s; its clip is limited to 2.2× and takes 1.0 s. The ruin storm's cast spans its
  1.5 s.
- **Guard:** up while the Core's guard action runs, down as soon as it ends. Under the old combat the guard shows for
  0.6 s.
- **Hit:** when the hero loses health while he is free. During an attack, a dodge or a raised guard he does not
  flinch, so the body never contradicts the action the Core is still running.
- **Defeat:** he falls and stays down. No queued strike gets him up; a new run does.
- On a dodge the hero stops holding his last target and turns into the roll.

A hero controller without the new states (no private overlay) behaves as before.

### Tests brought up to combat v2

Two hero-action tests that only run with the private overlay still expected the old combat and had failed unnoticed:

- Slot 2 is no ability under combat v2 (the guard is held), so the "every ability shows an action" test skips it there;
  the new test covers the held guard.
- Combat v2 runs one action at a time, so the "same tick" test expects the accepted strike, not two.

## Checks

Unity 6000.6.8f1; Unity CLI in the live editor, not a Director job. The Core is unchanged.

| Run | Code | Result |
|---|---|---|
| PlayMode `HeroClipsFollowTheCoresActions` (new): licensed takes in the controller, roll and flask at the Core's pace, flinch, held guard, defeat stays | `ae194be53a197c8269b0baa8e92fdf0766937724` | PASS |
| Full EditMode suite | `ae194be53a197c8269b0baa8e92fdf0766937724` | 254 PASS / 0 FAIL / 28 ignored |
| Full PlayMode suite | `ae194be53a197c8269b0baa8e92fdf0766937724` | 183 PASS / 0 FAIL / 28 ignored |
| Seven hero and companion animation tests that need the private overlay (`ARE_ENTRY_EVIDENCE_DIR` set in the editor for these runs): `EveryExecutedAbilityShowsAVisibleHeroAction`, `SameTickAbilitiesEachShowAnAction`, `TargetFacingHoldsWhileRunningDuringTheAction`, `HitsReactOpticallyKillsToppleAndLevelUpsShine`, `DefeatedEnemiesStayFrozenAfterHitStop`, `RoadBearReactsToHitsOptically`, `HunterBowShotMovesTheHumanoidArmsAndReturnsWithoutChangingCoreOrSave` | the commit before `ae194be53a197c8269b0baa8e92fdf0766937724`, which only adds the reset for a rebuilt hero | 7 / 7 PASS |

The builder step ran in the live editor; the controller then held Guard, Cast, Hit, Death, Dodge and Drink with the
takes and lengths of the table above, the bools `Guarding` and `Dead`, and the speed `ActionSpeed`.

## Limits

- The pack has no drinking motion. The flask plays the "Buff" gesture (arms spread), which reads as gathering strength,
  not as drinking.
- The clips span the Core's action as a whole. The exact frame of a release (the spell leaving the hands) is not
  matched to the Core's effect tick.
- Looks and feel were checked on five still frames (roll, flinch, flask, guard, defeat), not in motion and not on the
  tablet.
- Only the hero. The companion and the enemies keep their current motion.
