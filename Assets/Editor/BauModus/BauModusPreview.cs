using AreArpg.UnityClient;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Live view of the Bau-Modus inside the open Unity editor (Edit Mode). The world is assembled by code at runtime,
/// so the preview shows the area's reviewed world shell (if it has one) plus the placed pieces. Everything is
/// HideFlags.DontSave: it never ends up in a scene file and is removed before Play Mode starts.
/// </summary>
[InitializeOnLoad]
internal static class BauModusPreview
{
    public const string RootName = "Bau-Modus Vorschau";
    const string AreaKey = "AshenBauModus.PreviewArea";
    static Transform _root, _layer, _ghost;
    static bool _world;

    static BauModusPreview()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) DestroyRoot();
            if (state == PlayModeStateChange.EnteredEditMode && !string.IsNullOrEmpty(Area))
                EditorApplication.delayCall += () => { if (!EditorApplication.isPlayingOrWillChangePlaymode) Show(Area); };
        };
        // A domain reload loses the static references; drop stale roots and rebuild once the editor is idle.
        EditorApplication.delayCall += () =>
        {
            RemoveStaleRoots();
            if (!string.IsNullOrEmpty(Area) && !EditorApplication.isPlayingOrWillChangePlaymode) Show(Area);
        };
    }

    /// <summary>Area shown in the editor preview (survives script reloads, not editor restarts).</summary>
    public static string Area
    {
        get { return SessionState.GetString(AreaKey, ""); }
        private set { SessionState.SetString(AreaKey, value ?? ""); }
    }

    public static bool Visible { get { return _root != null; } }
    public static bool HasWorld { get { return _root != null && _world; } }
    public static int Pieces { get { return BuildLayer.CountPieces(_layer); } }
    public static bool GhostVisible { get { return _ghost != null; } }
    public static Transform Layer { get { return _layer; } }
    public static Transform GhostRoot { get { return _ghost; } }

    public static void Show(string area)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        BuildArea site = BauModusStore.Area(area);
        BuildLayoutData data = BauModusStore.Load(area);
        DestroyRoot();
        RemoveStaleRoots();
        Area = area;
        _root = EditorUtility.CreateGameObjectWithHideFlags(RootName + " · " + site.Label, HideFlags.DontSave).transform;
        _world = false;
        if (!string.IsNullOrEmpty(site.PreviewWorld))
        {
            var world = EditorUtility.CreateGameObjectWithHideFlags("Gebiet: " + site.Label, HideFlags.DontSave).transform;
            world.SetParent(_root, false);
            _world = PassiveArtVisuals.TryBind(world, site.PreviewWorld);
            if (!_world) Object.DestroyImmediate(world.gameObject);
        }
        if (!_world)
        {
            // Code-built areas: a neutral ground plate; the real area is visible in Play Mode.
            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "Bauplatz (Gebiet wird im Play-Modus aus Code gebaut)";
            Object.DestroyImmediate(plate.GetComponent<Collider>());
            plate.transform.SetParent(_root, false);
            plate.transform.localPosition = new Vector3(0f, -0.06f, 0f);
            plate.transform.localScale = new Vector3(site.Extent * 2f, 0.1f, site.Extent * 2f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { hideFlags = HideFlags.DontSave };
            var color = new Color(0.20f, 0.21f, 0.19f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            plate.GetComponent<Renderer>().sharedMaterial = material;
        }
        if (!SceneHasDirectionalLight())
        {
            AddLight("Bau-Modus Licht", new Color(1f, 0.92f, 0.80f), 1.35f, Quaternion.Euler(50f, -28f, 0f), LightShadows.Soft);
            AddLight("Bau-Modus Aufhellung", new Color(0.62f, 0.70f, 0.85f), 0.45f, Quaternion.Euler(35f, 150f, 0f), LightShadows.None);
        }
        _layer = BuildLayer.Build(data, null, _root);
        MarkDontSave(_root);
    }

    /// <summary>Re-project a committed plan; returns the realised piece count (independent readback).</summary>
    public static int Refresh(BuildLayoutData data)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || data == null) return 0;
        if (_root == null || data.area != Area) { Show(data.area); return Pieces; }
        if (_layer != null) Object.DestroyImmediate(_layer.gameObject);
        _layer = BuildLayer.Build(data, null, _root);
        MarkDontSave(_layer);
        return Pieces;
    }

    public static void Ghost(BuildPiece piece)
    {
        if (_ghost != null) Object.DestroyImmediate(_ghost.gameObject);
        _ghost = null;
        if (piece == null || _root == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        _ghost = BuildLayer.CreateGhost(piece, null);
        if (_ghost == null) return;
        _ghost.SetParent(_root, true);
        MarkDontSave(_ghost);
    }

    public static void Hide()
    {
        Area = "";
        DestroyRoot();
    }

    static void DestroyRoot()
    {
        if (_root != null) Object.DestroyImmediate(_root.gameObject);
        _root = _layer = _ghost = null;
    }

    static void RemoveStaleRoots()
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go == null || go.transform.parent != null || (go.hideFlags & HideFlags.DontSave) == 0) continue;
            if (!go.name.StartsWith(RootName, System.StringComparison.Ordinal)) continue;
            if (_root != null && go == _root.gameObject) continue;
            Object.DestroyImmediate(go);
        }
    }

    static bool SceneHasDirectionalLight()
    {
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light.type == LightType.Directional && light.isActiveAndEnabled) return true;
        return false;
    }

    static void AddLight(string name, Color color, float intensity, Quaternion rotation, LightShadows shadows)
    {
        var go = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.DontSave, typeof(Light));
        go.transform.SetParent(_root, false);
        go.transform.rotation = rotation;
        Light light = go.GetComponent<Light>();
        light.type = LightType.Directional;
        light.color = color;
        light.intensity = intensity;
        light.shadows = shadows;
    }

    static void MarkDontSave(Transform root)
    {
        if (root == null) return;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
    }
}
