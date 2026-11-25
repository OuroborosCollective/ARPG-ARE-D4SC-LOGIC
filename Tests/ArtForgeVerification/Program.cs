using System;
using AshenCrypt.ArtForge.Tests;

internal static class Program
{
    public static int Main()
    {
        int passed = 0, failed = 0;
        foreach (string name in ArtGraphBehaviorCases.Names)
        {
            try { ArtGraphBehaviorCases.Run(name); passed++; Console.WriteLine("PASS " + name); }
            catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error.Message); }
        }
        Console.WriteLine("ArtForge contract: " + passed + " passed, " + failed + " failed; pure C# behavioral tests, not Unity runtime.");
        return failed == 0 ? 0 : 1;
    }
}
