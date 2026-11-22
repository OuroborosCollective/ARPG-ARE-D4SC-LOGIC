using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using AreArpg.Core;
using AshenCrypt.MemoryForge;

/// <summary>
/// Issue #6, causal memory 5/5: the regression and migration gates over the real Core. A camp decision names the
/// final camp transaction as its cause (#2); witnesses tell it at real village meetings (#3); nothing here adds a
/// second authority, a mock world or a new save version.
/// </summary>
public static class CausalRegressionTests
{
    private const int Ilyan = 0, Dorn = 2;

    public static void Run(Action<bool, string> check)
    {
        CausalParentValid(check);
        CorruptCauseWithValidChecksumsIsRejected(check);
        TransactionsHappenOnce(check);
        CurrentAndHistoricalFormatsStayValid(check);
        TenThousandTickCausalReplay(check);
        ReportedKnowledgeNeedsARealMeeting(check);
    }

    private static CharacterProgression Village(ulong seed, out ArenaSimulation world)
    {
        var progress = CharacterProgression.CreateEntryProfile();
        progress.EnterVillage();
        progress.Social.EnableForge();
        world = new ArenaSimulation(seed, progress, BigInteger.One, LootCatalog.Version1);
        if (!world.CompleteArrivalScene()) throw new InvalidOperationException("Causal fixture requires the first arrival");
        return progress;
    }

    private static void Near(ArenaSimulation world, int npcId)
    {
        var npc = world.Npcs.Single(n => n.Id == npcId);
        npc.Xmm = world.PlayerXmm + 1000;
        npc.Zmm = world.PlayerZmm;
        npc.InteractionCooldownTicks = 0;
    }

    /// <summary>Two donations: the first leaves the camp short, the second refills it and records the decision.</summary>
    private static void DecideByDonation(ArenaSimulation world, Action<bool, string> check, string label)
    {
        Near(world, SocialLedger.MaraNpcId);
        check(world.DonateCampRation().Accepted && !world.Progression.CampDecisionMade, label + "-first-donation-keeps-the-decision-open");
        Near(world, SocialLedger.MaraNpcId);
        check(world.DonateCampRation().Accepted && world.Progression.CampDecisionMade, label + "-final-donation-decides");
    }

    // ------------------------------------------------------------------ 1. causal-parent-valid

    /// <summary>Every cause names an earlier or simultaneous root transaction of the same archive: no cycle, no foreign or missing event, one decision.</summary>
    private static bool CausesAreSound(MemoryArchive archive)
    {
        var events = archive.Events;
        int decisions = 0;
        for (int i = 0; i < events.Count; i++)
        {
            var ev = events[i];
            if (ev.Id != (ulong)(i + 1)) return false;
            bool decision = ev.Context.StartsWith(CharacterProgression.CampDecisionPrefix, StringComparison.Ordinal);
            if (decision) decisions++;
            if (!ev.CauseEventId.HasValue) { if (decision) return false; continue; }
            ulong cause = ev.CauseEventId.Value;
            if (!decision || cause == 0 || cause >= ev.Id) return false;
            var parent = events[(int)cause - 1];
            bool stolen = ev.Context == "camp.decision.stolen";
            if (parent.Id != cause || parent.CauseEventId.HasValue || parent.Tick != ev.Tick || parent.Actor != ev.Actor ||
                parent.Target != ev.Target || parent.Kind != (stolen ? MemoryKind.Theft : MemoryKind.Help) ||
                parent.Context != (stolen ? "camp.theft" : "camp.donation")) return false;
        }
        return decisions == 1;
    }

    private static void CausalParentValid(Action<bool, string> check)
    {
        var theft = Village(6101, out var thief);
        Near(thief, SocialLedger.MaraNpcId);
        check(thief.TakeCampRation().Accepted && theft.CampDecisionMade, "causal-parent-valid-theft-decides");
        var taken = theft.Social.Forge.Archive.Events.Single(e => e.Context == "camp.theft");
        var stolen = theft.Social.Forge.Archive.Events.Single(e => e.Context == "camp.decision.stolen");
        check(CausesAreSound(theft.Social.Forge.Archive) && stolen.CauseEventId == taken.Id && stolen.Tick == taken.Tick, "causal-parent-valid-theft");

        var gift = Village(6102, out var giver);
        DecideByDonation(giver, check, "causal-parent-valid");
        var archive = gift.Social.Forge.Archive;
        var donations = archive.Events.Where(e => e.Context == "camp.donation").ToArray();
        var decision = archive.Events.Single(e => e.Context == "camp.decision.replaced");
        check(CausesAreSound(archive) && donations.Length == 2 && decision.CauseEventId == donations[1].Id && decision.Tick == donations[1].Tick,
              "causal-parent-valid-donation-names-the-final-transaction");

        string saved = gift.SaveString();
        check(CharacterProgression.TryLoad(saved, out var loaded) && CausesAreSound(loaded.Social.Forge.Archive) &&
              loaded.Social.Forge.Archive.Events.Select(e => e.Checksum).SequenceEqual(archive.Events.Select(e => e.Checksum)) &&
              loaded.SaveString() == saved,
              "causal-parent-valid");
    }

    // ------------------------------------------------------------------ 2. causal-corrupt-valid-checksum-reject

    private static string B64(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
    private static string Text(string b64) => new UTF8Encoding(false, true).GetString(Convert.FromBase64String(b64));
    private static string Hash(string payload) => LootCatalog.HashText(payload).ToString("X16", CultureInfo.InvariantCulture);

    /// <summary>
    /// Rewrite the archive inside a save and recompute every checksum on the way out, exactly as the game writes them:
    /// the AC22 container, the AC21/AC19/AC18 wrappers, the AC17 forge wrapper and the archive's own token. What
    /// remains to catch an invalid cause is the meaning of the data, not a broken checksum.
    /// </summary>
    private static string RewriteArchive(string save, Func<string, string> archiveEdit)
    {
        string[] f = save.Split('|');
        switch (f[0])
        {
            case "AC22":
                f[2] = B64(RewriteArchive(Text(f[2]), archiveEdit));
                f[f.Length - 1] = Hash(string.Join("|", f, 0, f.Length - 1));
                return string.Join("|", f);
            case "AC21": case "AC19": case "AC18":
                f[1] = B64(RewriteArchive(Text(f[1]), archiveEdit));
                f[3] = Hash(string.Join("|", f, 0, 3));
                return string.Join("|", f);
            case "AC17":
                string forge = Text(f[2]);
                int a = forge.IndexOf(':'), b = forge.IndexOf(':', a + 1), c = forge.IndexOf(':', b + 1);
                f[2] = B64(forge.Substring(0, b + 1) + archiveEdit(forge.Substring(b + 1, c - b - 1)) + forge.Substring(c));
                f[3] = Hash(string.Join("|", f, 0, 3));
                return string.Join("|", f);
            default:
                throw new InvalidOperationException("No forge archive in a " + f[0] + " save");
        }
    }

    private static string ReadText(BinaryReader reader) { int n = reader.ReadInt32(); return Encoding.UTF8.GetString(reader.ReadBytes(n)); }
    private static void WriteText(BinaryWriter writer, string value) { var bytes = Encoding.UTF8.GetBytes(value); writer.Write(bytes.Length); writer.Write(bytes); }

    /// <summary>Give one event of an NMF archive token another cause and recompute that event's checksum and the token hash.</summary>
    private static string WithCause(string token, ulong eventId, ulong cause)
    {
        string[] parts = token.Split('.');
        byte[] bytes = Convert.FromBase64String(parts[1]);
        using var input = new MemoryStream(bytes, false);
        using var reader = new BinaryReader(input, new UTF8Encoding(false, true));
        using var output = new MemoryStream();
        using (var writer = new BinaryWriter(output, new UTF8Encoding(false, true), leaveOpen: true))
        {
            int schema = reader.ReadInt32(); writer.Write(schema);
            int ledger = schema;
            if (schema == 4) { ledger = reader.ReadByte(); writer.Write((byte)ledger); }
            if (ledger != 3) throw new InvalidOperationException("A causal archive uses ledger schema 3");
            int events = reader.ReadInt32(); writer.Write(events);
            for (int i = 0; i < events; i++)
            {
                ulong id = reader.ReadUInt64(); byte kind = reader.ReadByte();
                string actor = ReadText(reader), target = ReadText(reader);
                ulong tick = reader.ReadUInt64(); int importance = reader.ReadInt32(); byte essential = reader.ReadByte();
                string context = ReadText(reader);
                var impact = new int[8];
                for (int j = 0; j < 8; j++) impact[j] = reader.ReadInt32();
                ulong own = reader.ReadUInt64(); string checksum = ReadText(reader);
                if (id == eventId)
                {
                    own = cause;
                    checksum = new MemoryEvent(id, (MemoryKind)kind, actor, target, tick, importance, essential == 1, context,
                        new RelationshipChange(impact[0], impact[1], impact[2], impact[3], impact[4], impact[5], impact[6], impact[7]),
                        cause == 0 ? (ulong?)null : cause).Checksum;
                }
                writer.Write(id); writer.Write(kind); WriteText(writer, actor); WriteText(writer, target);
                writer.Write(tick); writer.Write(importance); writer.Write(essential); WriteText(writer, context);
                foreach (int value in impact) writer.Write(value);
                writer.Write(own); WriteText(writer, checksum);
            }
        }
        output.Write(bytes, (int)input.Position, bytes.Length - (int)input.Position);
        string payload = Convert.ToBase64String(output.ToArray());
        return parts[0] + "." + payload + "." + ContractText.Hash(payload);
    }

    private static void CorruptCauseWithValidChecksumsIsRejected(Action<bool, string> check)
    {
        var gift = Village(6103, out var giver);
        DecideByDonation(giver, check, "causal-corrupt");
        var archive = gift.Social.Forge.Archive;
        var donations = archive.Events.Where(e => e.Context == "camp.donation").ToArray();
        var decision = archive.Events.Single(e => e.Context == "camp.decision.replaced");
        string saved = gift.SaveString();
        string token = archive.Save();
        var registry = NpcForgeState.Registry;

        // The rewriting itself is faithful: the same cause gives back the very same bytes, which load.
        check(WithCause(token, decision.Id, decision.CauseEventId.Value) == token &&
              RewriteArchive(saved, t => WithCause(t, decision.Id, decision.CauseEventId.Value)) == saved &&
              CharacterProgression.TryLoad(saved, out _), "causal-corrupt-rewrite-is-faithful");

        var forged = new Dictionary<string, KeyValuePair<ulong, ulong>>
        {
            { "first-partial-donation", new KeyValuePair<ulong, ulong>(decision.Id, donations[0].Id) },
            { "unrelated-earlier-event", new KeyValuePair<ulong, ulong>(decision.Id, 1) },
            { "itself", new KeyValuePair<ulong, ulong>(decision.Id, decision.Id) },
            { "later-or-foreign-event", new KeyValuePair<ulong, ulong>(decision.Id, (ulong)archive.Events.Count + 7) },
            { "no-cause", new KeyValuePair<ulong, ulong>(decision.Id, 0) },
            { "cause-on-a-root-transaction", new KeyValuePair<ulong, ulong>(donations[1].Id, donations[0].Id) },
        };
        foreach (var entry in forged)
        {
            ulong id = entry.Value.Key, cause = entry.Value.Value;
            string forgedToken = WithCause(token, id, cause);
            string forgedSave = RewriteArchive(saved, t => WithCause(t, id, cause));
            check(forgedToken != token && !MemoryArchive.TryLoad(forgedToken, registry, out _) &&
                  forgedSave != saved && !CharacterProgression.TryLoad(forgedSave, out _),
                  "causal-corrupt-valid-checksum-reject-" + entry.Key);
        }
        check(CharacterProgression.TryLoad(saved, out var still) && still.SaveString() == saved, "causal-corrupt-valid-checksum-reject");
    }

    // ------------------------------------------------------------------ 3. causal-single-transaction

    private static void TransactionsHappenOnce(Action<bool, string> check)
    {
        var p = Village(6104, out var world);
        var archive = p.Social.Forge.Archive;
        BigInteger provisions = p.Social.Provisions, coins = p.Social.Coins;
        int camp = p.Social.CampProvisions;
        Near(world, SocialLedger.MaraNpcId);
        check(world.DonateCampRation().Accepted, "causal-single-transaction-donation-accepted");
        BigInteger provisionsAfter = p.Social.Provisions;
        int campAfter = p.Social.CampProvisions;
        var donation = archive.Events.Single(e => e.Context == "camp.donation");
        check(provisionsAfter == provisions - 1 && campAfter == camp + 1 && p.Social.Coins == coins,
              "causal-single-transaction-moves-one-ration");

        // Mara tells Ilyan at a real village meeting, the player hears it from Ilyan, the game is saved and loaded.
        var mara = world.Npcs.Single(n => n.Id == SocialLedger.MaraNpcId);
        var ilyan = world.Npcs.Single(n => n.Id == Ilyan);
        var dorn = world.Npcs.Single(n => n.Id == Dorn);
        for (int tick = 0; tick < 200 && archive.Get("ilyan").Knowledge(donation.Id) == null; tick++)
        {
            ilyan.Xmm = mara.Xmm + 850; ilyan.Zmm = mara.Zmm;
            dorn.Xmm = mara.Xmm + 12000; dorn.Zmm = mara.Zmm;
            world.Step();
        }
        check(archive.Get("ilyan").Knowledge(donation.Id)?.Origin == KnowledgeOrigin.Reported, "causal-single-transaction-report-travels");
        Near(world, Ilyan);
        var greeting = world.InteractNpc(Ilyan, NpcAction.Greet);
        check(greeting.Accepted && greeting.ChronicleEventId == donation.Id, "causal-single-transaction-player-hears-it");
        check(CharacterProgression.TryLoad(p.SaveString(), out var loaded), "causal-single-transaction-save-loads");
        var again = new ArenaSimulation(6104, loaded, BigInteger.One, LootCatalog.Version1);
        for (int tick = 0; tick < 400; tick++) again.Step();
        var after = loaded.Social.Forge.Archive;
        check(loaded.Social.Provisions == provisionsAfter && loaded.Social.CampProvisions == campAfter && loaded.Social.Coins == coins &&
              after.Events.Count(e => e.Context == "camp.donation") == 1 && after.Events.Count(e => e.Kind == MemoryKind.Help || e.Kind == MemoryKind.Theft) == 1,
              "causal-single-transaction");

        // Once decided, later attempts are refused without touching the world, the save or any event id.
        Near(again, SocialLedger.MaraNpcId);
        check(again.DonateCampRation().Accepted && loaded.CampDecisionMade, "causal-single-transaction-final-donation-decides");
        Near(again, SocialLedger.MaraNpcId);
        string save = loaded.SaveString(); ulong hash = again.ComputeWorldHash(); int events = loaded.Social.Forge.Archive.Events.Count;
        Func<bool> unchanged = () => loaded.SaveString() == save && again.ComputeWorldHash() == hash && loaded.Social.Forge.Archive.Events.Count == events;
        // Each refusal is compared with the state before it, so two refusals cannot hide a change by undoing each other.
        bool donationRefused = !again.DonateCampRation().Accepted && unchanged();
        check(donationRefused, "causal-single-transaction-refused-donation-changes-nothing");
        bool theftRefused = !again.TakeCampRation().Accepted && unchanged();
        check(theftRefused, "causal-single-transaction-refused-theft-changes-nothing");
        check(donationRefused && theftRefused, "causal-single-transaction-refused-actions-change-nothing");
    }

    // ------------------------------------------------------------------ 4. causal-migration-current

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name)).Trim();

    /// <summary>The checksum the AC1–AC15 line formats carry in their last field.</summary>
    private static string Signed(string payload) => payload + "|" + Hash(payload);

    /// <summary>
    /// Real saves of every format the game reads: AC1–AC4 from the oldest tests, AC5 and AC17–AC20 from the historical
    /// fixtures, NMF1/NMF2 archives inside the road fixtures, the AC22 container, and the formats the current game still
    /// writes (fixture <c>causal-migration-current.txt</c>, saves of real game states). AC6 and AC13 stay reserved.
    /// </summary>
    private static List<KeyValuePair<string, string>> MigrationSaves()
    {
        var saves = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("AC1", "AC1|160|8|1|1|83E7CB814DDECFE9"),
            new KeyValuePair<string, string>("AC2", "AC2|160|8|1|1|1|0|0|FA028359B173B723"),
            new KeyValuePair<string, string>("AC3", Signed("AC3|160|8|1|1|1|0|0|2")),
            new KeyValuePair<string, string>("AC4", Signed("AC4|160|8|1|1|1|0|0|2|150|0|0|0")),
        };
        string[] historical = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "causal-legacy-saves.txt"));
        foreach (string token in historical) saves.Add(new KeyValuePair<string, string>(token.Substring(0, token.IndexOf('|')), token));
        saves.Add(new KeyValuePair<string, string>("AC17-NMF1", Fixture("road-before-meeting-NMF1.txt")));
        saves.Add(new KeyValuePair<string, string>("AC17-NMF2", Fixture("road-before-meeting-NMF2.txt")));
        saves.Add(new KeyValuePair<string, string>("AC22", Fixture("economy-ac22-v1.txt")));
        foreach (string line in File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "causal-migration-current.txt")))
            if (line.Length > 0 && line[0] != '#') saves.Add(new KeyValuePair<string, string>(line.Substring(0, line.IndexOf('\t')), line.Substring(line.IndexOf('\t') + 1)));
        return saves;
    }

    private static bool Load(string token, out CharacterProgression progress)
    {
        return token.StartsWith("AC20|", StringComparison.Ordinal)
            ? CharacterProgression.TryLoad(token, JourneyPilotTests.Maps(), out progress)
            : CharacterProgression.TryLoad(token, out progress);
    }

    /// <summary>The NMF2 road checkpoint carries an admitted non-default guard controller (as RoadReportTests admits it).</summary>
    private static void AdmitRoadReportGuard()
    {
        var registry = NpcForgeState.Registry;
        var guard = registry.BindNew("guard");
        var next = new ControllerArtifact(guard.Archetype, "tests.road-report.guard", 1, guard.Rules);
        if (registry.TryRestore("guard", next.ControllerId, next.Version, next.Hash, out _)) return;
        var wait = new ObservationFrame(ActionSet.Of(ForgeAction.Wait), Array.Empty<Observation>());
        var receipt = ControllerAdmission.Run(next, "tests.road-report", new[] { new RegressionCase(wait, ForgeAction.Wait) });
        registry.Register(next, next.Hash, receipt, false);
    }

    private static void CurrentAndHistoricalFormatsStayValid(Action<bool, string> check)
    {
        check(Hash("AC1|160|8|1|1") == "83E7CB814DDECFE9", "causal-migration-legacy-checksum-understood");
        AdmitRoadReportGuard();
        var saves = MigrationSaves();
        bool all = true;
        foreach (var save in saves)
        {
            string label = save.Key, token = save.Value;
            bool legacy = token.StartsWith("AC1|", StringComparison.Ordinal) || token.StartsWith("AC2|", StringComparison.Ordinal) ||
                          token.StartsWith("AC3|", StringComparison.Ordinal) || token.StartsWith("AC4|", StringComparison.Ordinal);
            string failure = "";
            if (!Load(token, out var first)) failure = "does-not-load";
            string rewritten = failure == "" ? first.SaveString() : "";
            // The oldest line formats are read and upgraded; everything newer is written back byte for byte.
            if (failure == "" && (legacy ? rewritten == token : rewritten != token)) failure = legacy ? "not-upgraded" : "not-byte-identical";
            if (failure == "" && (!Load(rewritten, out var second) || second.SaveString() != rewritten)) failure = "rewrite-unstable";
            if (failure == "" && first.Social.Forge != null)
            {
                Load(rewritten, out var third);
                if (third?.Social.Forge == null || third.Social.Forge.Archive.Save() != first.Social.Forge.Archive.Save()) failure = "archive-changed";
                else if (!CausesAreSoundOrHistorical(third.Social.Forge.Archive)) failure = "unsound-cause";
            }
            if (failure == "" && !token.StartsWith("AC20|", StringComparison.Ordinal))
            {
                Load(rewritten, out var fourth);
                if (new ArenaSimulation(4242, first, BigInteger.One, LootCatalog.Version1).ComputeWorldHash() !=
                    new ArenaSimulation(4242, fourth, BigInteger.One, LootCatalog.Version1).ComputeWorldHash()) failure = "world-hash-differs";
            }
            bool ok = failure == "";
            check(ok, "causal-migration-current-" + label + (ok ? "" : "-" + failure));
            all &= ok;
        }
        // AC6 and AC13 are reserved: a save under those names is refused even with a correct checksum.
        string ac5 = saves.Single(s => s.Key == "AC5").Value;
        string body = ac5.Substring(0, ac5.LastIndexOf('|'));
        bool reserved = !CharacterProgression.TryLoad(Signed("AC6" + body.Substring(3)), out _) &&
                        !CharacterProgression.TryLoad(Signed("AC13" + body.Substring(3)), out _) &&
                        CharacterProgression.TryLoad(Signed(body), out _);
        check(reserved, "causal-migration-reserved-ac6-ac13-refused");

        // Exactly one current save for every format the game still writes; historical inputs do not count here.
        string[] expected = { "AC5", "AC7", "AC8", "AC9", "AC10", "AC11", "AC12", "AC14", "AC15", "AC16", "AC17", "AC18", "AC19", "AC20", "AC21", "AC22" };
        var current = saves.Where(s => s.Key.EndsWith("-current", StringComparison.Ordinal))
            .Select(s => new { Format = s.Key.Substring(0, s.Key.Length - "-current".Length), Token = s.Value }).ToArray();
        var found = current.Select(c => c.Format).ToArray();
        string problems = string.Join("-", expected.Except(found).Select(f => "missing-" + f)
            .Concat(found.GroupBy(f => f).Where(g => g.Count() > 1).Select(g => "duplicate-" + g.Key))
            .Concat(found.Except(expected).Select(f => "unexpected-" + f))
            .Concat(current.Where(c => !c.Token.StartsWith(c.Format + "|", StringComparison.Ordinal)).Select(c => "wrong-prefix-" + c.Format)));
        bool formats = problems.Length == 0;
        check(formats, "causal-migration-current-formats" + (formats ? "" : "-" + problems));
        check(all && reserved && formats, "causal-migration-current");
    }

    /// <summary>Historical archives were written before causes existed; a cause, where present, must be sound.</summary>
    private static bool CausesAreSoundOrHistorical(MemoryArchive archive)
    {
        if (archive.Events.All(e => !e.CauseEventId.HasValue)) return true;
        return CausesAreSound(archive);
    }

    // ------------------------------------------------------------------ 5. causal-replay-10000

    private static string Ledger(MemoryArchive archive) =>
        string.Join(";", archive.Events.Select(e => e.Id + ":" + e.Checksum)) + "#" +
        string.Join(";", archive.Acquisitions.Select(k => k.Sequence + ":" + k.Knower + ":" + (int)k.Origin + ":" + k.Checksum));

    private static void TenThousandTickCausalReplay(Action<bool, string> check)
    {
        var first = Village(6105, out var a);
        var second = Village(6105, out var b);
        DecideByDonation(a, check, "causal-replay-first-world");
        DecideByDonation(b, check, "causal-replay-second-world");
        bool same = true;
        for (int tick = 0; tick < 10000; tick++)
        {
            if (tick < 300)
                foreach (var w in new[] { a, b })
                {
                    // The same real meeting in both worlds: Mara and Ilyan stand together, Dorn far away.
                    var mara = w.Npcs.Single(n => n.Id == SocialLedger.MaraNpcId);
                    var ilyan = w.Npcs.Single(n => n.Id == Ilyan);
                    var dorn = w.Npcs.Single(n => n.Id == Dorn);
                    ilyan.Xmm = mara.Xmm + 850; ilyan.Zmm = mara.Zmm;
                    dorn.Xmm = mara.Xmm + 12000; dorn.Zmm = mara.Zmm;
                }
            a.Step(); b.Step();
            same &= a.ComputeWorldHash() == b.ComputeWorldHash();
            if (tick % 500 == 499)
            {
                same &= Ledger(first.Social.Forge.Archive) == Ledger(second.Social.Forge.Archive) && first.SaveString() == second.SaveString();
                check(same, "causal-replay-10000-identical-at-" + (tick + 1));
            }
            if (tick == 3333 || tick == 6666)
            {
                // Cold loads in the middle: both worlds continue from their own saved bytes.
                string savedA = first.SaveString(), savedB = second.SaveString();
                check(savedA == savedB && CharacterProgression.TryLoad(savedA, out var loadedA) && CharacterProgression.TryLoad(savedB, out var loadedB) &&
                      loadedA.SaveString() == savedA && loadedB.SaveString() == savedB, "causal-replay-10000-cold-load-" + tick);
                CharacterProgression.TryLoad(savedA, out first);
                CharacterProgression.TryLoad(savedB, out second);
                a = new ArenaSimulation(6105, first, BigInteger.One, LootCatalog.Version1);
                b = new ArenaSimulation(6105, second, BigInteger.One, LootCatalog.Version1);
            }
        }
        var archive = first.Social.Forge.Archive;
        var decision = archive.Events.Single(e => e.Context == "camp.decision.replaced");
        check(same && CausesAreSound(archive) && archive.Acquisitions.Any(k => k.Event.Id == decision.CauseEventId && k.Origin == KnowledgeOrigin.Reported) &&
              a.ComputeWorldHash() == b.ComputeWorldHash() && first.SaveString() == second.SaveString() && archive.Save() == second.Social.Forge.Archive.Save(),
              "causal-replay-10000");
    }

    // ------------------------------------------------------------------ 6. causal-reported-knowledge

    private static void ReportedKnowledgeNeedsARealMeeting(Action<bool, string> check)
    {
        var p = Village(6106, out var world);
        var archive = p.Social.Forge.Archive;
        Near(world, SocialLedger.MaraNpcId);
        check(world.DonateCampRation().Accepted, "causal-reported-knowledge-donation-accepted");
        var donation = archive.Events.Single(e => e.Context == "camp.donation");
        var mara = world.Npcs.Single(n => n.Id == SocialLedger.MaraNpcId);
        var ilyan = world.Npcs.Single(n => n.Id == Ilyan);
        var dorn = world.Npcs.Single(n => n.Id == Dorn);
        int events = archive.Events.Count;
        BigInteger provisions = p.Social.Provisions;
        int camp = p.Social.CampProvisions;

        for (int tick = 0; tick < 300; tick++)
        {
            ilyan.Xmm = mara.Xmm + 15000; ilyan.Zmm = mara.Zmm;
            dorn.Xmm = mara.Xmm - 15000; dorn.Zmm = mara.Zmm;
            world.Step();
        }
        check(archive.Get("ilyan").Knowledge(donation.Id) == null && archive.Get("dorn").Knowledge(donation.Id) == null,
              "causal-reported-knowledge-needs-a-meeting");

        for (int tick = 0; tick < 200 && archive.Get("ilyan").Knowledge(donation.Id) == null; tick++)
        {
            ilyan.Xmm = mara.Xmm + 850; ilyan.Zmm = mara.Zmm;
            dorn.Xmm = mara.Xmm - 15000; dorn.Zmm = mara.Zmm;
            world.Step();
        }
        var heard = archive.Get("ilyan").Knowledge(donation.Id);
        check(heard != null && heard.Origin == KnowledgeOrigin.Reported && heard.Source == "mara", "causal-reported-knowledge-at-a-meeting");
        check(archive.Events.Count == events && p.Social.Provisions == provisions && p.Social.CampProvisions == camp,
              "causal-reported-knowledge-no-second-mutation");
        // The archive itself refuses to pass hearsay on: Ilyan can recall the donation but only heard of it, so his
        // report to Dorn is refused and writes nothing, whatever the village code would ask for.
        ulong now = p.Social.Forge.ProcessedTicks;
        int acquisitions = archive.Acquisitions.Count;
        bool recalls = archive.Get("ilyan").Cognition.Recall(donation.Id, now) != null;
        bool relayed = archive.Report("ilyan", "dorn", donation.Id, now);
        check(recalls && !relayed && archive.Acquisitions.Count == acquisitions && archive.Get("dorn").Knowledge(donation.Id) == null,
              "causal-reported-knowledge-hearsay-is-not-relayed");

        // Everybody together for a long while: nobody learns an event twice, hearsay is never passed on again, Ilyan
        // never turns into an eyewitness, and the archive does not grow without end.
        for (int tick = 0; tick < 2000; tick++)
        {
            ilyan.Xmm = mara.Xmm + 850; ilyan.Zmm = mara.Zmm;
            dorn.Xmm = ilyan.Xmm + 850; dorn.Zmm = ilyan.Zmm;
            world.Step();
        }
        var forEvent = archive.Acquisitions.Where(k => k.Event.Id == donation.Id).ToArray();
        bool noLaundering = forEvent.Where(k => k.Origin == KnowledgeOrigin.Reported)
            .All(k => archive.Get(k.Source)?.Knowledge(donation.Id) is KnownMemory source && source.Origin != KnowledgeOrigin.Reported);
        check(forEvent.Select(k => k.Knower).Distinct().Count() == forEvent.Length && noLaundering &&
              archive.Get("ilyan").Knowledge(donation.Id).Origin == KnowledgeOrigin.Reported &&
              archive.Get("ilyan").Knowledge(donation.Id).Sequence == heard.Sequence &&
              archive.Events.Count == events && p.Social.Provisions == provisions && p.Social.CampProvisions == camp,
              "causal-reported-knowledge");
    }
}
