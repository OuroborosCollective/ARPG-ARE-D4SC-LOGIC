using NUnit.Framework;

namespace AshenCrypt.ArtForge.Tests
{
    public sealed class ArtGraphContractTests
    {
        [TestCaseSource(typeof(ArtGraphBehaviorCases), nameof(ArtGraphBehaviorCases.Names))]
        public void ReadOnlyGraphContract(string name) { ArtGraphBehaviorCases.Run(name); }
    }
}
