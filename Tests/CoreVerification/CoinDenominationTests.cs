using System;
using System.Numerics;
using AreArpg.Core;

internal static class CoinDenominationTests
{
    public static void Run(Action<bool, string> require)
    {
        require(CoinDenominations.Format(BigInteger.Zero) == "0 K", "coins-zero-copper");
        require(CoinDenominations.Format(new BigInteger(99)) == "99 K", "coins-99-copper");
        require(CoinDenominations.Format(new BigInteger(100)) == "1 S 0 K", "coins-one-silver");
        require(CoinDenominations.Format(new BigInteger(10099)) == "1 G 0 S 99 K", "coins-one-gold-mixed");
        require(CoinDenominations.Format(new BigInteger(1020304)) == "102 G 3 S 4 K", "coins-large-balance");
        var balance = BigInteger.Parse("1234567890123456789012345678901234");
        var parts = CoinDenominations.Split(balance);
        require(parts.Gold * 10000 + parts.Silver * 100 + parts.Copper == balance,
            "coins-unbounded-big-integer-exactly-preserved");
        require(parts.Silver >= 0 && parts.Silver < 100 && parts.Copper >= 0 && parts.Copper < 100,
            "coins-canonical-denomination-bounds");
        bool rejected = false;
        try { CoinDenominations.Format(BigInteger.MinusOne); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        require(rejected, "coins-reject-negative-balance");
        var progress = CharacterProgression.CreateEntryProfile();
        string before = progress.SaveString();
        _ = CoinDenominations.Format(progress.Social.Coins);
        require(progress.SaveString() == before, "coins-display-does-not-mutate-persisted-progress");
    }
}
