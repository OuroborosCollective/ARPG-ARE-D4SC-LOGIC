using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text;
using AreArpg.Core;

internal static class VillageEconomyMigrationTests
{
    // Written and successfully reloaded by the original PR at 66aee9e, using seed 99,
    // an entry profile in the village, one quest-earned resident and 4,020 ArenaSimulation steps.
    private static string ReadOldSave(string name)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name)).TrimEnd('\r', '\n');
    }

    // Recompute the public container checksum. This exercises the economy reader through a real save load.
    private static string ReplaceEconomy(string saved, Action<string[]> change)
    {
        string[] parts = saved.Split('|');
        int index = Array.FindIndex(parts, s => s.StartsWith("economy=", StringComparison.Ordinal));
        string token = Encoding.UTF8.GetString(Convert.FromBase64String(parts[index].Substring("economy=".Length)));
        string[] fields = token.Split(';');
        change(fields);
        parts[index] = "economy=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join(";", fields)));
        string body = string.Join("|", parts, 0, parts.Length - 1);
        ulong hash = 14695981039346656037UL;
        unchecked { foreach (byte b in Encoding.UTF8.GetBytes(body)) { hash ^= b; hash *= 1099511628211UL; } }
        return body + "|" + hash.ToString("X16", CultureInfo.InvariantCulture);
    }

    internal static void Run(Action<bool, string> check)
    {
        string saved = ReadOldSave("economy-ac22-v1.txt");
        check(ReplaceEconomy(saved, f => { }) == saved, "economy-fixture-checksum-is-the-original");
        check(!CharacterProgression.TryLoad(ReplaceEconomy(saved, f => f[3] = "4040"), out _),
            "economy-rejects-a-balanced-checkpoint-claiming-future-world-ticks");
        check(!CharacterProgression.TryLoad(ReplaceEconomy(saved, f => f[1] = "7"), out _),
            "economy-rejects-undeclared-content-revision");
        check(CharacterProgression.TryLoad(saved, out var p), "economy-v1-frozen-character-loads");
        check(p.SaveString() == saved && p.Economy.StableHash == 0x0F74200A22C3B814UL,
            "economy-v1-frozen-round-trip-retains-actual-old-bytes-and-hash");
        var resident = p.Economy.Residents.Single();
        check(p.Economy.LastTick == 4020 && resident.Work == VillageWork.MakeFrame &&
            resident.Held(VillageGood.Wood) == 2 && resident.WorkTicks == 300, "economy-v1-frame-in-progress");
        int events = p.Social.Forge.Archive.Events.Count;
        var world = new ArenaSimulation(99, p, 1, LootCatalog.Version1);
        for (int i = 1; i < VillageEconomyRules.Stride; i++) world.Step();
        check(p.Economy.StableHash == 0x0F74200A22C3B814UL, "economy-v1-no-unobserved-work-between-economic-ticks");
        world.Step();
        check(p.Economy.Token().StartsWith("1;2;", StringComparison.Ordinal),
            "economy-v1-activates-revision-two-on-first-economic-step");
        check(resident.Work == VillageWork.MakeFrame && resident.Held(VillageGood.Wood) == 2 &&
            p.Social.Forge.Archive.Events.Count == events, "economy-v1-outstanding-inputs-and-witnesses-survive-upgrade");
        for (int i = 13; i < VillageEconomyRules.GoodCount; i++)
        {
            var good = (VillageGood)i;
            check(p.Economy.StartStock(good) == VillageEconomyRules.StartStock(good) &&
                p.Economy.Made(good) == 0 && p.Economy.Used(good) == 0 && p.Economy.Received(good) == 0,
                "economy-v1-new-stock-opens-at-migration-without-inventing-history-" + good);
        }
        string makingFrame = p.SaveString();
        string unreported = p.Economy.Token();
        const string emptyShelf = ";m,0,4030,3";
        check(p.Economy.ReportedMillHerbs == -1 && VillageEconomyState.TryRead(unreported + emptyShelf, out var knownEmpty) &&
            knownEmpty.ReportedMillHerbs == 0 && knownEmpty.MillReportTick == 4030 && knownEmpty.MillReporterOrdinal == 3 &&
            knownEmpty.Token() == unreported + emptyShelf, "economy-mill-report-format-distinguishes-empty-shelf-and-unknown");
        foreach (string invalid in new[] { ";m,7,4030,3", ";m,0,4031,3", ";m,0,4030,4", emptyShelf + emptyShelf })
            check(!VillageEconomyState.TryRead(unreported + invalid, out _), "economy-mill-report-refuses-invalid-count-time-witness-or-duplicate-" + invalid);
        check(!CharacterProgression.TryLoad(ReplaceEconomy(makingFrame, f => f[f.Length-1] += emptyShelf), out _),
            "economy-mill-report-requires-the-actual-story-route");
        check(!CharacterProgression.TryLoad(ReplaceEconomy(makingFrame, f =>
        {
            string[] fields = f[13].Split(',');
            fields[4] = fields[5] = "1"; // Move the ongoing work from its village workbench to the forest.
            f[13] = string.Join(",", fields);
        }), out _), "economy-v2-rejects-active-work-at-the-wrong-site");
        long previous = p.Economy.Made(VillageGood.WoodFrame);
        for (int i = 0; i < 400 && p.Economy.Made(VillageGood.WoodFrame) == previous; i++) world.Step();
        check(p.Economy.Made(VillageGood.WoodFrame) == previous + 1 && p.Economy.Audit().Count == 0,
            "economy-v1-original-order-completes-exactly-once");
        string current = p.SaveString();
        check(CharacterProgression.TryLoad(current, out var restored) && restored.SaveString() == current &&
            restored.Economy.StableHash == p.Economy.StableHash, "economy-v2-checkpoint-round-trips-after-legacy-upgrade");
        check(!CharacterProgression.TryLoad(ReplaceEconomy(current, f => f[1] = "1"), out _),
            "economy-old-revision-cannot-smuggle-in-new-goods-and-jobs");
        BrokenAxeReturn(check);
    }

    // Seed 99, same original writer, Orsel unlocked through her quest. Camp rations above a three-ration
    // reserve were withdrawn every 1,400 ticks; available provisions below that level went back to Mara.
    // When resident 3 had four axe uses left, twelve quest-earned residents joined. No save fields were edited.
    private static void BrokenAxeReturn(Action<bool, string> check)
    {
        string saved = ReadOldSave("economy-ac22-v1-broken-axe.txt");
        check(CharacterProgression.TryLoad(saved, out var p) && p.SaveString() == saved,
            "economy-v1-real-overfull-felling-loads-without-changing-the-old-checkpoint");
        var carrier = p.Economy.Residents[0];
        check(p.Economy.LastTick == 147140 && carrier.Site == VillageSite.Forest && carrier.Load == 6 &&
            carrier.Carried(VillageGood.Wood) == 6 && carrier.Carried(VillageGood.BrokenAxe) == 1 && carrier.ReturningBrokenTool,
            "economy-v1-broken-tool-remains-with-its-actual-owner");
        string wrong = ReplaceEconomy(saved, f =>
        {
            string[] residents = f[13].Split('/');
            string[] first = residents[0].Split(',');
            first[4] = first[5] = "0";
            residents[0] = string.Join(",", first);
            f[13] = string.Join("/", residents);
        });
        check(!CharacterProgression.TryLoad(wrong, out _), "economy-v1-does-not-excuse-a-seven-item-load-in-the-village");
        var world = new ArenaSimulation(99, p, 1, LootCatalog.Version1);
        for (int i = 0; i < VillageEconomyRules.Stride; i++) world.Step();
        string inTransit = p.SaveString();
        check(CharacterProgression.TryLoad(inTransit, out var again) && again.SaveString() == inTransit &&
            again.Economy.Residents[0].ReturningBrokenTool, "economy-v2-recovers-a-second-save-on-the-original-return-journey");
        var resumed = new ArenaSimulation(99, again, 1, LootCatalog.Version1);
        var owner = again.Economy.Residents[0];
        for (ulong i = 0; i < WorldSkyClock.TicksPerDay && owner.ReturningBrokenTool; i++) resumed.Step();
        check(owner.Site == VillageSite.Village && !owner.ReturningBrokenTool && owner.Carried(VillageGood.BrokenAxe) == 0 &&
            again.Economy.Audit().Count == 0 && again.Economy.Present(VillageGood.BrokenAxe) + again.Economy.Used(VillageGood.BrokenAxe) ==
            p.Economy.Present(VillageGood.BrokenAxe) + p.Economy.Used(VillageGood.BrokenAxe),
            "economy-v2-original-broken-axe-actually-returns-and-is-accounted-once");
    }
}
