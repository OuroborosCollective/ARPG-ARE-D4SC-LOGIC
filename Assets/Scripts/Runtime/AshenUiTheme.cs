using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// Presentation-only visual language of the Ashen Crypt UI: charcoal stone, aged bronze, ember accents.
    /// Sprites are generated procedurally so public clones look complete; an owner's private UI kit can
    /// override any key by placing a sprite under Resources/ARE/UI/&lt;key&gt; (kept out of public Git).
    /// </summary>
    public static class AshenUiTheme
    {
        public static readonly Color StoneDark = new Color(0.075f, 0.062f, 0.055f, 0.92f);
        public static readonly Color StoneLight = new Color(0.17f, 0.14f, 0.12f, 0.92f);
        public static readonly Color Bronze = new Color(0.66f, 0.50f, 0.29f, 1f);
        public static readonly Color BronzeDim = new Color(0.40f, 0.31f, 0.19f, 1f);
        public static readonly Color Ember = new Color(0.98f, 0.50f, 0.16f, 1f);
        public static readonly Color Parchment = new Color(0.96f, 0.90f, 0.78f, 1f);
        public static readonly Color Muted = new Color(0.76f, 0.72f, 0.64f, 1f);
        public static readonly Color Health = new Color(0.78f, 0.11f, 0.08f, 1f);
        public static readonly Color Focus = new Color(0.16f, 0.56f, 0.88f, 1f);
        public static readonly Color Experience = new Color(0.96f, 0.76f, 0.27f, 1f);
        public static readonly Color Danger = new Color(0.72f, 0.20f, 0.16f, 1f);
        public static readonly Color Magic = new Color(0.40f, 0.60f, 1f, 1f);
        public static readonly Color Rare = new Color(1f, 0.84f, 0.32f, 1f);
        public static readonly Color Shade = new Color(0f, 0f, 0f, 0.62f);

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.Load<Font>("ARE/UI/font");
                    if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return _font;
            }
        }

        /// <summary>True when the owner's private UI kit supplies at least the main panel sprite.</summary>
        public static bool UsesPrivateKit { get { return Resources.Load<Sprite>("ARE/UI/panel") != null; } }

        public static Sprite Panel { get { return Get("panel", () => RoundedRect(96, 14, 3, true)); } }
        public static Sprite PanelPlain { get { return Get("panel_plain", () => RoundedRect(64, 10, 2, false)); } }
        public static Sprite Slot { get { return Get("slot", () => SlotSprite(72)); } }
        public static Sprite RoundButton { get { return Get("button_round", () => Disc(160, 0.84f, true)); } }
        public static Sprite Ring { get { return Get("ring", () => Disc(160, 0.96f, false)); } }
        public static Sprite Bar { get { return Get("bar", () => BarSprite()); } }
        public static Sprite Glow { get { return Get("glow", () => GlowSprite(128)); } }

        /// <summary>Optional private icon (e.g. icon_character); null falls back to text glyphs.</summary>
        public static Sprite Icon(string key) { return Resources.Load<Sprite>("ARE/UI/icon_" + key); }

        public static Color RarityColor(AreArpg.Core.ItemRarity rarity)
        { return rarity == AreArpg.Core.ItemRarity.Rare ? Rare : Magic; }

        private static Sprite Get(string key, System.Func<Sprite> build)
        {
            Sprite sprite;
            if (Cache.TryGetValue(key, out sprite) && sprite != null) return sprite;
            sprite = Resources.Load<Sprite>("ARE/UI/" + key);
            if (sprite == null) sprite = build();
            Cache[key] = sprite;
            return sprite;
        }

        // ---------- factory helpers (all RectTransforms use top-left GUI coordinates of ArenaHudLayout) ----------

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            return rt;
        }

        /// <summary>Place a node at a top-left GUI rectangle relative to its parent's top-left.</summary>
        public static void Place(RectTransform rt, Rect r)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(r.x, -r.y);
            rt.sizeDelta = new Vector2(r.width, r.height);
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        }

        public static Image Image(string name, Transform parent, Sprite sprite, Color color, bool sliced = true)
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.color = color; img.raycastTarget = false;
            img.type = sliced && sprite != null && sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            return img;
        }

        public static Text Label(string name, Transform parent, int size, Color color, TextAnchor anchor,
            FontStyle style = FontStyle.Normal, bool shadow = true)
        {
            var rt = Node(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.fontSize = size; t.color = color; t.alignment = anchor; t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false; t.supportRichText = true;
            if (shadow)
            {
                var s = rt.gameObject.AddComponent<Shadow>();
                s.effectColor = new Color(0f, 0f, 0f, 0.85f); s.effectDistance = new Vector2(1.5f, -1.5f);
            }
            return t;
        }

        /// <summary>A framed panel with an optional engraved title strip.</summary>
        public static Image Frame(string name, Transform parent, Rect rect, string title = null)
        {
            var img = Image(name, parent, Panel, Color.white);
            Place(img.rectTransform, rect);
            if (!string.IsNullOrEmpty(title))
            {
                var t = Label("Title", img.transform, 15, Bronze, TextAnchor.UpperLeft, FontStyle.Bold);
                Place(t.rectTransform, new Rect(14f, 8f, rect.width - 28f, 22f));
                t.text = title.ToUpperInvariant();
            }
            return img;
        }

        /// <summary>An interactive uGUI button for menus (HUD buttons stay layout hit-tested).</summary>
        public static Button MenuButton(string name, Transform parent, Rect rect, string text, System.Action onClick,
            Color? tint = null, int fontSize = 16)
        {
            var img = Image(name, parent, PanelPlain, tint ?? new Color(0.30f, 0.24f, 0.18f, 1f));
            img.raycastTarget = true;
            Place(img.rectTransform, rect);
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(1.15f, 1.08f, 0.95f);
            colors.pressedColor = new Color(0.75f, 0.62f, 0.48f); colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.7f);
            b.colors = colors;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            var label = Label("Label", img.transform, fontSize, Parchment, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(label.rectTransform, 4f); label.text = text;
            return b;
        }

        // ---------- procedural sprite generation (deterministic, no external assets) ----------

        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        private static Texture2D NewTexture(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear; t.hideFlags = HideFlags.DontSave;
            return t;
        }

        private static float RoundedRectDistance(float px, float py, float w, float h, float r)
        {
            float qx = Mathf.Abs(px - w * 0.5f) - (w * 0.5f - r);
            float qy = Mathf.Abs(py - h * 0.5f) - (h * 0.5f - r);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        /// <summary>Stone panel: grained vertical gradient, bronze bevelled rim, dark inner line, soft edge.</summary>
        private static Sprite RoundedRect(int size, float radius, float rim, bool grain)
        {
            var t = NewTexture(size, size);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedRectDistance(x + 0.5f, y + 0.5f, size, size, radius);
                    float v = y / (float)(size - 1);
                    Color c = Color.Lerp(StoneDark, StoneLight, v * 0.9f);
                    if (grain) { float n = (Hash(x, y) - 0.5f) * 0.035f; c.r += n; c.g += n; c.b += n; }
                    if (d > -rim - 1.2f && d <= -1.2f)
                    {
                        float light = Mathf.Lerp(0.72f, 1.18f, v);
                        c = new Color(Bronze.r * light, Bronze.g * light, Bronze.b * light, 1f);
                    }
                    else if (d > -rim - 2.4f && d <= -rim - 1.2f) c = new Color(0.03f, 0.025f, 0.02f, 0.95f);
                    float alpha = Mathf.Clamp01(0.5f - d);
                    c.a *= alpha;
                    px[y * size + x] = c;
                }
            t.SetPixels(px); t.Apply(false, true);
            float b = radius + rim + 3f;
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(b, b, b, b));
        }

        /// <summary>Inventory slot: recessed dark socket with lit lower edge and thin bronze rim.</summary>
        private static Sprite SlotSprite(int size)
        {
            var t = NewTexture(size, size);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedRectDistance(x + 0.5f, y + 0.5f, size, size, 7f);
                    float shade = Mathf.Lerp(0.05f, 0.11f, 1f - y / (float)size) + (Hash(x, y) - 0.5f) * 0.02f;
                    Color c = new Color(shade, shade * 0.9f, shade * 0.8f, 0.95f);
                    if (d > -2.2f && d <= -0.8f) c = BronzeDim;
                    else if (d > -4.2f && d <= -2.2f) c = new Color(0f, 0f, 0f, 0.9f);
                    c.a *= Mathf.Clamp01(0.5f - d);
                    px[y * size + x] = c;
                }
            t.SetPixels(px); t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(12, 12, 12, 12));
        }

        /// <summary>Round button (filled) or ring (outline) with bronze rim and radial depth.</summary>
        private static Sprite Disc(int size, float fillTo, bool filled)
        {
            var t = NewTexture(size, size);
            var px = new Color[size * size];
            float r = size * 0.5f - 2f, cx = size * 0.5f, cy = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = r - d;
                    Color c = Color.clear;
                    float rimWidth = size * 0.045f;
                    if (edge >= 0f && edge < rimWidth)
                    {
                        float light = Mathf.Lerp(0.7f, 1.25f, (dy / r + 1f) * 0.5f);
                        c = new Color(Bronze.r * light, Bronze.g * light, Bronze.b * light, 1f);
                    }
                    else if (edge >= rimWidth && edge < rimWidth + 2.5f) c = new Color(0.02f, 0.015f, 0.01f, 0.95f);
                    else if (edge >= rimWidth + 2.5f)
                    {
                        if (filled)
                        {
                            float k = d / (r * fillTo);
                            float s = Mathf.Lerp(0.24f, 0.07f, k);
                            c = new Color(s * 1.05f, s * 0.92f, s * 0.8f, 0.94f);
                        }
                        else c = new Color(0.1f, 0.08f, 0.07f, 0.28f);
                    }
                    c.a *= Mathf.Clamp01(edge + 0.5f);
                    px[y * size + x] = c;
                }
            t.SetPixels(px); t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>White rounded bar with vertical shading; tinted per resource, sliced horizontally.</summary>
        private static Sprite BarSprite()
        {
            const int w = 48, h = 20;
            var t = NewTexture(w, h);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = RoundedRectDistance(x + 0.5f, y + 0.5f, w, h, 6f);
                    float v = y / (float)(h - 1);
                    float s = Mathf.Lerp(0.62f, 1f, v);
                    if (v > 0.62f && v < 0.82f) s += 0.18f;
                    var c = new Color(s, s, s, Mathf.Clamp01(0.5f - d));
                    px[y * w + x] = c;
                }
            t.SetPixels(px); t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(8, 0, 8, 0));
        }

        private static Sprite GlowSprite(int size)
        {
            var t = NewTexture(size, size);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a * a);
                }
            t.SetPixels(px); t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
