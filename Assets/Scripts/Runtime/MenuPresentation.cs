using System.Collections.Generic;
using System.Numerics;
using AreArpg.Core;
using UnityEngine;
using UnityEngine.UI;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// Modal character / inventory / quest / options menu (uGUI with real raycasts). While open the
    /// simulation is paused and gameplay input is ignored; every displayed value is read from the Core.
    /// </summary>
    public sealed partial class ArenaRuntime
    {
        public const int MenuCharacter = 0, MenuInventory = 1, MenuQuests = 2, MenuSettings = 3;
        public static readonly string[] MenuTitles = { "CHARAKTER", "TASCHE", "QUESTS", "OPTIONEN" };
        public static readonly string[] ArmorSlots = { "KOPF", "BRUST", "HÄNDE", "BEINE" };
        public static readonly string[] TrinketSlots = { "WAFFE", "NEBENHAND", "AMULETT", "RING" };

        private int _menuTab = -1;
        private Canvas _menuCanvas;
        private RectTransform _menuRoot;
        private int _selectedItem;
        private bool _menuCredits;
        private Camera _portraitCamera;
        private RenderTexture _portraitTexture;
        private const int PortraitLayer = 30;
        private readonly Dictionary<Transform, int> _portraitOriginalLayers = new Dictionary<Transform, int>();

        public bool MenuOpen { get { return _menuTab >= 0; } }
        public int MenuTab { get { return _menuTab; } }

        /// <summary>Open (or switch to) a menu tab; releases held gameplay input first.</summary>
        public void OpenMenu(int tab)
        {
            _menuTab = Mathf.Clamp(tab, 0, MenuTitles.Length - 1);
            _hungerJournalOpen = false;
            if (_game != null) _game.SetMoveInput(0, 0);
            ReleasePresentationTouches();
            BuildMenu();
        }

        public void CloseMenu()
        {
            _menuTab = -1; _menuCredits = false;
            if (_menuRoot != null) Destroy(_menuRoot.gameObject);
            _menuRoot = null;
            RestorePortraitLayers();
        }

        /// <summary>Menu hotkeys and Escape; returns true while the modal menu owns input.</summary>
        private bool PollMenuInput()
        {
            if (Input.GetKeyDown(KeyCode.K)) { if (_menuTab == MenuCharacter) CloseMenu(); else OpenMenu(MenuCharacter); return true; }
            if (Input.GetKeyDown(KeyCode.I)) { if (_menuTab == MenuInventory) CloseMenu(); else OpenMenu(MenuInventory); return true; }
            if (Input.GetKeyDown(KeyCode.L)) { if (_menuTab == MenuQuests) CloseMenu(); else OpenMenu(MenuQuests); return true; }
            if (Input.GetKeyDown(KeyCode.F8)) { if (_menuTab == MenuSettings) CloseMenu(); else OpenMenu(MenuSettings); return true; }
            if (!MenuOpen) return false;
            if (Input.GetKeyDown(KeyCode.Escape)) CloseMenu();
            if (_game != null) _game.SetMoveInput(0, 0);
            ReleasePresentationTouches();
            return true;
        }

        private void EnsureMenuCanvas(ArenaHudLayout l)
        {
            if (_menuCanvas == null)
            {
                var go = new GameObject("ARE_Menu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                go.transform.SetParent(transform, false); go.layer = 5;
                _menuCanvas = go.GetComponent<Canvas>();
                _menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay; _menuCanvas.sortingOrder = 20;
                go.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            }
            _menuCanvas.GetComponent<CanvasScaler>().scaleFactor = l.Scale;
        }

        private void BuildMenu()
        {
            RestorePortraitLayers();
            EnsureEventSystem();
            ArenaHudLayout l = CurrentHudLayout();
            EnsureMenuCanvas(l);
            if (_menuRoot != null) Destroy(_menuRoot.gameObject);
            _menuRoot = AshenUiTheme.Node("SafeRoot", _menuCanvas.transform);
            float screenH = Mathf.Max(HudScreenHeight, 1f);
            _menuRoot.anchoredPosition = new Vector2(l.SafeArea.xMin / l.Scale, -(screenH - l.SafeArea.yMax) / l.Scale);
            _menuRoot.sizeDelta = new Vector2(l.Width, l.Height);

            var shade = AshenUiTheme.Image("Shade", _menuRoot, null, new Color(0.02f, 0.015f, 0.01f, 0.78f), false);
            AshenUiTheme.Stretch(shade.rectTransform, -4000f); shade.raycastTarget = true;

            Rect win = new Rect(Mathf.Round(l.Width * 0.04f), 14f, Mathf.Round(l.Width * 0.92f), l.Height - 28f);
            var window = AshenUiTheme.Frame("Window", _menuRoot, win);
            Transform w = window.transform;

            float tabW = Mathf.Min(170f, (win.width - 120f) / MenuTitles.Length);
            for (int i = 0; i < MenuTitles.Length; i++)
            {
                int tab = i;
                bool active = i == _menuTab;
                var b = AshenUiTheme.MenuButton("Tab" + i, w, new Rect(16f + i * (tabW + 6f), 12f, tabW, 40f), MenuTitles[i],
                    () => OpenMenu(tab), active ? new Color(0.62f, 0.38f, 0.16f, 1f) : new Color(0.24f, 0.19f, 0.14f, 1f), 15);
                if (active)
                {
                    var line = AshenUiTheme.Image("Active", b.transform, AshenUiTheme.Bar, AshenUiTheme.Ember, false);
                    AshenUiTheme.Place(line.rectTransform, new Rect(10f, 36f, tabW - 20f, 4f));
                }
            }
            AshenUiTheme.MenuButton("Close", w, new Rect(win.width - 60f, 12f, 44f, 40f), "✕", CloseMenu,
                new Color(0.45f, 0.17f, 0.13f, 1f), 18);
            var rule = AshenUiTheme.Image("Rule", w, AshenUiTheme.Bar, AshenUiTheme.BronzeDim, false);
            AshenUiTheme.Place(rule.rectTransform, new Rect(16f, 58f, win.width - 32f, 2f));

            Rect content = new Rect(16f, 68f, win.width - 32f, win.height - 84f);
            var body = AshenUiTheme.Node("Content", w);
            AshenUiTheme.Place(body, content);
            if (_menuTab == MenuCharacter) BuildCharacterTab(body, content.size);
            else if (_menuTab == MenuInventory) BuildInventoryTab(body, content.size);
            else if (_menuTab == MenuQuests) BuildQuestTab(body, content.size);
            else BuildSettingsTab(body, content.size);
        }

        // ------------------------------------------------------------------ CHARACTER
        private void BuildCharacterTab(RectTransform body, Vector2 size)
        {
            var p = _game.Progression;
            float statsW = Mathf.Round(size.x * 0.36f);
            float slotsW = size.x - statsW - 12f;
            const float slot = 74f, gap = 10f;
            float colX0 = 0f, colX1 = slotsW - slot;
            float slotsTop = 8f;
            for (int i = 0; i < 4; i++)
            {
                EquipmentSlot(body, new Rect(colX0, slotsTop + i * (slot + gap + 14f), slot, slot), ArmorSlots[i], null, Color.white);
                bool weapon = i == 0;
                EquipmentSlot(body, new Rect(colX1, slotsTop + i * (slot + gap + 14f), slot, slot), TrinketSlots[i],
                    weapon ? "⚔\n+" + _game.WeaponBonus : null, weapon ? AshenUiTheme.Ember : Color.white);
            }

            Rect stage = new Rect(slot + 14f, 0f, slotsW - 2f * slot - 28f, size.y - 40f);
            var back = AshenUiTheme.Image("Stage", body, AshenUiTheme.Slot, new Color(0.85f, 0.85f, 0.85f, 1f));
            AshenUiTheme.Place(back.rectTransform, stage);
            var glow = AshenUiTheme.Image("Glow", back.transform, AshenUiTheme.Glow, new Color(1f, 0.45f, 0.15f, 0.20f));
            AshenUiTheme.Stretch(glow.rectTransform, 10f);
            var render = AshenUiTheme.Node("Hero", back.transform).gameObject.AddComponent<RawImage>();
            render.raycastTarget = false;
            AshenUiTheme.Stretch(render.rectTransform, 6f);
            render.texture = HeroPortrait();
            if (render.texture == null) render.color = Color.clear;
            var caption = AshenUiTheme.Label("Caption", body, 15, AshenUiTheme.Parchment, TextAnchor.MiddleCenter, FontStyle.Bold);
            AshenUiTheme.Place(caption.rectTransform, new Rect(stage.x, stage.yMax + 6f, stage.width, 30f));
            caption.text = "STUFE " + p.Level + "   ·   " + DisplayChapterName(_game);

            var note = AshenUiTheme.Label("Note", body, 11, AshenUiTheme.Muted, TextAnchor.UpperCenter);
            AshenUiTheme.Place(note.rectTransform, new Rect(0f, size.y - 32f, slot * 1f + 6f, 32f));

            Rect stats = new Rect(size.x - statsW, 0f, statsW, size.y);
            var panel = AshenUiTheme.Frame("Stats", body, stats, "Werte");
            float y = 34f;
            System.Action<string, string> row = (k, v) =>
            {
                var kt = AshenUiTheme.Label("K", panel.transform, 13, AshenUiTheme.Muted, TextAnchor.MiddleLeft);
                AshenUiTheme.Place(kt.rectTransform, new Rect(16f, y, statsW * 0.58f, 20f)); kt.text = k;
                var vt = AshenUiTheme.Label("V", panel.transform, 13, AshenUiTheme.Parchment, TextAnchor.MiddleRight, FontStyle.Bold);
                AshenUiTheme.Place(vt.rectTransform, new Rect(statsW * 0.5f, y, statsW * 0.5f - 16f, 20f)); vt.text = v;
                y += 21f;
            };
            row("Leben", _game.PlayerHealth + " / " + _game.MaxPlayerHealth);
            row("Fokus", _game.Resource + " / 100");
            row("Waffenbonus", "+" + _game.WeaponBonus);
            row("Erfahrung bis Stufe " + (p.Level + BigInteger.One), p.ExperienceToNextLevel.ToString());
            row("Besiegte Gegner", p.LifetimeKills.ToString());
            row("Gesammelte Beute", p.LootCollected.ToString());
            y += 6f;
            string[] mastery = { "Macht", "Vitalität", "Entschlossenheit" };
            BigInteger[] ranks = { p.MightRank, p.VitalityRank, p.ResolveRank };
            bool canSpend = p.UnspentMasteryPoints > BigInteger.Zero;
            var head = AshenUiTheme.Label("MasteryHead", panel.transform, 13, AshenUiTheme.Bronze, TextAnchor.MiddleLeft, FontStyle.Bold);
            AshenUiTheme.Place(head.rectTransform, new Rect(16f, y, statsW - 32f, 20f));
            head.text = "MEISTERSCHAFT  ·  " + (canSpend ? "<color=#F7A23A>" + p.UnspentMasteryPoints + " Punkte frei</color>" : "keine Punkte frei");
            y += 24f;
            for (int i = 0; i < 3; i++)
            {
                int path = i;
                var kt = AshenUiTheme.Label("M", panel.transform, 13, AshenUiTheme.Muted, TextAnchor.MiddleLeft);
                AshenUiTheme.Place(kt.rectTransform, new Rect(16f, y, statsW * 0.5f, 26f)); kt.text = mastery[i];
                var vt = AshenUiTheme.Label("R", panel.transform, 13, AshenUiTheme.Parchment, TextAnchor.MiddleRight, FontStyle.Bold);
                AshenUiTheme.Place(vt.rectTransform, new Rect(statsW * 0.5f, y, statsW * 0.5f - 60f, 26f)); vt.text = "Rang " + ranks[i];
                var b = AshenUiTheme.MenuButton("Spend" + i, panel.transform, new Rect(statsW - 52f, y, 36f, 26f), "+",
                    () => SpendMasteryFromMenu((MasteryPath)path), new Color(0.58f, 0.34f, 0.12f, 1f), 16);
                b.interactable = canSpend;
                y += 30f;
            }
            y += 4f;
            var dh = AshenUiTheme.Label("DisciplineHead", panel.transform, 13, AshenUiTheme.Bronze, TextAnchor.MiddleLeft, FontStyle.Bold);
            AshenUiTheme.Place(dh.rectTransform, new Rect(16f, y, statsW - 32f, 20f)); dh.text = "DISZIPLINEN  ·  Lernen durch Tun";
            y += 22f;
            for (int i = 0; i < 4; i++)
                row(SkillLabels[i].Substring(0, 1) + SkillLabels[i].Substring(1).ToLowerInvariant(),
                    "Rang " + p.PracticeLevel(i) + "  ·  " + p.PracticeExperience(i) + " EP");
        }

        private void EquipmentSlot(Transform parent, Rect r, string label, string content, Color accent)
        {
            var s = AshenUiTheme.Image("Slot_" + label, parent, AshenUiTheme.Slot, Color.white);
            AshenUiTheme.Place(s.rectTransform, r);
            if (content != null)
            {
                var g = AshenUiTheme.Image("Glow", s.transform, AshenUiTheme.Glow, new Color(accent.r, accent.g, accent.b, 0.35f));
                AshenUiTheme.Stretch(g.rectTransform, 4f);
                var t = AshenUiTheme.Label("Item", s.transform, 17, AshenUiTheme.Parchment, TextAnchor.MiddleCenter, FontStyle.Bold);
                AshenUiTheme.Stretch(t.rectTransform, 4f); t.text = content;
            }
            else
            {
                var t = AshenUiTheme.Label("Empty", s.transform, 22, new Color(1f, 1f, 1f, 0.12f), TextAnchor.MiddleCenter, FontStyle.Bold, false);
                AshenUiTheme.Stretch(t.rectTransform); t.text = "—";
            }
            var cap = AshenUiTheme.Label("Caption", parent, 11, AshenUiTheme.Bronze, TextAnchor.UpperCenter, FontStyle.Bold);
            AshenUiTheme.Place(cap.rectTransform, new Rect(r.x - 10f, r.yMax + 1f, r.width + 20f, 14f)); cap.text = label;
        }

        /// <summary>Spend through the same Core call as the HUD quick buttons, then persist and redraw.</summary>
        private void SpendMasteryFromMenu(MasteryPath path)
        {
            bool spent = _game.Progression.TrySpendMastery(path);
            _message = spent ? "Meisterschaft verteilt: " + MasteryName(path) : "Keine Meisterschaftspunkte frei.";
            if (spent) SaveCharacter();
            BuildMenu();
        }

        public static string MasteryName(MasteryPath path)
        { return path == MasteryPath.Might ? "Macht" : path == MasteryPath.Vitality ? "Vitalität" : "Entschlossenheit"; }

        /// <summary>Render the actual hero (only its layer) into a texture for the character screen.</summary>
        private RenderTexture HeroPortrait()
        {
            if (_hero == null) return null;
            if (_portraitTexture == null)
            {
                _portraitTexture = new RenderTexture(512, 640, 24, RenderTextureFormat.ARGB32) { name = "ARE_HeroPortrait", antiAliasing = 2 };
                _portraitTexture.Create();
            }
            if (_portraitCamera == null)
            {
                var go = new GameObject("ARE_PortraitCamera");
                go.transform.SetParent(transform, false);
                _portraitCamera = go.AddComponent<Camera>();
                _portraitCamera.clearFlags = CameraClearFlags.SolidColor;
                _portraitCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                _portraitCamera.fieldOfView = 30f; _portraitCamera.nearClipPlane = 0.05f; _portraitCamera.farClipPlane = 30f;
                _portraitCamera.cullingMask = 1 << PortraitLayer;
                _portraitCamera.targetTexture = _portraitTexture;
                _portraitCamera.depth = -10;
            }
            SetLayerRecursively(_hero, PortraitLayer);
            _portraitCamera.enabled = true;
            AimPortraitCamera();
            return _portraitTexture;
        }

        /// <summary>Frame the hero's actual rendered bounds (any model size) from the front, slightly above.</summary>
        private void AimPortraitCamera()
        {
            if (_portraitCamera == null || _hero == null) return;
            Vector3 forward = _hero.forward; forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.back;
            forward.Normalize();
            Bounds bounds = new Bounds(_hero.position + Vector3.up, new Vector3(0.6f, 1.8f, 0.6f));
            bool any = false;
            foreach (var r in _hero.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue;
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
            float height = Mathf.Max(bounds.size.y, 0.8f) * 1.18f;
            float distance = height * 0.5f / Mathf.Tan(_portraitCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Vector3 target = bounds.center;
            _portraitCamera.transform.position = target + forward * distance + Vector3.up * height * 0.08f;
            _portraitCamera.transform.LookAt(target);
        }

        private void SetLayerRecursively(Transform root, int layer)
        {
            if (!_portraitOriginalLayers.ContainsKey(root)) _portraitOriginalLayers.Add(root, root.gameObject.layer);
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++) SetLayerRecursively(root.GetChild(i), layer);
        }

        private void RestorePortraitLayers()
        {
            if (_portraitCamera != null) _portraitCamera.enabled = false;
            foreach (var entry in _portraitOriginalLayers)
                if (entry.Key != null) entry.Key.gameObject.layer = entry.Value;
            _portraitOriginalLayers.Clear();
        }

        private void ClearPortraitVisuals()
        {
            RestorePortraitLayers();
            if (_portraitCamera != null) Destroy(_portraitCamera.gameObject);
            if (_portraitTexture != null) { _portraitTexture.Release(); Destroy(_portraitTexture); }
            _portraitCamera = null; _portraitTexture = null;
        }

        // ------------------------------------------------------------------ INVENTORY
        public static string ItemName(LootItem item)
        {
            string id = item.DefinitionId ?? "";
            string baseName = id.Contains("crown") ? "Kronensplitter" : id.Contains("iron") ? "Eisensplitter" : id.Contains("ash") ? "Aschensplitter" : "Splitter";
            return baseName;
        }

        public static string RarityName(ItemRarity rarity) { return rarity == ItemRarity.Rare ? "Selten" : "Magisch"; }

        private void BuildInventoryTab(RectTransform body, Vector2 size)
        {
            var p = _game.Progression;
            var items = p.Items;
            float detailW = Mathf.Round(size.x * 0.34f);
            float gridW = size.x - detailW - 14f;
            const int cols = 6, rows = 5;
            float cell = Mathf.Floor(Mathf.Min((gridW - (cols - 1) * 8f) / cols, (size.y - 44f - (rows - 1) * 8f) / rows));

            var purse = AshenUiTheme.Label("Purse", body, 14, AshenUiTheme.Parchment, TextAnchor.MiddleLeft, FontStyle.Bold);
            AshenUiTheme.Place(purse.rectTransform, new Rect(0f, 0f, gridW, 28f));
            purse.text = "<color=#F2C14E>●</color> Münzen " + p.Social.Coins + "      <color=#C9A26B>■</color> Proviant " + p.Social.Provisions +
                "      Gegenstände " + items.Count;

            int first = Mathf.Max(0, items.Count - cols * rows);
            if (_selectedItem >= items.Count) _selectedItem = items.Count - 1;
            for (int i = 0; i < cols * rows; i++)
            {
                int index = first + i;
                Rect r = new Rect((i % cols) * (cell + 8f), 36f + (i / cols) * (cell + 8f), cell, cell);
                var s = AshenUiTheme.Image("Cell" + i, body, AshenUiTheme.Slot, Color.white);
                AshenUiTheme.Place(s.rectTransform, r);
                if (index >= items.Count) continue;
                LootItem item = items[index];
                Color rc = AshenUiTheme.RarityColor(item.Rarity);
                var glow = AshenUiTheme.Image("Rarity", s.transform, AshenUiTheme.Glow, new Color(rc.r, rc.g, rc.b, index == _selectedItem ? 0.55f : 0.28f));
                AshenUiTheme.Stretch(glow.rectTransform, 3f);
                var t = AshenUiTheme.Label("Glyph", s.transform, Mathf.RoundToInt(cell * 0.36f), rc, TextAnchor.MiddleCenter, FontStyle.Bold);
                AshenUiTheme.Stretch(t.rectTransform); t.text = "◆";
                var v = AshenUiTheme.Label("Value", s.transform, 11, AshenUiTheme.Parchment, TextAnchor.LowerRight, FontStyle.Bold);
                AshenUiTheme.Stretch(v.rectTransform, 5f); v.text = "+" + item.AffixValue;
                var button = s.gameObject.AddComponent<Button>();
                s.raycastTarget = true;
                int pick = index;
                button.onClick.AddListener(() => { _selectedItem = pick; BuildMenu(); });
            }

            Rect detail = new Rect(size.x - detailW, 0f, detailW, size.y);
            var panel = AshenUiTheme.Frame("Detail", body, detail, "Gegenstand");
            var text = AshenUiTheme.Label("Text", panel.transform, 13, AshenUiTheme.Parchment, TextAnchor.UpperLeft);
            AshenUiTheme.Place(text.rectTransform, new Rect(16f, 36f, detailW - 32f, size.y - 52f));
            if (items.Count == 0)
                text.text = "Noch keine Beute.\n\nBesiege Gegner und hebe ihre Splitter auf. Jeder Splitter verstärkt deine Waffe dauerhaft.";
            else
            {
                LootItem item = items[_selectedItem < 0 ? items.Count - 1 : _selectedItem];
                string hex = ColorUtility.ToHtmlStringRGB(AshenUiTheme.RarityColor(item.Rarity));
                text.text = "<size=18><b><color=#" + hex + ">" + ItemName(item) + "</color></b></size>\n" +
                    "<color=#" + hex + ">" + RarityName(item.Rarity) + "</color>\n\n" +
                    "Waffe <b>+" + item.AffixValue + "</b>  <color=#A89A84>(dauerhaft eingesetzt)</color>\n\n" +
                    "<color=#A89A84>Erbeutet beim " + item.KillOrdinal + ". Sieg.\nKennung " + item.InstanceId.Substring(0, Mathf.Min(18, item.InstanceId.Length)) + "…</color>";
            }
        }

        // ------------------------------------------------------------------ QUESTS
        private void BuildQuestTab(RectTransform body, Vector2 size)
        {
            var p = _game.Progression;
            float leftW = Mathf.Round(size.x * 0.55f);
            var active = AshenUiTheme.Frame("Active", body, new Rect(0f, 0f, leftW, size.y), "Aktuelle Aufgabe");
            var objective = AshenUiTheme.Label("Objective", active.transform, 17, AshenUiTheme.Parchment, TextAnchor.UpperLeft, FontStyle.Bold);
            AshenUiTheme.Place(objective.rectTransform, new Rect(18f, 40f, leftW - 36f, 90f));
            objective.text = "<color=#F7A23A>◆</color> " + ObjectiveText(_game);
            var where = AshenUiTheme.Label("Where", active.transform, 13, AshenUiTheme.Muted, TextAnchor.UpperLeft);
            AshenUiTheme.Place(where.rectTransform, new Rect(18f, 134f, leftW - 36f, Mathf.Max(60f, size.y - 210f)));
            where.text = "Gebiet: " + DisplayChapterName(_game) + "\nLetzte Nachricht: " + _message;
            if (p.EntryAct >= 2)
                AshenUiTheme.MenuButton("Journal", active.transform, new Rect(18f, size.y - 60f, leftW - 36f, 42f),
                    "SPUREN UND ENTSCHEIDUNGEN ÖFFNEN", () => { CloseMenu(); _hungerJournalOpen = true; },
                    new Color(0.55f, 0.32f, 0.12f, 1f), 15);

            var log = AshenUiTheme.Frame("Log", body, new Rect(leftW + 12f, 0f, size.x - leftW - 12f, size.y), "Chronik");
            float y = 38f;
            foreach (var entry in QuestMilestones(p))
            {
                var t = AshenUiTheme.Label("Entry", log.transform, 13, entry.Value ? AshenUiTheme.Parchment : AshenUiTheme.Muted, TextAnchor.UpperLeft, entry.Value ? FontStyle.Bold : FontStyle.Normal);
                AshenUiTheme.Place(t.rectTransform, new Rect(16f, y, size.x - leftW - 44f, 22f));
                t.text = (entry.Value ? "<color=#8FD16A>✔</color>  " : "<color=#6E6355>○</color>  ") + entry.Key;
                y += 25f;
            }
        }

        /// <summary>Milestones derived only from persisted Core progress flags.</summary>
        public static List<KeyValuePair<string, bool>> QuestMilestones(CharacterProgression p)
        {
            var list = new List<KeyValuePair<string, bool>>();
            System.Action<string, bool> add = (n, d) => list.Add(new KeyValuePair<string, bool>(n, d));
            add("Aus der Krypta entkommen", p.EntryAct >= 2);
            add("Ilyans Geheimnis gehört", p.HungerStage >= 1);
            add("Spuren an der Aschestraße", p.HungerStage >= 2);
            add("Das Verletztenlager in der alten Mühle", p.HungerStage >= 3);
            add("Über die Vorräte entschieden", p.SupplyChoice != HungerChoice.Undecided);
            add("Der Jäger auf der Aschestraße", p.Road.Started);
            add("Speicher und verborgene Krypta", p.Investigation.Active || p.HungerStage >= 4);
            return list;
        }

        // ------------------------------------------------------------------ SETTINGS
        private void BuildSettingsTab(RectTransform body, Vector2 size)
        {
            float colW = Mathf.Round(size.x * 0.5f - 6f);
            var audio = AshenUiTheme.Frame("Audio", body, new Rect(0f, 0f, colW, size.y), "Ton und Effekte");
            var a = _atmosphere;
            if (a == null)
            {
                var none = AshenUiTheme.Label("None", audio.transform, 13, AshenUiTheme.Muted, TextAnchor.UpperLeft);
                AshenUiTheme.Place(none.rectTransform, new Rect(16f, 40f, colW - 32f, 40f)); none.text = "Audio ist nicht initialisiert.";
            }
            else if (_menuCredits)
            {
                if (_audioCredits == null) _audioCredits = Resources.Load<TextAsset>("AshenAudioCredits");
                var credits = AshenUiTheme.Label("Credits", audio.transform, 11, AshenUiTheme.Muted, TextAnchor.UpperLeft);
                AshenUiTheme.Place(credits.rectTransform, new Rect(16f, 36f, colW - 32f, size.y - 96f));
                credits.text = _audioCredits != null ? _audioCredits.text : "Credits fehlen.";
                AshenUiTheme.MenuButton("Back", audio.transform, new Rect(16f, size.y - 54f, colW - 32f, 40f), "ZURÜCK",
                    () => { _menuCredits = false; BuildMenu(); });
            }
            else
            {
                string[] names = { "Sprache", "Musik", "Umgebung / Kampf" };
                float[] values = { a.VoiceVolume, a.MusicVolume, a.EffectsVolume };
                int[] options = { 0, 1, 2 };
                for (int i = 0; i < 3; i++)
                {
                    int option = options[i];
                    float y = 40f + i * 52f;
                    var n = AshenUiTheme.Label("Name", audio.transform, 14, AshenUiTheme.Parchment, TextAnchor.MiddleLeft, FontStyle.Bold);
                    AshenUiTheme.Place(n.rectTransform, new Rect(16f, y, colW * 0.42f, 40f)); n.text = names[i];
                    var barBack = AshenUiTheme.Image("Bar", audio.transform, AshenUiTheme.Bar, new Color(0.1f, 0.08f, 0.06f, 1f));
                    AshenUiTheme.Place(barBack.rectTransform, new Rect(colW * 0.42f, y + 12f, colW * 0.34f, 16f));
                    var fill = FilledBar("Fill", barBack.transform, AshenUiTheme.Experience); fill.fillAmount = values[i];
                    AshenUiTheme.MenuButton("Step", audio.transform, new Rect(colW - 82f, y + 2f, 66f, 36f),
                        Mathf.RoundToInt(values[i] * 100f) + "%", () => { ChangeAudioOption(option); BuildMenu(); });
                }
                AshenUiTheme.MenuButton("Mute", audio.transform, new Rect(16f, 202f, colW - 32f, 40f),
                    a.Muted ? "GESAMTER TON: AUS" : "GESAMTER TON: AN", () => { ChangeAudioOption(3); BuildMenu(); });
                AshenUiTheme.MenuButton("Particles", audio.transform, new Rect(16f, 250f, colW - 32f, 40f),
                    a.ReducedEffects ? "PARTIKEL: REDUZIERT" : "PARTIKEL: NORMAL", () => { ChangeAudioOption(4); BuildMenu(); });
                AshenUiTheme.MenuButton("Credits", audio.transform, new Rect(16f, 298f, colW - 32f, 40f),
                    "STIMMEN UND QUELLEN", () => { _menuCredits = true; BuildMenu(); });
            }

            var view = AshenUiTheme.Frame("View", body, new Rect(colW + 12f, 0f, colW, size.y), "Kamera und Steuerung");
            AshenUiTheme.MenuButton("CameraAuto", view.transform, new Rect(16f, 40f, colW - 32f, 40f), "KAMERA HINTER DEN HELDEN",
                () => { ApplyCameraControl(2); _message = "Die Kamera steht wieder hinter dem Helden."; });
            var help = AshenUiTheme.Label("Help", view.transform, 13, AshenUiTheme.Muted, TextAnchor.UpperLeft);
            AshenUiTheme.Place(help.rectTransform, new Rect(16f, 92f, colW - 32f, size.y - 108f));
            help.text = Application.isMobilePlatform
                ? "<b><color=#F3E5C8>Touch</color></b>\nLinks unten ziehen: bewegen\nRechts unten: Klinge und Fertigkeiten\nFreie Fläche ziehen: Kamera drehen\nZwei Finger: Kamera zoomen\nOben rechts: Held, Tasche, Quests, Optionen"
                : "<b><color=#F3E5C8>Tastatur und Maus</color></b>\nWASD: bewegen    1–4: Fertigkeiten    E: aufheben\nF / V / B / H: grüßen, versorgen, angreifen, ausrauben\nK: Held    I: Tasche    L: Quests    F8: Optionen    J: Spuren\nRechte Maustaste ziehen: Kamera drehen    Mausrad: Zoom\nEsc: Menü schließen";
        }

        /// <summary>Keep the portrait camera aimed while the character screen is visible.</summary>
        private void UpdateMenu()
        {
            if (_menuTab == MenuCharacter) AimPortraitCamera();
            else if (_portraitCamera != null && _portraitCamera.enabled) _portraitCamera.enabled = false;
        }
    }
}
