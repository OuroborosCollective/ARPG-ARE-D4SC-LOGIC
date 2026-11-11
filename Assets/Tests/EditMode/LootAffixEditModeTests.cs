using AreArpg.Core;
using NUnit.Framework;
using System.Numerics;

namespace AreArpg.EditMode.Tests
{
    /// <summary>Execute the shipped Core in Unity's managed runtime without physics or a second simulation.</summary>
    public sealed class LootAffixEditModeTests
    {
        [TestCase(0UL)] [TestCase(123UL)] [TestCase(ulong.MaxValue)]
        public void KillSaveAndReplayPreserveAffix(ulong seed)
        {
            var a = new ArenaSimulation(seed, null, BigInteger.One, LootCatalog.Version1);
            var b = new ArenaSimulation(seed, null, BigInteger.One, LootCatalog.Version1);
            foreach (var w in new[] { a, b })
            {
                w.Monsters[0].Xmm = 1000; w.Monsters[0].Zmm = 0; w.Monsters[0].Health = 1;
                Assert.AreEqual(1, w.ActivateSkill(0).Kills);
                Assert.IsNotNull(w.GroundLoot[0].Item);
                Assert.IsTrue(w.TryCollectLoot());
            }
            Assert.AreEqual(a.Progression.Items[0].InstanceId, b.Progression.Items[0].InstanceId);
            Assert.AreEqual(a.ComputeWorldHash(), b.ComputeWorldHash());
            Assert.IsTrue(CharacterProgression.TryLoad(a.Progression.SaveString(), out var loaded));
            Assert.AreEqual(a.Progression.SaveString(), loaded.SaveString());
        }
    }
}
