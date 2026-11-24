# New crypt enemies with real attack, hit and death clips · 2026-11-24

Owner task of 2026-11-24: remove the ugly start enemies of the crypt and replace them with proper enemies from the
monster packs bought that day, which have better readable attack animations. Until now all three enemy kinds were
the same skeleton figure in three sizes; it only had a run cycle, a blow showed nothing but damage, a hit pushed the
figure optically and a kill toppled it like a board.

## What changed

### Figures

| Core kind | Game name | New figure | Triangles | Clips |
|---|---|---|---|---|
| 0 | Ghul | Necromancer Army – Ghoul | 12,148 | idle, run, attack, hit, death from the pack |
| 1 | Ritter | 3D Stylized Goblin with sword and shield | 30,146 | idle, run, attack, damage, death from the pack |
| 2 | heavy crypt boss | Scary_Evil, the horned demon | 15,999 | idle, walk, attack from the pack; hit and death from the licensed humanoid takes the hero uses |

- `CryptEnemyArtBuilder` (menu "ARE ARPG/Art/Build Crypt Enemies") builds the private, passive prefabs
  `EntryEnemy0..2`, each with its own controller: Idle, Move, Attack, Hit, Death. The full entry art build calls it
  per kind; a kind whose pack is not installed keeps the earlier skeleton.
- The packs are far brighter than the game. The builder bakes muted colour textures (lower saturation, darker,
  1024 px): the neon-blue ghoul becomes a pale corpse grey, the yellow goblin a dark olive, and the charcoal demon is
  lifted to ash grey because he was a black shape in the dark crypt.
- The demon ships with 224,729 triangles. A copy reduced to 15,999 triangles (Blender, weights kept) is used; a
  render comparison shows no visible loss at game size.
- Nothing of the packs is part of this repository: models, textures, clips, controllers and prefabs are private.

### Following the Core

`EnemyClipPresentation` asks the Core once per frame and never waits for an animation:

- **Attack:** when the Core starts an enemy's windup, the attack clip starts and the enemy turns to the hero. The clip
  is paced so that its blow lands when the windup ends. Under the old combat, which lands blows unannounced, the
  attack shows at once and hurries to its blow.
- **Hit:** a blow that gets through makes a free enemy flinch. An enemy that winds up does not flinch, so its body
  keeps announcing the blow the Core is going to land.
- **Defeat:** the death clip plays where the enemy stood, it lies still for 0.8 s and then sinks. The optical topple
  stays for figures without a death clip.

| Kind | Core windup | Clip second of the blow | Clip speed in the windup |
|---|---|---|---|
| Ghul | 5 ticks = 0.5 s | 0.47 s (claws meet) | 0.94 × |
| Ritter | 6 ticks = 0.6 s | 0.42 s (sword comes down) | 0.70 × |
| Boss | 9 ticks = 0.9 s | 1.00 s (backhand lands) | 1.11 × |

The contract between figure and game is small: states Attack, Hit and Death, the float `AttackSpeed` and the bool
`Dead`; the attack state's own speed is the clip second of the blow, so the game only divides by the Core's windup.

## Checks

Unity 6000.6.8f1; Unity CLI in the live editor, not a Director job. The Core is unchanged.

| Run | Code | Result |
|---|---|---|
| PlayMode `EnemyClipsFollowTheCoresWindupHitsAndDefeat` (new), all three kinds: attack starts with the windup and is at its blow when the windup ends (0.42 / 0.39 / 0.94 s of clip against 0.47 / 0.42 / 1.00 s, one frame of 0.05 s apart), the enemy faces the hero, no flinch in the windup, flinch when free, death clip upright in place, removal after lying still | `83227f3a5d632c9e4506b3b6b8148ba0c13fe880` | PASS |
| PlayMode `EnemyAttackShowsUnderTheOldCombatToo` (new): an unannounced blow shows the attack at once; a cooldown that only counts down starts none | `83227f3a5d632c9e4506b3b6b8148ba0c13fe880` | PASS |
| EditMode `BuiltEnemyFiguresKeepTheClipContract` (new): passive prefabs, five states with moving clips, parameters, attack pacing, Death only left by standing up, baked colour textures, at most 32,000 triangles | `83227f3a5d632c9e4506b3b6b8148ba0c13fe880` | PASS |
| PlayMode `EnemyFacesItsAcceptedCoreDisplacement` | `83227f3a5d632c9e4506b3b6b8148ba0c13fe880` | PASS |
| PlayMode with the private overlay `MeleeAnimationRequiresAnAcceptedCoreStrike`: stale since combat v2 (it expected the strike to hit in its first tick and a strike at the air to be rejected), brought up to it; the commits between the two revisions change only this test | `e7c4f92d650b6b0085c76c9e936b5a4a3c262b0b` | PASS |
| PlayMode with the private overlay (`ARE_ENTRY_EVIDENCE_DIR` set in the editor for these runs): `HitsReactOpticallyKillsToppleAndLevelUpsShine`, `DefeatedEnemiesStayFrozenAfterHitStop`, both brought up to figures with a death clip | `83227f3a5d632c9e4506b3b6b8148ba0c13fe880` | 2 / 2 PASS |
| Full EditMode suite | `dda2f387daf580e4f1f922df41458d55d30e3066` (the stacked figure block, which contains this one) | 285 total, 257 passed, 0 failed, 28 skipped; see [its acceptance](../dark-souls-figures-20261124/acceptance.md) |
| Full PlayMode suite with the private overlay | `dda2f387daf580e4f1f922df41458d55d30e3066` (the stacked figure block) | 213 total, 208 passed, 1 failed, 4 skipped; the one failure is the bow test that had been stale since combat v2, fixed there |

The builder ran in the live editor and reported: Ghoul 12,148 triangles with clips of 1.83 / 1.65 / 1.15 / 0.65 /
1.40 s (idle, run, attack, hit, death), Goblin 30,146 triangles with 3.33 / 1.25 / 1.25 / 1.25 / 1.25 s, Demon
15,999 triangles with 8.33 / 1.03 / 3.17 / 0.67 / 1.67 s.

`DefeatedEnemiesStayFrozenAfterHitStop` used to look at the animator a quarter second after the button, which can be
the very frame of the kill, inside the hit-stop. A toppling figure is frozen then and later, so that never showed;
a death clip must run once the hit-stop is over, so the test now looks 0.2 s after the kill.

## Limits

- The three figures now appear wherever these kinds do, also in the wood and on crypt floors, not only in the entry
  crypt.
- The ghoul and the goblin are not humanoid rigs: role marks (quiver, shield plate, crest) sit at a fixed spot of
  the figure and do not follow its bones.
- The goblin has 30,146 triangles and a skeleton of 218 nodes, the ghoul 343 nodes. Frame cost on the tablet is not
  measured.
- The blow seconds come from the weapon hand's speed peak and reviewed frames; movement was judged on still frames,
  not in motion and not on the tablet.
- The run cycles are not matched to the Core's walking speed; feet may slide.
- The new dark-fantasy interior from the prop packs is not part of this block.
- The owner's wish that the goblin drops his sword, shield and plank armour and the demon his horned helmet is a
  later block; it meets the rule that armour never comes from random loot.
