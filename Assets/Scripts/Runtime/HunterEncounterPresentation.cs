using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        private Transform _roadBear, _roadHunter, _roadTraveler, _roadWarning, _roadArrow;
        private Animator _roadBearAnimator;
        private int _pendingHunterAction = -1;
        private int _roadVisualAct = -1;
        private string _roadAnimation = "";
        private bool _roadCallShown;

        private static Rect HunterActionRect(ArenaHudLayout layout)
        { return new Rect(layout.Width * .35f, layout.Height - 180f, layout.Width * .28f, 42f); }

        private bool CanShowHunterAction()
        {
            var h = _game.Progression.Road;
            long dx = _game.PlayerXmm - HunterEncounter.HunterX;
            long dz = _game.PlayerZmm - HunterEncounter.HunterZ;
            return _game.Progression.EntryAct == 3 && !_game.GameOver &&
                dx * dx + dz * dz <= FixedMath.Square(2400) &&
                (h.HunterHealth == 0 || h.Rescued) && h.Purse > 0;
        }

        private bool PollRoadInput(ArenaHudLayout layout)
        {
            if (!CanShowHunterAction() || _hungerJournalOpen) return false;
            bool pressed = Input.GetKeyDown(KeyCode.H);
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began && layout.IsWithinSafeArea(t.position) && HunterActionRect(layout).Contains(layout.ToGuiTouch(t.position))) pressed = true;
            }
            if (!pressed) return false;
            _pendingHunterAction = _game.Progression.Road.HunterHealth == 0 ? 1 : 0;
            _game.SetMoveInput(0, 0); ReleasePresentationTouches(); return true;
        }

        private void DrawRoadHud(ArenaHudLayout layout)
        {
            if (_hungerJournalOpen || _game.Progression.EntryAct != 3) return;
            var h = _game.Progression.Road;
            if (h.Started && !h.Bear.IsDead && h.HunterHealth > 0)
                GUI.Box(new Rect(layout.Width * .25f, layout.StatusPanelRect.yMax + 8f, layout.Width * .5f, 62f),
                    "JAEGER IN GEFAHR  " + h.HunterHealth + "/700  |  BAER  " + h.Bear.Health + "/" + h.Bear.MaxHealth +
                    "\nAngreifen lenkt ihn ab. Dem roten Prankenhieb ausweichen!");
            if (!CanShowHunterAction()) return;
            string label = h.HunterHealth == 0 ? "H: LEICHNAM BERAUBEN (12 Muenzen)" : "H: MIT DEM JAEGER SPRECHEN";
            if (Application.isMobilePlatform) GUI.Box(HunterActionRect(layout), label.Substring(3));
            else if (GUI.Button(HunterActionRect(layout), label)) _pendingHunterAction = h.HunterHealth == 0 ? 1 : 0;
        }

        private void OnDestroy() { ClearRoadVisuals(); }

        private void ClearRoadVisuals()
        {
            foreach (var t in new[] { _roadBear, _roadHunter, _roadTraveler, _roadWarning, _roadArrow })
                if (t != null) Destroy(t.gameObject);
            _roadBear = _roadHunter = _roadTraveler = _roadWarning = _roadArrow = null;
            _roadBearAnimator = null; _roadAnimation = "";
        }

        private void UpdateRoadPresentation()
        {
            int act = _game.Progression.EntryAct;
            if (_roadVisualAct != act) { ClearRoadVisuals(); _roadVisualAct = act; }
            var h = _game.Progression.Road;
            if (act == 3)
            {
                if (_roadBear == null)
                {
                    _roadBear = Shape(PrimitiveType.Cube, "ARE_Road_Bear", Vector3.zero, Vector3.one, new Color(.32f, .19f, .09f));
                    PassiveArtVisuals.TryBindGrounded(_roadBear, "RoadBear", 0);
                    _roadBearAnimator = _roadBear.GetComponentInChildren<Animator>();
                    // This single encounter must project the current pose before its first camera render.
                    if (_roadBearAnimator != null) _roadBearAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    _roadHunter = Shape(PrimitiveType.Capsule, "ARE_Road_Hunter", new Vector3(2, 1, 9), new Vector3(.8f, 1, .8f), new Color(.27f, .45f, .25f));
                    PassiveArtVisuals.TryBindGrounded(_roadHunter, "RoadHunter", 0);
                    _roadWarning = Shape(PrimitiveType.Cylinder, "ARE_Bear_Swipe_Warning", Vector3.zero, new Vector3(4.8f, .012f, 4.8f), new Color(.9f, .12f, .06f));
                    _roadArrow = Shape(PrimitiveType.Cube, "ARE_Hunter_Arrow", Vector3.zero, new Vector3(.04f, .04f, 1), new Color(.8f, .68f, .4f));
                }
                var bear = h.Bear;
                _roadBear.gameObject.SetActive(h.Started);
                _roadBear.position = new Vector3(bear.Xmm / 1000f, 0, bear.Zmm / 1000f);
                Vector3 target = h.AggroTicks > 0 ? new Vector3(_game.PlayerXmm / 1000f, 0, _game.PlayerZmm / 1000f) : new Vector3(2, 0, 9);
                Vector3 facing = target - _roadBear.position;
                if (!bear.IsDead && facing.sqrMagnitude > .01f) _roadBear.rotation = Quaternion.LookRotation(facing);
                string animation = bear.IsDead ? "Death" : bear.BossWindupTicks > 0 ? "Attack" :
                    bear.Xmm != bear.PreviousXmm || bear.Zmm != bear.PreviousZmm ? "Run" : "Idle";
                if (_roadBearAnimator != null && animation != _roadAnimation)
                { _roadBearAnimator.CrossFade(animation, .1f); _roadAnimation = animation; }
                _roadHunter.position = new Vector3(2, h.HunterHealth > 0 ? 1 : .3f, 9);
                _roadHunter.rotation = h.HunterHealth > 0 ? Quaternion.identity : Quaternion.Euler(90, 0, 0);
                _roadWarning.gameObject.SetActive(bear.BossWindupTicks > 0);
                _roadWarning.position = new Vector3(h.StrikeX / 1000f, .04f, h.StrikeZ / 1000f);
                _roadArrow.gameObject.SetActive(h.ArrowFlash > 0);
                if (h.ArrowFlash > 0)
                {
                    Vector3 from = new Vector3(2, 1.4f, 9), to = _roadBear.position + Vector3.up;
                    _roadArrow.position = Vector3.Lerp(from, to, 1 - h.ArrowFlash / 4f);
                    if ((to - from).sqrMagnitude > .01f) _roadArrow.rotation = Quaternion.LookRotation(to - from);
                }
                if (h.Started && !_roadCallShown) { _roadCallShown = true; _message = "Jaeger: Hilfe! Halt ihn von mir fern, dann kann ich schiessen!"; }
            }
            bool showTraveler = act == h.TravelerRegion && (act == 3 || h.Started);
            if (showTraveler && _roadTraveler == null)
            {
                _roadTraveler = Shape(PrimitiveType.Capsule, "ARE_Road_Witness", Vector3.zero, new Vector3(.8f, 1, .8f), new Color(.5f, .4f, .25f));
                PassiveArtVisuals.TryBindGrounded(_roadTraveler, "EntryNpc0", 0);
            }
            if (_roadTraveler != null)
            { _roadTraveler.gameObject.SetActive(showTraveler); _roadTraveler.position = new Vector3(h.TravelerX / 1000f, 1, h.TravelerZ / 1000f); }
        }
    }
}
