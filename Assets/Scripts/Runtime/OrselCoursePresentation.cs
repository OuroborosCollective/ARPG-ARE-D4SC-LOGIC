using AreArpg.Core;
using UnityEngine;
using Input = AreArpg.UnityClient.PlayerDevice;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// Orsel and her healing course in the running game. The Core says where she teaches, what she says and what each
    /// way in costs; here she stands there, the button to talk to her shows within reach, and the panel offers the three
    /// ways in and the lesson. What the player picks goes to the Core on the game tick, and the Core alone decides.
    /// </summary>
    public sealed partial class ArenaRuntime
    {
        public const string OrselFigureName = "ARE_Orsel_Healer";
        /// <summary>Choices 1–3 are the ways in (<see cref="HealingCoursePath"/>), 4 is the lesson.</summary>
        public const int OrselLessonChoice = 4;
        private Transform _orselFigure;
        private bool _orselPanelOpen;
        private int _pendingOrselChoice = -1;
        private string _orselReply;

        // ---------------------------------------------------------------- her figure

        /// <summary>
        /// Orsel stands where the Core says she teaches: at the mill while she lives there; in the village in front of
        /// the finished sick room until the lesson is learned. Figure, hint and input follow the same Core answer.
        /// </summary>
        private void UpdateOrselFigure()
        {
            if (_game == null) return;
            if (_orselFigure == null && !ReferenceEquals(_orselFigure, null)) { _actorAnimations.Remove(_orselFigure); _orselFigure = null; }
            CharacterProgression p = _game.Progression;
            bool mill = !p.Houses.OrselInVillage;
            bool show = _game.TryGetOrselTeachingSpot(out long x, out long z);
            if (!show)
            {
                if (_orselFigure != null && _orselFigure.gameObject.activeSelf) _orselFigure.gameObject.SetActive(false);
                return;
            }
            if (_orselFigure == null) _orselFigure = BuildOrselFigure(new Vector2(x, z) / 1000f, mill);
            if (!_orselFigure.gameObject.activeSelf) _orselFigure.gameObject.SetActive(true);
            // She looks at whoever comes close, and otherwise at her beds (or out of the sick room's door).
            Vector3 at = _orselFigure.position;
            var toPlayer = new Vector3(_game.PlayerXmm / 1000f - at.x, 0f, _game.PlayerZmm / 1000f - at.z);
            Vector3 rest = mill ? new Vector3(1f, 0f, 0.6f) :
                new Vector3(VillagePlaceLayout.DoorOutX(VillagePlace.SickRoom), 0f, VillagePlaceLayout.DoorOutZ(VillagePlace.SickRoom));
            Vector3 look = toPlayer.sqrMagnitude < 36f && toPlayer.sqrMagnitude > 0.04f ? toPlayer : rest;
            _orselFigure.rotation = Quaternion.RotateTowards(_orselFigure.rotation, Quaternion.LookRotation(look), 240f * Time.deltaTime + 1f);
        }

        private Transform BuildOrselFigure(Vector2 at, bool mill)
        {
            Vector3 position = mill ? new Vector3(at.x, 1f, at.y) : OnGround(at, 1f);
            Transform figure = Shape(PrimitiveType.Capsule, OrselFigureName, position, new Vector3(0.8f, 1f, 0.8f), new Color(0.62f, 0.62f, 0.52f));
            _chapterScenery.Add(figure);
            PassiveArtVisuals.TryBindGrounded(figure, "Orsel0", 0f);
            var shared = _entryProfile ? Resources.Load<RuntimeAnimatorController>("ARE/Visuals/EntryMelee") : null;
            foreach (Animator animator in figure.GetComponentsInChildren<Animator>(true))
            {
                if (shared != null && PassiveArtVisuals.NeedsSharedLocomotion(animator)) animator.runtimeAnimatorController = shared;
                animator.applyRootMotion = false;
            }
            RegisterActorMotion(figure);
            AnimateActor(figure, 0, 0);
            return figure;
        }

        // ---------------------------------------------------------------- talking to her

        /// <summary>What is offered above the controls while the player stands with Orsel. Null elsewhere.</summary>
        public string OrselHint { get { return _game != null && _game.OrselInReach ? "ORSEL ANSPRECHEN" : null; } }

        public static Rect OrselToggleRect(ArenaHudLayout layout) { return layout.ContextActionRect; }

        /// <summary>One layout for drawing and for touch: the panel, its close button, three ways in or the lesson.</summary>
        public static Rect OrselPanel(ArenaHudLayout layout)
        {
            float height = Mathf.Min(330f, layout.Height - 58f);
            return new Rect((layout.Width - 660f) * .5f, (layout.Height - height) * .5f, 660f, height);
        }

        public static Rect OrselCloseRect(ArenaHudLayout layout)
        { Rect panel = OrselPanel(layout); return new Rect(panel.xMax - 132f, panel.y + 8f, 120f, 30f); }

        /// <summary>The three ways in sit side by side; the lesson takes the whole row.</summary>
        public static Rect OrselChoiceRect(ArenaHudLayout layout, int choice)
        {
            Rect panel = OrselPanel(layout);
            if (choice == OrselLessonChoice) return new Rect(panel.x + 14f, panel.yMax - 50f, panel.width - 28f, 40f);
            return new Rect(panel.x + 14f + (choice - 1) * 212f, panel.yMax - 50f, 208f, 40f);
        }

        public static Rect OrselTextRect(ArenaHudLayout layout)
        { Rect panel = OrselPanel(layout); return Rect.MinMaxRect(panel.x + 16f, panel.y + 46f, panel.xMax - 16f, panel.yMax - 56f); }

        /// <summary>The label of a choice, or null while it is not offered: the ways in until one was taken, then the lesson.</summary>
        private string OrselChoiceLabel(int choice)
        {
            HealingCourseState course = _game.Progression.Healing;
            if (!_game.HealingCourseOpen || course.Learned) return null;
            if (choice == OrselLessonChoice) return course.Path == HealingCoursePath.None ? null : "UNTERWEISUNG: WUNDSCHLUSS LERNEN";
            if (course.Path != HealingCoursePath.None || choice < 1 || choice > 3) return null;
            var path = (HealingCoursePath)choice;
            string what = path == HealingCoursePath.Supplies ? HealingRules.SupplyRations + " RATIONEN GEBEN" :
                path == HealingCoursePath.Payment ? HealingRules.PaymentCoins + " MÜNZEN ZAHLEN" : HealingRules.FavourHerbs + " ASCHENFLIEDER BRINGEN";
            return _game.CanAffordCoursePath(path) ? what : what + " (FEHLT)";
        }

        /// <summary>Her words (or her last answer), and while a way in is still open what the pack holds.</summary>
        private string OrselText()
        {
            CharacterProgression p = _game.Progression;
            var text = new System.Text.StringBuilder(_orselReply ?? _game.OrselCourseText);
            if (_game.HealingCourseOpen && p.Healing.Path == HealingCoursePath.None)
                text.AppendLine().AppendLine().Append("Du hast ").Append(p.Social.Provisions).Append(" Rationen · ")
                    .Append(p.Social.Coins).Append(" Münzen · ").Append(p.Crafting.VillageGiftCount(VillageGood.Herb)).Append(" Aschenflieder bei dir.");
            if (p.Healing.Learned)
                text.AppendLine().AppendLine().Append("Wundschluss: ").Append(HealingRules.WundschlussHealing).Append(" Heilung · ")
                    .Append(HealingRules.WundschlussFocus).Append(" Fokus · ").Append(HealingRules.WundschlussCooldownTicks / 10).Append(" s Abklingzeit. ")
                    .Append("Einhandstab 80 %, Zweihandstab 100 %; Rüstung senkt die Heilung, Stoff gar nicht.");
            return text.ToString();
        }

        private bool OrselTouch(ArenaHudLayout layout, TouchPhase phase, Vector2 point)
        {
            if (!_orselPanelOpen)
            {
                if (phase != TouchPhase.Began || !OrselToggleRect(layout).Contains(point)) return false;
                _orselPanelOpen = true; _orselReply = null;
                return true;
            }
            if (phase != TouchPhase.Began) return true;
            if (OrselCloseRect(layout).Contains(point)) { _orselPanelOpen = false; return true; }
            for (int choice = 1; choice <= OrselLessonChoice; choice++)
                if (OrselChoiceLabel(choice) != null && OrselChoiceRect(layout, choice).Contains(point)) { _pendingOrselChoice = choice; break; }
            return true;
        }

        /// <summary>The panel belongs to standing with Orsel; the Core checks the distance again when a choice is handed over.</summary>
        private bool PollOrselInput(ArenaHudLayout layout)
        {
            if (!_game.OrselInReach || _hungerJournalOpen) { _orselPanelOpen = false; return false; }
            bool consumed = false;
            if (Input.GetKeyDown(KeyCode.N) || (_orselPanelOpen && Input.GetKeyDown(KeyCode.Escape)))
            { _orselPanelOpen = !_orselPanelOpen; _orselReply = null; consumed = true; }
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (!layout.IsWithinSafeArea(touch.position)) continue;
                consumed |= OrselTouch(layout, touch.phase, layout.ToGuiTouch(touch.position));
            }
            if (!consumed && !_orselPanelOpen) return false;
            _game.SetMoveInput(0, 0);
            ReleasePresentationTouches();
            return true;
        }

        /// <summary>Hand the choice over on the game tick; an accepted way in or lesson is saved at once.</summary>
        private void ProcessOrselChoice()
        {
            if (_pendingOrselChoice < 0) return;
            int choice = _pendingOrselChoice;
            _pendingOrselChoice = -1;
            SocialResult result = choice == OrselLessonChoice ? _game.LearnWundschluss() : _game.HelpOrsel((HealingCoursePath)choice);
            if (!string.IsNullOrEmpty(result.Text)) { _orselReply = result.Text; _message = result.Text; FaceSpeak(_orselFigure, 2.6f); }
            if (result.Accepted) SaveCharacter();
        }

        private bool DrawOrselPanel(ArenaHudLayout layout)
        {
            if (!_game.OrselInReach || _hungerJournalOpen || _villageBoardOpen) return false;
            bool touch = Application.isMobilePlatform;
            if (!_orselPanelOpen)
            {
                if (touch) GUI.Box(OrselToggleRect(layout), OrselHint);
                else if (GUI.Button(OrselToggleRect(layout), "N: " + OrselHint)) { _orselPanelOpen = true; _orselReply = null; }
                return false;
            }
            Rect panel = OrselPanel(layout);
            GUI.Box(panel, "");
            GUI.Label(new Rect(panel.x + 16f, panel.y + 10f, panel.width - 160f, 26f), "ORSEL · FELDHEILERIN", _labelStyle);
            if (touch) GUI.Box(OrselCloseRect(layout), "SCHLIESSEN");
            else if (GUI.Button(OrselCloseRect(layout), "SCHLIESSEN (N)")) _orselPanelOpen = false;
            GUI.Label(OrselTextRect(layout), OrselText(), _smallStyle);
            for (int choice = 1; choice <= OrselLessonChoice; choice++)
            {
                string label = OrselChoiceLabel(choice);
                if (label == null) continue;
                if (touch) GUI.Box(OrselChoiceRect(layout, choice), label);
                else if (GUI.Button(OrselChoiceRect(layout, choice), label)) _pendingOrselChoice = choice;
            }
            return true;
        }

        private void ResetOrselPanel() { _orselPanelOpen = false; _pendingOrselChoice = -1; _orselReply = null; }
    }
}
