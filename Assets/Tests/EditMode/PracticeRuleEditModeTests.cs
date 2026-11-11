using AreArpg.Core;
using NUnit.Framework;
using System.Numerics;

namespace AreArpg.EditMode.Tests
{
    /// <summary>Execute the existing practice owner through canonical actions in Unity's runtime.</summary>
    public sealed class PracticeRuleEditModeTests
    {
        /// <summary>Overkill trains only effective HP, blocked reuse and full-health healing train nothing.</summary>
        [Test]
        public void OnlyEffectiveActionsTrainAndPersist()
        {
            var world = new ArenaSimulation(123);
            world.Monsters[0].Xmm = 1000; world.Monsters[0].Zmm = 0; world.Monsters[0].Health = 1;
            Assert.AreEqual(1, world.ActivateSkill(0).Kills);
            world.ActivateSkill(0);
            world.ActivateSkill(2);
            Assert.AreEqual(BigInteger.One, world.Progression.PracticeExperience(0));
            Assert.AreEqual(BigInteger.Zero, world.Progression.PracticeExperience(2));
            Assert.IsTrue(CharacterProgression.TryLoad(world.Progression.SaveString(), out var loaded));
            Assert.AreEqual(world.Progression.StableHash, loaded.StableHash);
            Assert.AreEqual(5000, PracticeRules.DamageBonusBp(BigInteger.Pow(10, 100)));
        }
    }
}
