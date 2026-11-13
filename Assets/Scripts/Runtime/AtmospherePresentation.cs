using UnityEngine;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        private AshenAtmosphere _atmosphere;
        private TextAsset _audioCredits;
        /// <summary>Sound and effect options live in the OPTIONEN menu; its HUD button is the fourth menu slot.</summary>
        public static Rect AudioToggleRect(ArenaHudLayout l) { return l.MenuButtonRect(3); }

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

        private static float NextVolume(float volume) { return volume >= .99f ? 0 : Mathf.Min(1, volume + .25f); }
        private void ChangeAudioOption(int option)
        {
            if (option == 5) { _menuCredits = !_menuCredits; return; }
            var a = _atmosphere;
            a.SetPreferences(option == 1 ? NextVolume(a.MusicVolume) : a.MusicVolume,
                option == 0 ? NextVolume(a.VoiceVolume) : a.VoiceVolume,
                option == 2 ? NextVolume(a.EffectsVolume) : a.EffectsVolume,
                option == 3 ? !a.Muted : a.Muted, option == 4 ? !a.ReducedEffects : a.ReducedEffects);
        }

        /// <summary>Subtitles: a centred band under the menu row; above the journal's action list while it is open.</summary>
        public static Rect SpeechSubtitleRect(ArenaHudLayout l, bool journalOpen, bool hasMastery)
        {
            if (journalOpen) return new Rect((l.Width - 560) / 2, l.Height - 80, 560, 74);
            Rect band = l.SubtitleBandRect;
            return new Rect(band.x, band.y + 66f, band.width, band.height - 66f);
        }

        private void DrawSpeechSubtitle(ArenaHudLayout l)
        {
            if (_atmosphere == null || _atmosphere.Subtitle.Length == 0) return;
            var rect = SpeechSubtitleRect(l, _hungerJournalOpen, _game.Progression.UnspentMasteryPoints > 0);
            var style = new GUIStyle(GUI.skin.box) { fontSize = 16, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            GUI.Box(rect, _atmosphere.Subtitle, style);
        }
    }
}
