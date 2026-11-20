using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AshenCrypt.MemoryForge;

internal static class CausalArchiveTests
{
    // Produced by the pre-causality MemoryArchive.Save() at cd5ae8a7, before the new writer existed.
    // NMF1 contains a historical stolen decision followed by the theft. NMF2 includes an admitted guard migration.
    private const string LegacyTheft = "NMF1.AQAAAAIAAAABAAAAAAAAAAYGAAAAcGxheWVyBAAAAG1hcmEJAAAAAAAAAKAPAAABFAAAAGNhbXAuZGVjaXNpb24uc3RvbGVuAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAAAAOWZiZGJhMjVjNzc4M2MwYjhlNGZjYTgwMGZlYTg3ZjRiMWY1YjkwZTBhNDk5ZWNlZTM2MmUwMjk1MTMxNTZmZAIAAAAAAAAAAwYAAABwbGF5ZXIEAAAAbWFyYQkAAAAAAAAAQB8AAAAKAAAAY2FtcC50aGVmdDD4///cBQAAAAAAAAAAAAAAAAAA0AcAAMQJAAD0AQAAQAAAADE1MjhlMTkxOGI4MDE3ZWFhMTM1OTY2NzQzNDUwNGJkNDlhMTMxZTk2YTg4MGNiY2UzMDkyMzUwMDYzODJhZjkBAAAABAAAAG1hcmEIAAAAbWVyY2hhbnQOAAAAYXNoZW4ubWVyY2hhbnQBAAAAQAAAADdhMTRkMzhlNGJkNjlmYjhiNjAxN2Q3M2FlMTczYTI4MTQ1MzBhOWZlYjI0NTYzOGRhZDFlZjU0YzUwOThhYTQAAQAAAAEAAAAAAAAAAgAAAAAAAAAEAAAAbWFyYQAEAAAAbWFyYQAAAAAAAAAACQAAAAAAAAAQJwAAECcAAEAAAABkMjg3MjU5ZjQwMWJlNTFlODE5YmJjMTMwOTQwMWNmZDRiYTc0ZTUxYTI3MGQ4OWZiNzQ5ZTAwODQwZDg2MTc2.c4a31422840952a5477d7b2ff6f9ba52bb2b9edd2c397e621487c2cb76cb97e6";
    private const string LegacyMigration = "NMF2.AgAAAAEAAAABAAAAAAAAAAEGAAAAcGxheWVyBAAAAGRvcm4DAAAAAAAAAKAPAAAACgAAAHByb3Zpc2lvbnNYGwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAAABhZjRlMTg4NmNkNmI4NzI5NDdkOTk4NjIxOWQ0MDA0YTA1OTMwMzFkZjQxNWY4NWE3MDVhNDk3YzYyMWI3YjM5AQAAAAQAAABkb3JuBQAAAGd1YXJkCwAAAGFzaGVuLmd1YXJkAgAAAEAAAABiY2I4ZWRmM2Q3MzVjN2NhNjI4NDBjZDI3NDdhNjk3MWU5MDA2ZGIyOWNiMzMyMmI3NDUzZjZiYTBhM2YzZWQ3AAEAAAABAAAAAAAAAAEAAAAAAAAABAAAAGRvcm4ABAAAAGRvcm4AAAAAAAAAAAMAAAAAAAAAECcAABAnAABAAAAAODhmZGU0MzMyNWRkODk1NTJiYmY0ZWE1ZGM1MTBhYWIwMTA1OTczMmM3OTRlYjhjZDY4NTVmMjg0MThjYzVmMgEAAAABAAAAAAAAAAQAAABkb3JuBAAAAAAAAAAFAAAAZ3VhcmQLAAAAYXNoZW4uZ3VhcmQBAAAAQAAAAGVhYmFkNDIyYjgxMjdmMDUwNTk0ZWIyMzI3OTAxNjUyYjNmYzc5Mjg2ODI3ZTcwNTFmYzYwOWMxMGM1YTE4OWMFAAAAZ3VhcmQLAAAAYXNoZW4uZ3VhcmQCAAAAQAAAAGJjYjhlZGYzZDczNWM3Y2E2Mjg0MGNkMjc0N2E2OTcxZTkwMDZkYjI5Y2IzMzIyYjc0NTNmNmJhMGEzZjNlZDdAAAAAZjYxNDEyYjkzNDgzNWZkMjBjNGNjZWY0YWRmYmI5YjcyMWMzNzRjYjU4NjI3ZDJhYTVhNTEyZmI4ZTRhMmZjMkAAAAAwODcxMWFiZTU0ZDI1OWYxMzdkODk0YjEwYTQ1OTc3MTQ4YzMzZTQ5N2M1ZmRmMzI0Yjg5YzkyZDcxNmYyN2M0.8e7c28f42c29f8c726dbbe8ecaa76030c442e561a5691f935375d16d2559a2b2";

    public static void Run(Action<bool, string> check)
    {
        var registry = ForgeDefaults.CreateRegistry();
        var theft = new WireEvent(1, 9, MemoryKind.Theft, "player", "mara", "camp.theft", 8000, false, 0);
        var consequence = new WireEvent(2, 9, MemoryKind.Quest, "player", "mara", "camp.decision.stolen", 4000, true, 1);
        string canonical = Token(theft, consequence);
        bool read = MemoryArchive.TryLoad(canonical, registry, out var archive);
        check(read && archive.Save() == canonical && archive.Events[1].CauseEventId == 1UL,
            "causal-valid-independently-encoded-nmf3-roundtrips");

        // Never rely on checksum failure: the encoder signs the actual adversarial contents at BOTH levels.
        void Reject(string label, params WireEvent[] events)
        {
            check(!MemoryArchive.TryLoad(Token(events), registry, out var invalid) && invalid == null,
                "causal-valid-checksums-reject-" + label);
        }
        Reject("nonexistent-parent", theft, consequence with { Cause = 100 });
        Reject("self-parent", theft, consequence with { Cause = 2 });
        Reject("forward-parent", consequence with { Id = 1, Cause = 2 }, theft with { Id = 2 });
        Reject("cycle", theft with { Cause = 2 }, consequence);
        Reject("later-tick", theft with { Tick = 10 }, consequence);
        Reject("wrong-action-kind", theft with { Kind = MemoryKind.Greeting }, consequence);
        Reject("wrong-action-context", theft with { Context = "road.theft" }, consequence);
        Reject("wrong-source-actor", theft with { Actor = "ilyan" }, consequence);
        Reject("wrong-source-target", theft with { Target = "dorn" }, consequence);
        Reject("wrong-decision-actor", theft, consequence with { Actor = "dorn" });
        Reject("wrong-decision-target", theft, consequence with { Target = "ilyan" });
        Reject("story-root-with-forged-cause", theft, consequence with { Context = "story.hearilyan" });
        Reject("duplicate-event-id", theft, consequence with { Id = 1 });
        Reject("out-of-order-event-ids", theft with { Id = 2 }, consequence with { Id = 1, Cause = 2 });
        Reject("nmf3-with-no-links", theft, consequence with { Cause = 0 });

        var donation = theft with { Kind = MemoryKind.Help, Context = "camp.donation", Importance = 5000 };
        var replaced = consequence with { Id = 3, Context = "camp.decision.replaced", Cause = 2 };
        check(MemoryArchive.TryLoad(Token(donation, donation with { Id = 2 }, replaced), registry, out var finalDonation) &&
            finalDonation.Events[2].CauseEventId == 2UL, "causal-two-donations-final-receipt-is-accepted");
        Reject("earlier-donation-as-final", donation, donation with { Id = 2 }, replaced with { Cause = 1 });

        var reconciled = new WireEvent(2, 9, MemoryKind.Visit, "player", "mara", "npc.reconciled", 5000, true, 0);
        var refilled = new WireEvent(3, 9, MemoryKind.Quest, "player", "mara", "quest.camp.refilled", 6000, true, 0);
        var afterBoth = replaced with { Id = 4, Cause = 1 };
        check(MemoryArchive.TryLoad(Token(donation, reconciled, refilled, afterBoth), registry, out _) &&
            MemoryArchive.TryLoad(Token(donation, reconciled, replaced with { Cause = 1 }), registry, out _) &&
            MemoryArchive.TryLoad(Token(donation, refilled with { Id = 2 }, replaced with { Cause = 1 }), registry, out _),
            "causal-permits-optional-real-effects-before-refill-decision");
        Reject("reversed-refill-effects", donation, refilled with { Id = 2 }, reconciled with { Id = 3 }, afterBoth);
        Reject("duplicated-reconciliation", donation, reconciled, reconciled with { Id = 3 }, afterBoth);
        Reject("duplicated-refill", donation, refilled with { Id = 2 }, refilled, afterBoth);
        Reject("wrong-effect-target", donation, reconciled with { Target = "ilyan" }, replaced with { Cause = 1 });
        Reject("intervening-effect-after-theft", theft, reconciled, consequence with { Id = 3 });
        Reject("camp-action-from-previous-tick", theft with { Tick = 8 }, consequence);
        Reject("second-camp-decision", theft, consequence, theft with { Id = 3 }, consequence with { Id = 4, Cause = 3 });
        Reject("missing-parent-in-linked-archive", theft, consequence, theft with { Id = 3 }, consequence with { Id = 4, Cause = 0 });

        var active = new MemoryArchive();
        var intact = active.Save();
        bool rejected = false;
        try { active.Append(MemoryKind.Quest, "player", "mara", 9, 4000, true, "camp.decision.stolen", new RelationshipChange(), 42); }
        catch (ArgumentException) { rejected = true; }
        check(rejected && active.Save() == intact &&
            active.Append(MemoryKind.Visit, "player", "mara", 9, 100, false, "village.visit", new RelationshipChange()).Id == 1,
            "causal-invalid-live-append-consumes-no-id-and-commits-no-bytes");


        bool legacy = MemoryArchive.TryLoad(LegacyTheft, registry, out var old);
        check(legacy && old.Save() == LegacyTheft && old.Events.All(ev => !ev.CauseEventId.HasValue) &&
            old.Events[0].Context == "camp.decision.stolen" && old.Events[1].Id == 2 &&
            old.Get("mara").Knowledge(2).Event == old.Events[1] &&
            old.Get("mara").RelationshipTo("player").Trust == -2000,
            "causal-historical-story-before-action-keeps-bytes-ids-and-no-invented-links");

        var guard = registry.BindNew("guard");
        var v2 = new ControllerArtifact(guard.Archetype, guard.ControllerId, 2, guard.Rules);
        var wait = new ObservationFrame(ActionSet.Of(ForgeAction.Wait), Array.Empty<Observation>());
        var admission = ControllerAdmission.Run(v2, "tests.causal-migrate", new[] { new RegressionCase(wait, ForgeAction.Wait) });
        registry.Register(v2, v2.Hash, admission, false);
        check(MemoryArchive.TryLoad(LegacyMigration, registry, out var migrated) && migrated.Save() == LegacyMigration &&
            migrated.Events[0].CauseEventId == null && migrated.Migrations.Count == 1,
            "causal-historical-nmf2-with-migration-roundtrips-byte-exact");
        if (migrated == null) return;

        var witness = migrated.Get("dorn");
        var previousKnowledge = witness.Knowledge(1);
        var receipt = migrated.Migrations[0];
        var incident = migrated.Append(MemoryKind.Theft, "player", "mara", 9, 8000, false, "camp.theft", new RelationshipChange());
        var child = migrated.Append(MemoryKind.Quest, "player", "mara", 9, 4000, true, "camp.decision.stolen", new RelationshipChange(), incident.Id);
        check(migrated.Learn("dorn", incident.Id, KnowledgeOrigin.Witnessed, "dorn", 9), "causal-new-witness-is-accepted");
        string upgraded = migrated.Save();
        check(upgraded.StartsWith("NMF3.", StringComparison.Ordinal) && MemoryArchive.TryLoad(upgraded, registry, out var copy) &&
            copy.Save() == upgraded && copy.Events[2].CauseEventId == incident.Id && copy.Events[2].Checksum == child.Checksum &&
            copy.Get("dorn").Controller.Hash == v2.Hash && copy.Migrations.Single().Checksum == receipt.Checksum &&
            copy.Get("dorn").Knowledge(1).Checksum == previousKnowledge.Checksum &&
            !copy.Learn("dorn", incident.Id, KnowledgeOrigin.Witnessed, "dorn", 10) &&
            copy.Get("dorn").RelationshipTo("player").Trust == 7000 && copy.Save() == upgraded,
            "causal-nmf2-upgrade-preserves-controller-migration-knowledge-and-dedupe");
        check(!MemoryArchive.TryLoad(upgraded, ForgeDefaults.CreateRegistry(), out _),
            "causal-nmf3-cannot-substitute-missing-historical-controller");
    }

    // Explicit binary contract. No production reader, event factory or Save() is used to construct forged tokens.
    private sealed record WireEvent(ulong Id, ulong Tick, MemoryKind Kind, string Actor, string Target,
        string Context, int Importance, bool Essential, ulong Cause);

    private static string Token(params WireEvent[] events)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write(3);
            writer.Write(events.Length);
            foreach (var e in events)
            {
                writer.Write(e.Id);
                writer.Write((byte)e.Kind);
                WriteText(writer, e.Actor);
                WriteText(writer, e.Target);
                writer.Write(e.Tick);
                writer.Write(e.Importance);
                writer.Write(e.Essential);
                WriteText(writer, e.Context);
                for (int i = 0; i < 8; ++i) writer.Write(0);
                writer.Write(e.Cause);
                string header = FormattableString.Invariant($"{e.Id},{e.Tick},{(int)e.Kind},{e.Actor},{e.Target},{e.Context},{e.Importance},{(e.Essential ? 1 : 0)},0,0,0,0,0,0,0,0");
                string cause = e.Cause == 0 ? "" : "|cause:" + e.Cause.ToString(CultureInfo.InvariantCulture);
                WriteText(writer, ContractText.Hash(header + cause));
            }
            writer.Write(0); // minds
            writer.Write(0); // acquisitions
            writer.Write(0); // migrations
        }
        string payload = Convert.ToBase64String(stream.ToArray());
        return "NMF3." + payload + "." + ContractText.Hash(payload);
    }

    private static void WriteText(BinaryWriter writer, string text)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(text);
        writer.Write(utf8.Length);
        writer.Write(utf8);
    }
}
