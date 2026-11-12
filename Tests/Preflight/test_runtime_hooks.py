"""Static runtime-wiring checks for ArenaRuntime partial presentations, NOT Unity runtime tests."""
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RUNTIME = ROOT / "Assets/Scripts/Runtime"


def _sources():
    return {p: p.read_text(encoding="utf-8") for p in sorted(RUNTIME.glob("*.cs"))}


def _method_body(text, signature):
    start = text.index(signature)
    depth, i = 0, text.index("{", start)
    for j in range(i, len(text)):
        depth += {"{": 1, "}": -1}.get(text[j], 0)
        if depth == 0:
            return text[i:j + 1]
    raise AssertionError("unterminated " + signature)


class RuntimeHookTests(unittest.TestCase):
    def test_every_region_input_poll_runs_in_poll_inputs(self):
        sources = _sources()
        body = _method_body(sources[RUNTIME / "ArenaRuntime.cs"], "private void PollInputs()")
        polls = {m for t in sources.values() for m in re.findall(r"private bool (Poll\w+Input)\(ArenaHudLayout", t)}
        self.assertTrue(polls)
        self.assertEqual([], sorted(p for p in polls if p + "(layout)" not in body),
                         "region input defined but never polled: sneak/actions unreachable in play")

    def test_every_region_hud_draws_in_on_gui(self):
        sources = _sources()
        body = _method_body(sources[RUNTIME / "ArenaRuntime.cs"], "private void OnGUI()")
        huds = {m for t in sources.values() for m in re.findall(r"private void (Draw\w+Hud)\(ArenaHudLayout", t)}
        self.assertTrue(huds)
        self.assertEqual([], sorted(h for h in huds if h + "(layout)" not in body),
                         "region HUD defined but never drawn")


if __name__ == "__main__":
    unittest.main()
