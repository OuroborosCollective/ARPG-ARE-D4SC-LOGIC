using AreArpg.UnityClient;
using NUnit.Framework;

namespace AreArpg.EditMode.Tests
{
    /// <summary>
    /// The owner's rules work in quarter percent: +0,25 % block per shield level and +1,25 % accuracy per bow level.
    /// The menus must show these values exactly instead of cutting them to one decimal (0,25 % was shown as 0,2 %).
    /// </summary>
    public sealed class MenuNumberFormatTests
    {
        [TestCase(25, "0,25 %")]       // shield level 1
        [TestCase(2475, "24,75 %")]    // shield level 99
        [TestCase(6125, "61,25 %")]    // bow accuracy at level 1
        [TestCase(250, "2,5 %")]       // weapon damage every 5 levels
        [TestCase(1900, "19 %")]
        [TestCase(10000, "100 %")]
        [TestCase(0, "0 %")]
        [TestCase(-1200, "-12 %")]
        [TestCase(-25, "-0,25 %")]
        public void PercentShowsQuarterPercentExactly(int basisPoints, string expected)
        {
            Assert.AreEqual(expected, ArenaRuntime.Percent(basisPoints));
        }
    }
}
