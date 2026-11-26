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
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Hero) == null ||
                Armour.Any(a => AssetDatabase.LoadAssetAtPath<GameObject>(AschenwachtCourtBuilder.CourtModels + a.model + ".fbx") == null)) return false;
            BuildStaff("Klauenstab_Einhand", RegionalRelic.A_KlauenstabEinhand);
            BuildStaff("Klauenstab_Zweihand", RegionalRelic.A_KlauenstabZweihand);
            var pieces = Armour.Select(a => (a.id, ReadableSkins(AschenwachtCourtBuilder.CourtModels + a.model + ".fbx"))).ToList();
            if (!FitToHero(pieces, MeshFolder)) return false;
            Debug.Log("ARE_ASCHENWACHT_GEAR_BUILT armour=" + Armour.Length + " staffs=2");
            return true;
        }

        /// <summary>The skinned meshes of a model file on the Glockenwacht's skeleton, made readable for fitting.</summary>
        internal static SkinnedMeshRenderer[] ReadableSkins(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("Model missing: " + path);
            if (!importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<SkinnedMeshRenderer>(true);
        }

        /// <summary>The hero as the target of fitting: his skin, his body, his bones' places in the skin, and the guard's head width.</summary>
        private sealed class HeroFit
        {
            public SkinnedMeshRenderer Skin;
            public Body Body;
            public Dictionary<HumanBodyBones, int> Index;
            public float GuardHead;
        }

        /// <summary>
        /// Fit pieces made on the Glockenwacht's skeleton to the hero. A piece is one or more skinned meshes (the Nachtvesper's
        /// cuirass is its mail, plate and gorget); it is saved as one mesh in <paramref name="meshFolder"/>, one submesh per
        /// material, and as an arsenal prefab named after its regional piece. Used for Aschenwacht's armour and the Nachtvesper.
        /// </summary>
        internal static bool FitToHero(IList<(RegionalRelic id, SkinnedMeshRenderer[] skins)> pieces, string meshFolder)
        {
            return FitToHero(pieces.Select(p => new FitPiece(p.id.ToString(), p.skins,
                RegionalRelics.Slot(p.id) == RegionalRelicSlot.Head ? HeadFit.Closed : HeadFit.None)).ToList(), meshFolder);
        }

        /// <summary>How a piece sits on the head: not at all, closed over the whole head and face (a helm), or open at the face (a hood).</summary>
        public enum HeadFit { None, Closed, Open }

        /// <summary>One piece to fit: the name of its mesh and prefab, its meshes, and how it sits on the head.</summary>
        internal sealed class FitPiece
        {
            public readonly string Name;
            public readonly SkinnedMeshRenderer[] Skins;
            public readonly HeadFit Head;
            public FitPiece(string name, SkinnedMeshRenderer[] skins, HeadFit head) { Name = name; Skins = skins; Head = head; }
        }

        internal static bool FitToHero(IList<FitPiece> pieces, string meshFolder)
        {
            var hero = AssetDatabase.LoadAssetAtPath<GameObject>(Hero);
            if (hero == null) return false;
            Directory.CreateDirectory(meshFolder);
            var heroInstance = UnityEngine.Object.Instantiate(hero);
            try
            {
                var animator = heroInstance.GetComponentsInChildren<Animator>(true).First(a => a.isHuman);
                var fit = new HeroFit { Skin = heroInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(s => s.sharedMesh ? s.sharedMesh.vertexCount : 0).First() };
                fit.Body = HeroBody(animator, fit.Skin, out fit.Index);
                fit.Body.HeadWidth = HeadWidth(fit.Skin.sharedMesh, fit.Index[HumanBodyBones.Head], fit.Body);
                fit.Body.HeadPoints = HeadPoints(fit.Skin, animator.GetBoneTransform(HumanBodyBones.Head));
                fit.Body.HeadBox = new Bounds(fit.Body.HeadPoints[0], Vector3.zero);
                foreach (var p in fit.Body.HeadPoints) fit.Body.HeadBox.Encapsulate(p);
                fit.GuardHead = GuardHeadWidth();
                foreach (var piece in pieces) BuildArmour(piece, meshFolder, fit);
            }
            finally { UnityEngine.Object.DestroyImmediate(heroInstance); }
            AssetDatabase.SaveAssets();
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
            // A mesh lists only the joints it is weighted to; the skeleton is read from every mesh of the same model.
            var named = new Dictionary<string, Vector3>(StringComparer.Ordinal);
            foreach (var family in skin.transform.root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Prepend(skin))
            {
                var poses = family.sharedMesh.bindposes;
                for (int i = 0; i < family.bones.Length; i++)
                    if (family.bones[i] && !named.ContainsKey(family.bones[i].name)) named[family.bones[i].name] = poses[i].inverse.MultiplyPoint3x4(Vector3.zero);
            }
            var hips = named["Hips"];
            var up = (named["Neck"] - hips).normalized;
            Vector3 Knee(string s) => named["Shin." + s] - (named["Thigh." + s] + named["Foot." + s]) * .5f;
            var front = Vector3.ProjectOnPlane(Knee("L") + Knee("R"), up).normalized;
            var right = Vector3.Cross(up, front);
            bool swapped = Vector3.Dot(named["UpperArm.L"] - named["Chest"], right) > 0f; // the ".L" side lies on the body's right
            var body = new Body();
            joint = new Dictionary<int, HumanBodyBones>();
            HumanBodyBones Side(HumanBodyBones human) => swapped ? Mirror(human) : human;
            foreach (var pair in named)
                if (Joints.TryGetValue(pair.Key, out var human)) body.Pivot[Side(human)] = pair.Value;
            for (int i = 0; i < skin.bones.Length; i++)
                if (skin.bones[i] && Joints.TryGetValue(skin.bones[i].name, out var human)) joint[i] = Side(human);
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

        /// <summary>
        /// The points of the hero's head and face: weighted mostly to the head bone or a bone below it (jaw, brows, eyes,
        /// nose, ears), but not to his hair, which a worn helm hides (AschenwachtGearPresentation).
        /// </summary>
        private static Vector3[] HeadPoints(SkinnedMeshRenderer skin, Transform head)
        {
            var bones = skin.bones;
            var face = new bool[bones.Length];
            for (int b = 0; b < bones.Length; b++)
            {
                var bone = bones[b];
                if (!bone || (bone != head && !bone.IsChildOf(head))) continue;
                bool hair = false;
                for (var t = bone; t && t != head; t = t.parent) hair |= t.name.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0;
                face[b] = !hair;
            }
            var vertices = skin.sharedMesh.vertices; var weights = skin.sharedMesh.boneWeights;
            var points = new List<Vector3>();
            for (int v = 0; v < vertices.Length; v++) if (face[weights[v].boneIndex0]) points.Add(vertices[v]);
            if (points.Count == 0) throw new InvalidOperationException("No head points to measure");
            return points.ToArray();
        }

        /// <summary>
        /// A piece worn only on the head (the visored helm) goes over the whole head and face. Its box is first laid on the
        /// box of the hero's head; then it is moved by up to two centimetres up, down, forward and back to where the head
        /// sticks out of it least, and scaled evenly until every head point lies inside its shell with a little room. A head
        /// point is compared with the farthest helm point in about its direction from the centre.
        /// </summary>
        private const float HelmRoom = .98f, HelmCone = .97f, HelmShiftStepMm = 5f;
        private const int HelmShiftSteps = 4, HelmShellPoints = 2500, HelmProbePoints = 1600;
        /// <summary>A hood open at the face holds only the head behind the face: points this far forward are left out.</summary>
        private const float OpenFaceCone = .4f;
        private static void FitOverHead(Vector3[] points, Body hero, bool openFace)
        {
            var head = hero.HeadBox;
            if (!openFace)
            {
                // A helm's box is first laid on the head's box. A hood keeps the place its joints gave it (it hangs down
                // the back, so its box does not stand for the head).
                var box = new Bounds(points[0], Vector3.zero);
                foreach (var p in points) box.Encapsulate(p);
                float scale = 0f;
                for (int a = 0; a < 3; a++) if (box.extents[a] > 1e-5f) scale = Mathf.Max(scale, head.extents[a] / box.extents[a]);
                for (int i = 0; i < points.Length; i++) points[i] = head.center + (points[i] - box.center) * scale;
            }

            var shellDirection = new List<Vector3>(); var shellLength = new List<float>();
            for (int i = 0; i < points.Length; i += Math.Max(1, points.Length / HelmShellPoints))
            {
                var d = points[i] - head.center; float m = d.magnitude;
                if (m > 1e-4f) { shellDirection.Add(d / m); shellLength.Add(m); }
            }
            var probe = new List<Vector3>();
            for (int i = 0; i < hero.HeadPoints.Length; i += Math.Max(1, hero.HeadPoints.Length / HelmProbePoints))
                if (!openFace || Vector3.Dot((hero.HeadPoints[i] - head.center).normalized, hero.Front) <= OpenFaceCone) probe.Add(hero.HeadPoints[i]);
            float Worst(Vector3 shift)
            {
                float worst = 0f;
                foreach (var p in probe)
                {
                    var d = p - head.center - shift; float length = d.magnitude;
                    if (length < 1e-4f) continue;
                    var direction = d / length; float farthest = 0f;
                    for (int j = 0; j < shellDirection.Count; j++)
                        if (Vector3.Dot(shellDirection[j], direction) > HelmCone && shellLength[j] > farthest) farthest = shellLength[j];
                    if (farthest > 0f) worst = Mathf.Max(worst, length / farthest);
                }
                return worst;
            }
            Vector3 bestShift = Vector3.zero; float best = Worst(Vector3.zero);
            for (int up = -HelmShiftSteps; up <= HelmShiftSteps; up++)
                for (int forward = -HelmShiftSteps; forward <= HelmShiftSteps; forward++)
                {
                    var shift = (hero.Up * up + hero.Front * forward) * (HelmShiftStepMm * .001f);
                    float worst = Worst(shift);
                    if (worst < best - 1e-5f) { best = worst; bestShift = shift; }
                }
            float needed = best / HelmRoom;
            var centre = head.center + bestShift;
            for (int i = 0; i < points.Length; i++)
            {
                var moved = points[i] + bestShift;
                points[i] = needed > 1f ? centre + (moved - centre) * needed : moved;
            }
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

        private static void BuildArmour(FitPiece piece, string meshFolder, HeroFit fit)
        {
            string id = piece.Name; var skins = piece.Skins;
            var hero = fit.Body;
            float headScale = hero.HeadWidth / fit.GuardHead;
            var used = new List<HumanBodyBones>();
            int Slot(HumanBodyBones b) { int s = used.IndexOf(b); if (s < 0) { used.Add(b); s = used.Count - 1; } return s; }
            var outVertices = new List<Vector3>(); var outNormals = new List<Vector3>(); var outUv = new List<Vector2>();
            var outWeights = new List<BoneWeight>();
            var groups = new List<(Material material, List<int> triangles)>();

            foreach (var skin in skins)
            {
                var mesh = skin.sharedMesh;
                var gw = ArmourBody(skin, out var joint);
                float bodyScale = hero.Torso / gw.Torso;
                var vertices = mesh.vertices; var normals = mesh.normals; var weights = mesh.boneWeights; var uv = mesh.uv;
                int offset = outVertices.Count;
                for (int v = 0; v < vertices.Length; v++)
                {
                    var w = weights[v];
                    var influences = new[] { (w.boneIndex0, w.weight0), (w.boneIndex1, w.weight1), (w.boneIndex2, w.weight2), (w.boneIndex3, w.weight3) };
                    Vector3 p = Vector3.zero, n = Vector3.zero; float total = 0f;
                    var mapped = new Dictionary<int, float>();
                    foreach (var (bone, weight) in influences)
                    {
                        if (weight <= 0f || !joint.TryGetValue(bone, out var human)) continue;
                        var target = Target(human, fit.Index);
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
                    if (total <= 0f) throw new InvalidOperationException(id + " (" + skin.name + "): a point without weights");
                    outVertices.Add(p / total);
                    outNormals.Add(n.normalized);
                    outUv.Add(uv.Length == vertices.Length ? uv[v] : Vector2.zero);
                    var top = mapped.OrderByDescending(kv => kv.Value).Take(4).ToArray();
                    float norm = top.Sum(kv => kv.Value);
                    var bw = new BoneWeight();
                    if (top.Length > 0) { bw.boneIndex0 = top[0].Key; bw.weight0 = top[0].Value / norm; }
                    if (top.Length > 1) { bw.boneIndex1 = top[1].Key; bw.weight1 = top[1].Value / norm; }
                    if (top.Length > 2) { bw.boneIndex2 = top[2].Key; bw.weight2 = top[2].Value / norm; }
                    if (top.Length > 3) { bw.boneIndex3 = top[3].Key; bw.weight3 = top[3].Value / norm; }
                    outWeights.Add(bw);
                }
                // Submeshes of the same material, from whichever mesh, become one.
                var materials = skin.sharedMaterials;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var material = materials.Length > 0 ? materials[Math.Min(s, materials.Length - 1)] : null;
                    int g = groups.FindIndex(x => x.material == material);
                    if (g < 0) { groups.Add((material, new List<int>())); g = groups.Count - 1; }
                    foreach (int index in mesh.GetTriangles(s)) groups[g].triangles.Add(index + offset);
                }
            }
            var points = outVertices.ToArray();
            if (piece.Head != HeadFit.None) FitOverHead(points, hero, piece.Head == HeadFit.Open);

            string meshPath = meshFolder + id + ".asset";
            var fitted = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            bool fresh = fitted == null;
            if (fresh) fitted = new Mesh();
            fitted.Clear();
            fitted.name = id;
            fitted.indexFormat = points.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            fitted.vertices = points;
            fitted.normals = outNormals.ToArray();
            fitted.uv = outUv.ToArray();
            fitted.subMeshCount = groups.Count;
            for (int g = 0; g < groups.Count; g++) fitted.SetTriangles(groups[g].triangles, g);
            fitted.boneWeights = outWeights.ToArray();
            var heroBind = fit.Skin.sharedMesh.bindposes;
            fitted.bindposes = used.Select(b => heroBind[fit.Index[b]]).ToArray();
            fitted.RecalculateTangents();
            fitted.RecalculateBounds();
            if (fresh) AssetDatabase.CreateAsset(fitted, meshPath); else EditorUtility.SetDirty(fitted);

            var root = new GameObject(id);
            try
            {
                var bones = new GameObject("Bones").transform;
                bones.SetParent(root.transform, false);
                var list = used.Select(b => { var t = new GameObject("Bone_" + b).transform; t.SetParent(bones, false); return t; }).ToArray();
                var part = new GameObject("Skin");
                part.transform.SetParent(root.transform, false);
                var renderer = part.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = fitted;
                renderer.sharedMaterials = groups.Select(x => x.material).ToArray();
                renderer.bones = list;
                renderer.rootBone = list[0];
                PrefabUtility.SaveAsPrefabAsset(root, Arsenal + id + ".prefab", out bool ok);
                if (!ok) throw new InvalidOperationException("Saving failed: " + id);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void BuildStaff(string model, RegionalRelic id) { BuildHandPiece(AschenwachtCourtBuilder.ForecourtModels + model + ".fbx", id); }

        /// <summary>A weapon from a static model becomes an arsenal piece: its file's own scale and its grip at the origin kept.</summary>
        internal static void BuildHandPiece(string path, RegionalRelic id)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null) throw new InvalidOperationException("Weapon model missing: " + path);
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
