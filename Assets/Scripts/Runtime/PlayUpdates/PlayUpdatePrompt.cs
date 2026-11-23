using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// Asks Google Play once per game start whether a newer version exists and, if so, offers it in a small panel in the
    /// style of the game's menus. Accepting starts Play's own update; the game saves itself when Play pauses it.
    /// </summary>
    public sealed class PlayUpdatePrompt : MonoBehaviour
    {
        private IPlayUpdateSource _source;
        private GameObject _canvas;
        private bool _checked;

        public PlayUpdateAction LastAction { get; private set; }
        public bool PromptVisible { get { return _canvas != null; } }
        public Button UpdateButton { get; private set; }
        public Button LaterButton { get; private set; }
        public Text BodyText { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallOnAndroid()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (FindObjectOfType<PlayUpdatePrompt>() == null) Install(new AndroidPlayUpdateSource());
#endif
        }

        /// <summary>One prompt per game start; tests pass a fake source.</summary>
        public static PlayUpdatePrompt Install(IPlayUpdateSource source)
        {
            var go = new GameObject("ARE_PlayUpdates");
            if (Application.isPlaying) DontDestroyOnLoad(go);
            var prompt = go.AddComponent<PlayUpdatePrompt>();
            prompt._source = source;
            return prompt;
        }

        private void Start() { RunCheck(); }

        /// <summary>Ask Play once; later calls do nothing.</summary>
        public void RunCheck()
        {
            if (_checked || _source == null) return;
            _checked = true;
            _source.Check(OnState);
        }

        private void OnState(PlayUpdateState state)
        {
            if (this == null) return;
            LastAction = PlayUpdatePolicy.Decide(state);
            if (LastAction == PlayUpdateAction.Resume) _source.StartUpdate();
            else if (LastAction == PlayUpdateAction.Offer) ShowPrompt();
            if (!state.Known) Debug.Log("Play update check skipped: " + state.Problem);
        }

        public void UpdateNow()
        {
            HidePrompt();
            _source.StartUpdate();
        }

        public void Later() { HidePrompt(); }

        private void HidePrompt()
        {
            if (_canvas == null) return;
            if (Application.isPlaying) Destroy(_canvas); else DestroyImmediate(_canvas);
            _canvas = null; UpdateButton = null; LaterButton = null; BodyText = null;
        }

        private void ShowPrompt()
        {
            if (_canvas != null) return;
            EnsureEventSystem();
            _canvas = new GameObject("ARE_PlayUpdatePrompt", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas.transform.SetParent(transform, false);
            _canvas.layer = 5;
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60; // above HUD (10) and menus (20)
            var scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = 0.5f;

            // The shade catches every touch, so nothing behind the panel reacts while it is open.
            Image shade = AshenUiTheme.Image("Shade", _canvas.transform, null, AshenUiTheme.Shade, false);
            AshenUiTheme.Stretch(shade.rectTransform);
            shade.raycastTarget = true;

            RectTransform centre = AshenUiTheme.Node("Centre", _canvas.transform);
            centre.anchorMin = centre.anchorMax = centre.pivot = new Vector2(0.5f, 0.5f);
            centre.anchoredPosition = Vector2.zero;
            centre.sizeDelta = new Vector2(520f, 220f);

            Image frame = AshenUiTheme.Frame("Panel", centre, new Rect(0f, 0f, 520f, 220f), PlayUpdatePolicy.Title);
            frame.raycastTarget = true;
            BodyText = AshenUiTheme.Label("Body", frame.transform, 17, AshenUiTheme.Parchment, TextAnchor.UpperLeft);
            AshenUiTheme.Place(BodyText.rectTransform, new Rect(24f, 48f, 472f, 84f));
            BodyText.text = PlayUpdatePolicy.Body;
            UpdateButton = AshenUiTheme.MenuButton("UpdateNow", frame.transform, new Rect(24f, 150f, 260f, 48f), PlayUpdatePolicy.UpdateNow,
                UpdateNow, new Color(0.62f, 0.38f, 0.16f, 1f), 17);
            LaterButton = AshenUiTheme.MenuButton("Later", frame.transform, new Rect(300f, 150f, 196f, 48f), PlayUpdatePolicy.Later, Later, null, 17);
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("ARE_EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            if (Application.isPlaying) DontDestroyOnLoad(go);
        }
    }
}
