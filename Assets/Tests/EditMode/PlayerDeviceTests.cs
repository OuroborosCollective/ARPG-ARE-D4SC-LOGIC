using System.IO;
using System.Text.RegularExpressions;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    /// <summary>
    /// The runtime reads keys, mouse and touches only through <see cref="PlayerDevice"/>, so the player-run test sees
    /// every input path a person on a phone uses. A file that reads Unity's input directly would hide its controls.
    /// </summary>
    public sealed class PlayerDeviceTests
    {
        private sealed class Fake : IPlayerDevice
        {
            public bool GetKey(KeyCode key) { return key == KeyCode.W; }
            public bool GetKeyDown(KeyCode key) { return key == KeyCode.J; }
            public bool GetMouseButton(int button) { return button == 1; }
            public bool GetMouseButtonDown(int button) { return button == 0; }
            public Vector3 MousePosition { get { return new Vector3(3f, 4f, 0f); } }
            public Vector2 MouseScrollDelta { get { return new Vector2(0f, 2f); } }
            public int TouchCount { get { return 1; } }
            public Touch GetTouch(int index) { return new Touch { fingerId = 7, phase = TouchPhase.Began, position = new Vector2(5f, 6f) }; }
        }

        [Test]
        public void AScriptedDeviceStandsInForUnityInputUntilItIsRemoved()
        {
            try
            {
                PlayerDevice.Scripted = new Fake();
                Assert.IsTrue(PlayerDevice.GetKey(KeyCode.W)); Assert.IsFalse(PlayerDevice.GetKey(KeyCode.S));
                Assert.IsTrue(PlayerDevice.GetKeyDown(KeyCode.J));
                Assert.IsTrue(PlayerDevice.GetMouseButton(1)); Assert.IsTrue(PlayerDevice.GetMouseButtonDown(0));
                Assert.AreEqual(new Vector3(3f, 4f, 0f), PlayerDevice.mousePosition);
                Assert.AreEqual(new Vector2(0f, 2f), PlayerDevice.mouseScrollDelta);
                Assert.AreEqual(1, PlayerDevice.touchCount);
                Assert.AreEqual(7, PlayerDevice.GetTouch(0).fingerId);
            }
            finally { PlayerDevice.Scripted = null; }
            Assert.AreEqual(Input.touchCount, PlayerDevice.touchCount, "without a scripted device the real input answers");
            Assert.AreEqual(Input.GetKey(KeyCode.W), PlayerDevice.GetKey(KeyCode.W));
        }

        [Test]
        public void EveryRuntimeFileReadsInputThroughThePlayerDevice()
        {
            string runtime = Path.Combine(Application.dataPath, "Scripts", "Runtime");
            var read = new Regex(@"\bInput\.(GetKey|GetKeyDown|GetKeyUp|GetMouseButton|GetMouseButtonDown|GetMouseButtonUp|GetTouch|GetAxis|GetAxisRaw|GetButton|GetButtonDown|touchCount|touches|mousePosition|mouseScrollDelta|anyKey|anyKeyDown|inputString|acceleration)\b");
            int readers = 0;
            foreach (string file in Directory.GetFiles(runtime, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file), text = File.ReadAllText(file);
                if (name == "PlayerDevice.cs") continue;
                StringAssert.DoesNotContain("UnityEngine.Input.", text, name + " reads Unity's input past the PlayerDevice");
                if (!read.IsMatch(text)) continue;
                readers++;
                StringAssert.Contains("using Input = AreArpg.UnityClient.PlayerDevice;", text, name + " reads input without the PlayerDevice alias");
            }
            Assert.GreaterOrEqual(readers, 10, "the HUD, journal, menus, scenes and village panels all read input");
        }
    }
}
