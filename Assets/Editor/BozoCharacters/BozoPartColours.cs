#if ARE_BOZO_CHARACTERS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Bozo.ModularCharacters;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditorTools
{
    /// <summary>
    /// Which of the nine colours of a part paints how much of it, and where on the body. The names the pack shows
    /// for a part are often only a three-name placeholder while the part uses up to nine colours, so a recipe
    /// written from the names alone leaves visible areas in the pack's default colours. The pack's clothes shader
    /// picks a group of three colours with the ID map (red 1–3, green 4–6, blue 7–9) and weights them with the red,
    /// green and blue of the diffuse map. Read this report before writing or changing a figure recipe.
    /// </summary>
    public static class BozoPartColours
    {
        static readonly string[] Slots =
        {
            "Top", "Bottom", "Feet", "Gloves", "Hat", "Shoulders", "Neck", "HairFront", "HairBack", "LowerFace", "UpperFace", "Head", "Body"
        };
        static string ReportPath { get { return Path.Combine(Directory.GetCurrentDirectory(), "Library/BozoCharacters/part-colours.txt"); } }

        [MenuItem("ARE ARPG/Art/BoZo Characters/Write Part Colour Report")]
        public static void WriteReport()
        {
            var report = new StringBuilder();
            report.AppendLine("part [names of the pack] / colour: approximate share of the part's surface (64 equal-area samples per triangle), height range in metres, default colour");
            foreach (string slot in Slots)
                foreach (Outfit part in Resources.LoadAll<Outfit>(slot).OrderBy(p => p.name, StringComparer.Ordinal))
                    Describe(slot + "/" + part.name, part, report);
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, report.ToString());
            Debug.Log("ARE_BOZO_PART_COLOURS: " + ReportPath);
        }

        static void Describe(string path, Outfit part, StringBuilder report)
        {
            var share = new double[9]; var low = new double[9]; var high = new double[9];
            for (int k = 0; k < 9; k++) { low[k] = double.MaxValue; high[k] = double.MinValue; }
            var pixels = new Dictionary<Texture, Color32[]>(); var sizes = new Dictionary<Texture, Vector2Int>();
            Material first = null;
            foreach (SkinnedMeshRenderer skin in part.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh mesh = skin.sharedMesh;
                if (mesh == null || mesh.uv.Length != mesh.vertexCount) continue;
                Vector3[] vertices = mesh.vertices; Vector2[] uv = mesh.uv;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    Material material = skin.sharedMaterials[Mathf.Min(sub, skin.sharedMaterials.Length - 1)];
                    if (material == null) continue;
                    if (first == null) first = material;
                    Texture idMap = material.HasProperty("_IDMap") ? material.GetTexture("_IDMap") : null;
                    Texture diffuse = material.HasProperty("_Diffuse") ? material.GetTexture("_Diffuse") : null;
                    Color32[] ids = Read(idMap, pixels, sizes), tones = Read(diffuse, pixels, sizes);
                    int[] triangles = mesh.GetTriangles(sub);
                    for (int t = 0; t + 2 < triangles.Length; t += 3)
                    {
                        Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                        double area = Vector3.Cross(b - a, c - a).magnitude * .5;
                        if (area <= 0) continue;
                        // A square-root map distributes equal-area samples across the whole triangle.
                        const int steps = 8;
                        for (int row = 0; row < steps; row++)
                        for (int column = 0; column < steps; column++)
                        {
                        float u = Mathf.Sqrt((row + .5f) / steps), v = (column + .5f) / steps;
                        Vector3 bary = new Vector3(1 - u, u * (1 - v), u * v);
                        Vector2 at = uv[triangles[t]] * bary.x + uv[triangles[t + 1]] * bary.y + uv[triangles[t + 2]] * bary.z;
                        float green = 0, blue = 0, r = 1, g = 0, bl = 0;
                        if (ids != null)
                        {
                            Color32 id = Sample(ids, sizes[idMap], at);
                            if (id.a < 8) continue; // outside the part's tile of a shared texture
                            green = id.g / 255f; blue = id.b / 255f;
                        }
                        if (tones != null) { Color32 tone = Sample(tones, sizes[diffuse], at); r = tone.r / 255f; g = tone.g / 255f; bl = tone.b / 255f; }
                        float[] group = { (1 - green) * (1 - blue), green * (1 - blue), blue }; float[] weight = { r, g, bl };
                        double y = a.y * bary.x + b.y * bary.y + c.y * bary.z;
                        for (int k = 0; k < 9; k++)
                        {
                            double amount = group[k / 3] * weight[k % 3];
                            if (amount <= 0) continue;
                            share[k] += area * amount / (steps * steps);
                            if (amount > .3) { low[k] = Math.Min(low[k], y); high[k] = Math.Max(high[k], y); }
                        }
                        }
                    }
                }
            }
            double total = share.Sum();
            report.AppendLine(path + " [" + string.Join("|", part.ColorChannels ?? new string[0]) + "]");
            if (first == null || total <= 0) { report.AppendLine("   (texture layer without its own mesh: colours as in the pack's presets)"); return; }
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            for (int k = 0; k < 9; k++)
            {
                double percent = share[k] / total * 100;
                if (percent < .4) continue;
                report.AppendLine("   " + (k + 1) + ": " + percent.ToString("0.0", culture) + "%"
                    + (low[k] <= high[k] ? " height " + low[k].ToString("0.00", culture) + "–" + high[k].ToString("0.00", culture) : "")
                    + " default #" + ColorUtility.ToHtmlStringRGB(first.GetColor("_Color_" + (k + 1))));
            }
        }

        /// <summary>The pack's textures are not readable; decode the source file instead of changing import settings.</summary>
        static Color32[] Read(Texture texture, Dictionary<Texture, Color32[]> pixels, Dictionary<Texture, Vector2Int> sizes)
        {
            if (texture == null) return null;
            Color32[] known;
            if (pixels.TryGetValue(texture, out known)) return known;
            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) { pixels[texture] = null; return null; }
            var copy = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!copy.LoadImage(File.ReadAllBytes(path))) { pixels[texture] = null; return null; }
                sizes[texture] = new Vector2Int(copy.width, copy.height);
                known = copy.GetPixels32();
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
            pixels[texture] = known;
            return known;
        }

        static Color32 Sample(Color32[] pixels, Vector2Int size, Vector2 uv)
        {
            float u = uv.x - Mathf.Floor(uv.x), v = uv.y - Mathf.Floor(uv.y);
            return pixels[Mathf.Min(size.y - 1, (int)(v * size.y)) * size.x + Mathf.Min(size.x - 1, (int)(u * size.x))];
        }
    }
}
#endif
