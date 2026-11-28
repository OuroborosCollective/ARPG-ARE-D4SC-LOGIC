using System;
using System.Linq;
using AreArpg.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    /// <summary>
    /// The armour fitted to the hero (owner decisions 2026-11-26: Aschenwacht's Glockenwacht plate and the Nachtvesper set):
    /// every piece is one skinned renderer whose bones name the hero's humanoid bones, with one bind pose per bone, the sets'
    /// URP materials, no collider and no script, inside the hero's height, and the Nachtvesper within its tablet budget.
    /// The prefabs are private art: without them the test is ignored.
    /// </summary>
    public sealed class FittedArmourArtTests
    {
        private const string Arsenal = "Assets/Resources/ARE/Visuals/Werkarsenal/";
        private const int NachtvesperTriangleBudget = 30000;

        private static readonly RegionalRelic[] Aschenwacht =
            { RegionalRelic.A_Visierhelm, RegionalRelic.A_Kuerass, RegionalRelic.A_Schulterplatten, RegionalRelic.A_Gliedpanzer };

        [Test]
        public void AschenwachtArmourIsSkinnedToTheHeroesBones() => CheckSet(Names(Aschenwacht), int.MaxValue);

        [Test]
        public void TheNachtvesperIsSkinnedToTheHeroesBonesWithinTheTabletBudget() => CheckSet(Names(NachtvesperSet.Pieces), NachtvesperTriangleBudget);

        /// <summary>Owner request 2026-11-26: the hunter's and the sorcerer's sets, the hunter's quiver with them.</summary>
        [Test]
        public void TheHunterAndCantorSetsAreSkinnedToTheHeroesBonesWithinTheTabletBudget()
        {
            CheckSet(Names(Worn(RotfaehrteSet.Pieces)).Concat(new[] { "F_Koecher" }).ToArray(), NachtvesperTriangleBudget);
            CheckSet(Names(Worn(RusskantorSet.Pieces)), NachtvesperTriangleBudget);
        }

        [Test]
        public void ThreeOrderBuilderReportsMissingGuardModelBeforeFitting()
        {
            const string model = "Assets/PrivateArt/Aschenwacht/Game/Glockenwacht.fbx";
            if (AssetImporter.GetAtPath(model) is ModelImporter)
                Assert.Ignore("Requires a project without the Glockenwacht source model");

            var builder = Type.GetType("AreArpg.EditorTools.ThreeOrderGearBuilder, Assembly-CSharp-Editor", true);
            var error = Assert.Throws<System.Reflection.TargetInvocationException>(() => builder.GetMethod("Build").Invoke(null, null));
            Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(error.InnerException.Message, Does.Contain(model));
        }

        [Test]
        public void ThreeOrderPiecesFitThePrivateHero()
        {
            if (ThreeOrderSet.All.SelectMany(set => set.Pieces)
                .All(id => AssetDatabase.LoadAssetAtPath<GameObject>(Arsenal + id + ".prefab") == null))
                Assert.Ignore("Requires the private Three Orders armour overlay");
            foreach (var set in ThreeOrderSet.All)
            {
                foreach (var id in set.Pieces)
                    Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(Arsenal + id + ".prefab"), id.ToString());
                CheckSet(set.Pieces.Select(id => id.ToString()).ToArray(), 30000);
                CheckOrderHead(set);
            }
        }

        private static void CheckOrderHead(ThreeOrderSet set)
        {
            var hero = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ARE/Visuals/EntryHero.prefab");
            var animator = hero.GetComponentsInChildren<Animator>(true).First(a => a.isHuman);
            var figure = hero.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(s => s.sharedMesh.vertexCount).First();
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            bool CoversFace(Transform t)
            {
                for (; t && t != head; t = t.parent)
                    if (t.name.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                return t == head;
            }
            var faces = figure.bones.Select(CoversFace).ToArray();
            var weights = figure.sharedMesh.boneWeights;
            var vertices = figure.sharedMesh.vertices.Where((v, i) => faces[weights[i].boneIndex0]).ToArray();
            Assert.IsNotEmpty(vertices);
            var face = new Bounds(vertices[0], Vector3.zero);
            foreach (var v in vertices) face.Encapsulate(v);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Arsenal + set.Pieces[0] + ".prefab");
            var mesh = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMesh;
            var garment = mesh.bounds;
            Vector3 delta = garment.center - face.center;
            Assert.Less(Mathf.Abs(delta.x), face.size.x * .23f, set.Name + " is centred left/right");
            Assert.Less(Mathf.Abs(delta.y), face.size.y * .26f, set.Name + " sits on the head");
            Assert.Less(Mathf.Abs(delta.z), face.size.z * .34f, set.Name + " follows the face plane");
            float width = garment.size.x / face.size.x, height = garment.size.y / face.size.y, depth = garment.size.z / face.size.z;
            if (set == ThreeOrderSet.Moorvogt)
            {
                Assert.That(width, Is.InRange(1.75f, 2.30f), "wide hat brim");
                Assert.That(depth, Is.InRange(1.50f, 2.16f), "hat front and back brim");
            }
            else
            {
                Assert.That(width, Is.InRange(1.09f, 1.65f), set.Name + " width");
                Assert.That(depth, Is.InRange(.98f, 1.62f), set.Name + " depth");
            }
            Assert.That(height, Is.InRange(.95f, 1.68f), set.Name + " head height");
            Debug.Log("THREE_ORDER_HEAD " + set.Name + " face=" + face + " garment=" + garment);
        }

        [Test]
        public void TheBowAndTheStaffsArePassiveArsenalPieces()
        {
            foreach (var id in new[] { RegionalRelic.F_Langbogen, RegionalRelic.K_Kurzstab, RegionalRelic.K_Langstab })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Arsenal + id + ".prefab");
                if (prefab == null) Assert.Ignore("Requires the private weapon pictures");
                Assert.IsNotEmpty(prefab.GetComponentsInChildren<MeshRenderer>(true), id + " has its picture");
                Assert.IsEmpty(prefab.GetComponentsInChildren<Collider>(true), id + " has no collider");
                Assert.IsEmpty(prefab.GetComponentsInChildren<MonoBehaviour>(true), id + " carries no script");
                foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
                    foreach (var material in renderer.sharedMaterials)
                        Assert.IsTrue(material != null && material.shader.name == "Universal Render Pipeline/Lit", id + " wears URP/Lit materials");
            }
        }

        private static RegionalRelic[] Worn(RegionalRelic[] set) => set.Where(id => RegionalRelics.HandDefinition(id) == null).ToArray();
        private static string[] Names(RegionalRelic[] set) => set.Select(id => id.ToString()).ToArray();

        private static void CheckSet(string[] pieces, int budget)
        {
            var prefabs = pieces.Select(id => AssetDatabase.LoadAssetAtPath<GameObject>(Arsenal + id + ".prefab")).ToArray();
            if (prefabs.Any(p => p == null)) Assert.Ignore("Requires the private armour pictures");
            long triangles = 0;
            for (int i = 0; i < pieces.Length; i++)
            {
                var prefab = prefabs[i];
                var skins = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Assert.AreEqual(1, skins.Length, pieces[i] + " is one skinned renderer");
                var skin = skins[0];
                Assert.IsNotNull(skin.sharedMesh, pieces[i] + " has its fitted mesh");
                Assert.AreEqual(skin.bones.Length, skin.sharedMesh.bindposes.Length, pieces[i] + ": one bind pose per bone");
                foreach (var bone in skin.bones)
                    Assert.IsTrue(bone != null && bone.name.StartsWith("Bone_", StringComparison.Ordinal) &&
                        Enum.TryParse(bone.name.Substring(5), out HumanBodyBones _), pieces[i] + ": bone " + (bone ? bone.name : "missing") + " names a humanoid bone");
                Assert.AreEqual(skin.sharedMesh.subMeshCount, skin.sharedMaterials.Length, pieces[i] + ": one material per submesh");
                foreach (var material in skin.sharedMaterials)
                    Assert.IsTrue(material != null && material.shader.name == "Universal Render Pipeline/Lit", pieces[i] + " wears URP/Lit materials");
                Assert.IsEmpty(prefab.GetComponentsInChildren<Collider>(true), pieces[i] + " is a picture without collider");
                Assert.IsEmpty(prefab.GetComponentsInChildren<MonoBehaviour>(true), pieces[i] + " carries no script");
                var bounds = skin.sharedMesh.bounds;
                Assert.IsTrue(bounds.min.y > -.1f && bounds.max.y < 2.3f && bounds.size.x < 1.6f, pieces[i] + " lies on the hero's body: " + bounds);
                triangles += skin.sharedMesh.triangles.Length / 3;
            }
            Assert.LessOrEqual(triangles, budget, "the set stays within the tablet budget");
        }
    }
}
