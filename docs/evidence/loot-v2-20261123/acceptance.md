# Loot revision 2: fewer weapons, four tiers, supply bags · 2026-11-23

Issue #48, block 2 (Beute v2). The owner's tablet report of 2026-11-15 said the loot was too generous: the first
enemies already gave blue and yellow weapons.

## Owner decisions

- **2026-11-15, variant A:** a new catalog revision; not every enemy drops; tiers white, blue, yellow and orange; the
  better tiers come more often with depth, area and bosses; legendary only from bosses or deep in the cave, with a
  weapon aura; existing items stay.
- **2026-11-17, variant C:** supply bags are a visible, glowing drop of their own with coins, separate from the weapon
  chance; Mara's ration task stays reachable; each bag is taken exactly once.
- **2026-11-23, weapons "Mittel":**
  - Chance of a weapon: common enemies 25 %, elites 45 %, champions 75 %, bosses always.
  - Tiers on the surface: 65 % white, 28 % blue, 7 % yellow. Each cave or crypt depth shifts them, up to 30 / 45 / 25.
  - Bosses: 35 % blue, 50 % yellow, 15 % orange. Deep champions: 2 % orange.
- **2026-11-23, bags "every fourth enemy":**
  - A bag from 25 % of the enemies, whatever the weapon roll says.
  - Coins: common 3–8 copper, elite 8–20, champion 20–50, boss one silver.
  - Every third bag also holds a ration; a boss bag always does.

## What changed

### Weapons

- The game now runs on catalog **revision 2**. A kill may drop no weapon at all. The kill still counts, and its kill
  number is still used up.
- Four tiers. Their names in the menu are Gewöhnlich, Magisch, Selten and Legendär; their colours are white, blue,
  yellow and orange.

  | Depth | White | Blue | Yellow |
  |---|---|---|---|
  | 1 (surface, road, wood, first floor) | 65 % | 28 % | 7 % |
  | 2 | 53 % | 34 % | 13 % |
  | 3 | 42 % | 39 % | 19 % |
  | 4 and deeper | 30 % | 45 % | 25 % |

- Crypt floors now count as depth; before, every floor rolled as depth 1. The cave already counted its floors.
- **Legendary** falls only from bosses (15 %) and from champions at depth 3 or deeper (2 %, taken from yellow). The
  rattler drops like a boss.
- What a tier means for the weapon:
  - white: no affix;
  - blue: one affix;
  - yellow: two affixes;
  - orange: two affixes, each half again as high.
- The knight who carries the field pauldron always drops, because the pauldron rides on his drop.
- The permanent weapon points of a receipt stay 8, 17 or 34 by the enemy's kind.

### Supply bags

- A bag falls from every fourth enemy and from every boss. It lies where the enemy fell, a step beside a weapon from
  the same kill.
- A character's **first three kills** always leave a bag with a ration. This is the documented balancing for Mara's
  task: the entry crypt has three enemies, so a new character reaches the camp with three rations, as before. Mara
  asks for two.
- Rations now come from bags and chests. A revision-2 weapon brings none; before, every pickup brought one.
- Bags are taken with the same pickup as weapons; weapons within reach go first. Coins and rations are credited once.
- Weapons or bags on the ground keep the ways out of the area closed, as weapons already did.
- Bags live in the save (section `supplies`), so they survive a reload. A restart of the same area finds them again;
  another area, or a rebuilt cave, crypt or wood, starts empty.

### Old saves and old receipts

- Revision-1 receipts load unchanged and keep their weapons and affixes. Picking one up still brings its ration.
- A save without bags has no new section, so old saves stay byte-identical until the first bag falls.
- A world that is given revision 1 (the Core tests' fixtures) drops as before, with the same world hashes.

### In the scene

- Bags show the supply sack model and send up gold sparks.
- Weapons on the ground send up sparks in the colour of their tier. A legendary weapon has more and larger sparks, on
  the ground and in the hero's hand.
- All sparks share one particle system limited to 96 particles. With reduced effects only legendary weapons keep a
  faint glint.
- The pickup text names what was taken: the tier of a weapon, or the coins and ration of a bag.
- The minimap marks bags like other loot, and the loot button lights up near a bag.

## Tamper checks in the save

A weapon receipt and a bag are both regenerated from the identity of their kill when a save loads. These are rejected:

- a receipt with a raised tier, with flags that do not fit the enemy, or for a kill that dropped nothing;
- a fuller bag, the same bag twice, a bag of a kill the character never made, an unknown version or area.

## Limits

- Drop rates were checked by counting many generated kills, not by play time. How scarce weapons and rations feel over
  a longer session is unjudged.
- Rations are much rarer than before (about one in twelve kills instead of every pickup). The companion eats rations,
  so his upkeep may need a look.
- The coin models from the private asset registry are not used: their provenance and import are not shown yet. Bags
  show the existing supply sack.
- Not checked on the tablet.
