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
        Restart
    }

    /// <summary>
    /// Immutable layout in GUI top-left coordinates, including a single affine mapping
    /// from Unity's bottom-left physical touch coordinates. Gameplay stays in ArenaSimulation.
    /// </summary>
    public readonly struct ArenaHudLayout
    {
        private const float MinimumWidth = 960f;
        private const float MinimumHeight = 540f;
        private readonly float _screenHeight;

        public Rect SafeArea { get; }
        public float Scale { get; }
        public float Width { get; }
        public float Height { get; }

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

            Scale = Mathf.Min(1f, Mathf.Min(SafeArea.width / MinimumWidth,
                SafeArea.height / MinimumHeight));
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

        public bool IsWithinSafeArea(Vector2 screenPoint)
        {
            return SafeArea.Contains(screenPoint);
        }

        public Vector2 ToGuiTouch(Vector2 screenPoint)
        {
            return new Vector2((screenPoint.x - SafeArea.xMin) / Scale,
                (SafeArea.yMax - screenPoint.y) / Scale);
        }

        public Vector2 ToScreenTouch(Vector2 guiPoint)
        {
            return new Vector2(SafeArea.xMin + guiPoint.x * Scale,
                SafeArea.yMax - guiPoint.y * Scale);
        }

        public Rect StatusPanelRect { get { return new Rect(8f, 8f, Mathf.Min(600f, Width * 0.8f), 132f); } }
        public Rect CampPanelRect { get { return new Rect(8f, 148f, Mathf.Min(310f, Width * 0.43f), 145f); } }
        public Rect DonationRect { get { return new Rect(12f, 249f, Mathf.Min(290f, Width * 0.40f), 37f); } }
        public Rect MoveRect { get { return new Rect(25f, Height - 194f, 170f, 170f); } }
        public Rect PickupRect { get { return new Rect(Width * 0.43f, Height - 72f, Width * 0.16f, 60f); } }

        public Rect NpcPanelRect
        {
            get
            {
                float w = Mathf.Min(300f, Width - 24f);
                return new Rect(Width - w - 15f, 144f, w + 5f, 210f);
            }
        }

        public Rect NpcActionRect(int action)
        {
            float w = Mathf.Min(300f, Width - 24f);
            return new Rect(Width - w - 10f + (action % 2) * (w / 2f),
                266f + (action / 2) * 42f, w / 2f - 5f, 38f);
        }

        public Rect MasteryRect(int mastery)
        {
            // Vertical center column; no button can cover Mara's supply panel.
            return new Rect(CampPanelRect.xMax + 12f, 148f + mastery * 62f, 124f, 56f);
        }

        public Rect SkillRect(int skill)
        {
            float w = Mathf.Clamp(Width * 0.125f, 74f, 145f);
            float h = Mathf.Clamp(Height * 0.13f, 48f, 67f);
            return new Rect(Width - (2 - skill % 2) * (w + 10f) - 12f,
                Height - (2 - skill / 2) * (h + 9f) - 16f, w, h);
        }

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

        /// <summary>
        /// The same visual rectangles decide mobile touch actions. When defeated,
        /// every gameplay action is disabled and only Restart can be selected.
        /// </summary>
        public ArenaHudControl HitTest(Vector2 point, bool gameOver,
            bool nearNpc, bool chapterOne, bool hasMastery)
        {
            if (gameOver)
                return RestartRect.Contains(point) ? ArenaHudControl.Restart : ArenaHudControl.None;

            if (nearNpc)
            {
                for (int i = 0; i < 4; i++)
                    if (NpcActionRect(i).Contains(point))
                        return (ArenaHudControl)((int)ArenaHudControl.NpcGreet + i);
            }

            if (chapterOne && DonationRect.Contains(point))
                return ArenaHudControl.Donate;

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
