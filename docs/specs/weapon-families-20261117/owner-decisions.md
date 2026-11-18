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

## Staff (owner decision 2026-11-18)
- The staff is the **magic focus**: it never misses, strikes slowly and reaches far, and while it is carried **Ruin**
  (the spell) is stronger. Chosen from three proposals (magic focus, melee combat staff, elemental staves).
- Implementation value (not set by the owner, can be changed): Ruin gains **+25 %** additive damage share with a staff
  (`WeaponProfiles.StaffRuinFocusBp`, profile revision 2). Strikes (Klinge, Wucht) are unchanged.

## Menu tab WERTE (owner decision 2026-11-18)
- The owner asked why the skills menu was so full of text and not split or compact like the stat windows of other
  MMORPGs (e.g. Silkroad), and chose a **separate tab WERTE** from three proposals (separate tab WERTE, the same sheet
  inside the CHARAKTER tab, a scrollable FÄHIGKEITEN tab).
- WERTE is a compact sheet of short key/value rows ("DPS 548", "Krit 19 % ×1,5", "Treffer 100 %", "Block 5 %",
  "Fokus Ruin +25 %"), Macht/Vitalität/Entschlossenheit with + buttons; an explanation appears only when a value is
  tapped. FÄHIGKEITEN lists only the six weapon skills.
- Implementation choices (can be changed): shortcut **U**; damage rows show "normal / critical" against a ghoul, the knight
  and the armour rule are in the tapped detail; the tab bar uses a 13 px font when seven tabs share a 960-wide canvas.

## Interpretation taken while implementing (not decided by the owner, can be changed)
- "Total damage" (Gesamtschaden) is read as damage over time including critical hits (damage per second), because
  attack speed differs by weapon type. The menu shows damage per hit and per second so both can be compared.
- Weapon types the owner did not describe follow the same scale: one-handed axe and two-handed sword between the
  sword and the two-handed axe; staff (two-handed, long range, slow) close to the bow but without misses (confirmed by
  the owner on 2026-11-18, see above).
- Melee weapons keep a 100 % hit chance; only the bow can miss.
- Bow accuracy: 60 % plus 1.25 % per bow level, at most 100 % (reached at level 32).
- The shield skill trains on every enemy attack received while a shield is equipped (blocked or not), at the
  learn-by-doing rate (1 XP per full 10 attack damage, at least 1). Like the weapon skills it grants a free stat point every
  5 levels; instead of +2.5 % damage it grants +0.25 % block chance per level.
- Movement: dagger 100 %, one-handed sword and bow 98 %, staff 97 %, one-handed axe 96 %, two-handed sword 94 %,
  two-handed axe 90 %, and −12 percentage points while a shield is equipped.
