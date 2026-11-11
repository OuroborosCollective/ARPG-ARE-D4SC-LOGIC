using AreArpg.Core;
using NUnit.Framework;

namespace AreArpg.EditMode.Tests
{
    /// <summary>Verify overflow boundaries with Unity's compiler and actual Core combat.</summary>
    public sealed class CombatStatsEditModeTests
    {
        /// <summary>Large persisted weapon values still cause canonical kills and positive bounded reports.</summary>
        [TestCase(int.MaxValue)] [TestCase(int.MaxValue - 94)] [TestCase(int.MaxValue - 95)]
        public void LargeWeaponBonusDoesNotWrap(int bonus)
        {
            var progression = new CharacterProgression();
            progression.AddWeaponUpgrade(bonus);
            var world = new ArenaSimulation(123, progression);
            world.Monsters[0].Xmm = 1000;
            world.Monsters[0].Zmm = 0;
            world.Monsters[0].Health = 200000;
            var result = world.ActivateSkill(0);
            Assert.AreEqual(1, result.Kills);
            Assert.Greater(result.TotalDamage, 0);
            Assert.AreEqual(new System.Numerics.BigInteger(20000), progression.PracticeExperience(0));
        }
    }
}
