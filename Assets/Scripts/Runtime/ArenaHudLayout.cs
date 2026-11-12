using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>Pure UI targets. No simulation, persistence or world authority.</summary>
    public enum ArenaHudControl
    {
        None = 0,
        Move,
        Pickup,
        Donate,
        NpcGreet,
        NpcHelp,
        NpcAttack,
        NpcRob,
        Skill0,
        Skill1,
        Skill2,
        Skill3,
        MasteryMight,
        MasteryVitality,
        MasteryResolve,
        Restart,
        TakeRation,
        CameraCloser,
        CameraFarther,
        CameraReset,
        MenuCharacter,
        MenuInventory,
        MenuQuests,
        MenuSettings
    }

    /// <summary>
    /// Captures only a MOVE-initiated touch finger until release/cancel; keeps UI
    /// hit testing separate from the one authoritative game movement input.
    /// </summary>
    public sealed class ArenaHudTouchCapture
    {
        private int _fingerId = -1;
        private Vector2 _origin;

        public bool IsCaptured { get { return _fingerId >= 0; } }
        public int FingerId { get { return _fingerId; } }
        /// <summary>Where the captured finger first touched; the floating stick is drawn around it.</summary>
        public Vector2 Origin { get { return _origin; } }
        /// <summary>Last normalized stick deflection for presentation only.</summary>
        public Vector2 Direction { get; private set; }

        /// <summary>Release a held finger when the game resets or focus/touch disappears.</summary>
        public void Reset() { _fingerId = -1; Direction = Vector2.zero; }

        /// <summary>
        /// Accept MOVE only at TouchPhase.Began, then retain that finger even
        /// beyond MOVE and the safe area. The first contact point is neutral (floating stick).
        /// Returns false for other fingers and all released/canceled input.
        /// </summary>
        public bool TryMove(int fingerId, TouchPhase phase, Vector2 guiPoint,
            ArenaHudLayout layout, ArenaHudControl controlAtStart, out Vector2 direction)
        {
            direction = Vector2.zero;
            if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled)
            {
                if (_fingerId == fingerId) Reset();
                return false;
            }

            if (!IsCaptured && phase == TouchPhase.Began && controlAtStart == ArenaHudControl.Move)
            { _fingerId = fingerId; _origin = guiPoint; }
            if (_fingerId != fingerId) return false;

            Vector2 offset = guiPoint - _origin;
            direction = Vector2.ClampMagnitude(new Vector2(offset.x, -offset.y) / ArenaHudLayout.StickRadius, 1f);
            Direction = direction;
            return true;
        }
    }

    /// <summary>
    /// Immutable mobile-ARPG layout in GUI top-left coordinates, including a single affine mapping
    /// from Unity's bottom-left physical touch coordinates. The same rectangles drive the uGUI
    /// projection and touch hit testing. Gameplay stays in ArenaSimulation.
    /// </summary>
    public readonly struct ArenaHudLayout
    {
        private const float MinimumWidth = 960f;
        private const float MinimumHeight = 540f;
        public const float StickRadius = 75f;
        public const int MenuCount = 4;
        private readonly float _screenHeight;

        public Rect SafeArea { get; }
        public float Scale { get; }
        public float Width { get; }
        public float Height { get; }

        /// <summary>Map the physical safe area into one logical canvas shared by drawing and touch hit testing.</summary>
        public ArenaHudLayout(float screenWidth, float screenHeight, Rect safeArea)
        {
            float w = Mathf.Max(screenWidth, 1f);
            _screenHeight = Mathf.Max(screenHeight, 1f);
            float left = Mathf.Clamp(safeArea.xMin, 0f, w);
            float right = Mathf.Clamp(safeArea.xMax, 0f, w);
            float bottom = Mathf.Clamp(safeArea.yMin, 0f, _screenHeight);
            float top = Mathf.Clamp(safeArea.yMax, 0f, _screenHeight);
            SafeArea = right > left && top > bottom
                ? Rect.MinMaxRect(left, bottom, right, top)
                : new Rect(0f, 0f, w, _screenHeight);

            // Scale up as well as down: the 960x540 logical canvas fills phones and tablets, so touch
            // targets keep a constant share of the screen (a 2400x1080 phone renders the HUD at 2x).
            Scale = Mathf.Min(SafeArea.width / MinimumWidth, SafeArea.height / MinimumHeight);
            Width = SafeArea.width / Scale;
            Height = SafeArea.height / Scale;
        }

        /// <summary>GUI.matrix must be restored by the rendering caller.</summary>
        public Matrix4x4 GuiMatrix
        {
            get
            {
                return Matrix4x4.TRS(new Vector3(SafeArea.xMin,
                    _screenHeight - SafeArea.yMax, 0f), Quaternion.identity,
                    new Vector3(Scale, Scale, 1f));
            }
        }

        /// <summary>Reject new touches in system-notched and noninteractive screen regions.</summary>
        public bool IsWithinSafeArea(Vector2 screenPoint)
        {
            return SafeArea.Contains(screenPoint);
        }

        /// <summary>Convert physical bottom-left touch pixels into scaled GUI coordinates.</summary>
        public Vector2 ToGuiTouch(Vector2 screenPoint)
        {
            return new Vector2((screenPoint.x - SafeArea.xMin) / Scale,
                (SafeArea.yMax - screenPoint.y) / Scale);
        }

        /// <summary>Map the same GUI targets back to physical bottom-left touch pixels.</summary>
        public Vector2 ToScreenTouch(Vector2 guiPoint)
        {
            return new Vector2(SafeArea.xMin + guiPoint.x * Scale,
                SafeArea.yMax - guiPoint.y * Scale);
        }

        // ---- Left column: portrait/status, quest tracker, village camp ----
        private float LeftColumnWidth { get { return Mathf.Min(326f, Width * 0.34f); } }
        public Rect StatusPanelRect { get { return new Rect(12f, 12f, LeftColumnWidth, 96f); } }
        public Rect PortraitRect { get { return new Rect(18f, 18f, 84f, 84f); } }
        public Rect HealthBarRect { get { return new Rect(112f, 50f, LeftColumnWidth - 112f, 18f); } }
        public Rect FocusBarRect { get { return new Rect(112f, 74f, LeftColumnWidth - 112f, 12f); } }
        public Rect QuestTrackerRect { get { return new Rect(12f, 116f, LeftColumnWidth, 60f); } }
        public Rect CampPanelRect { get { return new Rect(12f, 184f, LeftColumnWidth, 140f); } }
        public Rect DonationRect { get { return new Rect(16f, 248f, LeftColumnWidth - 8f, 34f); } }
        public Rect TakeRationRect { get { return new Rect(16f, 288f, LeftColumnWidth - 8f, 34f); } }

        // ---- Bottom-left floating stick zone ----
        public Rect MoveRect { get { return new Rect(24f, Height - 212f, 188f, 188f); } }

        // ---- Top-right: minimap, camera, menu bar ----
        public Rect MinimapRect { get { return new Rect(Width - 176f, 12f, 164f, 164f); } }
        public Rect CameraPanelRect { get { return new Rect(Width - 176f, 182f, 164f, 40f); } }

        /// <summary>Three presentation-only camera controls directly under the minimap.</summary>
        public Rect CameraZoomRect(int control)
        {
            return new Rect(Width - 174f + control * 54f, 184f, 50f, 36f);
        }

        /// <summary>Character, inventory, quests and settings in one row left of the minimap.</summary>
        public Rect MenuButtonRect(int menu)
        {
            return new Rect(Width - 188f - (MenuCount - menu) * 60f, 12f, 54f, 54f);
        }

        /// <summary>Subtitles live between the left column and the menu bar, under the menu row.</summary>
        public Rect SubtitleBandRect
        {
            get
            {
                float left = StatusPanelRect.xMax + 8f;
                return new Rect(left, 76f, Mathf.Max(120f, Width - 196f - left), 132f);
            }
        }

        // ---- Bottom-right: big attack and a three-skill arc ----
        private Vector2 AttackCenter { get { return new Vector2(Width - 78f, Height - 78f); } }

        /// <summary>Skill 0 is the large basic attack; skills 1-3 sit on a 136-unit arc around it.</summary>
        public Rect SkillRect(int skill)
        {
            Vector2 c = AttackCenter;
            if (skill <= 0) return new Rect(c.x - 60f, c.y - 60f, 120f, 120f);
            float angle = Mathf.Deg2Rad * (180f - (skill - 1) * 45f);
            Vector2 s = c + new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle)) * 136f;
            return new Rect(s.x - 35f, s.y - 35f, 70f, 70f);
        }

        /// <summary>Loot pickup floats above the skill arc, clear of camera and minimap.</summary>
        public Rect PickupRect { get { return new Rect(Width - 110f, Mathf.Max(228f, Height - 318f), 62f, 60f); } }

        // ---- Bottom centre: contextual actions, mastery, NPC dialogue card ----
        public Rect ContextActionRect { get { return new Rect(Width * 0.5f - 140f, Height - 306f, 280f, 46f); } }

        /// <summary>Unspent mastery points appear as three quick buttons above the dialogue card.</summary>
        public Rect MasteryRect(int mastery)
        {
            return new Rect(LeftColumnWidth + 20f + mastery * 124f, Height - 250f, 116f, 50f);
        }

        public Rect NpcPanelRect
        {
            get { return new Rect(230f, Height - 190f, Mathf.Max(300f, Width - 490f), 178f); }
        }

        /// <summary>Place one of four NPC choices in a 2x2 grid in the dialogue card.</summary>
        public Rect NpcActionRect(int action)
        {
            Rect p = NpcPanelRect;
            float half = p.width / 2f;
            return new Rect(p.x + 8f + (action % 2) * half, p.y + 92f + (action / 2) * 42f, half - 12f, 38f);
        }

        /// <summary>Thin experience bar across the bottom edge; never interactive.</summary>
        public Rect ExperienceBarRect { get { return new Rect(230f, Height - 9f, Mathf.Max(300f, Width - 490f), 6f); } }

        public Rect DefeatPanelRect
        {
            get { return new Rect(Width * 0.3f, Height * 0.38f, Width * 0.4f, 90f); }
        }

        public Rect RestartRect
        {
            get
            {
                Rect box = DefeatPanelRect;
                return new Rect(box.x, box.yMax, box.width, 48f);
            }
        }

        public static bool ShowGameplayControls(bool gameOver) { return !gameOver; }

        /// <summary>Pinches can start only in free world space, never in visible HUD panels or gameplay controls.</summary>
        public bool BlocksCameraGesture(Vector2 point, bool gameOver, bool nearNpc,
            bool chapterOne, bool hasMastery, bool canTakeRation = false)
        {
            if (gameOver || StatusPanelRect.Contains(point) || CameraPanelRect.Contains(point) ||
                MinimapRect.Contains(point) || QuestTrackerRect.Contains(point)) return true;
            if (nearNpc && NpcPanelRect.Contains(point)) return true;
            if (chapterOne && CampPanelRect.Contains(point)) return true;
            return HitTest(point, false, nearNpc, chapterOne, hasMastery, canTakeRation) != ArenaHudControl.None;
        }

        /// <summary>
        /// The same visual rectangles decide mobile touch actions. When defeated,
        /// every gameplay action is disabled and only Restart can be selected.
        /// </summary>
        public ArenaHudControl HitTest(Vector2 point, bool gameOver,
            bool nearNpc, bool chapterOne, bool hasMastery, bool canTakeRation = false)
        {
            if (gameOver)
                return RestartRect.Contains(point) ? ArenaHudControl.Restart : ArenaHudControl.None;

            for (int i = 0; i < MenuCount; i++)
                if (MenuButtonRect(i).Contains(point))
                    return (ArenaHudControl)((int)ArenaHudControl.MenuCharacter + i);

            for (int i = 0; i < 3; i++)
                if (CameraZoomRect(i).Contains(point))
                    return (ArenaHudControl)((int)ArenaHudControl.CameraCloser + i);

            if (nearNpc)
            {
                for (int i = 0; i < 4; i++)
                    if (NpcActionRect(i).Contains(point))
                        return (ArenaHudControl)((int)ArenaHudControl.NpcGreet + i);
            }

            if (chapterOne && DonationRect.Contains(point))
                return ArenaHudControl.Donate;
            if (canTakeRation && chapterOne && TakeRationRect.Contains(point))
                return ArenaHudControl.TakeRation;

            if (hasMastery)
            {
                for (int i = 0; i < 3; i++)
                    if (MasteryRect(i).Contains(point))
                        return (ArenaHudControl)((int)ArenaHudControl.MasteryMight + i);
            }

            for (int i = 0; i < 4; i++)
                if (SkillRect(i).Contains(point))
                    return (ArenaHudControl)((int)ArenaHudControl.Skill0 + i);

            if (PickupRect.Contains(point)) return ArenaHudControl.Pickup;
            if (MoveRect.Contains(point)) return ArenaHudControl.Move;
            return ArenaHudControl.None;
        }
    }
}
