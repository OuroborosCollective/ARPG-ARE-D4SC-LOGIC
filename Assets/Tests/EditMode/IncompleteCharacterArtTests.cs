using System.Text.RegularExpressions;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.EditMode.Tests
{
    /// <summary>
    /// Full run 2026-11-18: Dorn's private prefab references four body meshes that the imported vendor model does not
    /// contain, so he would be drawn with holes. A character with an unresolved skinned mesh is never shown; the binder
    /// rejects it, the complete fallback model is used and the warning names the prefab.
    /// </summary>
    public sealed class IncompleteCharacterArtTests
    {
        private static Mesh Triangle()
        {
            var mesh = new Mesh { name = "TestTriangle" };
            mesh.vertices = new[] { Vector3.zero, Vector3.up, Vector3.right };
            mesh.triangles = new[] { 0, 1, 2 };
            return mesh;
        }

        private static GameObject Character(Mesh armour, Mesh body)
        {
            var root = new GameObject("TestCharacter");
            foreach (var part in new[] { ("Armour", armour), ("Body", body) })
            {
                var child = new GameObject(part.Item1);
                child.transform.SetParent(root.transform, false);
                child.AddComponent<SkinnedMeshRenderer>().sharedMesh = part.Item2;
            }
            return root;
        }

        [Test]
        public void CharacterWithAnUnresolvedBodyMeshIsRejected()
        {
            var armour = Triangle();
            var prefab = Character(armour, null);
            var placeholder = new GameObject("Placeholder");
            try
            {
                LogAssert.Expect(LogType.Warning, new Regex("rejected .*TestCharacter"));
                Assert.IsFalse(PassiveArtVisuals.TryBindPrefab(placeholder.transform, prefab));
                Assert.AreEqual(0, placeholder.transform.childCount, "Nothing of the incomplete character is shown");
            }
            finally { Object.DestroyImmediate(placeholder); Object.DestroyImmediate(prefab); Object.DestroyImmediate(armour); }
        }

        [Test]
        public void CompleteCharacterIsBound()
        {
            Mesh armour = Triangle(), body = Triangle();
            var prefab = Character(armour, body);
            var placeholder = new GameObject("Placeholder");
            try
            {
                Assert.IsTrue(PassiveArtVisuals.TryBindPrefab(placeholder.transform, prefab));
                Assert.AreEqual(1, placeholder.transform.childCount);
            }
            finally
            {
                Object.DestroyImmediate(placeholder); Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(armour); Object.DestroyImmediate(body);
            }
        }
    }
}
