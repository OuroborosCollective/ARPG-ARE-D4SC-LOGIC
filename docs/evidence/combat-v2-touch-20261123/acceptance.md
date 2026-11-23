# Combat v2 in the game: touch controls and the switch · 2026-11-23

Issue #127, second step: the Core foundation of #129 becomes playable. New games start with combat version 2, an
older save moves over once, and the phone gets the controls the issue asks for.

## What changed

- **Switch:** in the entry profile a new game starts with combat version 2. An existing save is moved over once, at
  load, and saved right away. The player sees one message that names the new controls and says that free healing is
  gone. Other test scenes and the legacy profile keep combat version 1.
- **Controls** (one routing for touch and mouse):
  - SCHUTZ (skill slot 2) is **held**: the finger that touched it guards until it lifts. On a desktop the mouse button
    or key 3 does the same.
  - AUSWEICHEN is a new button on an outer ring left of the skill arc; it dodges along the stick, or backwards without
    one. Desktop: space.
  - TRANK is a new button above it with the number of heal potions. Desktop: Q.
  - Every panel or scene that takes the input lowers the guard.
- **HUD:** a thin stamina bar between health and focus. The dodge button greys out below 25 stamina. The NPC dialogue
  card now ends 340 units before the right edge so that the dodge button never lies on it.
- **Presentation:** the swing animation and the swing sound start with the windup; hit reactions, hit stop, camera
  shake, Ruin's shockwave and the impact sound come on the Core's impact tick. Guard raise, blocked blows, dodge and
  the flask's effect have their own small effects. A flask that took effect is saved at once.
- **Tests:**
  - Fixtures that used a skill and checked its result in the same breath now let the Core run to the impact
    (`Resolve`).
  - The crypt fixture puts the enemies next to the hero on the Ruin's last windup tick. A ghoul at one hit point may
    choose to flee during the six windup ticks, which the instant legacy Ruin never allowed. This alone caused 27
    failures that showed only in the full suite.
  - The bear rescue rolls out of the red swipe, keeps the bear busy with short swings and casts Ruin only when its
    windup ends before the bear can strike again.
  - The Rattler fight keeps stamina for a roll, waits for the beast's recovery and attacks while it has no fix on the
    player.
  - The player-run test does the same with real taps on AUSWEICHEN and TRANK.
  - The HUD layout test now keeps dodge and flask on screen and clear of every other control on six canvases
    (720×405, 960×540, 1007×601, 1334×750, 2400×1080, 2560×1600). It found the overlap with the dialogue card.

## Evidence

Unity 6000.6.8f1, Unity CLI runs in the live editor (not a Director job):

- Full PlayMode suite on the content of `09790f9`: 177 passed, 1 failed, 28 ignored. The one failure was a test that
  still expected the save format without sections (combat v2 adds the section `combat`); fixed in `acdbed0`, then
  that test passed alone.
- On `d8f4ecc` (dialogue card): HUD layout 12/12, camera rig 21/21, mobile UI 12/12, the ration and Mara dialogue tests
  4/4.
- Full EditMode suite on `d8f4ecc`: 254 passed, 0 failed, 28 ignored.
- Player run under combat v2 on `d8f4ecc`, real taps only: passed in 268 s (1421 frames, 0 defeats, 75 taps, dodges
  against the bear, the hunter survived; the app was reopened after each passage and the combat version survived every
  restart).
- Bear rescue fixture under combat v2: passed (the player dodges and the hunter lives).

## Not verified

- A phone in hand: whether the hold, the dodge button and the flask button feel right needs the owner's test on a
  Play build.
- Dodge and flask animations: there are no clips yet; the hero slides and small particle effects mark the moment.
