using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>Owner report 2026-11-15: houses, villagers and shrubs in the first village floated above the ground.</summary>
    public sealed partial class EntryJourneyRuntimeTests
    {
        private const float GroundContactTolerance = .05f;

        /// <summary>Regression: negative mesh-to-ground gap is never silently accepted as contact.</summary>
        [Test]
        public void BuriedVillageActorMustFailTheVisibleGroundContract()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Ground";
            floor.transform.position = new Vector3(0, -.05f, 0);
            floor.transform.localScale = new Vector3(10f, .1f, 10f);
            var actor = new GameObject("ARE_NPC_1");
            try
            {
                var failures = new List<string>();
                Check(actor, -.25f, new List<Renderer> { floor.GetComponent<Renderer>() }, failures, true);
                Assert.AreEqual(1, failures.Count, "A quarter-metre-deep body must not appear grounded");
                StringAssert.Contains("sinks", failures[0]);
            }
            finally
            {
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(floor);
            }
        }

        /// <summary>Missing licensed skinned art must still expose the actual visible fallback geometry.</summary>
        [Test]
        public void VisibleFallbackMeshHasMeasurableGroundContact()
        {
            var actor = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            actor.name = "ARE_NPC_2";
            actor.transform.position = Vector3.up;
            try
            {
                float bottom = SkinBottom(actor);
                Assert.AreEqual(0f, bottom, .02f, "Visible capsule fallback feet must meet y=0");
            }
            finally { Object.DestroyImmediate(actor); }
        }

        /// <summary>Existing imported NPC rigs need a real locomotion controller, not a bare animator.</summary>
        [UnityTest]
        public IEnumerator PrivateVillageNpcRigsHavePlayableLocomotionControllers()
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR")))
                Assert.Ignore("Requires private actor and village overlay");
            _runtime.enabled = false;
            ArrangeCryptKill();
            CollectCryptLoot();
            MoveStory(0, 24000); TravelStory();
            Assert.IsTrue(Game.IsVillage);
            yield return null;
            foreach (int id in new[] { 0, 1, 2 })
            {
                var actor = Find("ARE_NPC_" + id);
                Assert.IsNotNull(actor, "Missing actual NPC projection " + id);
                var animators = actor.GetComponentsInChildren<Animator>(true);
                Assert.IsNotEmpty(animators, "NPC " + id + " has no skinned motion rig");
                foreach (var animator in animators)
                {
                    Assert.IsNotNull(animator.runtimeAnimatorController,
                        "NPC " + id + " is missing its idle/movement controller");
                    bool canWalk = false;
                    foreach (var parameter in animator.parameters)
                        canWalk |= parameter.name == "MoveMagnitude" &&
                            parameter.type == AnimatorControllerParameterType.Float;
                    Assert.IsTrue(canWalk, "NPC " + id + " controller must accept canonical movement projection");
                    Assert.IsFalse(animator.applyRootMotion,
                        "NPC " + id + " animation must never override Core coordinates");
                }
            }
        }

        /// <summary>Keep private visual acceptance strict while naming the exact character with missing art.</summary>
        [UnityTest]
        public IEnumerator PrivateVillageActorsAllContainSkinnedBodies()
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR")))
                Assert.Ignore("Requires real private village and character overlay");
            _runtime.enabled = false;
            ArrangeCryptKill();
            CollectCryptLoot();
            MoveStory(0, 24000); TravelStory();
            Assert.IsTrue(Game.IsVillage);
            yield return null;
            foreach (string name in new[] { "Wanderer", "ARE_NPC_0", "ARE_NPC_1", "ARE_NPC_2" })
            {
                var actor = Find(name);
                Assert.IsNotNull(actor, "Core projection for " + name + " is absent");
                var skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>();
                Debug.Log("NPC_PRIVATE_SKINS actor=" + name + " count=" + skins.Length);
                Assert.IsNotEmpty(skins, name + " still uses a primitive, violating private actor art acceptance");
                foreach (var skin in skins)
                    Assert.IsNotNull(skin.sharedMesh, name + " contains an unresolved skinned mesh");
            }
        }

        /// <summary>Real private village characters must have visible feet on the surface, even with rejected vendor meshes.</summary>
        [UnityTest]
        public IEnumerator PrivateVillageNpcFallbackFeetTouchTheGround()
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR")))
                Assert.Ignore("Needs the real private village and NPC prefabs");
            _runtime.enabled = false;
            ArrangeCryptKill();
            CollectCryptLoot();
            MoveStory(0, 24000);
            TravelStory();
            Assert.IsTrue(Game.IsVillage, "Test must traverse the canonical crypt checkpoint");
            yield return null;
            Assert.IsNotNull(Resources.Load<GameObject>("ARE/Visuals/Npc2"), "Private fallback model must be present");
            foreach (int id in new[] { 0, 1, 2 })
            {
                var actor = Find("ARE_NPC_" + id);
                Assert.IsNotNull(actor, "Core NPC " + id + " must have a Unity projection");
                float footY = SkinBottom(actor);
                Debug.Log("NPC_PRIVATE_FEET id=" + id + " bottom=" + footY.ToString("F4",
                    System.Globalization.CultureInfo.InvariantCulture));
                Assert.That(footY, Is.InRange(-GroundContactTolerance, GroundContactTolerance),
                    "Private actor " + id + " must touch the actual village ground at y=0");
            }
        }

        /// <summary>Every standing village piece and villager touches the visible ground directly beneath it.</summary>
        [UnityTest]
        public IEnumerator VillageArtAndVillagersStandOnTheVisibleGround()
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR")))
                Assert.Ignore("Requires the reviewed private village and character overlay");
            _runtime.enabled = false;
            ArrangeCryptKill();
            CollectCryptLoot();
            MoveStory(0, 24000); TravelStory();
            Assert.IsTrue(Game.IsVillage, "The report concerns the first village after the crypt");
            yield return null;

            var grounds = new List<Renderer>();
            foreach (var root in _scene.GetRootGameObjects())
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    Bounds b = renderer.bounds;
                    if (renderer.enabled && b.size.y < .6f && b.size.x > 3f && b.size.z > 3f)
                    {
                        grounds.Add(renderer);
                        Debug.Log("VILLAGE_GROUND " + Path(renderer.transform) + " top=" + b.max.y.ToString("0.000") + " size=" + b.size);
                    }
                }
            Assert.IsNotEmpty(grounds, "The village needs a visible ground surface");

            var floating = new List<string>();
            var art = Find("EntryVillageWorld(Clone)") ?? Find(AreArpg.UnityClient.AschengrenzeArt.BackdropLabel);
            Assert.IsNotNull(art, "Private village art must be bound");
            foreach (Transform piece in StandingPieces(art.transform))
                Check(piece.gameObject, PieceBottom(piece.gameObject), grounds, floating);
            foreach (string actor in new[] { "Wanderer", "ARE_NPC_0", "ARE_NPC_1", "ARE_NPC_2" })
            {
                var figure = Find(actor);
                try { Check(figure, SkinBottom(figure), grounds, floating, true); }
                catch (AssertionException failure) { floating.Add(actor + ": " + failure.Message); }
            }
            yield return Capture("village-grounding.png");
            Assert.IsEmpty(floating, "Floating village pieces:\n" + string.Join("\n", floating));
            float ground = float.MinValue;
            foreach (var g in grounds) if (g.name == "Ground") ground = Mathf.Max(ground, g.bounds.max.y);
            int roads = 0;
            foreach (Transform piece in art.GetComponentsInChildren<Transform>())
            {
                if (piece.name != "Old village road") continue;
                roads++;
                float surface = PavingSurfaceHeight(piece);
                Debug.Log("VILLAGE_ROAD " + Path(piece) + " surface=" + surface.ToString("0.000") + " ground=" + ground.ToString("0.000"));
                Assert.That(surface - ground, Is.InRange(0f, .08f), "The laid road must stay visible just above the ground, not buried or raised as a slab");
            }
            Assert.Greater(roads, 0, "The old village road must be present");
        }

        /// <summary>Composed pieces below the village art root: wrapper levels are skipped, ground-like surfaces excluded.</summary>
        private static IEnumerable<Transform> StandingPieces(Transform art)
        {
            Transform node = art;
            while (node.GetComponent<Renderer>() == null && node.childCount == 1) node = node.GetChild(0);
            foreach (Transform child in node)
            {
                var renderers = child.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                Bounds b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                if (b.size.y < .6f && b.size.x > 3f && b.size.z > 3f) continue;
                yield return child;
            }
        }

        private void Check(GameObject piece, float bottom, List<Renderer> grounds, List<string> floating, bool requireActorContact = false)
        {
            Vector3 p = piece.transform.position;
            float ground = float.MinValue;
            string surface = "none";
            foreach (var g in grounds)
            {
                Bounds b = g.bounds;
                if (p.x < b.min.x || p.x > b.max.x || p.z < b.min.z || p.z > b.max.z) continue;
                if (b.max.y <= bottom + .6f && b.max.y > ground) { ground = b.max.y; surface = g.name; }
            }
            if (requireActorContact && ground == float.MinValue)
                floating.Add(Path(piece.transform) + " has no visible ground underneath");
            float gap = bottom - ground;
            Debug.Log("VILLAGE_CONTACT " + Path(piece.transform) + " bottom=" + bottom.ToString("0.000") + " ground=" + ground.ToString("0.000") + " surface=" + surface + " gap=" + gap.ToString("0.000"));
            if (requireActorContact && ground > float.MinValue && gap < -GroundContactTolerance)
                floating.Add(Path(piece.transform) + " sinks " + (-gap).ToString("0.00") + " m below " + surface);
            if (ground > float.MinValue && gap > GroundContactTolerance) floating.Add(Path(piece.transform) + " floats " + gap.ToString("0.00") + " m above " + surface);
        }

        private static float PieceBottom(GameObject piece)
        {
            float bottom = float.MaxValue;
            foreach (var r in piece.GetComponentsInChildren<Renderer>()) bottom = Mathf.Min(bottom, r.bounds.min.y);
            return bottom;
        }

        /// <summary>Lowest vertex of the posed visible skin; renderer bounds of skinned meshes are conservative.</summary>
        private static float SkinBottom(GameObject actor)
        {
            Assert.IsNotNull(actor, "Villager projection is missing");
            var mesh = new Mesh();
            float bottom = float.MaxValue;
            try
            {
                foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!skin.enabled || skin.sharedMesh == null) continue;
                    skin.BakeMesh(mesh, true);
                    var toWorld = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                    foreach (var vertex in mesh.vertices) bottom = Mathf.Min(bottom, toWorld.MultiplyPoint3x4(vertex).y);
                }
            }
            finally { Object.Destroy(mesh); }
            if (bottom == float.MaxValue)
            {
                // A broken licensed skinned import may leave the existing visible primitive fallback.
                foreach (var filter in actor.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer == null || !renderer.enabled || filter.sharedMesh == null) continue;
                    foreach (var vertex in filter.sharedMesh.vertices)
                        bottom = Mathf.Min(bottom, filter.transform.localToWorldMatrix.MultiplyPoint3x4(vertex).y);
                }
            }
            Assert.Less(bottom, float.MaxValue, actor.name + " has no visible skinned or mesh fallback body");
            return bottom;
        }

        /// <summary>Median height of the upward-facing faces of the visible highest-detail paving meshes.</summary>
        private static float PavingSurfaceHeight(Transform strip)
        {
            var heights = new List<float>();
            foreach (var filter in strip.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<Renderer>();
                if (renderer == null || !renderer.enabled || filter.sharedMesh == null || filter.name.EndsWith("_LOD01")) continue;
                Vector3[] vertices = filter.sharedMesh.vertices, normals = filter.sharedMesh.normals;
                if (normals.Length != vertices.Length) continue;
                var toWorld = filter.transform.localToWorldMatrix;
                for (int i = 0; i < vertices.Length; i++)
                    if (toWorld.MultiplyVector(normals[i]).normalized.y > .7f) heights.Add(toWorld.MultiplyPoint3x4(vertices[i]).y);
            }
            Assert.IsNotEmpty(heights, strip.name + " has no readable upward paving surface");
            heights.Sort();
            return heights[heights.Count / 2];
        }

        private static string Path(Transform t) { return t.parent == null ? t.name : Path(t.parent) + "/" + t.name; }
    }
}
