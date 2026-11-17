using System.Collections;
using System.Reflection;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        /// <summary>Review of the villager ground fix: the road witness host follows the Core at y = 1.</summary>
        [UnityTest]
        public IEnumerator RoadWitnessStaysOnTheGroundWhileFollowingTheCorePosition()
        {
            if (Resources.Load<GameObject>("ARE/Visuals/EntryNpc0") == null)
                Assert.Ignore("Requires the reviewed private villager prefab");
            var create = typeof(ArenaRuntime).GetMethod("CreateRoadWitness", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(create, "The witness is created through one canonical method");
            var at = new Vector3(4f, 1f, -7f);
            var host = (Transform)create.Invoke(null, new object[] { at });
            try
            {
                host.position = at; // what UpdateRoadPresentation does every frame
                for (int frame = 0; frame < 4; frame++) yield return null;
                var mesh = new Mesh();
                float bottom = float.MaxValue;
                foreach (var skin in host.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!skin.enabled || skin.sharedMesh == null) continue;
                    skin.BakeMesh(mesh, true);
                    var toWorld = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                    foreach (var vertex in mesh.vertices) bottom = Mathf.Min(bottom, toWorld.MultiplyPoint3x4(vertex).y);
                }
                Object.Destroy(mesh);
                Assert.Less(bottom, float.MaxValue, "The witness must show its skinned body");
                Assert.That(bottom, Is.InRange(-.05f, .05f), "The witness stands on the ground, neither buried nor floating");
            }
            finally { Object.Destroy(host.gameObject); }
        }
    }
}
