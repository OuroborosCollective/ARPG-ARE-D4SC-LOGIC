using System;
using System.Numerics;
using AreArpg.Core;

/// <summary>Sectioned checkpoint (AC22): container, migration and rejection. Core rules only, no PlayerPrefs evidence.</summary>
internal static class SaveSectionTests
{
    private static string BaseField(string saved) => saved.Split('|')[2];

    private static void RoundTrip(Action<bool, string> require, string legacy, string label)
    {
        require(CharacterProgression.TryLoad(legacy, out var loaded), "section-legacy-loads-" + label);
        string before = loaded.StableHash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
        loaded.SetSection("probe", label);
        string sectioned = loaded.SaveString();
        require(sectioned.StartsWith("AC22|", StringComparison.Ordinal), "section-migration-writes-ac22-" + label);
        require(CharacterProgression.TryLoad(sectioned, out var again), "section-migration-reloads-" + label);
        require(again.StableHash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture) == before,
            "section-migration-keeps-values-" + label);
        require(again.SaveString() == sectioned, "section-migration-is-canonical-" + label);
        require(again.TryGetSection("probe", out var probe) && probe == label, "section-migration-keeps-section-" + label);
    }

    public static void Run(Action<bool, string> require)
    {
        // Without a section the checkpoint stays byte-identical to the existing chain, so old saves and
        // their tests are untouched until a system actually contributes a section.
        var fresh = new CharacterProgression();
        require(fresh.SaveString().StartsWith("AC5|", StringComparison.Ordinal), "section-empty-save-stays-legacy");
        require(!fresh.SaveString().StartsWith("AC22|", StringComparison.Ordinal), "section-no-container-without-content");

        RoundTrip(require, "AC1|160|8|1|1|83E7CB814DDECFE9", "ac1");
        RoundTrip(require, "AC2|160|8|1|1|1|0|0|FA028359B173B723", "ac2");
        RoundTrip(require, new CharacterProgression().SaveString(), "ac5");
        RoundTrip(require, CharacterProgression.CreateEntryProfile().SaveString(), "ac8");

        // A new section changes neither the base nor the other sections.
        var alpha = new CharacterProgression();
        alpha.SetSection("alpha", "1,2,3");
        string alphaOnly = alpha.SaveString();
        string alphaBase = BaseField(alphaOnly);
        alpha.SetSection("beta", "9");
        string both = alpha.SaveString();
        require(both.StartsWith("AC22|", StringComparison.Ordinal), "section-format-activates-with-a-section");
        require(BaseField(both) == alphaBase, "section-addition-keeps-base-unchanged");
        require(CharacterProgression.TryLoad(both, out var reloaded) &&
            reloaded.TryGetSection("alpha", out var alphaValue) && alphaValue == "1,2,3" &&
            reloaded.TryGetSection("beta", out var betaValue) && betaValue == "9",
            "section-both-values-survive");
        reloaded.SetSection("gamma", "z");
        require(reloaded.TryGetSection("alpha", out var stillAlpha) && stillAlpha == "1,2,3" &&
            reloaded.TryGetSection("beta", out var stillBeta) && stillBeta == "9",
            "section-third-addition-keeps-earlier-sections");

        // Unknown sections are preserved byte-for-byte so a newer system's data is never dropped.
        string withUnknown = alpha.SaveString();
        require(CharacterProgression.TryLoad(withUnknown, out var kept) && kept.SaveString() == withUnknown,
            "section-unknown-section-preserved");

        // Damaged, truncated or foreign containers are rejected (checksum, version, count, canonical base64).
        var single = new CharacterProgression();
        single.SetSection("alpha", "1,2,3");
        string valid = single.SaveString();
        require(CharacterProgression.TryLoad(valid, out _), "section-valid-container-loads");
        require(!CharacterProgression.TryLoad(valid.Substring(0, valid.Length - 1), out _), "section-truncated-rejected");
        require(!CharacterProgression.TryLoad(valid.Substring(0, valid.Length - 5), out _), "section-cut-rejected");
        require(!CharacterProgression.TryLoad(valid.Replace("AC22|1|", "AC22|2|"), out _), "section-wrong-version-rejected");
        require(!CharacterProgression.TryLoad(valid.Replace("|1|alpha=", "|2|alpha="), out _), "section-wrong-count-rejected");
        char first = valid[valid.Length - 1];
        require(!CharacterProgression.TryLoad(valid.Substring(0, valid.Length - 1) + (first == '0' ? '1' : '0'), out _),
            "section-changed-checksum-rejected");
        string[] fields = valid.Split('|');
        require(!CharacterProgression.TryLoad(string.Join("|", fields).Replace(fields[2], fields[2] + " "), out _),
            "section-non-canonical-base64-rejected");
        require(!CharacterProgression.TryLoad("AC22|1|AC22|1|0|0000000000000000", out _), "section-nested-container-rejected");
        require(!CharacterProgression.TryLoad("AC22|1|!!|0|0000000000000000", out _), "section-invalid-base-rejected");
        require(!CharacterProgression.TryLoad("AC22|1|" + fields[2] + "|1|BAD NAME=QQ|0000000000000000", out _),
            "section-invalid-name-rejected");
        require(!CharacterProgression.TryLoad("AC22|1|" + fields[2] + "|2|alpha=MQ|alpha=Mg|0000000000000000", out _),
            "section-duplicate-name-rejected");
        require(!CharacterProgression.TryLoad("AC22|1|" + fields[2] + "|0|deadbeefdeadbeef", out _),
            "section-empty-container-checksum-still-enforced");
    }
}
