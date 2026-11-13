using System.Collections.Generic;
using System.Numerics;
using AreArpg.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// uGUI projection of the mobile-ARPG HUD. Every element is placed from ArenaHudLayout so drawing and
    /// touch hit testing share one geometry; HUD graphics never raycast. Reads confirmed Core state only.
    /// </summary>
    public sealed partial class ArenaRuntime
    {
        public static readonly string[] SkillLabels = { "KLINGE", "WUCHT", "SCHUTZ", "RUIN" };
        public static readonly string[] MenuLabels = { "HELD", "TASCHE", "QUESTS", "OPTION" };
        public static readonly string[] MenuGlyphs = { "⚔", "▣", "❖", "⚙" };
        public static readonly string[] MasteryLabels = { "+ MACHT", "+ VITALITÄT", "+ ENTSCHLOSSENHEIT" };
        public static readonly string[] NpcActionLabels = { "GRÜSSEN", "VERSORGEN", "ANGREIFEN", "AUSRAUBEN" };
        private static readonly string[] NpcActionKeys = { "F", "V", "B", "H" };

        private Canvas _hudCanvas;
        private RectTransform _hudRoot;
        private Vector4 _hudGeometry;
        private int _hudBuiltAct = -1;
        private Text _hudChapter, _hudLevel, _hudHealthText, _hudFocusText, _hudObjective, _hudMessage;
        private Image _hudHealthFill, _hudFocusFill, _hudXpFill, _hudFade;
        private GameObject _hudCamp, _hudTakeRation, _hudNpc, _hudMastery, _hudDefeat, _hudFault, _hudGameplay, _hudMenuBadge;
        private Text _hudCampStock, _hudCampState, _hudNpcName, _hudNpcLine1, _hudNpcLine2, _hudMinimapLabel;
        private readonly Image[] _hudSkillCooldown = new Image[4];
        private readonly Text[] _hudSkillTimer = new Text[4], _hudSkillRank = new Text[4];
        private readonly Image[] _hudSkillFace = new Image[4];
        private RectTransform _hudStickBase, _hudStickKnob, _hudMinimapDots, _hudHeroMarker, _hudNorth;
        private Image _hudPickupGlow;
        private readonly List<Image> _hudDots = new List<Image>();

        /// <summary>Ensure exactly one EventSystem so menu buttons work with mouse and touch.</summary>
        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("ARE_EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(go);
        }

        private void BuildHud()
        {
            EnsureEventSystem();
            if (_hudCanvas == null)
            {
                var go = new GameObject("ARE_HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                go.transform.SetParent(transform, false);
                go.layer = 5;
                _hudCanvas = go.GetComponent<Canvas>();
                _hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _hudCanvas.sortingOrder = 10;
                _hudCanvas.pixelPerfect = false;
                go.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            }
            if (_hudRoot != null) Destroy(_hudRoot.gameObject);
            _hudDots.Clear();
            ArenaHudLayout l = CurrentHudLayout();
            _hudCanvas.GetComponent<CanvasScaler>().scaleFactor = l.Scale;
            _hudRoot = AshenUiTheme.Node("SafeRoot", _hudCanvas.transform);
            float screenH = Mathf.Max(HudScreenHeight, 1f);
            _hudRoot.anchorMin = _hudRoot.anchorMax = new Vector2(0f, 1f);
            _hudRoot.anchoredPosition = new Vector2(l.SafeArea.xMin / l.Scale, -(screenH - l.SafeArea.yMax) / l.Scale);
            _hudRoot.sizeDelta = new Vector2(l.Width, l.Height);
            _hudGeometry = new Vector4(HudScreenWidth, HudScreenHeight, l.SafeArea.xMin + l.SafeArea.width * 3f, l.SafeArea.yMin + l.SafeArea.height * 7f);

            _hudBuiltAct = _game != null ? _game.Progression.EntryAct : -1;
            _hudGameplay = AshenUiTheme.Node("Gameplay", _hudRoot).gameObject;
            AshenUiTheme.Stretch((RectTransform)_hudGameplay.transform);
            Transform g = _hudGameplay.transform;

            BuildStatus(g, l);
            BuildQuestTracker(g, l);
            BuildCamp(g, l);
            BuildMinimap(g, l);
            BuildTopRightButtons(g, l);
            BuildStick(g, l);
            BuildSkills(g, l);
            BuildMastery(g, l);
            BuildNpcCard(g, l);

            var xpBack = AshenUiTheme.Image("XpBack", g, AshenUiTheme.Bar, new Color(0.08f, 0.06f, 0.04f, 0.85f));
            AshenUiTheme.Place(xpBack.rectTransform, l.ExperienceBarRect);
            _hudXpFill = FilledBar("XpFill", xpBack.transform, AshenUiTheme.Experience);

            BuildDefeat(l);
            _hudFade = AshenUiTheme.Image("RegionFade", _hudRoot, null, Color.clear, false);
            AshenUiTheme.Stretch(_hudFade.rectTransform, -4000f);
            _hudFault = AshenUiTheme.Frame("StorageFault", _hudRoot, new Rect(16f, 16f, l.Width - 32f, 116f)).gameObject;
            var fault = AshenUiTheme.Label("Text", _hudFault.transform, 18, AshenUiTheme.Parchment, TextAnchor.MiddleCenter, FontStyle.Bold);
            AshenUiTheme.Stretch(fault.rectTransform, 14f);
            fault.text = "SPIELSTAND-PRÜFUNG FEHLGESCHLAGEN\nDein Charakter wurde nicht überschrieben.\nStelle einen gültigen Spielstand wieder her.";
        }

        private static Image FilledBar(string name, Transform parent, Color color)
        {
            var fill = AshenUiTheme.Image(name, parent, AshenUiTheme.Bar, color, false);
            AshenUiTheme.Stretch(fill.rectTransform, 2f);
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0;
            return fill;
        }

        private void BuildStatus(Transform g, ArenaHudLayout l)
        {
            AshenUiTheme.Frame("Status", g, l.StatusPanelRect);
            var portrait = AshenUiTheme.Image("Portrait", g, AshenUiTheme.RoundButton, Color.white);
            AshenUiTheme.Place(portrait.rectTransform, l.PortraitRect);
            var glow = AshenUiTheme.Image("PortraitGlow", portrait.transform, AshenUiTheme.Glow, new Color(1f, 0.45f, 0.12f, 0.22f));
            AshenUiTheme.Stretch(glow.rectTransform, 10f);
            _hudLevel = AshenUiTheme.Label("Level", portrait.transform, 26, AshenUiTheme.Parchment, TextAnchor.MiddleCenter, FontStyle.Bold);
            AshenUiTheme.Stretch(_hudLevel.rectTransform, 6f);
            var lvTag = AshenUiTheme.Label("LvTag", portrait.transform, 11, AshenUiTheme.Bronze, TextAnchor.UpperCenter, FontStyle.Bold);
            AshenUiTheme.Place(lvTag.rectTransform, new Rect(0f, 14f, l.PortraitRect.width, 14f)); lvTag.text = "STUFE";

            _hudChapter = AshenUiTheme.Label("Chapter", g, 15, AshenUiTheme.Parchment, TextAnchor.MiddleLeft, FontStyle.Bold);
            AshenUiTheme.Place(_hudChapter.rectTransform, new Rect(112f, 20f, l.StatusPanelRect.width - 104f, 24f));

            var hpBack = AshenUiTheme.Image("HealthBack", g, AshenUiTheme.Bar, new Color(0.16f, 0.03f, 0.02f, 0.95f));
            AshenUiTheme.Place(hpBack.rectTransform, l.HealthBarRect);
            _hudHealthFill = FilledBar("HealthFill", hpBack.transform, AshenUiTheme.Health);
            _hudHealthText = AshenUiTheme.Label("HealthText", hpBack.transform, 12, AshenUiTheme.Parchment, TextAnchor.MiddleCenter, FontStyle.Bold);
            AshenUiTheme.Stretch(_hudHealthText.rectTransform);

            var fBack = AshenUiTheme.Image("FocusBack", g, AshenUiTheme.Bar, new Color(0.02f, 0.06f, 0.12f, 0.95f));
            AshenUiTheme.Place(fBack.rectTransform, l.FocusBarRect);
            _hudFocusFill = FilledBar("FocusFill", fBack.transform, AshenUiTheme.Focus);
            _hudFocusText = AshenUiTheme.Label("FocusText", g, 11, AshenUiTheme.Muted, TextAnchor.MiddleLeft);
            AshenUiTheme.Place(_hudFocusText.rectTransform, new Rect(112f, 87f, l.StatusPanelRect.width - 104f, 16f));
        }

        private void BuildQuestTracker(Transform g, ArenaHudLayout l)
        {
            Rect q = l.QuestTrackerRect;
            var frame = AshenUiTheme.Frame("QuestTracker", g, q);
            frame.color = new Color(1f, 1f, 1f, 0.82f);
            var marker = AshenUiTheme.Label("Marker", frame.transform, 14, AshenUiTheme.Ember, TextAnchor.UpperLeft, FontStyle.Bold);
            AshenUiTheme.Place(marker.rectTransform, new Rect(10f, 6f, 16f, 18f)); marker.text = "◆";
            // The journal button (from act II) occupies the right end of the tracker.
            float textWidth = q.width - (_game != null && _game.Progression.EntryAct >= 2 ? 128f : 36f);
            _hudObjective = AshenUiTheme.Label("Objective", frame.transform, 13, AshenUiTheme.Parchment, TextAnchor.UpperLeft, FontStyle.Bold);
            AshenUiTheme.Place(_hudObjective.rectTransform, new Rect(26f, 5f, textWidth, 36f));
            _hudObjective.resizeTextForBestFit = true; _hudObjective.resizeTextMinSize = 10; _hudObjective.resizeTextMaxSize = 13;
            _hudMessage = AshenUiTheme.Label("Message", frame.transform, 11, AshenUiTheme.Muted, TextAnchor.UpperLeft);
            AshenUiTheme.Place(_hudMessage.rectTransform, new Rect(12f, 41f, textWidth + 14f, 16f));
            _hudMessage.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private void BuildCamp(Transform g, ArenaHudLayout l)
        {
            _hudCamp = AshenUiTheme.Frame("Camp", g, l.CampPanelRect, "Die gestohlenen Vorräte").gameObject;
            Transform c = _hudCamp.transform;
            _hudCampStock = AshenUiTheme.Label("Stock", c, 13, AshenUiTheme.Parchment, TextAnchor.UpperLeft, FontStyle.Bold);
            AshenUiTheme.Place(_hudCampStock.rectTransform, new Rect(14f, 28f, l.CampPanelRect.width - 28f, 18f));
            _hudCampState = AshenUiTheme.Label("State", c, 12, AshenUiTheme.Muted, TextAnchor.UpperLeft);
            AshenUiTheme.Place(_hudCampState.rectTransform, new Rect(14f, 45f, l.CampPanelRect.width - 28f, 20f));
            HudButton("Donate", g, l.DonationRect, Application.isMobilePlatform ? DonationButtonLabel(true) : DonationButtonLabel(false),
                new Color(0.32f, 0.27f, 0.18f, 1f)).transform.SetParent(c, true);
            _hudTakeRation = HudButton("TakeRation", g, l.TakeRationRect, Application.isMobilePlatform ? "RATION ENTWENDEN" : "T: RATION ENTWENDEN",
                new Color(0.42f, 0.18f, 0.14f, 1f));
            _hudTakeRation.transform.SetParent(c, true);
        }

        private GameObject HudButton(string name, Transform parent, Rect rect, string text, Color tint, int size = 13)
        {
            var img = AshenUiTheme.Image(name, parent, AshenUiTheme.PanelPlain, tint);
            AshenUiTheme.Place(img.rectTransform, rect);
            var label = AshenUiTheme.Label("Label", img.transform, size, AshenUiTheme.Parchment, TextAnchor.MiddleCenter, FontStyle.Bold);
            AshenUiTheme.Stretch(label.rectTransform, 3f); label.text = text;
            return img.gameObject;
        }

        private void BuildMinimap(Transform g, ArenaHudLayout l)
        {
            Rect m = l.MinimapRect;
            var back = AshenUiTheme.Image("Minimap", g, AshenUiTheme.RoundButton, new Color(0.85f, 0.85f, 0.85f, 1f));
            AshenUiTheme.Place(back.rectTransform, m);
            var mask = AshenUiTheme.Image("Mask", back.transform, AshenUiTheme.Ring, new Color(1f, 1f, 1f, 0.01f));
            AshenUiTheme.Stretch(mask.rectTransform, 9f);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var tint = AshenUiTheme.Image("Ground", mask.transform, AshenUiTheme.Glow, new Color(0.32f, 0.27f, 0.20f, 0.55f));
            AshenUiTheme.Stretch(tint.rectTransform, -20f);
            _hudMinimapDots = AshenUiTheme.Node("Dots", mask.transform);
            _hudMinimapDots.anchorMin = _hudMinimapDots.anchorMax = _hudMinimapDots.pivot = new Vector2(0.5f, 0.5f);
            _hudMinimapDots.anchoredPosition = Vector2.zero; _hudMinimapDots.sizeDelta = Vector2.zero;
            var hero = AshenUiTheme.Image("Hero", mask.transform, AshenUiTheme.Glow, Color.white);
            _hudHeroMarker = hero.rectTransform;
            _hudHeroMarker.anchorMin = _hudHeroMarker.anchorMax = _hudHeroMarker.pivot = new Vector2(0.5f, 0.5f);
            _hudHeroMarker.sizeDelta = new Vector2(16f, 16f); _hudHeroMarker.anchoredPosition = Vector2.zero;
            var north = AshenUiTheme.Label("North", back.transform, 12, AshenUiTheme.Bronze, TextAnchor.MiddleCenter, FontStyle.Bold);
            _hudNorth = north.rectTransform;
            _hudNorth.anchorMin = _hudNorth.anchorMax = _hudNorth.pivot = new Vector2(0.5f, 0.5f);
            _hudNorth.sizeDelta = new Vector2(18f, 16f); _hudNorth.anchoredPosition = new Vector2(0f, m.height * 0.5f - 9f);
            north.text = "N";
            _hudMinimapLabel = AshenUiTheme.Label("Region", back.transform, 11, AshenUiTheme.Parchment, TextAnchor.LowerCenter, FontStyle.Bold);
            AshenUiTheme.Place(_hudMinimapLabel.rectTransform, new Rect(14f, m.height - 34f, m.width - 28f, 22f));
        }

        private void BuildTopRightButtons(Transform g, ArenaHudLayout l)
        {
            string[] cam = { "+", "–", "AUTO" };
            for (int i = 0; i < 3; i++)
            {
                var b = AshenUiTheme.Image("Camera" + i, g, AshenUiTheme.PanelPlain, new Color(0.26f, 0.21f, 0.16f, 0.95f));
                AshenUiTheme.Place(b.rectTransform, l.CameraZoomRect(i));
                var t = AshenUiTheme.Label("Label", b.transform, i == 2 ? 12 : 20, AshenUiTheme.Parchment, TextAnchor.MiddleCenter, FontStyle.Bold);
                AshenUiTheme.Stretch(t.rectTransform); t.text = cam[i];
            }
            for (int i = 0; i < ArenaHudLayout.MenuCount; i++)
            {
                Rect r = l.MenuButtonRect(i);
                var b = AshenUiTheme.Image("Menu" + i, g, AshenUiTheme.Panel, Color.white);
                AshenUiTheme.Place(b.rectTransform, r);
                Sprite icon = AshenUiTheme.Icon(new[] { "character", "inventory", "quests", "settings" }[i]);
                if (icon != null)
                {
                    var ic = AshenUiTheme.Image("Icon", b.transform, icon, Color.white, false);
                    AshenUiTheme.Place(ic.rectTransform, new Rect(11f, 6f, r.width - 22f, r.height - 24f));
                }
                else
                {
                    var glyph = AshenUiTheme.Label("Glyph", b.transform, 22, AshenUiTheme.Bronze, TextAnchor.MiddleCenter, FontStyle.Bold);
                    AshenUiTheme.Place(glyph.rectTransform, new Rect(0f, 4f, r.width, r.height - 20f)); glyph.text = MenuGlyphs[i];
                }
                var t = AshenUiTheme.Label("Label", b.transform, 10, AshenUiTheme.Parchment, TextAnchor.LowerCenter, FontStyle.Bold);
                AshenUiTheme.Place(t.rectTransform, new Rect(0f, r.height - 18f, r.width, 14f)); t.text = MenuLabels[i];
                if (i == 0)
                {
                    var badge = AshenUiTheme.Image("Badge", b.transform, AshenUiTheme.RoundButton, AshenUiTheme.Ember);
                    AshenUiTheme.Place(badge.rectTransform, new Rect(r.width - 18f, -4f, 22f, 22f));
                    var bt = AshenUiTheme.Label("Text", badge.transform, 13, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                    AshenUiTheme.Stretch(bt.rectTransform); bt.text = "!";
                    _hudMenuBadge = badge.gameObject;
                }
            }
        }

        private void BuildStick(Transform g, ArenaHudLayout l)
        {
            var area = AshenUiTheme.Image("StickArea", g, AshenUiTheme.Glow, new Color(0f, 0f, 0f, 0.18f));
            AshenUiTheme.Place(area.rectTransform, l.MoveRect);
            var b = AshenUiTheme.Image("StickBase", g, AshenUiTheme.Ring, Color.white);
            _hudStickBase = b.rectTransform;
            _hudStickBase.pivot = new Vector2(0.5f, 0.5f); _hudStickBase.sizeDelta = new Vector2(150f, 150f);
            var k = AshenUiTheme.Image("StickKnob", _hudStickBase, AshenUiTheme.RoundButton, Color.white);
            _hudStickKnob = k.rectTransform;
            _hudStickKnob.anchorMin = _hudStickKnob.anchorMax = _hudStickKnob.pivot = new Vector2(0.5f, 0.5f);
            _hudStickKnob.sizeDelta = new Vector2(66f, 66f);
        }

        private void BuildSkills(Transform g, ArenaHudLayout l)
        {
            for (int i = 0; i < 4; i++)
            {
                Rect r = l.SkillRect(i);
                var face = AshenUiTheme.Image("Skill" + i, g, AshenUiTheme.RoundButton, Color.white);
                AshenUiTheme.Place(face.rectTransform, r);
                _hudSkillFace[i] = face;
                var glow = AshenUiTheme.Image("Glow", face.transform, AshenUiTheme.Glow,
                    i == 0 ? new Color(1f, 0.42f, 0.12f, 0.20f) : new Color(1f, 0.75f, 0.4f, 0.10f));
                AshenUiTheme.Stretch(glow.rectTransform, r.width * 0.16f);
                Sprite icon = AshenUiTheme.Icon("skill" + i);
                if (icon != null)
                {
                    var ic = AshenUiTheme.Image("Icon", face.transform, icon, Color.white, false);
                    AshenUiTheme.Stretch(ic.rectTransform, r.width * 0.2f);
                }
                var name = AshenUiTheme.Label("Name", face.transform, i == 0 ? 17 : 12, AshenUiTheme.Parchment, TextAnchor.MiddleCenter, FontStyle.Bold);
                AshenUiTheme.Stretch(name.rectTransform, 6f); name.text = SkillLabels[i];
                var cd = AshenUiTheme.Image("Cooldown", face.transform, AshenUiTheme.RoundButton, new Color(0f, 0f, 0f, 0.68f), false);
                AshenUiTheme.Stretch(cd.rectTransform, 3f);
                cd.type = Image.Type.Filled; cd.fillMethod = Image.FillMethod.Radial360; cd.fillOrigin = (int)Image.Origin360.Top; cd.fillClockwise = false;
                _hudSkillCooldown[i] = cd;
                _hudSkillTimer[i] = AshenUiTheme.Label("Timer", face.transform, i == 0 ? 26 : 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                AshenUiTheme.Stretch(_hudSkillTimer[i].rectTransform);
                var rankBack = AshenUiTheme.Image("Rank", face.transform, AshenUiTheme.PanelPlain, new Color(0.18f, 0.13f, 0.09f, 1f));
                AshenUiTheme.Place(rankBack.rectTransform, new Rect(r.width * 0.5f - 20f, r.height - 16f, 40f, 18f));
                _hudSkillRank[i] = AshenUiTheme.Label("Text", rankBack.transform, 11, AshenUiTheme.Experience, TextAnchor.MiddleCenter, FontStyle.Bold);
                AshenUiTheme.Stretch(_hudSkillRank[i].rectTransform);
                if (!Application.isMobilePlatform)
                {
                    var key = AshenUiTheme.Label("Key", face.transform, 11, AshenUiTheme.Bronze, TextAnchor.UpperLeft, FontStyle.Bold);
                    AshenUiTheme.Place(key.rectTransform, new Rect(r.width * 0.16f, r.height * 0.1f, 20f, 16f)); key.text = (i + 1).ToString();
                }
            }
            var pick = AshenUiTheme.Image("Pickup", g, AshenUiTheme.RoundButton, Color.white);
            AshenUiTheme.Place(pick.rectTransform, l.PickupRect);
            _hudPickupGlow = AshenUiTheme.Image("Glow", pick.transform, AshenUiTheme.Glow, new Color(1f, 0.8f, 0.3f, 0f));
            AshenUiTheme.Stretch(_hudPickupGlow.rectTransform, -12f);
            var pt = AshenUiTheme.Label("Label", pick.transform, 10, AshenUiTheme.Parchment, TextAnchor.MiddleCenter, FontStyle.Bold);
            AshenUiTheme.Stretch(pt.rectTransform, 2f); pt.text = Application.isMobilePlatform ? "AUF-\nHEBEN" : "AUF-\nHEBEN (E)";
        }

        private void BuildMastery(Transform g, ArenaHudLayout l)
        {
            _hudMastery = AshenUiTheme.Node("Mastery", g).gameObject;
            AshenUiTheme.Stretch((RectTransform)_hudMastery.transform);
            for (int i = 0; i < 3; i++)
                HudButton("Mastery" + i, _hudMastery.transform, l.MasteryRect(i), MasteryLabels[i], new Color(0.55f, 0.32f, 0.12f, 1f), 11);
        }

        private void BuildNpcCard(Transform g, ArenaHudLayout l)
        {
            Rect p = l.NpcPanelRect;
            _hudNpc = AshenUiTheme.Frame("NpcCard", g, p).gameObject;
            Transform c = _hudNpc.transform;
            _hudNpcName = AshenUiTheme.Label("Name", c, 17, AshenUiTheme.Parchment, TextAnchor.UpperLeft, FontStyle.Bold);
            AshenUiTheme.Place(_hudNpcName.rectTransform, new Rect(16f, 10f, p.width - 32f, 24f));
            _hudNpcLine1 = AshenUiTheme.Label("Line1", c, 12, AshenUiTheme.Muted, TextAnchor.UpperLeft);
            AshenUiTheme.Place(_hudNpcLine1.rectTransform, new Rect(16f, 38f, p.width - 32f, 18f));
            _hudNpcLine2 = AshenUiTheme.Label("Line2", c, 12, AshenUiTheme.Muted, TextAnchor.UpperLeft);
            AshenUiTheme.Place(_hudNpcLine2.rectTransform, new Rect(16f, 58f, p.width - 32f, 18f));
            for (int i = 0; i < 4; i++)
            {
                string label = Application.isMobilePlatform ? NpcActionLabels[i] : NpcActionKeys[i] + "  " + NpcActionLabels[i];
                var b = HudButton("Action" + i, g, l.NpcActionRect(i), label,
                    i >= 2 ? new Color(0.46f, 0.16f, 0.12f, 1f) : new Color(0.30f, 0.25f, 0.18f, 1f));
                b.transform.SetParent(c, true);
            }
        }

        private void BuildDefeat(ArenaHudLayout l)
        {
            _hudDefeat = AshenUiTheme.Node("Defeat", _hudRoot).gameObject;
            AshenUiTheme.Stretch((RectTransform)_hudDefeat.transform);
            var shade = AshenUiTheme.Image("Shade", _hudDefeat.transform, null, new Color(0.08f, 0f, 0f, 0.55f), false);
            AshenUiTheme.Stretch(shade.rectTransform, -4000f);
            var box = AshenUiTheme.Frame("Panel", _hudDefeat.transform, l.DefeatPanelRect);
            var t = AshenUiTheme.Label("Text", box.transform, 22, AshenUiTheme.Parchment, TextAnchor.MiddleCenter, FontStyle.Bold);
            AshenUiTheme.Stretch(t.rectTransform, 8f);
            t.text = "<color=#E0472C>BESIEGT</color>\n<size=14>" + (Application.isMobilePlatform ? "Tippe auf NEU BEGINNEN" : "R oder Klick: neu beginnen") + "</size>";
            HudButton("Restart", _hudDefeat.transform, l.RestartRect, "NEU BEGINNEN", new Color(0.50f, 0.20f, 0.14f, 1f), 16);
        }

        /// <summary>Current quest objective for the tracker; Core-owned texts first.</summary>
        public static string ObjectiveText(ArenaSimulation game)
        {
            if (game.Progression.HungerStage > 0) return game.HungerObjective;
            if (game.Progression.EntryAct == 1) return "Verlasse die Krypta. Besiege ihre Wächter und sichere die Beute.";
            if (game.Progression.EntryAct == 2) return "Folge dem Dorfweg: Ilyan hungert, Mara und Dorn erwarten dich.";
            if (game.IsVillage) return "Aschengrenze: Lager, Wache und Nordpass.";
            return game.CryptCleared ? "Die Halle ist frei. Suche den Weg in die Tiefe." : "Erkunde die Krypta und bezwinge ihre Bewohner.";
        }

        private static float Fraction(BigInteger value, BigInteger max)
        {
            if (max <= BigInteger.Zero) return 0f;
            if (value <= BigInteger.Zero) return 0f;
            if (value >= max) return 1f;
            // Divide while still arbitrary precision; only the bounded ratio is converted for drawing.
            const int precision = 1000000;
            return (float)(value * precision / max) / precision;
        }

        public static float ExperienceFraction(CharacterProgression p)
        {
            BigInteger floor = CharacterProgression.RequiredExperienceForLevel(p.Level);
            BigInteger next = CharacterProgression.RequiredExperienceForLevel(p.Level + BigInteger.One);
            return Fraction(p.TotalExperience - floor, next - floor);
        }

        /// <summary>Refresh the projection; rebuild only when screen or safe area changed.</summary>
        private void UpdateHud()
        {
            if (_game == null) return;
            ArenaHudLayout l = CurrentHudLayout();
            var geometry = new Vector4(HudScreenWidth, HudScreenHeight, l.SafeArea.xMin + l.SafeArea.width * 3f, l.SafeArea.yMin + l.SafeArea.height * 7f);
            if (_hudCanvas == null || _hudRoot == null || geometry != _hudGeometry || _hudBuiltAct != _game.Progression.EntryAct) BuildHud();

            _hudFault.SetActive(_storageFault);
            bool over = _game.GameOver;
            bool menuOpen = MenuOpen;
            _hudDefeat.SetActive(!_storageFault && over);
            _hudGameplay.SetActive(!_storageFault && !over && !menuOpen && !_hungerJournalOpen);
            var fade = _hudFade.color; fade.a = _regionFade; fade.r = fade.g = fade.b = 0f; _hudFade.color = fade;
            if (_storageFault || over || menuOpen || _hungerJournalOpen) return;

            var p = _game.Progression;
            _hudLevel.text = p.Level.ToString();
            _hudChapter.text = DisplayChapterName(_game).ToUpperInvariant();
            _hudHealthFill.fillAmount = Mathf.Clamp01(_game.PlayerHealth / (float)Mathf.Max(1, _game.MaxPlayerHealth));
            _hudHealthText.text = _game.PlayerHealth + " / " + _game.MaxPlayerHealth;
            _hudFocusFill.fillAmount = Mathf.Clamp01(_game.Resource / 100f);
            _hudFocusText.text = "Fokus " + _game.Resource + "/100   ·   Waffe +" + _game.WeaponBonus;
            _hudXpFill.fillAmount = ExperienceFraction(p);
            string objective = ObjectiveText(_game);
            _hudObjective.text = objective;
            // Show the latest event line only when it adds something beyond the objective itself.
            string message = _message ?? "";
            bool redundant = message.Length > 12 && objective.Contains(message.Substring(message.IndexOf(':') + 1).Trim().Split('.')[0]);
            _hudMessage.text = redundant ? "" : message;
            _hudMenuBadge.SetActive(p.UnspentMasteryPoints > BigInteger.Zero);

            _hudCamp.SetActive(_game.IsVillage);
            if (_game.IsVillage)
            {
                int missing = SocialLedger.CampCapacity - p.Social.CampProvisions;
                _hudCampStock.text = "Maras Lager: " + p.Social.CampProvisions + "/" + SocialLedger.CampCapacity + "   Fehlbestand: " + missing;
                _hudCampState.text = p.HungerStage > 0 ? "Ilyan hilft Verletzten. Mehr unter Spuren / Entscheidung." :
                    missing > 0 ? "Ilyan hungert. Niemand kennt den Täter." : "Das Lager ist versorgt. Die Schuldfrage bleibt offen.";
                _hudTakeRation.SetActive(p.EntryAct == 2);
            }
            _hudMastery.SetActive(p.UnspentMasteryPoints > BigInteger.Zero);

            NpcAgent npc = _game.NearestNpc();
            _hudNpc.SetActive(npc != null);
            if (npc != null)
            {
                NpcDisposition mood = npc.Memory.Disposition(npc.Personality);
                string moodText = mood == NpcDisposition.Friendly ? "<color=#8FD16A>freundlich</color>" :
                    mood == NpcDisposition.Wary ? "<color=#E8C25A>misstrauisch</color>" :
                    mood == NpcDisposition.Hostile ? "<color=#E0472C>feindselig</color>" : "<color=#BBB2A2>neutral</color>";
                string who = npc.Id == 0 ? "Ilyan  <size=12><color=#A88A5A>Pfadleser</color></size>" :
                    npc.Id == 1 ? "Mara  <size=12><color=#A88A5A>Versorgerin</color></size>" : "Dorn  <size=12><color=#A88A5A>Wache</color></size>";
                _hudNpcName.text = who + "   " + moodText;
                _hudNpcLine1.text = "Gute Taten +" + npc.Memory.PositivePoints + "   ·   Übergriffe −" + npc.Memory.NegativePoints;
                _hudNpcLine2.text = "Hunger " + npc.Memory.Hunger + "   ·   Erschöpfung " + npc.Memory.Fatigue +
                    "   ·   Proviant " + p.Social.Provisions + "   ·   Münzen " + p.Social.Coins;
            }

            for (int i = 0; i < 4; i++)
            {
                SkillDefinition def = SkillCatalog.Get(i);
                int cd = _game.Cooldowns[i];
                _hudSkillCooldown[i].fillAmount = cd > 0 ? Mathf.Clamp01(cd / (float)def.CooldownTicks) : 0f;
                _hudSkillTimer[i].text = cd > 0 ? Mathf.CeilToInt(cd * ArenaSimulation.TickMs / 1000f).ToString() : "";
                _hudSkillRank[i].text = "R" + p.PracticeLevel(i);
                bool affordable = _game.Resource >= def.FocusCost;
                _hudSkillFace[i].color = affordable ? Color.white : new Color(0.55f, 0.55f, 0.65f, 1f);
            }

            bool lootNear = false;
            foreach (var loot in _game.GroundLoot)
            {
                long dx = loot.Xmm - _game.PlayerXmm, dz = loot.Zmm - _game.PlayerZmm;
                if (dx * dx + dz * dz <= 2500L * 2500L) { lootNear = true; break; }
            }
            var pg = _hudPickupGlow.color; pg.a = lootNear ? 0.45f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f) : 0f; _hudPickupGlow.color = pg;

            Vector2 origin = _movementTouch.IsCaptured ? _movementTouch.Origin : l.MoveRect.center;
            _hudStickBase.anchoredPosition = new Vector2(origin.x, -origin.y);
            Vector2 dir = _movementTouch.IsCaptured ? _movementTouch.Direction : KeyboardDirection();
            _hudStickKnob.anchoredPosition = new Vector2(dir.x, dir.y) * ArenaHudLayout.StickRadius * 0.62f;
            float a = _movementTouch.IsCaptured || dir.sqrMagnitude > 0.01f ? 0.95f : 0.55f;
            _hudStickBase.GetComponent<Image>().color = new Color(1f, 1f, 1f, a);
            _hudStickKnob.GetComponent<Image>().color = new Color(1f, 1f, 1f, a);

            UpdateMinimap();
        }

        private static Vector2 KeyboardDirection()
        {
            float x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            float y = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
        }

        /// <summary>
        /// Radar-style minimap around the hero from Core positions. It turns with the camera so
        /// "up" is what the player sees ahead; the N marker rides the rim.
        /// </summary>
        private void UpdateMinimap()
        {
            const float PerMetre = 3.6f, Radius = 70f;
            float yaw = _cameraRig != null ? _cameraRig.Yaw : 0f;
            _hudMinimapDots.localEulerAngles = new Vector3(0f, 0f, yaw);
            float rim = _hudNorth.parent is RectTransform ring ? ring.rect.height * 0.5f - 9f : 73f;
            _hudNorth.anchoredPosition = new Vector2(-Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad)) * rim;
            int used = 0;
            foreach (var m in _game.Monsters)
                if (!m.IsDead) PlaceDot(ref used, m.Xmm, m.Zmm, m.Kind >= 2 ? new Color(1f, 0.25f, 0.1f) : new Color(0.9f, 0.18f, 0.14f), m.Kind >= 2 ? 13f : 9f, PerMetre, Radius);
            foreach (var n in _game.Npcs)
                PlaceDot(ref used, n.Xmm, n.Zmm, new Color(1f, 0.82f, 0.35f), 10f, PerMetre, Radius);
            foreach (var loot in _game.GroundLoot)
                PlaceDot(ref used, loot.Xmm, loot.Zmm, new Color(1f, 0.95f, 0.55f), 8f, PerMetre, Radius);
            if (_worldPassage != null)
                PlaceDot(ref used, ArenaSimulation.PassageXmm, ArenaSimulation.PassageZmm, new Color(0.35f, 0.85f, 1f), 12f, PerMetre, Radius);
            if (_returnPassage != null)
                PlaceDot(ref used, 0, ArenaSimulation.ReturnPassageZmm, new Color(0.35f, 0.85f, 1f), 12f, PerMetre, Radius);
            foreach(var portal in _woodlandPortals) if(portal!=null) PlaceDot(ref used,(long)(portal.position.x*1000),(long)(portal.position.z*1000),new Color(.35f,.85f,1f),12f,PerMetre,Radius);
            for (int i = used; i < _hudDots.Count; i++) _hudDots[i].enabled = false;
            _hudMinimapLabel.text = DisplayChapterName(_game);
        }

        private void PlaceDot(ref int used, long xmm, long zmm, Color color, float size, float perMetre, float radius)
        {
            Vector2 offset = new Vector2((xmm - _game.PlayerXmm) / 1000f, (zmm - _game.PlayerZmm) / 1000f) * perMetre;
            if (offset.magnitude > radius) offset = offset.normalized * radius;
            if (used >= _hudDots.Count)
            {
                var dot = AshenUiTheme.Image("Dot", _hudMinimapDots, AshenUiTheme.Glow, color, false);
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = dot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _hudDots.Add(dot);
            }
            var img = _hudDots[used++];
            img.enabled = true; img.color = color;
            img.rectTransform.sizeDelta = new Vector2(size * 1.8f, size * 1.8f);
            img.rectTransform.anchoredPosition = offset;
        }
    }
}
