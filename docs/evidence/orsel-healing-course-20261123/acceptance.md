# Orsel's healing course and Wundschluss · 2026-11-23

Issue #127 with the owner's Heilkunst draft of 2026-11-12 (option B, the full course): Orsel at the old mill teaches the
healing spell Wundschluss on combat v2. Learning stays classless: the lesson unlocks the spell once, practice comes only
from health a cast really restores, and the equipment decides how well it works.

## Owner decisions (2026-11-23)

- The three ways in cost **2 rations, 12 coins or 3 Aschenflieder**; all three lead to the same lesson.
- Wundschluss heals **180 plus the existing healing bonuses**, costs **15 focus** and cools down for **8 s**.
- Orsel hands over her own **one-handed staff** (80 %); the loot staffs are two-handed (100 %). Random loot stays as it is.
- Orsel teaches **after the camp is examined** (Ilyan's help revealed) and **before the beds**.
- Earlier the same day: armor lowers outgoing healing 100/75/50/25 % by material, weighted by slot; free healing on
  skill slot 2 is gone and its practice counts as healing mastery.

## What changed

- **Orsel at the mill:** she stands just west of the sick-bed shelter, at the head of the beds, clear of its posts and
  of the other offers' talking circles. Within reach the button ORSEL ANSPRECHEN opens her panel. Before the camp is
  examined she only says to look at what is missing first. After it she offers the three ways in; the help must really
  be handed over (rations, coins or Aschenflieder leave the pack), otherwise nothing changes. Then the lesson:
  Wundschluss is learned, her staff is handed over and taken in hand, and her old robe is worn when the chest is free.
  If she has already moved to the village before the player took the lesson, she teaches in front of the sick room
  once it stands and until the lesson is learned (while she still asks for its lot, that talk keeps the place to
  itself). Her figure, the talk button and the touch input follow the same Core answer (review fix on #131).
- **Wundschluss** (combat v2 only), button WUNDSCHLUSS on the outer ring left of AUSWEICHEN, key X on a desktop:
  - The button appears once the spell is learned and only then takes touches. It needs a staff in hand, 15 focus and
    somebody hurt within 4 m; otherwise it dims and the reason is shown when tapped. It shows the cooldown in seconds.
  - The focus is paid when the cast starts. The target is fixed then: the most hurt by share of health among the player,
    the companion close by and the patients on the mill's beds; ties go to the player.
  - 4 ticks windup, the heal on tick 5, 4 ticks recovery; a dodge may cut the recovery short.
  - Heal = floor((180 + resolve and practice bonuses) × staff × armor / 10^8), one rounding at the end. Staff and armor
    are read when it lands.
  - A blow during the windup cuts it short: no heal, no cooldown, no refund. The cooldown starts when the heal lands.
  - Practice goes to the healing mastery (the former guard practice) only for health really restored: nothing at full
    health, nothing for overhealing.
- **The patients** on the three beds have authored wounds (160, 220 and 110 of 300). A closed wound stays closed. The
  treated patient remembers the first treatment personally; Orsel sees it while she lives at the mill; nobody else
  learns of it.
- **Orsel's staff** is a fixed reward piece (regional relic `M_Heilerstab`, one-handed staff, borrowing the wanderer's
  staff model). It never lies in the world and is not part of the random weapon catalog, so loot rolls are unchanged.
- **Save:** the course has its own section `healing` (absent while nothing happened). The lesson and the staff belong
  together; a save with one without the other, or help before the camp, is rejected instead of repaired.
- The beds text no longer promises three lessons that did not exist; the character menu shows "Heilkunst" for the
  former guard practice under combat v2, and the switch message says so.
- Heals of one tick are not reported again after a defeat (like the impacts in #130).

## Tests

- `Tests/CoreVerification/HealingCourseTests` (78 checks): the course opens only after the camp and only in reach; each
  way in takes exactly its price, refuses without it and leads to the same lesson, staff and robe; the spell needs the
  lesson, a staff and focus; the heal lands on tick 5 with the owner's formula, then cools down 8 s; staff and armor
  factors; the most hurt is chosen; a patient's wound closes without overhealing practice and is remembered; a blow in
  the windup cuts it short without refund; save round trip and rejected tampering; the staff is no loot and lies
  nowhere; same inputs give the same world hash; the village fallback.
- `RegionalRelicTests`: the 23 world pieces keep their sources; the handed-over staff has none.
- PlayMode `OrselTeachesWundschlussAndThePlayerHealsAPatient`: the story path to the mill after the camp, then every
  step with real phone taps (open her panel, two rations, the lesson, close, WUNDSCHLUSS on the westmost patient), and
  the save holds the course, the staff and the closed wound.
- HUD layout: WUNDSCHLUSS stays on screen and clear of every other control on six canvases.
- The workshop catalog review leaves out the handed-over staff, which borrows a skinned staff model.

## Evidence

Unity 6000.6.8f1, Unity CLI runs in the live editor (not a Director job); Core with Unity's bundled .NET SDK:

- `Tests/CoreVerification` on `2c7b129`: ALL PASS 23,068 deterministic assertions, 79 of them for the course.
- Full PlayMode suite on code identical to `03fc2bb` (scripts and tests compared file by file): 179 passed, 0 failed,
  28 ignored.
- On `22bb7a7` (adds only the village rule and its Core check): `OrselTeachesWundschlussAndThePlayerHealsAPatient`
  passed in 38 s; again on `2c7b129` (the village spot ends with the lesson) in 37 s.
- Earlier on the same runtime code: HUD layout 12/12 (with WUNDSCHLUSS), camera rig 21/21, mobile UI 12/12, workshop
  catalog review 1/1, and the mill-chapter player run under combat v2 passed in 270 s (1421 frames, 0 defeats, 75 taps).
- Full EditMode suite on `22bb7a7`: 254 passed, 0 failed, 28 ignored.
- Review frame from the PlayMode test (Temp, not committed): Orsel stands west of the shelter next to the hero, who holds
  her staff; shelter posts, roof and a patient's bed on the right.

## Not verified

- Orsel speaks in text only; the draft's spoken lines with subtitles need recordings.
- Her staff borrows the wanderer's staff model; her own model and her figure in the dark-fantasy look are the next step
  (owner remark of 2026-11-23).
- Phone feel, the balance of focus (it returns only through landed strikes) and healing villagers or story NPCs other
  than the companion and the mill's patients.

