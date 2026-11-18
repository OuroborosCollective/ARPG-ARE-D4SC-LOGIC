// Minimal, test-only stand-in for the small UnityEngine surface the Fadenwerk runtime rules and the
// validator need. It lets those pure rules compile and run in plain .NET CI without the Unity engine.
// This shim is NOT part of the game and never ships; it only mirrors the members the tested files touch.
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnityEngine
{
    public class Object
    {
        public string name = "";
        public static void Destroy(Object obj) { }
        public static void DontDestroyOnLoad(Object obj) { }
    }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
    }

    public class GameObject : Object
    {
        public string tag = "";
        public Transform transform = new Transform();
        public static GameObject FindWithTag(string t) => null;
    }

    public class Transform : Object
    {
        public Vector3 position;
        public Transform parent;
    }

    public class Component : Object
    {
        public GameObject gameObject = new GameObject();
        public Transform transform = new Transform();
    }

    public class Behaviour : Component { }
    public class MonoBehaviour : Behaviour { }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0f, 0f, 0f);
        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => (float)Math.Sqrt(sqrMagnitude);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
    }

    public static class Mathf
    {
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
    }

    public static class Time { public static float unscaledTime = 0f; }

    public static class Gizmos
    {
        public static Color color;
        public static void DrawWireSphere(Vector3 center, float radius) { }
        public static void DrawWireCube(Vector3 center, Vector3 size) { }
    }

    /// <summary>Field-based JSON, matching Unity's JsonUtility shape for plain [Serializable] data.</summary>
    public static class JsonUtility
    {
        static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            IncludeFields = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
        public static string ToJson(object obj) => JsonSerializer.Serialize(obj, obj.GetType(), Options);
        public static T FromJson<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
    }

    public class PropertyAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field)] public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string text) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class TextAreaAttribute : Attribute { public TextAreaAttribute(int min, int max) { } public TextAreaAttribute(int lines) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string header) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class RangeAttribute : Attribute { public RangeAttribute(float min, float max) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class SpaceAttribute : Attribute { public SpaceAttribute() { } public SpaceAttribute(float height) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class HideInInspector : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public sealed class CreateAssetMenuAttribute : Attribute { public string fileName; public string menuName; public int order; }
    [AttributeUsage(AttributeTargets.Class)] public sealed class AddComponentMenuAttribute : Attribute { public AddComponentMenuAttribute(string menuName) { } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DisallowMultipleComponentAttribute : Attribute { }

    public enum RuntimeInitializeLoadType { AfterSceneLoad = 0, BeforeSceneLoad = 1, AfterAssembliesLoaded = 2, BeforeSplashScreen = 3, SubsystemRegistration = 4 }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { }
    }
}

namespace UnityEngine.Events
{
    public class UnityEvent<T>
    {
        event Action<T> _handlers;
        public void AddListener(Action<T> handler) { _handlers += handler; }
        public void RemoveListener(Action<T> handler) { _handlers -= handler; }
        public void Invoke(T arg) { _handlers?.Invoke(arg); }
    }
}
