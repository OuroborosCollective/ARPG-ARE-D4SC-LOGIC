using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AreArpg.UnityClient;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace AreArpg.EditorTools
{
    /// <summary>
    /// Builds the crypt and dungeon visuals from the purchased pack "Stylized Dark Fantasy Asset" (owner task
    /// 2026-11-19/20: rework the whole dungeon environment with it). The pack stays private; this file only names
    /// its parts. Every result is a passive game visual: one combined mesh, materials converted for this project's
    /// render pipeline, no colliders, lights or particle systems. Candle flames and lights of the pack become empty
    /// marker nodes that the game dresses with its own flame and a budgeted light.
    /// </summary>
    public static class DarkFantasyCryptArt
    {
        public const string VendorRoot = "Assets/Stylized Dark Fantasy Asset";
        /// <summary>Private folder for converted materials and combined meshes. Never committed.</summary>
        public const string WorkFolder = "Assets/PrivateArt/DarkFantasy";
        public const string Visuals = "Assets/Resources/ARE/Visuals/";
        const string VendorPrefabs = VendorRoot + "/Prefabs/";
        const string VendorMaterials = VendorRoot + "/Materials/";
        const string VendorTextures = VendorRoot + "/Materials/Textures/";
        /// <summary>Module size of the procedural crypt in metres: tile edge, wall height, wall thickness.</summary>
        public const float Tile = CryptDungeonDressing.TileMetres, WallHeight = 3.2f, WallDepth = .3f;
        const float EmissionBoost = 3f;

        sealed class Part
        {
            public string Prefab; public Vector3 At; public Vector3 Scale = Vector3.one; public float Yaw;
        }
        sealed class Slab
        {
            public string Material; public Vector3 Centre, Size; public float UvPerMetre = 1f;
        }
        sealed class Piece
        {
            public string Key; public Part[] Parts = new Part[0]; public Slab[] Slabs = new Slab[0];
            /// <summary>Extra marker nodes (name and position) besides those taken from the pack's flames and lights.</summary>
            public KeyValuePair<string, Vector3>[] Markers = new KeyValuePair<string, Vector3>[0];
        }

        static readonly Dictionary<Material, Material> Converted = new Dictionary<Material, Material>();
        static readonly List<Mesh> Scratch = new List<Mesh>();
        static string ReportPath { get { return Path.Combine(Directory.GetCurrentDirectory(), "Library/DarkFantasy/report.txt"); } }

        public static bool Installed { get { return AssetDatabase.IsValidFolder(VendorRoot); } }

        /// <summary>
        /// True when this game visual was built from the pack in this workspace. Older art builders then leave the
        /// key alone, so a rebuilt crypt is never replaced by the previous look.
        /// </summary>
        public static bool Owns(string key)
        {
            return Installed && Keys().Contains(key) && File.Exists(MeshPath(key));
        }

        public static string[] Keys() { return Pieces().Select(p => p.Key).ToArray(); }

        static string MeshPath(string key) { return WorkFolder + "/Meshes/" + key + ".asset"; }

        static Part P(string prefab, float x, float y, float z, float scale = 1f, float yaw = 0f)
        {
            return new Part { Prefab = prefab, At = new Vector3(x, y, z), Scale = Vector3.one * scale, Yaw = yaw };
        }

        static Slab S(string material, float x, float y, float z, float width, float height, float depth, float uvPerMetre)
        {
            return new Slab { Material = material, Centre = new Vector3(x, y, z), Size = new Vector3(width, height, depth), UvPerMetre = uvPerMetre };
        }

        /// <summary>A plain brick wall of one tile with a stone coping; decorated walls add relief in front (+Z).</summary>
        static Slab[] Wall(params Slab[] extra)
        {
            var slabs = new List<Slab>
            {
                S("Wall_Basic", 0, WallHeight * .5f, 0, Tile, WallHeight, WallDepth, 1f),
                S("Floor", 0, WallHeight + .09f, 0, Tile, .18f, WallDepth + .16f, .5f),
            };
            slabs.AddRange(extra);
            return slabs.ToArray();
        }

        /// <summary>
        /// All visuals of the set. Walls face +Z (towards the room), stand on y = 0 and are centred on their tile
        /// border. Small props are enlarged: at their real size they vanish under the game's high camera.
        /// </summary>
        static Piece[] Pieces()
        {
            float front = WallDepth * .5f;
            return new[]
            {
                new Piece { Key = "DungeonFloor", Slabs = new[] { S("Floor", 0, -.15f, 0, Tile, .3f, Tile, .5f) } },
                new Piece { Key = "DungeonWall", Slabs = Wall() },
                new Piece { Key = "DungeonWallB", Slabs = Wall(), Parts = new[] { P("Fundamentals/Wall_Holders_x1", 0, 0, front + .26f, 1.3f) } },
                new Piece { Key = "DungeonWallC", Slabs = Wall(), Parts = new[] { P("Fundamentals/Wall_Holders_x3", 0, 0, front + .155f, .84f) } },
                new Piece { Key = "DungeonWallD", Slabs = Wall(), Parts = new[]
                {
                    P("Decorative/Skull_low_02", 0, 1.25f, front + .36f, .6f),
                    P("Decorative/Candle_x3_Light", -1.25f, 0, front + .32f, 2.4f), P("Decorative/Candle_x2_Light", 1.25f, 0, front + .3f, 2.4f),
                } },
                new Piece { Key = "DungeonCorner", Parts = new[] { new Part { Prefab = "Fundamentals/Big_Gate_Pillar", Scale = new Vector3(1.5f, 1.07f, 1.5f) } } },
                new Piece { Key = "DungeonGate", Parts = new[] { P("Doors/Doors_01", 0, 0, 0, 1.2f, 180f) }, Slabs = new[]
                {
                    S("Wall_Basic", -1.5875f, 1.4f, 0, .725f, 2.8f, WallDepth, 1f), S("Wall_Basic", 1.5875f, 1.4f, 0, .725f, 2.8f, WallDepth, 1f),
                } },
                new Piece { Key = "DungeonPortal", Parts = new[]
                {
                    P("Fundamentals/Big_Gate_Pillar", -1.31f, 0, 0, 1.25f), P("Fundamentals/Big_Gate_Pillar", 1.31f, 0, 0, 1.25f),
                    P("Fundamentals/Big_Gate_Frame", 0, 2.42f, 0, 1.25f),
                }, Slabs = new[] { S("PortalVeil", 0, 1.5f, 0, 2.1f, 3f, .02f, 1f / 2.1f) } },
                new Piece { Key = "DungeonShrine", Parts = new[] { P("Stones/Stone_low_01", 0, 0, 0, 3.2f), P("Decorative/Skull_with_candle_x3", 0, .4f, 0, 2f) } },
                // A room's light source: a large lantern on a chain that disappears into the dark above. Its origin is
                // the floor point below it, so the game places it on the room centre.
                // It hangs above head height: lower, it would cover the view of the camera that follows the hero.
                new Piece { Key = "DungeonLantern", Parts = new[] { P("Decorative/Lamp_01", 0, 4f, 0, 2.2f), P("Decorative/Chain_01", 0, 4.95f, 0, 1f) },
                    Markers = new[] { new KeyValuePair<string, Vector3>(CryptDungeonDressing.LanternMarker, new Vector3(0, CryptDungeonDressing.LanternHeight, 0)) } },
                new Piece { Key = "DungeonProp0", Parts = new[] { P("Decorative/Bones_Stack_01", 0, 0, 0, 2.2f) } },
                new Piece { Key = "DungeonProp1", Parts = new[]
                {
                    P("Decorative/Skull_low_01", 0, 0, 0, 1.5f, 20f), P("Decorative/Bone_01", .45f, 0, .15f, 1.8f, 35f), P("Decorative/Bone_04", -.4f, 0, -.2f, 1.8f, -50f),
                } },
                new Piece { Key = "DungeonProp2", Parts = new[] { P("Stones/Stone_low_02", 0, 0, 0, 2.6f), P("Decorative/Book_01", 0, .33f, 0, 1.3f, 15f) } },
                new Piece { Key = "DungeonProp3", Parts = new[] { P("Decorative/Candle_x3_Light", 0, 0, 0, 2.6f), P("Decorative/Candle_x2_Light", .45f, 0, .2f, 2.2f) } },
                new Piece { Key = "DungeonProp4", Parts = new[]
                {
                    P("Stones/Stone_low_01", 0, 0, 0, 3f), P("Stones/Stone_low_03", .7f, 0, .25f, 2f, 40f), P("Stones/Stone_low_02", -.55f, 0, -.3f, 1.6f, -25f),
                } },
                new Piece { Key = "DungeonProp5", Parts = new[] { P("Decorative/Skull_low_02", 0, 0, 0, .5f), P("Decorative/Bones_Stack_03", .55f, 0, .3f, 2f, 25f) } },
                new Piece { Key = "DungeonProp6", Parts = new[] { P("Fundamentals/Altar_03", 0, 0, 0, 1.9f), P("Decorative/Candle_x2_Light", .5f, 0, .15f, 2f) } },
                new Piece { Key = "DungeonProp7", Parts = new[] { P("Stones/Stone_Pillar", 0, 0, 0, .55f) } },
            };
        }

        [MenuItem("ARE ARPG/Art/Dark Fantasy/Build Crypt Visuals")]
        public static void Build() { Build(null); }

        /// <summary>Build the whole set, or only the named visuals while one of them is being tuned.</summary>
        public static void Build(string[] only)
        {
            if (!Installed) throw new InvalidOperationException("Paket fehlt: " + VendorRoot);
            if (only != null)
                foreach (string key in only)
                    if (!Keys().Contains(key)) throw new InvalidOperationException("Unbekanntes Visual: " + key);
            EnsureFolder(WorkFolder + "/Materials"); EnsureFolder(WorkFolder + "/Meshes");
            Converted.Clear();
            var report = new StringBuilder();
            try
            {
                foreach (Piece piece in Pieces())
                    if (only == null || only.Contains(piece.Key)) report.AppendLine(BuildPiece(piece));
            }
            finally
            {
                foreach (Mesh mesh in Scratch) if (mesh != null) Object.DestroyImmediate(mesh);
                Scratch.Clear();
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, report.ToString());
            Debug.Log("ARE_DARK_FANTASY_CRYPT: " + (only == null ? Pieces().Length : only.Length) + " visuals built; report " + ReportPath);
        }

        static string BuildPiece(Piece piece)
        {
            var root = new GameObject(piece.Key);
            var stage = new GameObject("DarkFantasyStage");
            try
            {
                var byMaterial = new Dictionary<Material, List<CombineInstance>>();
                var order = new List<Material>();
                var markers = new List<KeyValuePair<string, Vector3>>();
                foreach (Slab slab in piece.Slabs)
                    Add(byMaterial, order, SlabMaterial(slab.Material), new CombineInstance { mesh = Box(slab.Size, slab.UvPerMetre), transform = Matrix4x4.Translate(slab.Centre) });
                foreach (Part part in piece.Parts) AddPart(part, stage.transform, byMaterial, order, markers);
                markers.AddRange(piece.Markers);
                if (order.Count == 0) throw new InvalidOperationException("Leeres Visual: " + piece.Key);

                var perMaterial = new List<CombineInstance>();
                foreach (Material material in order)
                {
                    var merged = new Mesh { indexFormat = IndexFormat.UInt32 };
                    merged.CombineMeshes(byMaterial[material].ToArray(), true, true, false);
                    Scratch.Add(merged);
                    perMaterial.Add(new CombineInstance { mesh = merged, transform = Matrix4x4.identity });
                }
                int vertices = perMaterial.Sum(c => c.mesh.vertexCount);
                var mesh = new Mesh { name = piece.Key, indexFormat = vertices < 65000 ? IndexFormat.UInt16 : IndexFormat.UInt32 };
                mesh.CombineMeshes(perMaterial.ToArray(), false, false, false);
                mesh.RecalculateBounds();
                Mesh saved = SaveMesh(mesh, piece.Key);

                root.AddComponent<MeshFilter>().sharedMesh = saved;
                root.AddComponent<MeshRenderer>().sharedMaterials = order.ToArray();
                foreach (var marker in markers)
                {
                    var node = new GameObject(marker.Key);
                    node.transform.SetParent(root.transform, false);
                    node.transform.localPosition = marker.Value;
                }
                if (PrefabUtility.SaveAsPrefabAsset(root, Visuals + piece.Key + ".prefab") == null)
                    throw new IOException("Konnte nicht speichern: " + piece.Key);
                long triangles = 0;
                for (int sub = 0; sub < saved.subMeshCount; sub++) triangles += saved.GetIndexCount(sub) / 3;
                Vector3 size = saved.bounds.size;
                return piece.Key + "\ttris=" + triangles + "\tmaterials=" + order.Count + "\tmarkers=" + markers.Count +
                       "\tsize=" + size.x.ToString("0.00") + "x" + size.y.ToString("0.00") + "x" + size.z.ToString("0.00");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(stage); }
        }

        /// <summary>
        /// Copy the meshes of one pack prefab. The part is first moved so that its bottom centre sits on the
        /// origin (the pack's pivots are arbitrary), then scaled, turned and placed. Flames and lights of the pack
        /// leave marker positions only.
        /// </summary>
        static void AddPart(Part part, Transform stage, Dictionary<Material, List<CombineInstance>> byMaterial, List<Material> order,
            List<KeyValuePair<string, Vector3>> markers)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VendorPrefabs + part.Prefab + ".prefab");
            if (prefab == null) throw new InvalidOperationException("Teil fehlt im Paket: " + part.Prefab);
            GameObject instance = Object.Instantiate(prefab, stage, false);
            try
            {
                instance.transform.localPosition = Vector3.zero; instance.transform.localRotation = Quaternion.identity; instance.transform.localScale = Vector3.one;
                var filters = instance.GetComponentsInChildren<MeshFilter>(true).Where(f =>
                    f.sharedMesh != null && f.gameObject.activeInHierarchy && f.GetComponent<MeshRenderer>() != null && f.GetComponent<MeshRenderer>().enabled).ToArray();
                if (filters.Length == 0) throw new InvalidOperationException("Teil ohne Mesh: " + part.Prefab);
                Bounds bounds = filters[0].GetComponent<MeshRenderer>().bounds;
                foreach (MeshFilter filter in filters) bounds.Encapsulate(filter.GetComponent<MeshRenderer>().bounds);
                Matrix4x4 place = Matrix4x4.TRS(part.At, Quaternion.Euler(0f, part.Yaw, 0f), part.Scale) *
                                  Matrix4x4.Translate(-new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
                foreach (MeshFilter filter in filters)
                {
                    Material[] materials = filter.GetComponent<MeshRenderer>().sharedMaterials;
                    if (materials.Length == 0) throw new InvalidOperationException("Teil ohne Material: " + part.Prefab);
                    for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++)
                        Add(byMaterial, order, Convert(materials[Mathf.Min(sub, materials.Length - 1)]),
                            new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = sub, transform = place * filter.transform.localToWorldMatrix });
                }
                foreach (ParticleSystem flame in instance.GetComponentsInChildren<ParticleSystem>(true))
                    markers.Add(new KeyValuePair<string, Vector3>(CryptDungeonDressing.FlameMarker, place.MultiplyPoint3x4(flame.transform.position)));
                foreach (Light light in instance.GetComponentsInChildren<Light>(true))
                    markers.Add(new KeyValuePair<string, Vector3>(CryptDungeonDressing.LightMarker, place.MultiplyPoint3x4(light.transform.position)));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        static void Add(Dictionary<Material, List<CombineInstance>> byMaterial, List<Material> order, Material material, CombineInstance instance)
        {
            List<CombineInstance> list;
            if (!byMaterial.TryGetValue(material, out list)) { list = new List<CombineInstance>(); byMaterial.Add(material, list); order.Add(material); }
            list.Add(instance);
        }

        static Mesh SaveMesh(Mesh mesh, string key)
        {
            string path = MeshPath(key);
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
            else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); } // keep the asset identity
            // The game never reads the vertices: do not ship a second copy for scripts.
            var serialized = new SerializedObject(saved);
            serialized.FindProperty("m_IsReadable").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(saved);
            return saved;
        }

        /// <summary>
        /// The pack's materials use the built-in Standard shader and would render magenta here. The copy keeps colour
        /// map, normal map, tint and glow; stone stays matt (no highlights, no reflections: cheaper on phones too).
        /// The files of the pack are never changed.
        /// </summary>
        static Material Convert(Material source)
        {
            if (source == null) throw new InvalidOperationException("Teil ohne Material.");
            Material material;
            if (Converted.TryGetValue(source, out material)) return material;
            material = Asset(Safe(source.name));
            Apply(source, material, true);
            Converted.Add(source, material);
            return material;
        }

        /// <summary>Material for generated slabs: their UVs are in metres, so the texture is not tiled again.</summary>
        static Material SlabMaterial(string name)
        {
            if (name == "PortalVeil") return PortalVeil();
            var source = AssetDatabase.LoadAssetAtPath<Material>(VendorMaterials + name + ".mat");
            if (source == null) throw new InvalidOperationException("Material fehlt im Paket: " + name);
            Material material = Asset(Safe(name) + "_Metric");
            Apply(source, material, false);
            return material;
        }

        static Material Asset(string name)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit fehlt.");
            string path = WorkFolder + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            else material.shader = shader;
            return material;
        }

        static void Apply(Material source, Material target, bool keepTiling)
        {
            Texture colour = source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
            target.SetTexture("_BaseMap", colour);
            target.SetTextureScale("_BaseMap", keepTiling && colour != null ? source.GetTextureScale("_MainTex") : Vector2.one);
            target.SetColor("_BaseColor", source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
            Texture normal = source.HasProperty("_BumpMap") ? source.GetTexture("_BumpMap") : null;
            target.SetTexture("_BumpMap", normal);
            target.SetFloat("_BumpScale", normal != null && source.HasProperty("_BumpScale") ? source.GetFloat("_BumpScale") : 1f);
            Keyword(target, "_NORMALMAP", normal != null);
            bool glows = source.IsKeywordEnabled("_EMISSION") && source.HasProperty("_EmissionColor");
            target.SetTexture("_EmissionMap", glows && source.HasProperty("_EmissionMap") ? source.GetTexture("_EmissionMap") : null);
            target.SetColor("_EmissionColor", glows ? source.GetColor("_EmissionColor") * EmissionBoost : Color.black);
            Keyword(target, "_EMISSION", glows);
            target.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            target.SetFloat("_Smoothness", .1f); target.SetFloat("_Metallic", 0f);
            target.SetFloat("_SpecularHighlights", 0f); Keyword(target, "_SPECULARHIGHLIGHTS_OFF", true);
            target.SetFloat("_EnvironmentReflections", 0f); Keyword(target, "_ENVIRONMENTREFLECTIONS_OFF", true);
            EditorUtility.SetDirty(target);
        }

        /// <summary>The glowing veil inside a passage frame: the pack's teleport cloud, added to the picture.</summary>
        static Material PortalVeil()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("URP/Unlit fehlt.");
            string path = WorkFolder + "/Materials/PortalVeil.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            else material.shader = shader;
            var cloud = AssetDatabase.LoadAssetAtPath<Texture2D>(VendorTextures + "Teleport_Texture.png");
            if (cloud == null) throw new InvalidOperationException("Textur fehlt im Paket: Teleport_Texture");
            material.SetTexture("_BaseMap", cloud);
            material.SetColor("_BaseColor", new Color(.25f, 1.25f, .95f, .85f));
            material.SetFloat("_Surface", 1f); material.SetFloat("_Blend", 2f); // transparent, additive
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f); material.SetFloat("_Cull", (float)CullMode.Off);
            Keyword(material, "_SURFACE_TYPE_TRANSPARENT", true);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void Keyword(Material material, string keyword, bool on)
        {
            if (on) material.EnableKeyword(keyword); else material.DisableKeyword(keyword);
        }

        static string Safe(string name)
        {
            var text = new StringBuilder();
            foreach (char c in name) text.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            return text.ToString();
        }

        /// <summary>
        /// A box whose texture coordinates are metres times <paramref name="uvPerMetre"/>, so neighbouring modules
        /// continue the same stone pattern without a visible repeat seam.
        /// </summary>
        static Mesh Box(Vector3 size, float uvPerMetre)
        {
            Vector3 h = size * .5f;
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uvs = new List<Vector2>(); var triangles = new List<int>();
            // Each face: origin corner, then the two edges (u direction, v direction) and their lengths in metres.
            Action<Vector3, Vector3, Vector3, float, float> face = (origin, u, v, width, height) =>
            {
                int start = vertices.Count;
                vertices.Add(origin); vertices.Add(origin + u * width); vertices.Add(origin + v * height); vertices.Add(origin + u * width + v * height);
                Vector3 normal = Vector3.Cross(u, v).normalized;
                for (int i = 0; i < 4; i++) normals.Add(normal);
                uvs.Add(Vector2.zero); uvs.Add(new Vector2(width * uvPerMetre, 0)); uvs.Add(new Vector2(0, height * uvPerMetre)); uvs.Add(new Vector2(width * uvPerMetre, height * uvPerMetre));
                // Unity draws the side from which the corners run clockwise: this order faces along cross(u, v).
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2); triangles.Add(start + 2); triangles.Add(start + 1); triangles.Add(start + 3);
            };
            face(new Vector3(-h.x, -h.y, h.z), Vector3.right, Vector3.up, size.x, size.y);     // front, towards +Z
            face(new Vector3(h.x, -h.y, -h.z), Vector3.left, Vector3.up, size.x, size.y);      // back
            face(new Vector3(-h.x, -h.y, -h.z), Vector3.forward, Vector3.up, size.z, size.y);  // left
            face(new Vector3(h.x, -h.y, h.z), Vector3.back, Vector3.up, size.z, size.y);       // right
            face(new Vector3(-h.x, h.y, h.z), Vector3.right, Vector3.back, size.x, size.z);    // top
            face(new Vector3(-h.x, -h.y, -h.z), Vector3.right, Vector3.forward, size.x, size.z); // bottom
            var mesh = new Mesh { name = "DarkFantasySlab" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            Scratch.Add(mesh);
            return mesh;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
