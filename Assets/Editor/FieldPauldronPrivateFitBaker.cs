using System;
using System.Collections.Generic;
using System.Linq;
using AreArpg.UnityClient;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AreArpg.Editor
{
    public static partial class FieldPauldronPrivateFitBaker
    {
        private const string HeroPath = "Assets/Resources/ARE/Visuals/EntryHero.prefab";
        private const string ModulePath = "Assets/BoZo_StylizedModularCharacters/Prefabs/Fantasy/Resources/Shoulders/Shoulders_SoldiersPauldrons.prefab";
        private const string FitPath = "Assets/Resources/ARE/Visuals/FieldPauldronBodyFit.asset";
        private const string MeshPath = "Assets/PrivateArt/BozoCharacters/hero/hero_FeldschulterBody.asset";

        // The two outputs stay inside the project's ignored private visual folders.
        public static void Bake()
        {
            var hero = AssetDatabase.LoadAssetAtPath<GameObject>(HeroPath);
            var module = AssetDatabase.LoadAssetAtPath<GameObject>(ModulePath);
            if (hero == null || module == null) return;
            var inspection = Object.Instantiate(hero);
            inspection.hideFlags = HideFlags.HideAndDontSave;
            try { BakeHero(inspection, module); }
            finally { Object.DestroyImmediate(inspection); }
        }

        private static void BakeHero(GameObject hero, GameObject module)
        {
            var avatar = hero.GetComponentsInChildren<Animator>(true).First(a => a.avatar != null && a.isHuman);
            avatar.Rebind();
            var arm = avatar.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var skin = hero.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => Array.IndexOf(r.bones, arm) >= 0);
            int armIndex = Array.IndexOf(skin.bones, arm);
            if (skin.sharedMesh.subMeshCount != 1) throw new InvalidOperationException("Privater Held benötigt eine eindeutige Atlas-Oberfläche.");
            var input = module.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var parts = input.Where(r => WeightedTo(r, arm.name)).ToArray();
            if (parts.Length != 1) throw new InvalidOperationException("Linke Soldatenschulter anhand der Knochengewichte nicht eindeutig.");
            var part = parts[0];
            var copy = ReadableCopy(skin.sharedMesh);
            var originalPart = ReadableCopy(part.sharedMesh);
            try
            {
                var vertices = copy.vertices;
                var triangles = copy.triangles;
                var area = OriginalArea(avatar, skin, copy);
                var armRest = copy.bindposes[armIndex].inverse.MultiplyPoint3x4(Vector3.zero);
                var body = Quaternion.LookRotation(Vector3.Cross(area.rightUnit, area.torso).normalized, area.torso);
                var srcComponents = Components(originalPart.vertices, originalPart.triangles);
                var dstComponents = Components(vertices, triangles);
                var weights = copy.boneWeights;
                var remove = new HashSet<int>();
                var bounds = new Bounds();
                bool any = false;
                foreach (var source in srcComponents)
                {
                    var matches = dstComponents.Where(candidate =>
                        source.Faces.Count == candidate.Faces.Count && source.Topology.SequenceEqual(candidate.Topology) &&
                        Vector3.Distance(candidate.Centre, armRest) < area.span * .84f &&
                        candidate.Corners.All(v => Dominant(weights[v]) == armIndex) &&
                        Distance(source.Geometry, candidate.Geometry) < .0075).ToArray();
                    if (matches.Length != 1)
                        throw new InvalidOperationException("Keine eindeutige linke Modulinsel: " + source.Faces.Count +
                            " Dreiecke, " + matches.Length + " Treffer; Modul " + part.name);
                    var match = matches[0];
                    foreach (int index in match.Faces)
                        if (!remove.Add(index)) throw new InvalidOperationException("Doppelte private Fitting-Zuordnung.");
                    foreach (int vertex in match.Corners)
                    {
                        var p = Quaternion.Inverse(body) * vertices[vertex];
                        if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; } else bounds.Encapsulate(p);
                    }
                }
                if (!any || remove.Count * 3 != originalPart.triangles.Length || remove.Count * 3 >= triangles.Length)
                    throw new InvalidOperationException("Kaufteil im Gesamtmesh nicht vollständig nachgewiesen.");
                var kept = new List<int>(triangles.Length - remove.Count * 3);
                for (int i = 0; i < triangles.Length; i += 3)
                    if (!remove.Contains(i)) { kept.Add(triangles[i]); kept.Add(triangles[i+1]); kept.Add(triangles[i+2]); }
                copy.name = "hero_FeldschulterBody";
                copy.SetTriangles(kept, 0, false);
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
                if (saved == null) { saved = Object.Instantiate(copy); saved.name = copy.name; AssetDatabase.CreateAsset(saved, MeshPath); }
                else EditorUtility.CopySerialized(copy, saved);
                SetReadable(saved, false);
                var fit = AssetDatabase.LoadAssetAtPath<FieldPauldronBodyFit>(FitPath);
                if (fit == null) { fit = ScriptableObject.CreateInstance<FieldPauldronBodyFit>(); AssetDatabase.CreateAsset(fit, FitPath); }
                fit.Original = skin.sharedMesh;
                fit.WithoutLeftCap = saved;
                fit.CapCenterInUpperArm = copy.bindposes[armIndex].MultiplyPoint3x4(body * bounds.center);
                fit.CapWidthDepthInSkin = new Vector2(bounds.size.x, bounds.size.z);
                EditorUtility.SetDirty(saved);
                EditorUtility.SetDirty(fit);
                AssetDatabase.SaveAssets();
                Debug.Log("Feldschulter private fit: body=" + skin.sharedMesh.name + " cap=" + part.name + " bone=" + arm.name +
                    " sourceVertices=" + originalPart.vertexCount + " originalTriangles=" + triangles.Length/3 +
                    " removed=" + remove.Count + " remaining=" + kept.Count/3 + " retainedVertices=" + saved.vertexCount +
                    " centreInUpperArm=" + fit.CapCenterInUpperArm.ToString("F5") + " widthDepth=" + fit.CapWidthDepthInSkin.ToString("F5"));
            }
            finally { Object.DestroyImmediate(copy); Object.DestroyImmediate(originalPart); }
        }

        private static (Vector3 rightUnit, Vector3 torso, float span) OriginalArea(Animator avatar, SkinnedMeshRenderer skin, Mesh mesh)
        {
            var bones = skin.bones;
            var bind = mesh.bindposes;
            Vector3 Joint(HumanBodyBones name) =>
                bind[Array.IndexOf(bones, avatar.GetBoneTransform(name))].inverse.MultiplyPoint3x4(Vector3.zero);
            var across = Joint(HumanBodyBones.RightUpperArm) - Joint(HumanBodyBones.LeftUpperArm);
            var torso = (Joint(HumanBodyBones.Chest) - Joint(HumanBodyBones.Hips)).normalized;
            return (across.normalized, torso, across.magnitude);
        }

        private static bool WeightedTo(SkinnedMeshRenderer renderer, string bone)
        {
            if (!renderer.sharedMesh) return false;
            int index = Array.FindIndex(renderer.bones, b => b && b.name == bone);
            if (index < 0) return false;
            var copy = ReadableCopy(renderer.sharedMesh);
            try { var weights = copy.boneWeights; return weights.Length == copy.vertexCount && weights.All(w => Dominant(w) == index); }
            finally { Object.DestroyImmediate(copy); }
        }

        private static int Dominant(BoneWeight w)
        {
            float greatest = w.weight0;
            int bone = w.boneIndex0;
            if (w.weight1 > greatest) { greatest = w.weight1; bone = w.boneIndex1; }
            if (w.weight2 > greatest) { greatest = w.weight2; bone = w.boneIndex2; }
            if (w.weight3 > greatest) bone = w.boneIndex3;
            return bone;
        }

        private static Mesh ReadableCopy(Mesh original)
        {
            var copy = Object.Instantiate(original);
            SetReadable(copy, true);
            return copy;
        }

        private static void SetReadable(Mesh mesh, bool readable)
        {
            var so = new SerializedObject(mesh);
            so.FindProperty("m_IsReadable").boolValue = readable;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class Island
        {
            public readonly List<int> Faces = new List<int>();
            public readonly HashSet<int> Corners = new HashSet<int>();
            public Vector3 Centre;
            public int[] Topology;
            public float[] Geometry;
        }

        // UV borders split indices on the purchased mesh. Weld equal positions for component identity only.
        // The production body keeps its own vertices, indices outside the matched islands, UVs and bone weights.
        private static Island[] Components(Vector3[] points, int[] triangles)
        {
            var canonical = new Dictionary<Vector3Int, int>();
            var parent = new int[points.Length];
            int Root(int v) { while (v != parent[v]) { parent[v] = parent[parent[v]]; v = parent[v]; } return v; }
            int[] welded = new int[points.Length];
            for (int i = 0; i < points.Length; ++i)
            {
                parent[i] = i;
                var key = Vector3Int.RoundToInt(points[i] * 100000f);
                if (!canonical.TryGetValue(key, out var existing)) { canonical.Add(key, i); existing = i; }
                welded[i] = existing;
            }
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = welded[triangles[i]], b = welded[triangles[i+1]], c = welded[triangles[i+2]];
                parent[Root(b)] = Root(a); parent[Root(c)] = Root(a);
            }
            var sections = new Dictionary<int, Island>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int root = Root(welded[triangles[i]]);
                if (!sections.TryGetValue(root, out var part)) { part = new Island(); sections.Add(root, part); }
                part.Faces.Add(i);
                for (int k = 0; k < 3; ++k) part.Corners.Add(triangles[i+k]);
            }
            foreach (var part in sections.Values)
            {
                var degree = new Dictionary<int,int>();
                var edges = new List<float>(part.Faces.Count * 3);
                var size = new Bounds(points[triangles[part.Faces[0]]], Vector3.zero);
                foreach (int i in part.Faces)
                    for (int j = 0; j < 3; ++j)
                    {
                        int a = triangles[i+j], b = triangles[i+(j+1)%3], v = welded[a];
                        size.Encapsulate(points[a]);
                        degree.TryGetValue(v, out int count); degree[v] = count + 1;
                        edges.Add(Vector3.Distance(points[a], points[b]));
                    }
                part.Centre = size.center;
                part.Topology = degree.Values.OrderBy(value => value).ToArray();
                edges.Sort(); double sum = edges.Sum(v => (double)v);
                if (sum <= 0d) throw new InvalidOperationException("Leere Geometrie im privaten Schulterabgleich.");
                part.Geometry = edges.Select(e => (float)(e/sum)).ToArray();
            }
            return sections.Values.ToArray();
        }

        private static double Distance(float[] a, float[] b)
        {
            double error = 0;
            for (int i = 0; i < a.Length; ++i) error += Math.Abs(a[i] - b[i]);
            return error;
        }
    }
}
