using System.Reflection;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace AreArpg.EditMode.Tests
{
    public sealed class WorldHorizonTests
    {
        [TestCase("old_crypt")]
        [TestCase("forecourt")]
        [TestCase("kiln")]
        [TestCase("bellcourt")]
        [TestCase("village")]
        [TestCase("ash_road")]
        [TestCase("mill")]
        [TestCase("granary")]
        [TestCase("hidden_crypt")]
        [TestCase("nest")]
        [TestCase("west_forest")]
        [TestCase("bear")]
        [TestCase("freight")]
        [TestCase("forest_road")]
        [TestCase("side_1")]
        [TestCase("side_2")]
        [TestCase("side_3")]
        [TestCase("side_4")]
        [TestCase("under_1")]
        [TestCase("under_2")]
        [TestCase("under_3")]
        public void AuthoredPlateBuildsOneMeshWithBakedColors(string site)
        {
            var source = Resources.Load<TextAsset>("Weltferne/" + site);
            Assert.That(source, Is.Not.Null, site);
            var decode = typeof(WorldHorizon).GetMethod("Decode", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(decode, Is.Not.Null);
            var mesh = (Mesh)decode.Invoke(null, new object[] { source });
            try
            {
                Assert.That(mesh.vertexCount, Is.EqualTo((source.dataSize - 8) / 16));
                Assert.That(mesh.subMeshCount, Is.EqualTo(1));
                Assert.That(mesh.GetIndexCount(0), Is.EqualTo((uint)mesh.vertexCount));
                Assert.That(mesh.HasVertexAttribute(VertexAttribute.Color), Is.True);
                Assert.That(mesh.bounds.min.x, Is.LessThan(0));
                Assert.That(mesh.bounds.max.x, Is.GreaterThan(0));
                Assert.That(mesh.bounds.min.z, Is.LessThan(0));
                Assert.That(mesh.bounds.max.z, Is.GreaterThan(0));
            }
            finally { Object.DestroyImmediate(mesh); }
        }
    }
}
