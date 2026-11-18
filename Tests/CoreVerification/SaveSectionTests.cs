using System;
using System.Globalization;
using System.Numerics;
using System.Text;
using AreArpg.Core;

/// <summary>Sectioned checkpoint (AC22): container, migration and rejection. Core rules only, no PlayerPrefs evidence.</summary>
internal static class SaveSectionTests
{
    private static string BaseField(string saved) => saved.Split('|')[2];

    private static ulong HashText(string payload)
    {
        ulong hash = 14695981039346656037UL;
        unchecked { foreach (byte value in Encoding.UTF8.GetBytes(payload)) { hash ^= value; hash *= 1099511628211UL; } }
        return hash;
    }

    /// <summary>Rebuild the outer checksum after a deliberate change so a negative test checks only its named rule.</summary>
    private static string Rechecksum(string container)
    {
        int last = container.LastIndexOf('|');
        string body = container.Substring(0, last);
        return body + "|" + HashText(body).ToString("X16", CultureInfo.InvariantCulture);
    }

    private static string Encode(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

    private static void RoundTrip(Action<bool, string> require, string legacy, string label)
    {
        require(CharacterProgression.TryLoad(legacy, out var loaded), "section-legacy-loads-" + label);
        string before = loaded.StableHash.ToString("X16", CultureInfo.InvariantCulture);
        loaded.SetSection("probe", label);
        string sectioned = loaded.SaveString();
        require(sectioned.StartsWith("AC22|", StringComparison.Ordinal), "section-migration-writes-ac22-" + label);
        require(CharacterProgression.TryLoad(sectioned, out var again), "section-migration-reloads-" + label);
        require(again.StableHash.ToString("X16", CultureInfo.InvariantCulture) == before,
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
        // A loaded legacy checkpoint re-saved without a section keeps the legacy format (no silent AC22 switch).
        require(CharacterProgression.TryLoad(new CharacterProgression().SaveString(), out var resaved) &&
            !resaved.SaveString().StartsWith("AC22|", StringComparison.Ordinal), "section-legacy-resave-stays-legacy");

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
        string baseField = BaseField(valid);
        require(CharacterProgression.TryLoad(valid, out _), "section-valid-container-loads");
        require(!CharacterProgression.TryLoad(valid.Substring(0, valid.Length - 1), out _), "section-truncated-rejected");
        require(!CharacterProgression.TryLoad(valid.Substring(0, valid.Length - 5), out _), "section-cut-rejected");
        require(!CharacterProgression.TryLoad(Rechecksum(valid.Replace("AC22|1|", "AC22|2|")), out _),
            "section-wrong-version-rejected");
        require(!CharacterProgression.TryLoad(Rechecksum(valid.Replace("|1|alpha=", "|2|alpha=")), out _),
            "section-wrong-count-rejected");
        char first = valid[valid.Length - 1];
        require(!CharacterProgression.TryLoad(valid.Substring(0, valid.Length - 1) + (first == '0' ? '1' : '0'), out _),
            "section-changed-checksum-rejected");
        require(!CharacterProgression.TryLoad(Rechecksum(valid.Replace(baseField, baseField + " ")), out _),
            "section-non-canonical-base64-rejected");
        // Nested container: the base is a valid base64 AC22 string, only the nesting rule rejects it.
        require(!CharacterProgression.TryLoad(Rechecksum("AC22|1|" + Encode(valid) + "|0|"), out _), "section-nested-container-rejected");
        require(!CharacterProgression.TryLoad(Rechecksum("AC22|1|" + Encode("AC5|not a save") + "|0|"), out _),
            "section-invalid-base-rejected");
        // Names: otherwise valid fields, only the name rule rejects these.
        require(!CharacterProgression.TryLoad(Rechecksum("AC22|1|" + baseField + "|1|BAD NAME=" + Encode("1,2,3")), out _),
            "section-invalid-name-rejected");
        require(!CharacterProgression.TryLoad(Rechecksum("AC22|1|" + baseField + "|2|alpha=" + Encode("1") + "|alpha=" + Encode("2")), out _),
            "section-duplicate-name-rejected");
        // Empty container with a deliberately wrong checksum stays rejected (kept separate from the named rules).
        require(!CharacterProgression.TryLoad("AC22|1|" + baseField + "|0|deadbeefdeadbeef", out _), "section-empty-container-checksum-still-enforced");

        // Review 2026-11-18: a damaged AC22 primary may hold journey progress and must block an older backup.
        require(!CharacterProgression.MayHoldJourneyProgress(valid), "section-ac22-legacy-base-blocks-no-backup");
        string journeyContainer = Rechecksum("AC22|1|" + Encode("AC20|journey") + "|0|");
        require(CharacterProgression.MayHoldJourneyProgress(journeyContainer), "section-ac22-journey-base-holds-journey");
        require(CharacterProgression.MayHoldJourneyProgress(journeyContainer.Substring(0, journeyContainer.Length - 4)),
            "section-damaged-ac22-journey-holds-journey");
        require(CharacterProgression.MayHoldJourneyProgress("AC22|1|!!!|0|0000000000000000"), "section-undecodable-ac22-holds-journey");

        // Review 2026-11-18: the writer refuses to emit a container the loader would reject for its size.
        var tooBig = new CharacterProgression();
        bool sizeThrew = false;
        try { tooBig.SetSection("huge", new string('a', CharacterProgression.MaxSaveLength)); }
        catch (ArgumentException) { sizeThrew = true; }
        require(sizeThrew && !tooBig.HasSections, "section-oversize-value-rejected-at-set");

        // Review 2026-11-18: a lone surrogate must be rejected before it can replace an entry.
        var strict = new CharacterProgression();
        strict.SetSection("notes", "ok");
        bool threw = false;
        try { strict.SetSection("notes", "\uD800"); } catch (ArgumentException) { threw = true; }
        require(threw && strict.TryGetSection("notes", out var unchanged) && unchanged == "ok", "section-lone-surrogate-rejected");
        // A valid surrogate pair (a real emoji) still round-trips.
        strict.SetSection("notes", "\U0001F600");
        require(CharacterProgression.TryLoad(strict.SaveString(), out var emoji) &&
            emoji.TryGetSection("notes", out var emojiValue) && emojiValue == "\U0001F600", "section-valid-surrogate-pair-roundtrips");
    }
}
