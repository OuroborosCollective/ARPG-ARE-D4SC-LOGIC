using AreArpg.Core;
using NUnit.Framework;

namespace AreArpg.EditMode.Tests
{
    /// <summary>Run the shipped ability definitions through the real Core in Unity's managed runtime.</summary>
    public sealed class SkillContractEditModeTests
    {
        /// <summary>One successful activation sets the exact tick cooldown and rejects immediate reuse.</summary>
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void ShippedSkillUsesCanonicalCooldown(int slot)
        {
            var world = new ArenaSimulation(123);
            world.Monsters[0].Xmm = 1000;
            world.Monsters[0].Zmm = 0;
            world.Monsters[0].Health = 200000;
            Assert.IsTrue(world.ActivateSkill(slot).Activated);
            Assert.AreEqual(SkillCatalog.Get(slot).CooldownTicks, world.Cooldowns[slot]);
            Assert.IsFalse(world.ActivateSkill(slot).Activated);
            world.Step();
            Assert.AreEqual(SkillCatalog.Get(slot).CooldownTicks - 1, world.Cooldowns[slot]);
            Assert.AreEqual(100, ArenaSimulation.TickMs);
        }
    }
}
