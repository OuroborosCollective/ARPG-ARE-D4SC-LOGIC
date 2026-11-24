using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AreArpg.UnityClient;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AreArpg.EditorTools
{
    /// <summary>
    /// Rigid things a figure carries (staff, lantern, pouches, quiver, jewellery). The recipe names a model, the bone
    /// that carries it and where it sits on the standing figure; a passive copy is hung on that bone of the game
    /// visual, so it follows every animation without any game code. Models and textures come from the private packs;
    /// what is derived from them (static meshes, tinted materials, glow masks) is saved next to the baked figures,
    /// outside the repository.
    /// </summary>
    internal static class FigureProps
    {
        /// <summary>Name prefix of every prop on a figure (the game can find and drop them by it).</summary>
        public const string Prefix = PassiveArtVisuals.CarriedPrefix;
        const string Folder = BozoFigureRecipes.BakeFolder + "/Props";
        const int GlowSize = 512;
        const float GlowValue = .55f;

        public static void Attach(BozoFigureRecipes.Figure recipe, Transform figureRoot, Animator animator)
        {
            foreach (BozoFigureRecipes.Prop prop in recipe.props ?? new BozoFigureRecipes.Prop[0])
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(prop.asset);
                if (model == null)
                {
                    // A private model that is not installed here: the figure is built without it instead of not at all.
                    Debug.LogWarning("Figur " + recipe.id + ": Requisit " + prop.name + " fehlt und wird ausgelassen (" + prop.asset + ")");
                    continue;
                }
                Transform bone = Bone(animator, prop.bone);
                if (bone == null) throw new InvalidOperationException("Knochen fehlt: " + prop.bone + " (" + recipe.id + ")");
                EnsureFolder();
                // Built upright at the figure's feet in figure space, then handed to the bone in its rest pose.
                var holder = new GameObject(Prefix + prop.name).transform;
                holder.SetParent(figureRoot, false);
                var copy = Object.Instantiate(model, holder, false);
                copy.name = model.name;
                copy.transform.localPosition = Vector3.zero; // prefabs of a pack often keep their place in its demo scene
                MakeStatic(copy, holder, model.name);
                MeshRenderer[] renderers = holder.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("Requisit ohne sichtbares Mesh: " + prop.asset);
                var materials = new Dictionary<Material, Material>();
                foreach (MeshRenderer renderer in renderers)
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
                    {
                        if (source == null) return null;
                        Material made;
                        if (!materials.TryGetValue(source, out made)) materials[source] = made = Worn(source, prop);
                        return made;
                    }).ToArray();
                Bounds bounds = Visible(renderers);
                float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (longest < .0001f) throw new InvalidOperationException("Requisit ohne Ausdehnung: " + prop.asset);
                if (prop.size > 0f)
                {
                    holder.localScale = Vector3.one * (prop.size / longest);
                    bounds = Visible(renderers);
                }
                Vector3 anchor = prop.anchor == "origin" ? holder.position :
                    prop.anchor == "top" ? new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) :
                    prop.anchor == "bottom" ? new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) : bounds.center;
                Vector3 offset = anchor - holder.position;
                Quaternion turn = prop.rotation != null && prop.rotation.Length == 3 ?
                    Quaternion.Euler(prop.rotation[0], prop.rotation[1], prop.rotation[2]) : Quaternion.identity;
                Vector3 place = new Vector3(prop.position[0], prop.position[1], prop.position[2]);
                holder.rotation = figureRoot.rotation * turn;
                holder.position = figureRoot.TransformPoint(place) - holder.rotation * offset;
                holder.SetParent(bone, true);
            }
        }

        /// <summary>The bone by its humanoid name, from the animator or, where that is not ready in the editor, from the avatar's own mapping.</summary>
        static Transform Bone(Animator animator, string name)
        {
            HumanBodyBones id;
            if (!Enum.TryParse(name, out id) || animator == null || animator.avatar == null) return null;
            Transform bone = animator.GetBoneTransform(id);
            if (bone != null) return bone;
            foreach (HumanBone human in animator.avatar.humanDescription.human)
                if (human.humanName == name)
                    return animator.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == human.boneName);
            return null;
        }

        /// <summary>Only transforms and static meshes stay: no light, particles, physics, scripts, skeleton or lower detail levels.</summary>
        static void MakeStatic(GameObject copy, Transform holder, string modelName)
        {
            foreach (GameObject extra in copy.GetComponentsInChildren<Light>(true).Select(c => c.gameObject)
                         .Concat(copy.GetComponentsInChildren<ParticleSystem>(true).Select(c => c.gameObject))
                         .Concat(copy.GetComponentsInChildren<Camera>(true).Select(c => c.gameObject)).Distinct().ToArray())
                if (extra != null && extra != copy) Object.DestroyImmediate(extra);
            foreach (LODGroup group in copy.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] levels = group.GetLODs();
                var kept = new HashSet<Renderer>(levels.Length > 0 ? levels[0].renderers.Where(r => r != null) : new Renderer[0]);
                foreach (Renderer lower in levels.Skip(1).SelectMany(l => l.renderers).Where(r => r != null && !kept.Contains(r)).Distinct().ToArray())
                    Object.DestroyImmediate(lower.gameObject);
            }
            foreach (SkinnedMeshRenderer skin in copy.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.sharedMesh == null) continue;
                // A weapon with a skeleton of its own: its rest pose becomes a static mesh. The baked vertices already
                // carry the scale of all parents, so the new part keeps only the place and the turn.
                var mesh = new Mesh();
                skin.BakeMesh(mesh);
                mesh.name = Safe(modelName) + "_" + Safe(skin.name);
                mesh = Save(mesh, Folder + "/" + mesh.name + ".asset");
                var part = new GameObject(skin.name);
                part.transform.SetParent(holder, false);
                part.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
            }
            for (int pass = 0; pass < 3; pass++) // dependent components (RequireComponent) go in later passes
                foreach (Component component in copy.GetComponentsInChildren<Component>(true))
                {
                    if (component == null || component is Transform || component is MeshFilter || component is MeshRenderer) continue;
                    Object.DestroyImmediate(component);
                }
            foreach (Transform node in holder.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(node.gameObject);
            foreach (MeshRenderer renderer in holder.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (!renderer.enabled || filter == null || filter.sharedMesh == null) Object.DestroyImmediate(renderer.gameObject);
            }
            // Empty branches (bones, sockets, emptied groups) go, deepest first.
            foreach (Transform node in holder.GetComponentsInChildren<Transform>(true).Reverse().ToArray())
                if (node != null && node != holder && node.GetComponentInChildren<MeshRenderer>(true) == null) Object.DestroyImmediate(node.gameObject);
        }

        static Bounds Visible(MeshRenderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (MeshRenderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        /// <summary>
        /// The pack's material as the figure wears it: multiplied with the tint, dulled, and glowing where the recipe
        /// asks for it. One private material per source material and treatment, shared by all figures.
        /// </summary>
        static Material Worn(Material source, BozoFigureRecipes.Prop prop)
        {
            string treatment = prop.tint + "|" + prop.glow + "|" + prop.glowStrength.ToString("R") + "|" + prop.glowSaturation.ToString("R") + "|" +
                prop.metallic.ToString("R") + "|" + prop.smoothness.ToString("R");
            string path = Folder + "/" + Safe(source.name) + "_" + Stamp(AssetDatabase.GetAssetPath(source) + "|" + treatment) + ".mat";
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) throw new InvalidOperationException("The URP Lit shader is missing");
            bool native = source.shader != null && (source.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal) ||
                source.shader.name.StartsWith("Shader Graphs/", StringComparison.Ordinal));
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(native ? source.shader : lit); AssetDatabase.CreateAsset(material, path); }
            if (native)
            {
                material.shader = source.shader;
                material.CopyPropertiesFromMaterial(source);
            }
            else
            {
                // A material of another render pipeline: its colour and its texture on the game's own shader.
                material.shader = lit;
                material.SetTexture("_BaseMap", source.mainTexture);
                material.SetColor("_BaseColor", source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
            }
            Color tint;
            if (BozoFigureRecipes.TryColor(prop.tint, out tint))
                foreach (string colour in new[] { "_BaseColor", "_Color" })
                    if (material.HasProperty(colour))
                    {
                        Color own = material.GetColor(colour);
                        material.SetColor(colour, new Color(own.r * tint.r, own.g * tint.g, own.b * tint.b, own.a));
                    }
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", prop.smoothness >= 0f ? prop.smoothness : material.GetFloat("_Smoothness") * .5f);
            if (prop.metallic >= 0f && material.HasProperty("_Metallic")) material.SetFloat("_Metallic", prop.metallic);
            Color glow;
            if (BozoFigureRecipes.TryColor(prop.glow, out glow) && material.HasProperty("_EmissionColor"))
            {
                Texture colours = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
                if (material.HasProperty("_EmissionMap")) material.SetTexture("_EmissionMap", colours != null ? GlowMask(colours, prop.glowSaturation) : null);
                material.SetColor("_EmissionColor", glow.linear * prop.glowStrength);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Where a colour texture glows: its bright, saturated texels (a candle flame, a vivid liquid) keep their colour,
        /// everything else is black. Written once per texture and threshold.
        /// </summary>
        static Texture2D GlowMask(Texture colours, float saturation)
        {
            string path = Folder + "/" + Safe(colours.name) + "_Glow" + Mathf.RoundToInt(saturation * 100f) + ".png";
            string source = AssetDatabase.GetAssetPath(colours);
            if (!File.Exists(path) || (File.Exists(source) && File.GetLastWriteTimeUtc(source) > File.GetLastWriteTimeUtc(path)))
            {
                Color32[] pixels = Pixels(colours, GlowSize);
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 p = pixels[i];
                    int high = Mathf.Max(p.r, Mathf.Max(p.g, p.b)), low = Mathf.Min(p.r, Mathf.Min(p.g, p.b));
                    bool glows = high >= GlowValue * 255f && high - low >= saturation * high;
                    pixels[i] = glows ? new Color32(p.r, p.g, p.b, 255) : new Color32(0, 0, 0, 255);
                }
                WritePng(path, pixels, GlowSize);
            }
            return Imported(path, GlowSize);
        }

        /// <summary>The sRGB texels of any texture at a given edge length (the texture need not be readable).</summary>
        internal static Color32[] Pixels(Texture source, int size)
        {
            RenderTexture target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            var copy = new Texture2D(size, size, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                copy.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                return copy.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(copy);
            }
        }

        internal static void WritePng(string path, Color32[] pixels, int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(pixels);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
        }

        /// <summary>A written colour texture as the game loads it: sRGB, with mip maps, at most the given size.</summary>
        internal static Texture2D Imported(string path, int size)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Default || !importer.sRGBTexture || importer.maxTextureSize != size || !importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
                importer.maxTextureSize = size; importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Store a mesh, keeping the identity of an existing asset so that figures built earlier keep drawing it.</summary>
        static Mesh Save(Mesh mesh, string path)
        {
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, saved);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(saved);
            return saved;
        }

        static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(Folder)) return;
            if (!AssetDatabase.IsValidFolder(BozoFigureRecipes.BakeFolder)) throw new InvalidOperationException("Der Ordner der gebackenen Figuren fehlt.");
            AssetDatabase.CreateFolder(BozoFigureRecipes.BakeFolder, "Props");
        }

        static string Safe(string name)
        {
            return new string((name ?? "").Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray());
        }

        /// <summary>A short, stable name part for a treatment (FNV-1a; not string.GetHashCode, which may differ between runs).</summary>
        static string Stamp(string text)
        {
            uint hash = 2166136261;
            foreach (char c in text) { hash ^= c; hash *= 16777619; }
            return hash.ToString("x8");
        }
    }
}
