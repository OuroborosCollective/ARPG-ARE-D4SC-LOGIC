using System;
using System.Globalization;
using System.Numerics;

namespace AreArpg.Core
{
    /// <summary>
    /// Read-only coin denomination projection of the canonical copper-unit SocialLedger.Coins.
    /// Does not change coin storage, social ownership, the 100-ms tick, or checkpoint schema.
    /// </summary>
    public static class CoinDenominations
    {
        public readonly struct Amount
        {
            public readonly BigInteger Gold;
            public readonly int Silver;
            public readonly int Copper;

            public Amount(BigInteger gold, int silver, int copper)
            {
                Gold = gold;
                Silver = silver;
                Copper = copper;
            }
        }

        public static Amount Split(BigInteger copperUnits)
        {
            if (copperUnits < 0)
                throw new ArgumentOutOfRangeException(nameof(copperUnits));
            BigInteger gold = BigInteger.DivRem(copperUnits, 10000, out BigInteger remainder);
            BigInteger silver = BigInteger.DivRem(remainder, 100, out BigInteger copper);
            return new Amount(gold, (int)silver, (int)copper);
        }

        public static string Format(BigInteger copperUnits)
        {
            Amount value = Split(copperUnits);
            if (value.Gold > 0)
                return value.Gold.ToString(CultureInfo.InvariantCulture) + " G " +
                    value.Silver.ToString(CultureInfo.InvariantCulture) + " S " +
                    value.Copper.ToString(CultureInfo.InvariantCulture) + " K";
            if (value.Silver > 0)
                return value.Silver.ToString(CultureInfo.InvariantCulture) + " S " +
                    value.Copper.ToString(CultureInfo.InvariantCulture) + " K";
            return value.Copper.ToString(CultureInfo.InvariantCulture) + " K";
        }
    }
}
