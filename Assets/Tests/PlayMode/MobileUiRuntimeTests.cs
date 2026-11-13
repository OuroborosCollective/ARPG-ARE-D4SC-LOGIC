using System.Collections;
using System.Reflection;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>Real Unity test of the uGUI HUD and modal menus on the entry profile (isolated save keys).</summary>
    public sealed class MobileUiRuntimeTests
    {
        private readonly string[] _keys = { "ARE_ARPG_entry_v1", "ARE_ARPG_entry_v1_backup" };
        private readonly string[] _saved = new string[2];
        private readonly bool[] _had = new bool[2];
        private Scene _previous, _scene;
        private GameObject[] _previousRoots;
        private bool[] _previousActive;
        private ArenaRuntime _runtime;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private ArenaSimulation Game { get { return (ArenaSimulation)typeof(ArenaRuntime).GetField("_game", Flags).GetValue(_runtime); } }

        [UnitySetUp]
        public IEnumerator Setup()
        {
            for (int i = 0; i < _keys.Length; i++) { _had[i] = PlayerPrefs.HasKey(_keys[i]); _saved[i] = PlayerPrefs.GetString(_keys[i], ""); PlayerPrefs.DeleteKey(_keys[i]); }
            _previous = SceneManager.GetActiveScene();
            _previousRoots = _previous.GetRootGameObjects();
            _previousActive = new bool[_previousRoots.Length];
            for (int i = 0; i < _previousRoots.Length; i++)
            {
                _previousActive[i] = _previousRoots[i].activeSelf;
                var root = _previousRoots[i];
                if (root.GetComponent<ArenaRuntime>() != null || root.GetComponentsInChildren<Renderer>(true).Length > 0 ||
                    root.GetComponentsInChildren<Camera>(true).Length > 0 || root.GetComponentsInChildren<Light>(true).Length > 0 ||
                    root.GetComponentsInChildren<Canvas>(true).Length > 0)
                    root.SetActive(false);
            }
            _scene = SceneManager.CreateScene("AshenEntry");
            SceneManager.SetActiveScene(_scene);
            _runtime = new GameObject("Mobile_UI_Check").AddComponent<ArenaRuntime>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            ArenaRuntime.HudScreenOverride = null;
            if (_previous.IsValid() && _previous.isLoaded) SceneManager.SetActiveScene(_previous);
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
            for (int i = 0; i < _previousRoots.Length; i++)
                if (_previousRoots[i] != null) _previousRoots[i].SetActive(_previousActive[i]);
            for (int i = 0; i < _keys.Length; i++)
                if (_had[i]) PlayerPrefs.SetString(_keys[i], _saved[i]); else PlayerPrefs.DeleteKey(_keys[i]);
            PlayerPrefs.Save();
        }

        private Canvas Hud { get { return (Canvas)typeof(ArenaRuntime).GetField("_hudCanvas", Flags).GetValue(_runtime); } }
        private void Route(ArenaHudControl control) { typeof(ArenaRuntime).GetMethod("RouteHudControl", Flags).Invoke(_runtime, new object[] { control }); }
        private void Refresh() { typeof(ArenaRuntime).GetMethod("UpdateHud", Flags).Invoke(_runtime, null); }

        private static Transform Child(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        [UnityTest]
        public IEnumerator HudProjectsTheSharedLayoutWithoutRaycasts()
        {
            ArenaRuntime.HudScreenOverride = new Vector2(1920f, 1080f);
            Refresh();
            yield return null;
            Canvas hud = Hud;
            Assert.IsNotNull(hud, "the uGUI HUD canvas must exist");
            Assert.IsNull(hud.GetComponent<GraphicRaycaster>(), "HUD input is layout hit-tested; the HUD must not raycast");
            var layout = new ArenaHudLayout(1920f, 1080f, new Rect(0f, 0f, 1920f, 1080f));
            Assert.AreEqual(layout.Scale, hud.GetComponent<CanvasScaler>().scaleFactor, 1e-4f);
            foreach (string name in new[] { "Status", "QuestTracker", "Minimap", "Menu0", "Menu1", "Menu2", "Menu3", "StickBase", "Skill0", "Skill1", "Skill2", "Skill3", "Pickup", "XpBack" })
                Assert.IsNotNull(Child(hud.transform, name), name + " is missing from the HUD");
            var skill = (RectTransform)Child(hud.transform, "Skill0");
            Rect expected = layout.SkillRect(0);
            Assert.AreEqual(new Vector2(expected.x, -expected.y), skill.anchoredPosition, "drawn attack button must equal its touch target");
            Assert.AreEqual(expected.size, skill.sizeDelta);
            foreach (var graphic in hud.GetComponentsInChildren<Graphic>(true)) Assert.IsFalse(graphic.raycastTarget, graphic.name + " must not raycast");
            yield return CaptureUi("ui-hud-1920x1080.png", 1920, 1080);
            yield return CaptureUi("ui-hud-phone-2400x1080.png", 2400, 1080);
            yield return CaptureUi("ui-hud-tablet-2560x1600.png", 2560, 1600);
        }

        [UnityTest]
        public IEnumerator MenuButtonsOpenModalMenusThatPauseTheCore()
        {
            yield return new WaitForSeconds(0.3f);
            ulong before = Game.CurrentTick;
            Assert.Greater(before, 0UL, "the core must run before the menu opens");
            Route(ArenaHudControl.MenuInventory);
            Assert.IsTrue(_runtime.MenuOpen);
            Assert.AreEqual(ArenaRuntime.MenuInventory, _runtime.MenuTab);
            ulong paused = Game.CurrentTick;
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(paused, Game.CurrentTick, "no tick may advance while the menu is open");
            var menu = GameObject.Find("ARE_Menu");
            Assert.IsNotNull(menu);
            Assert.IsNotNull(menu.GetComponent<GraphicRaycaster>(), "menus use real uGUI buttons");
            Assert.IsNotNull(Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>());
            var gameplay = Child(Hud.transform, "Gameplay");
            Assert.IsFalse(gameplay.gameObject.activeSelf, "HUD controls hide behind the modal menu");
            yield return CaptureUi("ui-menu-inventory.png", 1920, 1080);

            for (int tab = 0; tab < ArenaRuntime.MenuTitles.Length; tab++)
            {
                _runtime.OpenMenu(tab);
                yield return null;
                Assert.AreEqual(tab, _runtime.MenuTab);
                Assert.IsNotNull(Child(menu.transform, "Window"));
                yield return CaptureUi("ui-menu-" + ArenaRuntime.MenuTitles[tab].ToLowerInvariant() + ".png", 1920, 1080);
                yield return CaptureUi("ui-menu-" + ArenaRuntime.MenuTitles[tab].ToLowerInvariant() + "-phone.png", 2400, 1080);
            }

            _runtime.CloseMenu();
            Assert.IsFalse(_runtime.MenuOpen);
            yield return new WaitForSeconds(0.35f);
            Assert.Greater(Game.CurrentTick, paused, "closing the menu resumes the 100-ms core");
            Assert.IsTrue(Child(Hud.transform, "Gameplay").gameObject.activeSelf, "HUD controls return after closing");
        }

        [UnityTest]
        public IEnumerator CharacterScreenRendersTheActualHeroOnly()
        {
            _runtime.OpenMenu(ArenaRuntime.MenuCharacter);
            yield return null;
            var camera = (Camera)typeof(ArenaRuntime).GetField("_portraitCamera", Flags).GetValue(_runtime);
            Assert.IsNotNull(camera, "the character screen renders the real hero");
            Assert.IsTrue(camera.enabled);
            Assert.AreEqual(1 << 30, camera.cullingMask, "only the hero layer is rendered into the portrait");
            Assert.IsNotNull(camera.targetTexture);
            var hero = (Transform)typeof(ArenaRuntime).GetField("_hero", Flags).GetValue(_runtime);
            Assert.AreEqual(30, hero.gameObject.layer);
            var raw = Object.FindObjectsOfType<RawImage>();
            Assert.IsTrue(System.Array.Exists(raw, r => r.texture == camera.targetTexture), "the RenderTexture must be shown");
            _runtime.CloseMenu();
            yield return null;
            Assert.IsFalse(camera.enabled, "the portrait camera stops rendering when the menu closes");
        }

        [UnityTest]
        public IEnumerator CharacterValuesStayInsideTheirPanelOnPhoneAndTablet()
        {
            foreach (var size in new[] { new Vector2(1920, 1080), new Vector2(2400, 1080), new Vector2(2560, 1600) })
            {
                ArenaRuntime.HudScreenOverride = size;
                _runtime.OpenMenu(ArenaRuntime.MenuCharacter);
                yield return null;
                Canvas.ForceUpdateCanvases();
                var panel = (RectTransform)Child(GameObject.Find("ARE_Menu").transform, "Stats");
                Assert.IsNotNull(panel);
                var corners = new Vector3[4];
                foreach (var label in panel.GetComponentsInChildren<Text>())
                {
                    label.rectTransform.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        Vector3 local = panel.InverseTransformPoint(corner);
                        Assert.That(local.y, Is.InRange(panel.rect.yMin - 1f, panel.rect.yMax + 1f),
                            size + ": " + label.text + " must remain readable inside the stats panel");
                    }
                }
            }
        }

        [Test]
        public void QuestChronicleReadsPersistedCoreProgressOnly()
        {
            var fresh = CharacterProgression.CreateEntryProfile();
            var milestones = ArenaRuntime.QuestMilestones(fresh);
            Assert.Greater(milestones.Count, 4);
            Assert.IsTrue(milestones.TrueForAll(m => !m.Value), "a fresh profile has completed nothing");
            Assert.AreEqual(0f, ArenaRuntime.ExperienceFraction(fresh), 1e-6f);
        }

        /// <summary>Render HUD and menu (switched to camera space) at an exact device size into the evidence folder.</summary>
        private IEnumerator CaptureUi(string name, int width, int height)
        {
            string directory = System.Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(directory)) yield break;
            ArenaRuntime.HudScreenOverride = new Vector2(width, height);
            Refresh();
            if (_runtime.MenuOpen) _runtime.OpenMenu(_runtime.MenuTab);
            yield return null;
            var camera = (Camera)typeof(ArenaRuntime).GetField("_camera", Flags).GetValue(_runtime);
            var canvases = _runtime.GetComponentsInChildren<Canvas>();
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = camera.targetTexture; var active = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = camera.nearClipPlane + 0.05f; }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, name), pixels.EncodeToPNG());
            }
            finally
            {
                foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceOverlay; c.worldCamera = null; }
                camera.targetTexture = previous; RenderTexture.active = active;
                Object.Destroy(pixels); target.Release(); Object.Destroy(target);
                ArenaRuntime.HudScreenOverride = null;
                Refresh();
                if (_runtime.MenuOpen) _runtime.OpenMenu(_runtime.MenuTab);
            }
        }
    }
}
