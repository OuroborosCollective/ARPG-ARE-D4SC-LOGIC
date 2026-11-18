using AreArpg.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// Owner decision 2026-11-15: a fixed, non-skippable arrival scene in the Aschengrenze. The simulation is paused
    /// while it plays; a tap advances a line only after a short minimum reading time. Completion is recorded by the Core.
    /// </summary>
    public sealed partial class ArenaRuntime
    {
        private const float SceneMinimumLineSeconds = .6f;
        private bool _sceneActive, _arrivalDone;
        private int _sceneIndex;
        private float _sceneLineShown;
        private GameObject _sceneRoot;
        private Text _sceneSpeaker, _sceneText, _sceneHint;

        /// <summary>True while the arrival scene owns input and pauses the simulation.</summary>
        public bool SceneOpen { get { return _sceneActive; } }
        private bool SimulationPaused { get { return MenuOpen || _sceneActive; } }
        /// <summary>Index of the line on screen, for checks; -1 when no scene plays.</summary>
        public int SceneLineIndex { get { return _sceneActive ? _sceneIndex : -1; } }

        /// <summary>Start, advance and finish the scene; returns true while the scene consumes all input.</summary>
        private bool PollArrivalScene()
        {
            if (!_sceneActive)
            {
                if (_arrivalDone || MenuOpen || !_game.ArrivalScenePending) { if (!_game.IsVillage) _arrivalDone = false; return false; }
                BeginArrivalScene();
            }
            // A HUD rebuild (new act, resized screen) destroys child panels; restore the scene panel at once.
            if (_sceneRoot == null) { BuildSceneUi(); ShowSceneTexts(); }
            bool tapped = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);
            for (int i = 0; i < Input.touchCount; i++) if (Input.GetTouch(i).phase == TouchPhase.Began) tapped = true;
            if (tapped) AdvanceArrivalScene();
            _game.SetMoveInput(0, 0);
            ReleasePresentationTouches();
            return true;
        }

        private void BeginArrivalScene()
        {
            _sceneActive = true; _sceneIndex = 0; _hungerJournalOpen = false;
            DiscardQueuedStoryInput();
            _accumulator = 0f; _game.SetMoveInput(0, 0);
            BuildSceneUi();
            ShowSceneLine();
        }

        /// <summary>The same step a tap performs: ignored until the current line was readable for a moment. Measured on the
        /// game clock (Time.time), which equals real time in play because the game never changes timeScale, and which a fixed
        /// test frame time drives like a device frame rate.</summary>
        private void AdvanceArrivalScene()
        {
            if (!_sceneActive || Time.time - _sceneLineShown < SceneMinimumLineSeconds) return;
            _sceneIndex++;
            if (_sceneIndex < ArrivalScene.Lines.Length) { ShowSceneLine(); return; }
            _sceneActive = false;
            DiscardQueuedStoryInput();
            if (_sceneRoot != null) _sceneRoot.SetActive(false);
            if (_cameraRig != null) { _cameraRig.ClearCinematic(); _cameraRig.SnapToTarget(); }
            if (_game.CompleteArrivalScene()) SaveCharacter();
            _arrivalDone = true;
            _message = ArrivalScene.Paths;
            _hungerJournalOpen = true;
        }

        /// <summary>
        /// Review fix (#47): the scene owns all input. Story, camp and NPC actions queued before or during it are
        /// dropped, so none of them can run the moment the scene ends.
        /// </summary>
        private void DiscardQueuedStoryInput()
        {
            _pendingHungerAction = -1; _pendingCampDonation = false; _pendingTakeRation = false; _takeRationArmedUntil = -1f;
            _pendingNpcActions.Clear();
        }

        /// <summary>The IMGUI layer (journal, choices, subtitles) draws only outside menus, defeat and the arrival scene.</summary>
        private bool ImguiLayerVisible { get { return _game != null && !_storageFault && !MenuOpen && !_game.GameOver && !_sceneActive; } }

        private void ShowSceneTexts()
        {
            var line = ArrivalScene.Lines[_sceneIndex];
            if (_sceneSpeaker != null) _sceneSpeaker.text = line.SpeakerId == 0 ? "Ilyan" : line.SpeakerId == 1 ? "Mara" : "Dorn";
            if (_sceneText != null) _sceneText.text = line.Text;
            if (_sceneHint != null) _sceneHint.text = Application.isMobilePlatform ? "Tippen zum Weiter" : "Klick / Leertaste zum Weiter";
        }

        private void ShowSceneLine()
        {
            var line = ArrivalScene.Lines[_sceneIndex];
            _sceneLineShown = Time.time;
            if (_sceneRoot == null) BuildSceneUi();
            ShowSceneTexts();
            if (_npcVisuals.TryGetValue(line.SpeakerId, out var speaker) && speaker != null && _hero != null && _cameraRig != null)
            {
                Vector3 toHero = _hero.position - speaker.position; toHero.y = 0f;
                if (toHero.sqrMagnitude < .01f) toHero = Vector3.back;
                toHero.Normalize();
                speaker.rotation = Quaternion.LookRotation(toHero);
                var face = new Vector3(speaker.position.x, GroundHeight(speaker.position.x, speaker.position.z) + 1.55f, speaker.position.z);
                Vector3 side = Vector3.Cross(Vector3.up, toHero) * .9f;
                _cameraRig.SetCinematic(face, face + toHero * 3.1f + side + Vector3.up * .45f);
            }
        }

        /// <summary>Letterbox bars and a subtitle panel on the HUD canvas; the gameplay HUD is hidden meanwhile.</summary>
        private void BuildSceneUi()
        {
            if (_hudRoot == null) return;
            if (_sceneRoot != null) Destroy(_sceneRoot);
            var l = CurrentHudLayout();
            _sceneRoot = AshenUiTheme.Node("ArrivalScene", _hudRoot).gameObject;
            AshenUiTheme.Stretch((RectTransform)_sceneRoot.transform);
            var top = AshenUiTheme.Image("LetterboxTop", _sceneRoot.transform, null, Color.black, false);
            AshenUiTheme.Place(top.rectTransform, new Rect(-200f, -200f, l.Width + 400f, 262f));
            var bottom = AshenUiTheme.Image("LetterboxBottom", _sceneRoot.transform, null, Color.black, false);
            AshenUiTheme.Place(bottom.rectTransform, new Rect(-200f, l.Height - 150f, l.Width + 400f, 350f));
            float w = Mathf.Min(900f, l.Width - 80f), x = (l.Width - w) * .5f;
            _sceneSpeaker = AshenUiTheme.Label("Speaker", _sceneRoot.transform, 20, AshenUiTheme.Bronze, TextAnchor.UpperLeft, FontStyle.Bold);
            AshenUiTheme.Place(_sceneSpeaker.rectTransform, new Rect(x, l.Height - 138f, w, 26f));
            _sceneText = AshenUiTheme.Label("Line", _sceneRoot.transform, 17, AshenUiTheme.Parchment, TextAnchor.UpperLeft);
            AshenUiTheme.Place(_sceneText.rectTransform, new Rect(x, l.Height - 110f, w, 74f));
            _sceneText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _sceneHint = AshenUiTheme.Label("Hint", _sceneRoot.transform, 12, AshenUiTheme.Muted, TextAnchor.LowerRight);
            AshenUiTheme.Place(_sceneHint.rectTransform, new Rect(x, l.Height - 34f, w, 20f));
        }
    }
}
