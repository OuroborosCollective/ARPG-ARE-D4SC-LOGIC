using System;
using System.Numerics;
using System.Globalization;
using System.Text;
using AreArpg.Core;

internal static class SupplyTheftTests
{
    private static string Resign(string payload)
    {
        ulong hash = 14695981039346656037UL;
        unchecked { foreach (byte value in Encoding.UTF8.GetBytes(payload)) { hash ^= value; hash *= 1099511628211UL; } }
        return payload + "|" + hash.ToString("X16", CultureInfo.InvariantCulture);
    }

    public static void Run(Action<bool, string> require)
    {
        var world = new ArenaSimulation(151);
        require(world.Progression.Social.CampProvisions == 8, "supply-case-initial-stock-actually-two-below-capacity");
        ulong remoteHash = world.ComputeWorldHash();
        SocialResult remote = world.DonateCampRation();
        require(!remote.Accepted && world.ComputeWorldHash() == remoteHash, "supply-case-remote-donation-is-rejected-without-mutation");
        world.Npcs[1].Xmm = 1000; world.Npcs[1].Zmm = 0;
        int before = world.Progression.Social.CampProvisions;
        BigInteger rations = world.Progression.Social.Provisions;
        var mara = world.Npcs[1].Memory;
        BigInteger priorHelp = mara.HelpfulActions;
        require(world.DonateCampRation().Accepted, "supply-case-near-mara-can-donate-real-ration");
        require(world.Progression.Social.CampProvisions == before + 1 &&
                world.Progression.Social.Provisions == rations - 1,
                "supply-case-exact-one-ration-moves-from-player-to-canonical-camp");
        require(mara.HelpfulActions == priorHelp + 1 && mara.PositivePoints == 8 && mara.Meetings == 1,
                "supply-case-existing-mara-memory-recorded-exactly-once");
        ulong acceptedHash = world.ComputeWorldHash();
        require(!world.DonateCampRation().Accepted && world.ComputeWorldHash() == acceptedHash,
                "supply-case-cooldown-rejects-repeat-without-mutating-world-hash");
        string saved = world.Progression.SaveString();
        require(saved.StartsWith("AC5|") && CharacterProgression.TryLoad(saved, out var savedMara) &&
                savedMara.Social.CampProvisions == 9 && savedMara.Social.Provisions == 2 &&
                savedMara.Social.GetMemory(1).PositivePoints == 8 &&
                savedMara.StableHash == world.Progression.StableHash,
                "supply-case-ac5-save-roundtrip-binds-provisions-and-personal-reputation");
        for (int i = 0; i < 10; i++) world.Step();
        world.Npcs[1].Xmm = 1000; world.Npcs[1].Zmm = 0;
        require(world.DonateCampRation().Accepted && world.Progression.Social.CampProvisions == 10 &&
                world.Progression.Social.Provisions == 1, "supply-case-real-restoration-reaches-existing-capacity");
        for (int i = 0; i < 10; i++) world.Step();
        world.Npcs[1].Xmm = 1000; world.Npcs[1].Zmm = 0;
        ulong fullHash = world.ComputeWorldHash();
        require(!world.DonateCampRation().Accepted && world.ComputeWorldHash() == fullHash,
                "supply-case-full-camp-does-not-consume-a-player-ration");

        var mercy = new ArenaSimulation(152);
        mercy.Npcs[0].Xmm = 1000; mercy.Npcs[0].Zmm = 0;
        require(mercy.InteractNpc(0, NpcAction.Help).Accepted &&
                mercy.Progression.Social.Provisions == 2 &&
                mercy.Npcs[0].Memory.PositivePoints == 8 &&
                mercy.Progression.Social.CampProvisions == 8 &&
                mercy.Npcs[1].Memory.PositivePoints.IsZero,
                "supply-case-mercy-uses-canonical-hungry-npc-and-does-not-fabricate-global-fame");
        require(CharacterProgression.TryLoad(mercy.Progression.SaveString(), out var afterMercy) &&
                new ArenaSimulation(152, afterMercy).Npcs[0].Greeting().Contains("Beistand"),
                "supply-case-real-mercy-persists-across-cold-load");
        var force = new ArenaSimulation(153);
        force.Npcs[0].Xmm = 1000; force.Npcs[0].Zmm = 0;
        require(force.InteractNpc(0, NpcAction.Attack).Accepted &&
                force.Npcs[0].Memory.NegativePoints > 0 &&
                force.Npcs[0].Memory.Health < 600 &&
                force.Progression.Social.CampProvisions == 8,
                "supply-case-violence-really-hurts-the-accused-without-solving-the-shortage");
        require(CharacterProgression.TryLoad(force.Progression.SaveString(), out var afterForce) &&
                afterForce.Social.GetMemory(0).NegativePoints == force.Npcs[0].Memory.NegativePoints,
                "supply-case-harm-remains-saved-even-though-guilt-is-unproven");

        var legacy = new CharacterProgression();
        string[] fields = legacy.SaveString().Split('|');
        string[] social = fields[13].Split(';');
        string[] header = social[0].Split(',');
        header[2] = "10"; social[0] = string.Join(",", header);
        fields[13] = string.Join(";", social);
        string oldSave = Resign(string.Join("|", fields, 0, 14));
        require(CharacterProgression.TryLoad(oldSave, out var older) && older.Social.CampProvisions == 10,
                "supply-case-valid-older-ac5-full-stock-remains-readable");
        var continued = new ArenaSimulation(154, older);
        continued.Npcs[1].Xmm = 1000; continued.Npcs[1].Zmm = 0;
        ulong legacyHash = continued.ComputeWorldHash();
        require(!continued.DonateCampRation().Accepted && continued.ComputeWorldHash() == legacyHash,
                "supply-case-veteran-full-stock-not-reset-or-double-funded");

        var nextRegion = new ArenaSimulation(155, new CharacterProgression(), new BigInteger(2));
        ulong regionHash = nextRegion.ComputeWorldHash();
        require(!nextRegion.DonateCampRation().Accepted && nextRegion.ComputeWorldHash() == regionHash,
                "supply-case-outside-first-region-has-no-second-authority");
        var a = new ArenaSimulation(156); var b = new ArenaSimulation(156);
        a.Npcs[1].Xmm = b.Npcs[1].Xmm = 1000;
        a.Npcs[1].Zmm = b.Npcs[1].Zmm = 0;
        require(a.DonateCampRation().Accepted && b.DonateCampRation().Accepted &&
                a.ComputeWorldHash() == b.ComputeWorldHash(),
                "supply-case-same-seed-same-action-same-replay-hash");
        for (int i = 0; i < 20; i++) { a.Step(); b.Step(); }
        require(a.ComputeWorldHash() == b.ComputeWorldHash(),
                "supply-case-real-tick-replay-remains-deterministic");
        b.Progression.Social.GetMemory(0).Record(NpcAction.Attack, 1);
        require(a.ComputeWorldHash() != b.ComputeWorldHash(),
                "supply-case-different-moral-decision-changes-world-hash");
    }
}
