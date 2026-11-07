using System;
using System.Numerics;
using AreArpg.Core;

/// <summary>Real core transitions and canonical save compatibility; not Android PlayerPrefs evidence.</summary>
internal static class CheckpointTests
{
    public static void Run(Action<bool, string> require)
    {
        var veteran = new CharacterProgression();
        require(veteran.UnlockedCryptDepth == BigInteger.One,
            "checkpoint-new-character-starts-at-first-crypt");
        var first = new ArenaSimulation(0xD0UL, veteran);
        require(first.TryAdvanceCrypt() == null && veteran.UnlockedCryptDepth == BigInteger.One,
            "checkpoint-locked-before-kill");

        foreach (MonsterState enemy in first.Monsters)
        {
            enemy.Xmm = 1000;
            enemy.Zmm = 0;
            enemy.Health = 1;
        }
        require(first.ActivateSkill(3).Kills == 3, "checkpoint-first-boss-defeated");
        require(first.TryAdvanceCrypt() == null && veteran.UnlockedCryptDepth == BigInteger.One,
            "checkpoint-no-progress-with-uncollected-loot");
        for (int i = 0; i < 3; i++)
            require(first.TryCollectLoot(), "checkpoint-pickup-" + i);

        ulong sealedHash = first.ComputeWorldHash();
        BigInteger oldXp = first.Progression.TotalExperience;
        int oldWeapon = first.Progression.WeaponBonus;
        ArenaSimulation second = first.TryAdvanceCrypt();
        require(second != null && second.ChapterId == 2 && second.CryptDepth == new BigInteger(2),
            "checkpoint-real-victory-opens-ember-rift");
        require(second.Progression.UnlockedCryptDepth == new BigInteger(2),
            "checkpoint-unlocked-chapter-stored-on-character");
        require(first.Progression.UnlockedCryptDepth == BigInteger.One &&
            first.ComputeWorldHash() == sealedHash,
            "checkpoint-previous-run-sealed-without-mutating-hash");
        require(!object.ReferenceEquals(first.Progression, second.Progression) &&
            second.Progression.TotalExperience == oldXp &&
            second.Progression.WeaponBonus == oldWeapon &&
            second.Progression.LootCollected == first.Progression.LootCollected,
            "checkpoint-new-run-preserves-progress-as-copy");

        string saved = second.Progression.SaveString();
        require(saved.StartsWith("AC3|", StringComparison.Ordinal),
            "checkpoint-save-format-ac3");
        CharacterProgression loaded;
        require(CharacterProgression.TryLoad(saved, out loaded) &&
            loaded.UnlockedCryptDepth == new BigInteger(2),
            "checkpoint-canonical-save-reloads-chapter");
        require(loaded.StableHash == second.Progression.StableHash,
            "checkpoint-save-roundtrip-hash");
        var resumed = new ArenaSimulation(0xD0UL, loaded, loaded.UnlockedCryptDepth);
        require(resumed.ChapterId == 2 && resumed.Monsters[2].Kind == 3 &&
            resumed.Progression.TotalExperience == oldXp,
            "checkpoint-restart-resumes-the-unlocked-ember-rift");
        require(resumed.TryAdvanceCrypt() == null &&
            resumed.Progression.UnlockedCryptDepth == new BigInteger(2),
            "checkpoint-restart-does-not-auto-unlock-next-region");

        const string legacyAc2 = "AC2|160|8|1|1|1|0|0|FA028359B173B723";
        require(CharacterProgression.TryLoad(legacyAc2, out loaded) &&
            loaded.UnlockedCryptDepth == BigInteger.One &&
            loaded.MightRank == BigInteger.One && loaded.Level == new BigInteger(2) &&
            loaded.WeaponBonus == 8 && loaded.LifetimeKills == BigInteger.One,
            "checkpoint-legacy-ac2-retains-ranks-xp-gear");
        require(CharacterProgression.TryLoad(loaded.SaveString(), out var migrated) &&
            migrated.UnlockedCryptDepth == BigInteger.One &&
            migrated.MightRank == BigInteger.One &&
            migrated.WeaponBonus == 8,
            "checkpoint-ac2-migrates-to-ac3-without-reset");

        const string legacyAc1 = "AC1|160|8|1|1|83E7CB814DDECFE9";
        require(CharacterProgression.TryLoad(legacyAc1, out loaded) &&
            loaded.UnlockedCryptDepth == BigInteger.One && loaded.Level == 2,
            "checkpoint-legacy-ac1-still-works");

        string corrupted = saved.Replace("|2|", "|0|");
        require(!CharacterProgression.TryLoad(corrupted, out loaded),
            "checkpoint-invalid-or-tampered-save-rejected");

        // Content checksum alone must not validate a zero depth.
        const string invalidZeroDepth = "AC3|0|0|0|0|0|0|0|0|";
        string forged = invalidZeroDepth + FnvHex(invalidZeroDepth.Substring(0, invalidZeroDepth.Length - 1));
        require(!CharacterProgression.TryLoad(forged, out loaded),
            "checkpoint-zero-depth-rejected-even-with-valid-checksum");
    }

    private static string FnvHex(string text)
    {
        ulong h = 14695981039346656037UL;
        unchecked
        {
            foreach (byte b in System.Text.Encoding.UTF8.GetBytes(text))
            {
                h ^= b;
                h *= 1099511628211UL;
            }
        }
        return h.ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
    }
}
