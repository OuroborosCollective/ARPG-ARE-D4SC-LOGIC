using UnityEngine;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        private AshenAtmosphere _atmosphere;
        private bool _audioPanelOpen, _audioCreditsOpen;
        private TextAsset _audioCredits;
        private Vector2 _creditsScroll;
        public static Rect AudioToggleRect(ArenaHudLayout l) { return new Rect(l.Width - 198, 106, 190, 32); }
        private static Rect AudioPanel(ArenaHudLayout l) { return new Rect((l.Width - 480) / 2, 155, 480, 350); }
        private static Rect AudioOption(ArenaHudLayout l, int i)
        { var p = AudioPanel(l); return new Rect(p.x + 20, p.y + 35 + i * 50, 440, 44); }

        private void InitializeAtmosphere()
        {
            var go = new GameObject("Ashen atmosphere"); go.transform.SetParent(transform, false);
            _atmosphere = go.AddComponent<AshenAtmosphere>(); _atmosphere.Initialize(_hero);
            RebuildAtmosphere();
        }

        private void RebuildAtmosphere()
        {
            if (_atmosphere == null) return;
            _atmosphere.EnterRegion(_game.Progression.EntryAct);
            int act = _game.Progression.EntryAct;
            if (act == 3)
                foreach (int side in new[] { -1, 1 }) ChapterShape(PrimitiveType.Cube, "Grass verge", new Vector3(side * 8, -.015f, 0),
                    new Vector3(4, .04f, 36), new Color(.20f, .25f, .14f));
            if (act == 4)
            {
                for (int i = 0; i < 10; i++) ChapterShape(PrimitiveType.Cube, "Mill wooden board", new Vector3(-6.25f + i * .5f, .01f, -2),
                    new Vector3(.47f, .04f, 4), new Color(.29f + .01f * (i % 2), .21f, .13f));
                ChapterShape(PrimitiveType.Cube, "Mill shallow puddle", new Vector3(-5, .005f, 8.5f),
                    new Vector3(4, .025f, 3), new Color(.15f, .23f, .25f));
            }
        }

        private bool PollAudioInput(ArenaHudLayout layout)
        {
            bool consumed = false;
            if (Input.GetKeyDown(KeyCode.F8) || (_audioPanelOpen && Input.GetKeyDown(KeyCode.Escape)))
            { _audioPanelOpen = !_audioPanelOpen; _audioCreditsOpen = false; consumed = true; }
            for (int i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began || !layout.IsWithinSafeArea(touch.position)) continue;
                var point = layout.ToGuiTouch(touch.position);
                if (!_hungerJournalOpen && AudioToggleRect(layout).Contains(point)) { _audioPanelOpen = !_audioPanelOpen; _audioCreditsOpen = false; consumed = true; }
                else if (_audioPanelOpen)
                { if (!_audioCreditsOpen) for (int option = 0; option < 6; option++) if (AudioOption(layout, option).Contains(point)) ChangeAudioOption(option); consumed = true; }
            }
            if (!_audioPanelOpen && !consumed) return false;
            _hungerJournalOpen = false; _game.SetMoveInput(0, 0); ReleasePresentationTouches(); return true;
        }

        private static float NextVolume(float volume) { return volume >= .99f ? 0 : Mathf.Min(1, volume + .25f); }
        private void ChangeAudioOption(int option)
        {
            if (option == 5) { _audioCreditsOpen = !_audioCreditsOpen; return; }
            var a = _atmosphere;
            a.SetPreferences(option == 1 ? NextVolume(a.MusicVolume) : a.MusicVolume,
                option == 0 ? NextVolume(a.VoiceVolume) : a.VoiceVolume,
                option == 2 ? NextVolume(a.EffectsVolume) : a.EffectsVolume,
                option == 3 ? !a.Muted : a.Muted, option == 4 ? !a.ReducedEffects : a.ReducedEffects);
        }

        private bool DrawAudioPanel(ArenaHudLayout l)
        {
            if (_atmosphere == null) return false;
            if (!_hungerJournalOpen)
            {
                string label = _audioPanelOpen ? "TON / EFFEKTE SCHLIESSEN" : "TON / EFFEKTE";
                if (Application.isMobilePlatform) GUI.Box(AudioToggleRect(l), label);
                else if (GUI.Button(AudioToggleRect(l), label)) { _audioPanelOpen = !_audioPanelOpen; _audioCreditsOpen = false; }
            }
            if (!_audioPanelOpen) return false;
            var a = _atmosphere;
            if (_audioCreditsOpen)
            {
                if (_audioCredits == null) _audioCredits = Resources.Load<TextAsset>("AshenAudioCredits");
                var panel = AudioPanel(l); GUI.Box(panel, "AUDIO-QUELLEN / BESETZUNG");
                GUILayout.BeginArea(new Rect(panel.x + 15, panel.y + 28, panel.width - 30, panel.height - 40));
                _creditsScroll = GUILayout.BeginScrollView(_creditsScroll);
                GUILayout.Label(_audioCredits != null ? _audioCredits.text : "Credits fehlen.", new GUIStyle(GUI.skin.label) { wordWrap = true });
                GUILayout.EndScrollView(); GUILayout.EndArea(); return true;
            }
            GUI.Box(AudioPanel(l), "TON / EFFEKTE  ·  Tippen ändert die Stufe");
            string[] labels = { "Sprache: " + Mathf.RoundToInt(a.VoiceVolume * 100) + "%", "Musik: " + Mathf.RoundToInt(a.MusicVolume * 100) + "%",
                "Umgebung / Kampf: " + Mathf.RoundToInt(a.EffectsVolume * 100) + "%", a.Muted ? "Gesamter Ton: AUS" : "Gesamter Ton: AN",
                a.ReducedEffects ? "Partikel: REDUZIERT" : "Partikel: NORMAL", "Stimmen / Quellen" };
            for (int i = 0; i < labels.Length; i++)
                if (Application.isMobilePlatform) GUI.Box(AudioOption(l, i), labels[i]);
                else if (GUI.Button(AudioOption(l, i), labels[i])) ChangeAudioOption(i);
            return true;
        }

        public static Rect SpeechSubtitleRect(ArenaHudLayout l, bool journalOpen, bool hasMastery)
        {
            float width = journalOpen ? 560 : Mathf.Min(360, l.Width - 650);
            var rect = new Rect((l.Width - width) / 2, journalOpen ? l.Height - 80 : 148, width, journalOpen ? 74 : 132);
            if (!journalOpen && hasMastery)
                for (int i = 0; i < 3; i++) if (rect.Overlaps(l.MasteryRect(i)))
                {
                    rect.y = l.MasteryRect(2).yMax + 8;
                    rect.height = Mathf.Min(rect.height, l.PickupRect.yMin - 8 - rect.y);
                    break;
                }
            return rect;
        }

        private void DrawSpeechSubtitle(ArenaHudLayout l)
        {
            if (_atmosphere == null || _atmosphere.Subtitle.Length == 0 || _audioPanelOpen) return;
            var rect = SpeechSubtitleRect(l, _hungerJournalOpen, _game.Progression.UnspentMasteryPoints > 0);
            var style = new GUIStyle(GUI.skin.box) { fontSize = 16, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            GUI.Box(rect, _atmosphere.Subtitle, style);
        }
    }
}
