using System;
using UnityEditor;
using UnityEngine;

namespace AshenCrypt.WorldForge.EditorTools
{
    /// <summary>Main entry point: Tools ▸ World Forge ▸ World Designer.</summary>
    public class WFWorldDesignerWindow : EditorWindow
    {
        [SerializeField] private WorldProfile _profile;
        private Texture2D _preview;
        private string _report = "";
        private Vector2 _scroll;
        private bool _showPaths = true, _showPois = true;

        [MenuItem("Tools/World Forge/World Designer", priority = 0)]
        public static void OpenMenu() { Open(null); }

        public static void Open(WorldProfile profile)
        {
            var w = GetWindow<WFWorldDesignerWindow>("World Forge");
            w.minSize = new Vector2(360f, 520f);
            if (profile != null) { w._profile = profile; w._preview = null; }
            w.Show();
        }

        private void OnDisable()
        {
            if (_preview != null) DestroyImmediate(_preview);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("World Forge – World Designer", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Deterministic multi-tile terrain, texturing and spawning for Unity 6.", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            EditorGUI.BeginChangeCheck();
            _profile = (WorldProfile)EditorGUILayout.ObjectField("World Profile", _profile, typeof(WorldProfile), false);
            if (EditorGUI.EndChangeCheck()) _preview = null;

            if (_profile == null)
            {
                EditorGUILayout.HelpBox("Pick a World Profile or create the Ashen Krypt sample world.", MessageType.Info);
                if (GUILayout.Button("Create Ashen Krypt Sample World", GUILayout.Height(30)))
                    Guard(() => { _profile = WFAshenKryptSample.CreateSample(); _preview = null; });
                if (GUILayout.Button("Create Empty Profile")) Guard(() => { _profile = CreateEmptyProfile(); });
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.HelpBox(
                _profile.WorldSizeX + " × " + _profile.WorldSizeZ + " m  |  " + _profile.tilesX + "×" + _profile.tilesZ + " tiles à " + _profile.tileSize + " m  |  height " + _profile.maxHeight +
                " m  |  seed " + _profile.seed + "  |  " + _profile.quality, MessageType.None);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select Profile")) Selection.activeObject = _profile;
            if (_profile.biome != null && GUILayout.Button("Select Biome")) Selection.activeObject = _profile.biome;
            if (GUILayout.Button("New Seed"))
            {
                Undo.RecordObject(_profile, "New Seed");
                _profile.seed = new System.Random().Next(1, int.MaxValue);
                EditorUtility.SetDirty(_profile);
                _preview = null;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _showPaths = GUILayout.Toggle(_showPaths, "Paths", "Button");
            _showPois = GUILayout.Toggle(_showPois, "POIs", "Button");
            if (GUILayout.Button("Render Preview")) Guard(() => { _preview = BuildPreview(); });
            EditorGUILayout.EndHorizontal();
            if (_preview != null)
            {
                float size = Mathf.Min(position.width - 24f, 512f);
                Rect r = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(false));
                EditorGUI.DrawPreviewTexture(r, _preview);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Build", EditorStyles.boldLabel);
            GUI.backgroundColor = new Color(1f, 0.72f, 0.45f);
            if (GUILayout.Button("Generate All  (heights → textures → spawns → POIs → core map)", GUILayout.Height(34)))
                Guard(() => ShowReport(WFTerrainBuilder.GenerateAll(_profile)));
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("1 · Generate Heights Only")) Guard(() => ShowReport(WFTerrainBuilder.GenerateHeightsOnly(_profile)));
            if (GUILayout.Button("2 · Repaint Textures (keeps manual sculpting)")) Guard(() => ShowReport(WFTerrainBuilder.RepaintTextures(_profile)));
            if (GUILayout.Button("3 · Respawn Trees / Details / Objects / POIs")) Guard(() => ShowReport(WFTerrainBuilder.Respawn(_profile)));
            if (GUILayout.Button("4 · Export Core Map (integer map for the simulation)")) Guard(() => ShowReport(WFCoreMapExporter.Export(_profile)));
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear Spawned")) Guard(() => { WFTerrainBuilder.ClearSpawned(_profile); _report = "Spawned content cleared."; });
            if (GUILayout.Button("Bake NavMesh")) Guard(() => ShowReport(WFTerrainBuilder.BakeNavMesh(_profile)));
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Select World in Scene"))
            {
                ForgedWorld w = WFTerrainBuilder.FindWorld(_profile);
                if (w != null) { Selection.activeGameObject = w.gameObject; SceneView.FrameLastActiveSceneView(); }
                else _report = "No world for this profile in the open scene yet.";
            }

            if (!string.IsNullOrEmpty(_report))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(_report, MessageType.None);
            }
            EditorGUILayout.EndScrollView();
        }

        private void ShowReport(WFBuildReport r)
        {
            _report = r.ToString();
            Debug.Log("[WorldForge] " + _report);
            _preview = null;
        }

        /// <summary>Runs long operations outside OnGUI (avoids layout errors) and reports failures.</summary>
        private void Guard(Action a)
        {
            EditorApplication.delayCall += () =>
            {
                try { a(); }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    _report = "Failed: " + e.Message;
                    EditorUtility.DisplayDialog("World Forge", e.Message, "OK");
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                    Repaint();
                }
            };
        }

        private Texture2D BuildPreview()
        {
            WFHeightContext ctx = WFTerrainBuilder.PreviewHeights(_profile);
            var fields = new WFWorldFields(ctx);
            const int size = 384;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[size * size];
            BiomeProfile biome = _profile.biome;
            float[] weights = biome != null ? new float[biome.textureRules.Count] : null;
            Vector3 light = new Vector3(-0.55f, 0.75f, -0.35f).normalized;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float wx = (x + 0.5f) / size * ctx.SizeX, wz = (y + 0.5f) / size * ctx.SizeZ;
                    WFWorldFields.Sample s = fields.At(wx, wz);
                    Vector3 n = fields.NormalAt(wx, wz);
                    float shade = Mathf.Clamp01(Vector3.Dot(n, light)) * 0.85f + 0.25f;
                    Color baseCol = Color.Lerp(new Color(0.18f, 0.19f, 0.22f), new Color(0.55f, 0.56f, 0.6f), s.height / Mathf.Max(1f, ctx.MaxHeight));
                    if (biome != null && biome.textureRules.Count > 0)
                    {
                        WFRules.TextureWeights(biome.textureRules, s, _profile.seed, biome.textureSharpness, weights);
                        Color c = Color.black;
                        for (int r = 0; r < weights.Length; r++) c += RuleColor(biome.textureRules[r], r) * weights[r];
                        baseCol = c;
                    }
                    if (_showPaths && s.pathDistance < 1.2f) baseCol = Color.Lerp(baseCol, new Color(0.65f, 0.38f, 0.25f), 0.6f);
                    Color final = baseCol * shade;
                    final.a = 1f;
                    pixels[y * size + x] = final;
                }
            }
            if (_showPois && _profile.pois != null)
                foreach (WFPoiDefinition poi in _profile.pois)
                {
                    if (poi == null) continue;
                    int px = Mathf.RoundToInt(poi.position.x / ctx.SizeX * size), py = Mathf.RoundToInt(poi.position.y / ctx.SizeZ * size);
                    Color32 col = ForgedPoi.GizmoColor(poi.type);
                    for (int oy = -3; oy <= 3; oy++)
                        for (int ox = -3; ox <= 3; ox++)
                        {
                            int qx = px + ox, qy = py + oy;
                            if (qx < 0 || qy < 0 || qx >= size || qy >= size || ox * ox + oy * oy > 9) continue;
                            pixels[qy * size + qx] = col;
                        }
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            if (_preview != null) DestroyImmediate(_preview);
            _report = "Preview: heights " + Min(ctx.Heights).ToString("0.0") + " – " + Max(ctx.Heights).ToString("0.0") + " m, unwalkable " +
                      (fields.UnwalkableFraction(_profile.maxWalkableSlope) * 100f).ToString("0.0") + " %";
            return tex;
        }

        private static Color RuleColor(WFTextureRule rule, int index)
        {
            if (rule != null && rule.terrainLayer != null)
            {
                Color tint = rule.terrainLayer.diffuseRemapMax;
                if (rule.terrainLayer.diffuseTexture != null)
                {
                    string n = rule.terrainLayer.name.ToLowerInvariant();
                    if (n.Contains("basalt")) return new Color(0.27f, 0.30f, 0.36f) * tint;
                    if (n.Contains("scoria") || n.Contains("path")) return new Color(0.40f, 0.30f, 0.27f) * tint;
                    if (n.Contains("lichen")) return new Color(0.38f, 0.40f, 0.27f) * tint;
                    if (n.Contains("ash")) return new Color(0.33f, 0.33f, 0.35f) * tint;
                }
            }
            Color[] palette = { new Color(0.33f, 0.33f, 0.35f), new Color(0.27f, 0.30f, 0.36f), new Color(0.40f, 0.30f, 0.27f), new Color(0.38f, 0.40f, 0.27f), new Color(0.5f, 0.45f, 0.4f), new Color(0.2f, 0.25f, 0.2f) };
            return palette[index % palette.Length];
        }

        private static float Min(float[] a) { float m = float.MaxValue; foreach (float v in a) if (v < m) m = v; return m; }
        private static float Max(float[] a) { float m = float.MinValue; foreach (float v in a) if (v > m) m = v; return m; }

        private static WorldProfile CreateEmptyProfile()
        {
            WFEditorUtil.EnsureFolder("Assets/WorldForgeData");
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/WorldForgeData/WF_WorldProfile.asset");
            var p = CreateInstance<WorldProfile>();
            p.heightLayers.Add(new WFNoiseLayer { label = "Base noise", amplitude = 8f, baseHeight = 2f });
            AssetDatabase.CreateAsset(p, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = p;
            return p;
        }
    }
}
