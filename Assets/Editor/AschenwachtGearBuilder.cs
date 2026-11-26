using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AreArpg.Core;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditorTools
{
    /// <summary>
    /// Owner decision 2026-11-26: the Glockenwacht's armour (first Aschenwacht archive) and the Aschbeter's two claw staffs
    /// (second archive) are rare loot, fitted to the hero. This builds their private pictures (never in the repository):
    ///
    /// - The armour is carried over from the Glockenwacht's skeleton to the hero's: every point of a piece follows the bones
    ///   it is weighted to. In each bone's frame (origin at the joint, up along the bone or the body, forward with the body)
    ///   the point keeps its place, stretched along a limb by the ratio of the bone lengths and otherwise by the ratio of the
    ///   two torsos; then it is weighted to the hero's matching humanoid bone. The result is a mesh in the hero's bind pose
    ///   with his bones' bind poses, so the runtime only points the renderer at his bones (AschenwachtGearPresentation).
    /// - The two staffs become arsenal pieces like the others: shaft along the up axis, grip at the origin.
    ///
    /// Called from AschenwachtCourtBuilder after it has prepared both sets' models and materials.
    /// </summary>
    public static class AschenwachtGearBuilder
    {
        private const string Arsenal = "Assets/Resources/ARE/Visuals/Werkarsenal/";
        private const string MeshFolder = "Assets/PrivateArt/Aschenwacht_II_Game/Armor/";
        private const string Hero = "Assets/Resources/ARE/Visuals/EntryHero.prefab";

        private static readonly (string model, RegionalRelic id)[] Armour =
        {
            ("Visierhelm", RegionalRelic.A_Visierhelm), ("Kuerass", RegionalRelic.A_Kuerass),
            ("Schulterplatten", RegionalRelic.A_Schulterplatten), ("Gliedpanzer", RegionalRelic.A_Gliedpanzer)
        };

        /// <summary>The Glockenwacht's joints and the humanoid bone each stands for; a side is resolved by the body, not the name.</summary>
        private static readonly Dictionary<string, HumanBodyBones> Joints = new Dictionary<string, HumanBodyBones>(StringComparer.Ordinal)
        {
            { "Hips", HumanBodyBones.Hips }, { "Spine", HumanBodyBones.Spine }, { "Chest", HumanBodyBones.Chest },
            { "Neck", HumanBodyBones.Neck }, { "Head", HumanBodyBones.Head },
            { "Collar.L", HumanBodyBones.LeftShoulder }, { "UpperArm.L", HumanBodyBones.LeftUpperArm }, { "Forearm.L", HumanBodyBones.LeftLowerArm }, { "Hand.L", HumanBodyBones.LeftHand },
            { "Collar.R", HumanBodyBones.RightShoulder }, { "UpperArm.R", HumanBodyBones.RightUpperArm }, { "Forearm.R", HumanBodyBones.RightLowerArm }, { "Hand.R", HumanBodyBones.RightHand },
            { "Thigh.L", HumanBodyBones.LeftUpperLeg }, { "Shin.L", HumanBodyBones.LeftLowerLeg }, { "Foot.L", HumanBodyBones.LeftFoot },
            { "Thigh.R", HumanBodyBones.RightUpperLeg }, { "Shin.R", HumanBodyBones.RightLowerLeg }, { "Foot.R", HumanBodyBones.RightFoot }
        };

        /// <summary>The next joint along a limb: its direction is the bone's up, its distance the bone's length.</summary>
        private static readonly Dictionary<HumanBodyBones, HumanBodyBones> Distal = new Dictionary<HumanBodyBones, HumanBodyBones>
        {
            { HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm }, { HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand },
            { HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm }, { HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand },
            { HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg }, { HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot },
            { HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg }, { HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot }
        };

        private sealed class Body
        {
            public readonly Dictionary<HumanBodyBones, Vector3> Pivot = new Dictionary<HumanBodyBones, Vector3>();
            public Vector3 Up, Front;
            public float Torso, HeadWidth;
            /// <summary>The points mostly weighted to the head (face and hair), in bind space, and the box around them.</summary>
            public Vector3[] HeadPoints;
            public Bounds HeadBox;
            public Vector3 Axis(HumanBodyBones b)
            {
                if (Distal.TryGetValue(b, out var d) && Pivot.ContainsKey(d)) return (Pivot[d] - Pivot[b]).normalized;
                if (b == HumanBodyBones.LeftHand && Pivot.ContainsKey(HumanBodyBones.LeftLowerArm)) return (Pivot[b] - Pivot[HumanBodyBones.LeftLowerArm]).normalized;
                if (b == HumanBodyBones.RightHand && Pivot.ContainsKey(HumanBodyBones.RightLowerArm)) return (Pivot[b] - Pivot[HumanBodyBones.RightLowerArm]).normalized;
                if (b == HumanBodyBones.LeftFoot && Pivot.ContainsKey(HumanBodyBones.LeftLowerLeg)) return (Pivot[b] - Pivot[HumanBodyBones.LeftLowerLeg]).normalized;
                if (b == HumanBodyBones.RightFoot && Pivot.ContainsKey(HumanBodyBones.RightLowerLeg)) return (Pivot[b] - Pivot[HumanBodyBones.RightLowerLeg]).normalized;
                return Up;
            }
            public float Length(HumanBodyBones b)
            {
                return Distal.TryGetValue(b, out var d) && Pivot.ContainsKey(d) ? (Pivot[d] - Pivot[b]).magnitude : 0f;
            }
            /// <summary>The bone's frame as a rotation: up along the bone (or the body), forward with the body.</summary>
            public Quaternion Frame(HumanBodyBones b)
            {
                var up = Axis(b);
                var forward = Vector3.ProjectOnPlane(Front, up);
                if (forward.sqrMagnitude < 1e-6f) forward = Vector3.ProjectOnPlane(Vector3.Cross(up, Vector3.right), up);
                return Quaternion.LookRotation(forward.normalized, up);
            }
        }

        public static bool TryBuild()
        {
            var hero = AssetDatabase.LoadAssetAtPath<GameObject>(Hero);
            if (hero == null || Armour.Any(a => AssetDatabase.LoadAssetAtPath<GameObject>(AschenwachtCourtBuilder.CourtModels + a.model + ".fbx") == null)) return false;
            Directory.CreateDirectory(MeshFolder);
            BuildStaff("Klauenstab_Einhand", RegionalRelic.A_KlauenstabEinhand);
            BuildStaff("Klauenstab_Zweihand", RegionalRelic.A_KlauenstabZweihand);
            var heroInstance = UnityEngine.Object.Instantiate(hero);
            try
            {
                var animator = heroInstance.GetComponentsInChildren<Animator>(true).First(a => a.isHuman);
                var heroSkin = heroInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(s => s.sharedMesh ? s.sharedMesh.vertexCount : 0).First();
                var heroBody = HeroBody(animator, heroSkin, out var heroIndex);
                heroBody.HeadWidth = HeadWidth(heroSkin.sharedMesh, heroIndex[HumanBodyBones.Head], heroBody);
                heroBody.HeadPoints = HeadPoints(heroSkin.sharedMesh, heroIndex[HumanBodyBones.Head]);
                heroBody.HeadBox = new Bounds(heroBody.HeadPoints[0], Vector3.zero);
                foreach (var p in heroBody.HeadPoints) heroBody.HeadBox.Encapsulate(p);
                float guardHead = GuardHeadWidth();
                foreach (var (model, id) in Armour) BuildArmour(model, id, heroSkin, heroBody, heroIndex, guardHead);
            }
            finally { UnityEngine.Object.DestroyImmediate(heroInstance); }
            AssetDatabase.SaveAssets();
            Debug.Log("ARE_ASCHENWACHT_GEAR_BUILT armour=" + Armour.Length + " staffs=2");
            return true;
        }

        private static Body HeroBody(Animator animator, SkinnedMeshRenderer skin, out Dictionary<HumanBodyBones, int> index)
        {
            var body = new Body();
            index = new Dictionary<HumanBodyBones, int>();
            var bind = skin.sharedMesh.bindposes;
            foreach (HumanBodyBones b in Joints.Values.Distinct())
            {
                var t = animator.GetBoneTransform(b);
                int i = t ? Array.IndexOf(skin.bones, t) : -1;
                if (i < 0) continue;
                index[b] = i;
                body.Pivot[b] = bind[i].inverse.MultiplyPoint3x4(Vector3.zero);
            }
            Orient(body, rightToLeft: null);
            return body;
        }

        /// <summary>Up from the hips to the neck, forward from the arm roots, torso length from the hips to the neck.</summary>
        private static void Orient(Body body, bool? rightToLeft)
        {
            var hips = body.Pivot[HumanBodyBones.Hips];
            var top = body.Pivot.ContainsKey(HumanBodyBones.Neck) ? body.Pivot[HumanBodyBones.Neck] : body.Pivot[HumanBodyBones.Head];
            body.Up = (top - hips).normalized;
            body.Torso = (top - hips).magnitude;
            var across = body.Pivot[HumanBodyBones.RightUpperArm] - body.Pivot[HumanBodyBones.LeftUpperArm];
            body.Front = Vector3.Cross(across, body.Up).normalized;
        }

        /// <summary>
        /// The Glockenwacht's body read from a piece's own skin. Which named side is the body's left is decided by the knees,
        /// which bend forward: the side whose arm lies to the right of the forward direction is the right one.
        /// </summary>
        private static Body ArmourBody(SkinnedMeshRenderer skin, out Dictionary<int, HumanBodyBones> joint)
        {
            var named = new Dictionary<string, Vector3>(StringComparer.Ordinal);
            var bind = skin.sharedMesh.bindposes;
            for (int i = 0; i < skin.bones.Length; i++) named[skin.bones[i].name] = bind[i].inverse.MultiplyPoint3x4(Vector3.zero);
            var hips = named["Hips"];
            var up = (named["Neck"] - hips).normalized;
            Vector3 Knee(string s) => named["Shin." + s] - (named["Thigh." + s] + named["Foot." + s]) * .5f;
            var front = Vector3.ProjectOnPlane(Knee("L") + Knee("R"), up).normalized;
            var right = Vector3.Cross(up, front);
            bool swapped = Vector3.Dot(named["UpperArm.L"] - named["Chest"], right) > 0f; // the ".L" side lies on the body's right
            var body = new Body();
            joint = new Dictionary<int, HumanBodyBones>();
            for (int i = 0; i < skin.bones.Length; i++)
            {
                string name = skin.bones[i].name;
                if (!Joints.TryGetValue(name, out var human)) continue;
                if (swapped) human = Mirror(human);
                joint[i] = human;
                body.Pivot[human] = named[name];
            }
            Orient(body, swapped);
            return body;
        }

        private static HumanBodyBones Mirror(HumanBodyBones b)
        {
            string name = b.ToString();
            if (name.StartsWith("Left", StringComparison.Ordinal)) return (HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), "Right" + name.Substring(4));
            if (name.StartsWith("Right", StringComparison.Ordinal)) return (HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), "Left" + name.Substring(5));
            return b;
        }

        /// <summary>The hero's own bone for a Glockenwacht joint; joints he lacks (a shoulder, a neck) give way to their parent.</summary>
        private static HumanBodyBones Target(HumanBodyBones b, Dictionary<HumanBodyBones, int> heroIndex)
        {
            while (!heroIndex.ContainsKey(b))
            {
                switch (b)
                {
                    case HumanBodyBones.LeftShoulder: case HumanBodyBones.RightShoulder: case HumanBodyBones.Neck: b = HumanBodyBones.Chest; break;
                    case HumanBodyBones.Chest: b = HumanBodyBones.Spine; break;
                    case HumanBodyBones.Spine: b = HumanBodyBones.Hips; break;
                    default: throw new InvalidOperationException("The hero has no bone for " + b);
                }
            }
            return b;
        }

        /// <summary>
        /// How wide a head is across the body: the points mostly weighted to the head bone, measured along the body's right.
        /// The hero's stylized head is far larger for his torso than the Glockenwacht's, so the helm follows the heads.
        /// </summary>
        private static float HeadWidth(Mesh mesh, int head, Body body)
        {
            var right = Vector3.Cross(body.Up, body.Front).normalized;
            var vertices = mesh.vertices; var weights = mesh.boneWeights;
            float min = float.MaxValue, max = float.MinValue;
            for (int v = 0; v < vertices.Length; v++)
            {
                var w = weights[v];
                if (w.boneIndex0 != head || w.weight0 < .6f) continue;
                float x = Vector3.Dot(vertices[v], right);
                min = Mathf.Min(min, x); max = Mathf.Max(max, x);
            }
            if (min > max) throw new InvalidOperationException("No head points to measure");
            return max - min;
        }

        private static Vector3[] HeadPoints(Mesh mesh, int head)
        {
            var vertices = mesh.vertices; var weights = mesh.boneWeights;
            var points = new List<Vector3>();
            for (int v = 0; v < vertices.Length; v++)
                if (weights[v].boneIndex0 == head && weights[v].weight0 >= .6f) points.Add(vertices[v]);
            if (points.Count == 0) throw new InvalidOperationException("No head points to measure");
            return points.ToArray();
        }

        /// <summary>
        /// A piece worn only on the head (the visored helm) goes over the whole head. The hero's stylized head with its hair
        /// is as wide as the Glockenwacht's helm itself, so the helm is centred on that head and scaled evenly until nearly
        /// all of the head (<see cref="HelmHolds"/>) lies inside its shell with a little room: each head point is compared
        /// with the farthest helm point in about its direction from the centre. The rare stray hair tip may still show.
        /// </summary>
        private const float HelmHolds = .99f, HelmRoom = .97f, HelmCone = .97f;
        private static void FitOverHead(Vector3[] points, Body hero)
        {
            var box = new Bounds(points[0], Vector3.zero);
            foreach (var p in points) box.Encapsulate(p);
            var head = hero.HeadBox;
            float scale = 0f;
            for (int a = 0; a < 3; a++) if (box.extents[a] > 1e-5f) scale = Mathf.Max(scale, head.extents[a] / box.extents[a]);
            for (int i = 0; i < points.Length; i++) points[i] = head.center + (points[i] - box.center) * scale;

            var shell = new List<Vector3>(points.Length);
            foreach (var p in points) { var d = p - head.center; if (d.sqrMagnitude > 1e-8f) shell.Add(d); }
            var reach = new List<float>(hero.HeadPoints.Length);
            foreach (var p in hero.HeadPoints)
            {
                var d = p - head.center; float length = d.magnitude;
                if (length < 1e-4f) continue;
                var direction = d / length; float farthest = 0f;
                foreach (var s in shell) { float m = s.magnitude; if (Vector3.Dot(s / m, direction) > HelmCone) farthest = Mathf.Max(farthest, m); }
                if (farthest > 0f) reach.Add(length / farthest);
            }
            reach.Sort();
            float needed = reach.Count > 0 ? reach[(int)(HelmHolds * (reach.Count - 1))] / HelmRoom : 1f;
            if (needed <= 1f) return;
            for (int i = 0; i < points.Length; i++) points[i] = head.center + (points[i] - head.center) * needed;
        }

        /// <summary>The Glockenwacht's own head width, from its figure in the first archive.</summary>
        private static float GuardHeadWidth()
        {
            string path = AschenwachtCourtBuilder.CourtModels + "Glockenwacht.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (!importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
            var skin = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<SkinnedMeshRenderer>(true);
            var body = ArmourBody(skin, out var joint);
            int head = joint.First(kv => kv.Value == HumanBodyBones.Head).Key;
            return HeadWidth(skin.sharedMesh, head, body);
        }

        private static void BuildArmour(string model, RegionalRelic id, SkinnedMeshRenderer heroSkin, Body hero, Dictionary<HumanBodyBones, int> heroIndex, float guardHead)
        {
            string path = AschenwachtCourtBuilder.CourtModels + model + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (!importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var skin = source.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var mesh = skin.sharedMesh;
            var gw = ArmourBody(skin, out var joint);
            float bodyScale = hero.Torso / gw.Torso;
            float headScale = hero.HeadWidth / guardHead;

            var vertices = mesh.vertices; var normals = mesh.normals; var weights = mesh.boneWeights;
            var outVertices = new Vector3[vertices.Length]; var outNormals = new Vector3[vertices.Length];
            var used = new List<HumanBodyBones>();
            int Slot(HumanBodyBones b) { int s = used.IndexOf(b); if (s < 0) { used.Add(b); s = used.Count - 1; } return s; }
            var outWeights = new BoneWeight[vertices.Length];
            for (int v = 0; v < vertices.Length; v++)
            {
                var w = weights[v];
                var influences = new[] { (w.boneIndex0, w.weight0), (w.boneIndex1, w.weight1), (w.boneIndex2, w.weight2), (w.boneIndex3, w.weight3) };
                Vector3 p = Vector3.zero, n = Vector3.zero; float total = 0f;
                var mapped = new Dictionary<int, float>();
                foreach (var (bone, weight) in influences)
                {
                    if (weight <= 0f || !joint.TryGetValue(bone, out var human)) continue;
                    var target = Target(human, heroIndex);
                    // Each joint's own frame on both bodies; a joint the hero lacks keeps its own frame and moves with its parent.
                    var fromFrame = gw.Frame(human);
                    var toFrame = hero.Pivot.ContainsKey(human) ? hero.Frame(human) : hero.Frame(target);
                    var toPivot = hero.Pivot.ContainsKey(human) ? hero.Pivot[human] : hero.Pivot[target] + toFrame * (Quaternion.Inverse(gw.Frame(target)) * (gw.Pivot[human] - gw.Pivot[target]) * bodyScale);
                    var local = Quaternion.Inverse(fromFrame) * (vertices[v] - gw.Pivot[human]);
                    bool head = human == HumanBodyBones.Head || human == HumanBodyBones.Neck;
                    float across = head ? headScale : bodyScale;
                    float along = gw.Length(human) > 1e-5f && hero.Length(human) > 1e-5f ? hero.Length(human) / gw.Length(human) : across;
                    local = new Vector3(local.x * across, local.y * along, local.z * across);
                    p += weight * (toPivot + toFrame * local);
                    n += weight * (toFrame * (Quaternion.Inverse(fromFrame) * normals[v]));
                    total += weight;
                    int slot = Slot(target);
                    mapped[slot] = (mapped.TryGetValue(slot, out float sum) ? sum : 0f) + weight;
                }
                if (total <= 0f) throw new InvalidOperationException(model + ": a point without weights");
                outVertices[v] = p / total;
                outNormals[v] = n.normalized;
                var top = mapped.OrderByDescending(kv => kv.Value).Take(4).ToArray();
                float norm = top.Sum(kv => kv.Value);
                var bw = new BoneWeight();
                if (top.Length > 0) { bw.boneIndex0 = top[0].Key; bw.weight0 = top[0].Value / norm; }
                if (top.Length > 1) { bw.boneIndex1 = top[1].Key; bw.weight1 = top[1].Value / norm; }
                if (top.Length > 2) { bw.boneIndex2 = top[2].Key; bw.weight2 = top[2].Value / norm; }
                if (top.Length > 3) { bw.boneIndex3 = top[3].Key; bw.weight3 = top[3].Value / norm; }
                outWeights[v] = bw;
            }
            if (used.Count == 1 && used[0] == HumanBodyBones.Head) FitOverHead(outVertices, hero);

            string meshPath = MeshFolder + id + ".asset";
            var fitted = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            bool fresh = fitted == null;
            if (fresh) fitted = new Mesh();
            fitted.Clear();
            fitted.name = id.ToString();
            fitted.indexFormat = mesh.indexFormat;
            fitted.vertices = outVertices;
            fitted.normals = outNormals;
            fitted.uv = mesh.uv;
            fitted.subMeshCount = mesh.subMeshCount;
            for (int s = 0; s < mesh.subMeshCount; s++) fitted.SetTriangles(mesh.GetTriangles(s), s);
            fitted.boneWeights = outWeights;
            var heroBind = heroSkin.sharedMesh.bindposes;
            fitted.bindposes = used.Select(b => heroBind[heroIndex[b]]).ToArray();
            fitted.RecalculateTangents();
            fitted.RecalculateBounds();
            if (fresh) AssetDatabase.CreateAsset(fitted, meshPath); else EditorUtility.SetDirty(fitted);

            var root = new GameObject(id.ToString());
            try
            {
                var bones = new GameObject("Bones").transform;
                bones.SetParent(root.transform, false);
                var list = used.Select(b => { var t = new GameObject("Bone_" + b).transform; t.SetParent(bones, false); return t; }).ToArray();
                var part = new GameObject("Skin");
                part.transform.SetParent(root.transform, false);
                var renderer = part.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = fitted;
                renderer.sharedMaterials = skin.sharedMaterials;
                renderer.bones = list;
                renderer.rootBone = list[0];
                PrefabUtility.SaveAsPrefabAsset(root, Arsenal + id + ".prefab", out bool ok);
                if (!ok) throw new InvalidOperationException("Saving failed: " + id);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void BuildStaff(string model, RegionalRelic id)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(AschenwachtCourtBuilder.ForecourtModels + model + ".fbx");
            if (source == null) throw new InvalidOperationException("Staff missing: " + model);
            var filter = source.GetComponentInChildren<MeshFilter>(true);
            var root = new GameObject(id.ToString());
            try
            {
                var art = new GameObject(id + "_Art");
                art.transform.SetParent(root.transform, false);
                // The model asset's root stands at the origin; its own scale (the file's units) belongs to the piece, so the
                // mesh's full matrix is taken, not the one relative to that root (which would drop the root's scale).
                Matrix4x4 local = filter.transform.localToWorldMatrix;
                art.transform.localPosition = local.GetColumn(3);
                art.transform.localRotation = local.rotation;
                art.transform.localScale = local.lossyScale;
                art.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                art.AddComponent<MeshRenderer>().sharedMaterials = filter.GetComponent<MeshRenderer>().sharedMaterials;
                PrefabUtility.SaveAsPrefabAsset(root, Arsenal + id + ".prefab", out bool ok);
                if (!ok) throw new InvalidOperationException("Saving failed: " + id);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
