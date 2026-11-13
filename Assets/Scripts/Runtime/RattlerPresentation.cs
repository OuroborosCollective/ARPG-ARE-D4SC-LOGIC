using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        private Transform _rattler, _rattlerWarning;
        private Animator _rattlerAnimator;
        private string _rattlerAnimation = "";

        /// <summary>Reuse the contextual-action area in this region without overlapping the journal or combat buttons.</summary>
        public static Rect RattlerSneakRect(ArenaHudLayout layout)
        { return new Rect(layout.Width * .35f, layout.Height - 180f, layout.Width * .28f, 42f); }

        private bool PollRattlerInput(ArenaHudLayout layout)
        {
            if (_game.Progression.EntryAct != 7 || _game.GameOver || _hungerJournalOpen) return false;
            bool pressed = Input.GetKeyDown(KeyCode.C);
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began && layout.IsWithinSafeArea(t.position) && RattlerSneakRect(layout).Contains(layout.ToGuiTouch(t.position))) pressed = true;
            }
            if (!pressed) return false;
            _pendingHungerAction = (int)HungerAction.ToggleSneak;
            _game.SetMoveInput(0, 0); ReleasePresentationTouches(); return true;
        }

        private string RattlerStatusText
        {
            get
            {
                var r = _game.Progression.Rattler;
                return r.Creature.IsDead ? "MAULWURFRATTLER BESIEGT\nUntersuche seinen Hals unter SPUREN." :
                    "MAULWURFRATTLER  " + r.Creature.Health + "/" + r.Creature.MaxHealth +
                    "\nBlind: Laerm / naher Geruch. Zum Ausweichen SCHLEICHEN AUS!";
            }
        }

        private void DrawRattlerHud(ArenaHudLayout layout)
        {
            if (_game.Progression.EntryAct != 7 || _hungerJournalOpen) return;
            var r = _game.Progression.Rattler;
            GUI.Box(new Rect(layout.Width * .22f, layout.StatusPanelRect.yMax + 8f, layout.Width * .56f, 62f), RattlerStatusText);
            string label = r.Sneaking ? "SCHLEICHEN: AN" : "SCHLEICHEN: AUS";
            if (Application.isMobilePlatform) GUI.Box(RattlerSneakRect(layout), label);
            else if (GUI.Button(RattlerSneakRect(layout), "C: " + label)) _pendingHungerAction = (int)HungerAction.ToggleSneak;
        }

        /// <summary>A clear combat floor shares the Core footprint; distant wall recesses imply a former chained den.</summary>
        private void BuildRattlerDen()
        {
            RenderSettings.ambientLight = new Color(.22f, .28f, .27f);
            RenderSettings.fogColor = new Color(.10f, .14f, .14f); RenderSettings.fogDensity = .013f;
            ChapterShape(PrimitiveType.Cube, "RattlerDen_Ground", new Vector3(0, -.35f, -3), new Vector3(25, .5f, 48), new Color(.20f, .23f, .21f));
            for (int z = -24; z <= 18; z += 6)
                for (int x = -9; x <= 9; x += 6)
                    ChapterShape(PrimitiveType.Cube, "RattlerDen_Floor", new Vector3(x, -.06f, z), new Vector3(6, .12f, 6), new Color(.27f, .30f, .27f), "AshStoneFloor");
            for (int side = -1; side <= 1; side += 2)
                for (int z = -24; z <= 18; z += 6)
                    ChapterShape(PrimitiveType.Cube, "RattlerDen_Wall", new Vector3(side * 12.5f, 1.5f, z), new Vector3(1, 3, 6), new Color(.25f, .29f, .27f), "AshStoneWall");
            for (int x = -9; x <= 9; x += 6)
                ChapterShape(PrimitiveType.Cube, "RattlerDen_NorthWall", new Vector3(x, 1.5f, 21), new Vector3(6, 3, 1), new Color(.25f, .29f, .27f), "AshStoneWall");
            ChapterShape(PrimitiveType.Cylinder, "RattlerDen_FeedingTrace", new Vector3(0, .02f, 8), new Vector3(2, .01f, 2), new Color(.23f, .10f, .08f));
            for (int i = 0; i < 7; i++)
                ChapterShape(PrimitiveType.Cube, "RattlerDen_DragTrace", new Vector3(.6f, .015f, -14 + i * 3), new Vector3(.18f, .02f, 1.4f), new Color(.13f, .08f, .06f));
            _returnPassage = ChapterShape(PrimitiveType.Cylinder, "ARE_Return_Passage", new Vector3(0, .035f, -24), new Vector3(3.6f, .02f, 3.6f), new Color(.45f, .65f, .72f));
            _worldPassage = null;
        }

        private void ClearRattlerVisuals()
        {
            if (_rattler != null) Destroy(_rattler.gameObject);
            if (_rattlerWarning != null) Destroy(_rattlerWarning.gameObject);
            _rattler = _rattlerWarning = null; _rattlerAnimator = null; _rattlerAnimation = "";
        }

        /// <summary>Project only persisted combat state; passive private art cannot supply navigation or damage.</summary>
        private void UpdateRattlerPresentation()
        {
            if (_game.Progression.EntryAct != 7) { if (_rattler != null) ClearRattlerVisuals(); return; }
            var r = _game.Progression.Rattler; var m = r.Creature;
            if (_rattler == null)
            {
                _rattler = Shape(PrimitiveType.Capsule, "ARE_MaulwurfRattler", Vector3.zero, Vector3.one, new Color(.40f, .32f, .28f));
                PassiveArtVisuals.TryBindGrounded(_rattler, "CryptRattler", 0);
                _rattlerAnimator = _rattler.GetComponentInChildren<Animator>();
                if (_rattlerAnimator != null) _rattlerAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                _rattlerWarning = Shape(PrimitiveType.Cylinder, "ARE_Rattler_Strike_Warning", Vector3.zero,
                    new Vector3(RattlerEncounter.StrikeRadius / 500f, .012f, RattlerEncounter.StrikeRadius / 500f), new Color(.9f, .15f, .08f));
            }
            _rattler.position = new Vector3(m.Xmm / 1000f, 0, m.Zmm / 1000f);
            Vector3 facing = new Vector3((r.TargetX - m.Xmm) / 1000f, 0, (r.TargetZ - m.Zmm) / 1000f);
            if (!m.IsDead && facing.sqrMagnitude > .01f) _rattler.rotation = Quaternion.LookRotation(facing);
            string state = m.IsDead ? "Death" : m.BossWindupTicks > 0 ? "Attack" :
                m.Xmm != m.PreviousXmm || m.Zmm != m.PreviousZmm ? "Run" : "Idle";
            if (_rattlerAnimator != null && _rattlerAnimation != state)
            { _rattlerAnimator.CrossFade(state, .1f); _rattlerAnimation = state; }
            _rattlerWarning.gameObject.SetActive(m.BossWindupTicks > 0);
            _rattlerWarning.position = new Vector3(r.StrikeX / 1000f, .04f, r.StrikeZ / 1000f);
        }
    }
}
