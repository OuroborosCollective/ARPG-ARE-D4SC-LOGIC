using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>What the game reads from the player's hands: keys, mouse and touches.</summary>
    public interface IPlayerDevice
    {
        bool GetKey(KeyCode key);
        bool GetKeyDown(KeyCode key);
        bool GetMouseButton(int button);
        bool GetMouseButtonDown(int button);
        Vector3 MousePosition { get; }
        Vector2 MouseScrollDelta { get; }
        int TouchCount { get; }
        Touch GetTouch(int index);
    }

    /// <summary>
    /// The one place the runtime reads input. Normally this is Unity's own input. A test may install a scripted
    /// device that taps and swipes like a person on a phone, so a whole journey can be played through the same
    /// buttons, stick and journal a player uses. Runtime files alias <c>Input</c> to this class; the member names
    /// follow <see cref="UnityEngine.Input"/> so the calls read the same.
    /// </summary>
    public static class PlayerDevice
    {
        /// <summary>A scripted device that replaces Unity's input, or null for the real device.</summary>
        public static IPlayerDevice Scripted { get; set; }

        public static bool GetKey(KeyCode key) { return Scripted != null ? Scripted.GetKey(key) : UnityEngine.Input.GetKey(key); }
        public static bool GetKeyDown(KeyCode key) { return Scripted != null ? Scripted.GetKeyDown(key) : UnityEngine.Input.GetKeyDown(key); }
        public static bool GetMouseButton(int button) { return Scripted != null ? Scripted.GetMouseButton(button) : UnityEngine.Input.GetMouseButton(button); }
        public static bool GetMouseButtonDown(int button) { return Scripted != null ? Scripted.GetMouseButtonDown(button) : UnityEngine.Input.GetMouseButtonDown(button); }
        public static Vector3 mousePosition { get { return Scripted != null ? Scripted.MousePosition : UnityEngine.Input.mousePosition; } }
        public static Vector2 mouseScrollDelta { get { return Scripted != null ? Scripted.MouseScrollDelta : UnityEngine.Input.mouseScrollDelta; } }
        public static int touchCount { get { return Scripted != null ? Scripted.TouchCount : UnityEngine.Input.touchCount; } }
        public static Touch GetTouch(int index) { return Scripted != null ? Scripted.GetTouch(index) : UnityEngine.Input.GetTouch(index); }
    }
}
