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
            /// <summary>The hero's surface in bind space without his hair: clothes are laid over it.</summary>
            public Vector3[] Surface;
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
        public enum HeadFit { None, Closed, Open, WedgeHelm, BrimmedHat, ConventHood }

        /// <summary>One piece to fit: the name of its mesh and prefab, its meshes, and how it sits on the head.</summary>
        internal sealed class FitPiece
        {
            public readonly string Name;
            public readonly SkinnedMeshRenderer[] Skins;
            public readonly HeadFit Head;
            /// <summary>Soft clothes (leather, linen) are laid over the hero's own armour; plate keeps its fitted shape.</summary>
            public readonly bool LayOver;
            public FitPiece(string name, SkinnedMeshRenderer[] skins, HeadFit head, bool layOver = false) { Name = name; Skins = skins; Head = head; LayOver = layOver; }
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
                fit.Surface = Surface(fit.Skin);
                foreach (var piece in pieces) BuildArmour(piece, meshFolder, fit);
                BuildHairlessHero(fit.Skin);
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
        /// point counts as inside when a ray from the head's centre through it meets the piece's surface beyond it; a ray
        /// that meets no surface passes an opening (a visor slit, the face of a hood) and does not count. A smooth dome has
        /// few corners and large faces, so the faces themselves are measured, not their corners.
        /// </summary>
        private const float HelmRoom = .98f, HelmShiftStepMm = 10f;
        private const int HelmShiftSteps = 2, HelmProbePoints = 1200;
        /// <summary>A hood open at the face holds only the head behind the face: points this far forward are left out.</summary>
        private const float OpenFaceCone = .4f;
        private static void FitOverHead(Vector3[] points, int[] triangles, Body hero, bool openFace)
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

            var covered = openFace
                ? hero.HeadPoints.Where(point => Vector3.Dot((point - head.center).normalized, hero.Front) <= OpenFaceCone).ToArray()
                : hero.HeadPoints;
            var probe = new List<Vector3>();
            for (int i = 0; i < covered.Length; i += Math.Max(1, covered.Length / HelmProbePoints)) probe.Add(covered[i]);

            // Intersect the original mesh with the head point translated back by the candidate offset.
            float Worst(Vector3 shift, IEnumerable<Vector3> headPoints)
            {
                var origin = head.center;
                float worst = 0f;
                foreach (var p in headPoints)
                {
                    var d = p - shift - origin; float length = d.magnitude;
                    if (length < 1e-4f) continue;
                    float surface = NearestHit(origin, d / length, points, triangles);
                    if (surface < float.PositiveInfinity) worst = Mathf.Max(worst, length / surface);
                }
                return worst;
            }
            Vector3 bestShift = Vector3.zero; float best = Worst(Vector3.zero, probe);
            for (int up = -HelmShiftSteps; up <= HelmShiftSteps; up++)
                for (int forward = -HelmShiftSteps; forward <= HelmShiftSteps; forward++)
                {
                    var shift = (hero.Up * up + hero.Front * forward) * (HelmShiftStepMm * .001f);
                    float worst = Worst(shift, probe);
                    if (worst < best - 1e-5f) { best = worst; bestShift = shift; }
                }
            float needed = Worst(bestShift, covered) / HelmRoom;
            var centre = head.center + bestShift;
            for (int i = 0; i < points.Length; i++)
            {
                var moved = points[i] + bestShift;
                points[i] = needed > 1f ? centre + (moved - centre) * needed : moved;
            }
        }

        // The three-order archive has a common rest head. Both open coverings include cloth below the
        // face; the wide hat also has a brim across the shell. An intersection with an inside fold
        // cannot measure the head radius. Register the measured rest-space garment envelope to the
        // hero's actual hairless head along his anatomical axes. Keep the original brim and hood ratios.
        private static void FitOrderHead(Vector3[] points, List<Vector3> normals, Body hero, HeadFit kind)
        {
            var side = Vector3.Cross(hero.Up, hero.Front).normalized;
            Vector3 Anatomy(Vector3 p) => new Vector3(Vector3.Dot(p, side), Vector3.Dot(p, hero.Up), Vector3.Dot(p, hero.Front));
            Bounds Envelope(Vector3[] vertices)
            {
                var box = new Bounds(Anatomy(vertices[0]), Vector3.zero);
                foreach (var p in vertices) box.Encapsulate(Anatomy(p));
                return box;
            }
            var garment = Envelope(points);
            var face = Envelope(hero.HeadPoints);
            // The archives contain no nude donor head. This explicit fitting proxy in metres uses the
            // 22.2 cm headwrap width and 23.6 cm depth, with a design chin-to-scalp height of 30 cm.
            // The three full mesh envelopes were measured from the supplied Y-up GLB POSITION data.
            var referenceHead = new Bounds(new Vector3(0f, 1.70f, 0f), new Vector3(.222f, .30f, .236f));
            var source = kind == HeadFit.WedgeHelm
                ? new Bounds(new Vector3(0f, 1.7300f, .0296f), new Vector3(.2780f, .4303f, .3024f))
                : kind == HeadFit.BrimmedHat
                ? new Bounds(new Vector3(-.0016f, 1.7302f, .0031f), new Vector3(.4501f, .3604f, .4421f))
                : new Bounds(new Vector3(.0003f, 1.7132f, -.0236f), new Vector3(.2987f, .3378f, .2688f));
            Vector3 Ratio(Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
            var bodyScale = Ratio(face.size, referenceHead.size);
            var centre = face.center + Vector3.Scale(source.center - referenceHead.center, bodyScale);
            var scale = Ratio(Vector3.Scale(source.size, bodyScale), garment.size);
            for (int i = 0; i < points.Length; i++)
            {
                var p = centre + Vector3.Scale(Anatomy(points[i]) - garment.center, scale);
                points[i] = side * p.x + hero.Up * p.y + hero.Front * p.z;
                var n = Ratio(Anatomy(normals[i]), scale);
                normals[i] = (side * n.x + hero.Up * n.y + hero.Front * n.z).normalized;
            }
        }

        /// <summary>The hero's points in bind space, without those of his hair (a worn head piece hides it).</summary>
        private static Vector3[] Surface(SkinnedMeshRenderer skin)
        {
            var bones = skin.bones;
            var hair = new bool[bones.Length];
            for (int b = 0; b < bones.Length; b++)
                for (var t = bones[b]; t; t = t.parent) if (t.name.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0) { hair[b] = true; break; }
            var vertices = skin.sharedMesh.vertices; var weights = skin.sharedMesh.boneWeights;
            var surface = new List<Vector3>(vertices.Length);
            for (int v = 0; v < vertices.Length; v++) if (!hair[weights[v].boneIndex0]) surface.Add(vertices[v]);
            return surface.ToArray();
        }

        /// <summary>
        /// Soft clothes lie over the hero's own clothes. The archives' sets were cut for the slim Glockenwacht; the hero
        /// wears armour of his own (his shoulder plates stand far out), and thin leather or linen fitted to his frame would
        /// vanish under it. Every point of a piece is moved straight away from its bone until the hero's surface in that
        /// direction lies <see cref="GarmentGapMm"/> behind it, by at most <see cref="GarmentReachMm"/>; the moves are
        /// smoothed over neighbouring points so the cloth stays whole. Points already outside stay where they are. The
        /// direction comes from the bone, not from each face, so points that share a place move together and nothing tears.
        /// </summary>
        private const float GarmentGapMm = 6f, GarmentReachMm = 70f, GarmentSideMm = 25f, GarmentCellMm = 30f;
        private const int GarmentSmoothing = 4;
        private static void LayOverHero(Vector3[] points, HumanBodyBones[] bone, int[] triangles, HeroFit fit)
        {
            float cell = GarmentCellMm * .001f, reach = GarmentReachMm * .001f, side = GarmentSideMm * .001f, gap = GarmentGapMm * .001f;
            (int, int, int) Key(Vector3 v) => (Mathf.FloorToInt(v.x / cell), Mathf.FloorToInt(v.y / cell), Mathf.FloorToInt(v.z / cell));
            var grid = new Dictionary<(int, int, int), List<Vector3>>();
            foreach (var h in fit.Surface)
            {
                var k = Key(h);
                if (!grid.TryGetValue(k, out var list)) grid[k] = list = new List<Vector3>();
                list.Add(h);
            }
            int span = Mathf.CeilToInt(reach / cell);
            var push = new float[points.Length];
            var outward = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var b = fit.Body; var pivot = b.Pivot.ContainsKey(bone[i]) ? b.Pivot[bone[i]] : b.Pivot[HumanBodyBones.Hips];
                var radial = Vector3.ProjectOnPlane(points[i] - pivot, b.Axis(bone[i]));
                if (radial.sqrMagnitude < 1e-8f) continue;
                var n = radial.normalized;
                outward[i] = n;
                float depth = float.NegativeInfinity;
                var c = Key(points[i]);
                for (int x = -span; x <= span; x++)
                    for (int y = -span; y <= span; y++)
                        for (int z = -span; z <= span; z++)
                        {
                            if (!grid.TryGetValue((c.Item1 + x, c.Item2 + y, c.Item3 + z), out var list)) continue;
                            foreach (var h in list)
                            {
                                var d = h - points[i]; float t = Vector3.Dot(d, n);
                                if (t < -gap || t > reach || d.sqrMagnitude - t * t > side * side) continue;
                                if (t > depth) depth = t;
                            }
                        }
                if (depth > -gap) push[i] = Mathf.Min(reach, depth + gap);
            }
            // Neighbours by the triangles; a smoothed move never falls below the move a point needs itself.
            var near = new List<int>[points.Length];
            for (int i = 0; i < near.Length; i++) near[i] = new List<int>(6);
            for (int t = 0; t + 2 < triangles.Length; t += 3)
                for (int a = 0; a < 3; a++) { int u = triangles[t + a], v = triangles[t + (a + 1) % 3]; near[u].Add(v); near[v].Add(u); }
            var need = (float[])push.Clone();
            for (int round = 0; round < GarmentSmoothing; round++)
            {
                var next = new float[push.Length];
                for (int i = 0; i < push.Length; i++)
                {
                    float sum = push[i]; int count = 1;
                    foreach (int j in near[i]) { sum += push[j]; count++; }
                    next[i] = Mathf.Max(need[i], sum / count);
                }
                push = next;
            }
            for (int i = 0; i < points.Length; i++) points[i] += outward[i] * push[i];
        }

        /// <summary>Where the hero's figure without hair lies, for the runtime to swap in under a head piece (private art).</summary>
        public const string HeroHairFolder = "Assets/Resources/ARE/Visuals/HeroHair/";

        /// <summary>
        /// Owner 2026-11-26: under a helm the hero's hair disappears like a model taken off. His figure is one mesh, so
        /// two copies of it are saved without hair triangles: "Hairless" for helms (every triangle touching his hair
        /// bones gone) and "Hooded" for open hoods (only the back hair gone; the fringe frames the face). The runtime swaps
        /// the figure's mesh while such a piece is worn.
        /// </summary>
        private static void BuildHairlessHero(SkinnedMeshRenderer skin)
        {
            Directory.CreateDirectory(HeroHairFolder);
            SaveWithoutHair(skin, "Hairless", new[] { "FrontHair", "BackHair" });
            SaveWithoutHair(skin, "Hooded", new[] { "BackHair" });
        }

        private static void SaveWithoutHair(SkinnedMeshRenderer skin, string name, string[] roots)
        {
            var bones = skin.bones;
            var hair = new bool[bones.Length];
            for (int b = 0; b < bones.Length; b++)
                for (var t = bones[b]; t; t = t.parent) if (Array.IndexOf(roots, t.name) >= 0) { hair[b] = true; break; }
            var source = skin.sharedMesh; var weights = source.boneWeights;
            bool Hair(int v)
            {
                var w = weights[v];
                return (w.weight0 > 0f && hair[w.boneIndex0]) || (w.weight1 > 0f && hair[w.boneIndex1]) ||
                    (w.weight2 > 0f && hair[w.boneIndex2]) || (w.weight3 > 0f && hair[w.boneIndex3]);
            }
            var mesh = UnityEngine.Object.Instantiate(source);
            mesh.name = "Hero_" + name;
            for (int s = 0; s < source.subMeshCount; s++)
            {
                var triangles = source.GetTriangles(s);
                var kept = new List<int>(triangles.Length);
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                    if (!Hair(triangles[t]) && !Hair(triangles[t + 1]) && !Hair(triangles[t + 2])) kept.AddRange(new[] { triangles[t], triangles[t + 1], triangles[t + 2] });
                mesh.SetTriangles(kept, s, false);
            }
            string path = HeroHairFolder + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
        }

        /// <summary>The distance along a ray to the nearest face of a mesh (Möller–Trumbore), or infinity when it meets none.</summary>
        private static float NearestHit(Vector3 origin, Vector3 direction, Vector3[] points, int[] triangles)
        {
            float nearest = float.PositiveInfinity;
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                Vector3 a = points[triangles[t]], e1 = points[triangles[t + 1]] - a, e2 = points[triangles[t + 2]] - a;
                var h = Vector3.Cross(direction, e2); float det = Vector3.Dot(e1, h);
                if (det > -1e-9f && det < 1e-9f) continue;
                float inv = 1f / det; var s = origin - a; float u = Vector3.Dot(s, h) * inv;
                if (u < 0f || u > 1f) continue;
                var q = Vector3.Cross(s, e1); float v = Vector3.Dot(direction, q) * inv;
                if (v < 0f || u + v > 1f) continue;
                float distance = Vector3.Dot(e2, q) * inv;
                if (distance > 1e-4f && distance < nearest) nearest = distance;
            }
            return nearest;
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
            if (piece.Head == HeadFit.WedgeHelm || piece.Head == HeadFit.BrimmedHat || piece.Head == HeadFit.ConventHood)
                FitOrderHead(points, outNormals, hero, piece.Head);
            else if (piece.Head != HeadFit.None) FitOverHead(points, groups.SelectMany(g => g.triangles).ToArray(), hero, piece.Head == HeadFit.Open);
            else if (piece.LayOver) LayOverHero(points, outWeights.Select(w => used[w.boneIndex0]).ToArray(), groups.SelectMany(g => g.triangles).ToArray(), fit);

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
