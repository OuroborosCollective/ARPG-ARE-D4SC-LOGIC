# Player journey run · 2026-11-23

## Owner request

The pasted quality diagnosis said the chapter road (crypt, Aschengrenze, Aschestraße, mill) had never been played through in one go by a player. The existing journey tests arrange the world for each step (enemies teleported next to the hero and set to one hit point, Ilyan moved beside the player, arrival scenes completed by a call, travel invoked directly). They prove each piece, not that a person can get through.

## What changed

- `PlayerDevice` (runtime): the one place the game reads keys, mouse and touches. Normally it forwards to Unity's input unchanged. The ten runtime files that read input alias `Input` to it, so every control (stick, skill buttons, pickup, SPUREN journal, hunter button, arrival scene, defeat screen, menus, village panels) goes through it. A test may install a scripted device.
- `ScriptedPhone` (PlayMode test): a phone screen played by the test. One finger holds the floating stick, which has to start inside MOVE. A second finger taps points of the 960×540 HUD canvas, so each tap has to land on a visible button.
- `JourneyPlayer` (PlayMode test): looks at what the screen shows (positions, health, cooldowns, offered journal buttons) and answers only with the stick and taps. It walks with an A* route over the area's own Core collision (house doorways count as walls), fights with the four skill buttons, steps out of the bear's red swipe, taps *Aufheben* beside each drop, opens SPUREN and taps the offered story button, taps through arrival scenes and taps RESTART after a defeat. It never moves a figure, changes health or calls a game method. Travel, saving and the story run inside the game.
- `APlayerFinishesTheMillChapterWithStickAndTapsAlone` (PlayMode test): a new game in the crypt, then:
  1. crypt: three enemies, three drops, north passage;
  2. Aschengrenze: arrival scene, Ilyan's secret, north gate;
  3. Aschestraße: waymark, rescue the hunter from the bear, thank him;
  4. mill: the shelter;
  5. back south twice to the village and refill Mara's store (Protect if the rations were short);
  6. north twice to the mill and inspect the beds.

  After every passage the app is closed and reopened. Area, story stage, supply decision, carried items, rations, the hunter's fate and the standing position must come back from the save alone. A last restart in the middle of the mill checks that the finished chapter survives and that the game resumes at the area's checkpoint, where its passage put the player.
- The run uses a fixed device frame of 0.1 s (one Core tick per frame), so it is the same every time. It draws the 3D view into a 192×108 picture without the shadow map. The server renders without a graphics card (Mesa llvmpipe); at full size the same run took 54 minutes. Play, HUD hit tests, saves and every `Update` are unchanged.

## Evidence

Unity 6000.6.8f1, Unity CLI runs in the live editor (not a Director job), branch `feat/player-journey-run-20261123` on main `dac396ca3ecc816288e9948fda6783005d94497a`:

- `APlayerFinishesTheMillChapterWithStickAndTapsAlone` **Passed** in 210 s: 972 frames (97 s of game time), 0 defeats, 70 taps (5 on story buttons), 8 restarts of the app.
- The first run, at full picture size, completed the whole chapter. It failed only at the final restart in the middle of the mill, because the test wrongly expected the old standing position; the game resumes at the checkpoint. The check now expects the checkpoint.
- `PlayerDeviceTests` 2/2 (EditMode): a scripted device stands in for Unity's input until it is removed; every runtime file that reads input does so through `PlayerDevice`.
- Full suites on the tested revision: EditMode 254 passed, 0 failed, 28 ignored; PlayMode 206 tests, 178 passed, 0 failed, 28 ignored (31.6 min).
- After review the restart check compares every carried item by identity instead of counting them (CodeRabbit). The player run passed again on that final head in 192 s.

Trail of the passing run (frame · Core tick of the current area):

| Frame | Area | Event |
|---:|---|---|
| 38 | Ashen Crypt | three enemies defeated |
| 57 | Ashen Crypt | third drop picked up |
| 167 | Die Aschengrenze | arrived; app reopened |
| 231 | Die Aschengrenze | Ilyan: "Ich bringe Verwundeten heimlich Essen. Folge der Aschestrasse zur alten Muehle." |
| 305 | Die Aschestrasse | arrived; app reopened |
| 342 | Die Aschestrasse | waymark inspected |
| 420 | Die Aschestrasse | bear defeated |
| 426 | Die Aschestrasse | hunter rescued and thanked |
| 471 | Die alte Muehle | arrived; app reopened |
| 561 | Die alte Muehle | shelter inspected |
| 596 / 668 | Aschestraße / Aschengrenze | back south, app reopened after each passage |
| 745 | Die Aschengrenze | Mara's store refilled (Replace) |
| 843 / 915 | Aschestraße / mill | north again, app reopened after each passage |
| 970 | Die alte Muehle | beds inspected: chapter complete, Orsel offers her teachings |
| 972 | Die alte Muehle | app reopened in the middle of the mill: chapter still complete |

## Not verified

- A person playing the Play build on a phone. The owner's checklist covers that walk; it is **UNVERIFIED** until someone plays it.
- Frame rate, sound and readability on a device. The run checks controls, story and saves, not how the game looks or sounds.
- The other branches of the decision (Reveal, Protect) and the store and hidden crypt beyond the mill are not part of this run.
