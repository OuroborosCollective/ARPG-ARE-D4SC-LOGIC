# Combat v2 foundation and armor (Core) · 2026-11-23

Issue #127, phase 1 for the player, combined with Orsel's healing course as the owner asked. Core only: the game
keeps running the legacy combat until the next step adds the touch controls (dodge, guard, flask, stamina bar) and
switches new games and migrated saves to combat version 2.

## Owner decisions (2026-11-23)

- Build the #127 player combat core first, then Orsel's full healing course on top, then enemies and bosses, then the
  52 weapon and shield profiles.
- Skill slot 2 ("Guard", a free 170 HP self-heal) no longer heals: healing comes from potions and, later, Orsel's
  Wundschluss. The slot's practice experience is kept untouched for the healing course.
- Armor comes from crafting and rewards; random loot, its RNG streams and affixes stay as they are.
- Armor values: physical protection cloth 0 %, leather 15 %, chain 30 %, plate 45 %; outgoing healing 100/75/50/25 %
  (the owner's Heilkunst draft); slot weights chest 40, legs 25, head 15, hands 10, feet 10; everything inside the
  existing 60 % protection cap.

## What changed

- `CharacterProgression.CombatVersion` with its own save section `combat`. Version 1 is the legacy instant combat. A
  character switched once with `UseCombatV2()` keeps version 2 for good. A legacy save gets no new section, so its bytes
  and world hash stay as they were.
- `CombatActions` (player, version 2 only): one action at a time, counted in 100 ms ticks.
  - Attacks: strike, heavy strike and Ruin Storm with windup, one impact tick and recovery. The hit is resolved once, on
    its impact tick, through the existing damage, practice and loot code.
  - Weapon timing from the issue's archetype table, read from `Family` and `TwoHanded`.
  - Stamina: 100 maximum, 6 back per tick once five full ticks passed since the last spending.
  - Dodge: 25 stamina, 2.8 m over 8 ticks, ticks 2–4 untouchable, and it may cut an attack's recovery short.
  - Held guard: one tick to raise, 3 stamina per tick. It covers the front: a shield blocks fully within 120 degrees,
    a weapon parries within 90 degrees and lets 30 % through. The stamina cost is a quarter of the blow, eased by the
    shield level with the old block curve. When stamina runs out the guard breaks: half the blow gets through and the
    guard stays down for 6 ticks. Shield practice comes only from blows the shield really took.
  - Flask: 5 ticks lifting, effect and consumption once on tick 6, 3 ticks recovery. A blow while lifting spills the
    potion unused.
  - Movement: 4.6 m/s in a fight instead of 6.2 m/s, 40 % steering during windup, recovery, drinking and a raised
    guard, none on an attack's impact tick. Sneaking in the Rattler nest keeps its pace.
- `ArmorRules`, `ArmorCatalog`, `ArmorState`: five slots and four materials, 20 fixed pieces (among them the healer's
  robe), owned and worn pieces in the save section `armor`. Armor protection joins the existing protection sum. Its
  healing factor waits for Wundschluss. A malformed armor section rejects the save.
- Enemies that used to hit at once announce their swing under combat v2 (issue phase-4 start values):
  - ghouls and plain melee 5/1/11, knights 6/1/10, the heavy crypt boss 9/1/6;
  - the blow lands only within 2.1 m, so a step back or a dodge avoids it;
  - the attack rhythm stays as before.

  Without this, the first crypt fight under the new player combat was lost: the test player died against instant blows.
- World hash: combat v2 state and armor are mixed in only when present.

## Evidence

Real `Tests/CoreVerification` (Unity's bundled .NET SDK):

- **ALL PASS: 22,994** deterministic Core assertions. That is 64 new combat, armor and enemy-windup checks; all earlier
  ones are unchanged, including the legacy golden hashes.
- The new checks cover:
  - opt-in and legacy instant combat;
  - the impact tick and exactly one hit;
  - the archetype table for every catalogue weapon;
  - heavy strike and Ruin costs and timing;
  - stamina 0 to 100 in 22 ticks;
  - the dodge's untouchable ticks and distance;
  - guard angle, cost, chip damage, shield practice only on blocked contact, and guard break;
  - the flask's effect tick and spilling;
  - no free heal on slot 2 with practice kept;
  - armor protection and healing for full and mixed sets;
  - save round trip, fail-closed armor tokens, the one-way switch;
  - ghouls and knights winding up before they hit, and stepping out of reach avoiding the blow;
  - 200 ticks of identical inputs giving identical worlds.

## Not verified

- Nothing of this is visible in the game yet: the runtime does not switch any character to combat version 2, and the
  touch controls come in the next step.
- Enemy phases beyond the melee start values (ranged, elite, bosses, telegraph visuals), the 52 weapon profiles
  (phase 2), combos, poise and stagger are open.
- Balance: the values are the issue's and the owner's start values, not tested balance.
