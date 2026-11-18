# Owner decisions 2026-11-17 — weapon types and shields

Recorded verbatim in meaning from the owner's message of 2026-11-17 (German original in the session).

## Shield
- A shield is carried in the off-hand and cannot attack.
- It raises defence: with each shield-skill level the chance to block an enemy attack completely (0 damage) rises by
  **0.25 %**.
- While a shield is equipped the character can only equip **one-handed** weapons.
- With a shield the character moves **noticeably more slowly and heavily** than, for example, with a dagger.

## Every weapon type has its own character
- **Dagger:** shorter reach, faster attacks, higher critical chance, smaller total damage than e.g. a two-handed axe.
- **Two-handed axe:** heavier and slower than dagger or sword, very high total damage, lower critical chance than the
  dagger, larger reach; its strike is clearly slower than the dagger's.
- **One-handed sword:** the moderate middle of all weapon types.
- **Bow:** long reach, two-handed, more damage than the dagger but less than the sword; can miss its target more often
  than melee weapons; each bow-skill level raises its accuracy by **1.25 %**.

## Interpretation taken while implementing (not decided by the owner, can be changed)
- "Total damage" (Gesamtschaden) is read as damage over time including critical hits (damage per second), because
  attack speed differs by weapon type. The menu shows damage per hit and per second so both can be compared.
- Weapon types the owner did not describe follow the same scale: one-handed axe and two-handed sword between the
  sword and the two-handed axe; staff (two-handed, long range, slow) close to the bow but without misses.
- Melee weapons keep a 100 % hit chance; only the bow can miss.
- Bow accuracy: 60 % plus 1.25 % per bow level, at most 100 % (reached at level 32).
- The shield skill trains on every enemy attack received while a shield is equipped (blocked or not), at the
  learn-by-doing rate (1 XP per full 10 attack damage, at least 1). Like the weapon skills it grants a free stat point every
  5 levels; instead of +2.5 % damage it grants +0.25 % block chance per level.
- Movement: dagger 100 %, one-handed sword and bow 98 %, staff 97 %, one-handed axe 96 %, two-handed sword 94 %,
  two-handed axe 90 %, and −12 percentage points while a shield is equipped.
