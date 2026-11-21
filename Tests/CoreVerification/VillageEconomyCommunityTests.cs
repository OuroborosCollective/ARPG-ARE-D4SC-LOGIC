using System;
using System.Linq;
using AreArpg.Core;
using AshenCrypt.MemoryForge;

internal static class VillageEconomyCommunityTests
{
    public static void Run(Action<bool, string> check)
    {
        GiftAndSource(check);
        StoreDelivery(check, false);
        StoreDelivery(check, true);
        WaterAtHome(check);
        ReportsComeHome(check);
        TheMillSendsItsOwnReport(check);
        LocalGuard(check);
    }

    private static ArenaSimulation Village(int newResidents)
    {
        var profile = CharacterProgression.CreateEntryProfile();
        profile.EnterVillage();
        profile.Social.EnableForge();
        var world = new ArenaSimulation(77, profile, 1, LootCatalog.Version1);
        for (int i = 0; i < newResidents; i++) world.CompleteMainQuest();
        for (int i = 0; i < 10; i++) world.Step();
        return world;
    }

    private static void GiftAndSource(Action<bool, string> check)
    {
        var world = Village(1);
        var p = world.Progression;
        var forge = p.Social.Forge;
        HunterEncounterTests.Move(world, VillageEconomyLayout.StandXmm(VillageSpot.Board), VillageEconomyLayout.StandZmm(VillageSpot.Board));
        // A controlled meeting in the real village. The public transfer still checks player reach and debits their pack.
        var mara = world.Npcs.Single(n => n.Id == 1);
        var dorn = world.Npcs.Single(n => n.Id == 2);
        var ilyan = world.Npcs.Single(n => n.Id == 0);
        mara.Xmm = world.PlayerXmm; mara.Zmm = world.PlayerZmm;
        dorn.Xmm = 0; dorn.Zmm = 12000;
        ilyan.Xmm = 9000; ilyan.Zmm = 10000;
        p.Crafting.AddMaterial(MaterialKind.Holz, 1);
        int before = p.Economy.Commons(VillageGood.Wood);
        check(world.GiveToVillage(VillageGood.Wood, 1).Accepted && p.Economy.Commons(VillageGood.Wood) == before + 1 &&
            p.Crafting.MaterialCount(MaterialKind.Holz) == 0, "community-physical-gift-counted-once");
        var gift = forge.Archive.Events.Last(e => e.Context == VillageEconomyState.GiftContext);
        check(mara.Mind.Knowledge(gift.Id)?.Origin == KnowledgeOrigin.Witnessed && dorn.Mind.Knowledge(gift.Id) == null &&
            ilyan.Mind.Knowledge(gift.Id) == null && mara.Mind.RelationshipTo("player").Trust > 0, "community-only-present-people-see-the-gift");
        int eventCount = forge.Archive.Events.Count;
        check(!world.GiveToVillage(VillageGood.Wood, 1).Accepted && forge.Archive.Events.Count == eventCount, "community-no-good-no-social-credit");

        // Keep the two people within conversational range until their next talk opportunity.
        dorn.Xmm = mara.Xmm; dorn.Zmm = mara.Zmm;
        for (int t = 0; t < 110 && dorn.Mind.Knowledge(gift.Id) == null; t++) world.Step();
        var heard = dorn.Mind.Knowledge(gift.Id);
        check(heard?.Origin == KnowledgeOrigin.Reported && heard.Source == "mara" && heard.SourceSequence == mara.Mind.Knowledge(gift.Id).Sequence,
            "community-local-conversation-proves-the-source");
        var answer = world.InteractNpc(2, NpcAction.Greet);
        check(answer.Accepted && answer.Text.Contains("Mir wurde erzählt") && ilyan.Mind.Knowledge(gift.Id) == null,
            "community-actual-greeting-distinguishes-hearsay");
        check(CharacterProgression.TryLoad(p.SaveString(), out var loaded) && loaded.Social.Forge.Archive.Get("dorn").Knowledge(gift.Id).SourceSequence == heard.SourceSequence,
            "community-source-survives-save");
    }

    private static void WaterAtHome(Action<bool, string> check)
    {
        var world = Village(2);
        var p = world.Progression;
        var helper = p.Economy.Residents[0];
        var patient = p.Economy.Residents[1];
        patient.Health = 400;
        patient.Thirst = 900;
        var clocks = p.Social.Forge;
        long beforeUse = p.Economy.Used(VillageGood.Water);
        for (int i = 0; i < 12000 && !clocks.Archive.Events.Any(e => e.Context == VillageEconomyState.WaterAidContext); i++) world.Step();
        var aid = clocks.Archive.Events.FirstOrDefault(e => e.Context == VillageEconomyState.WaterAidContext);
        check(aid != null && aid.Actor == helper.Identity && aid.Target == patient.Identity, "community-healthy-neighbor-actually-brings-water-home");
        check(patient.Thirst < VillageEconomyState.DrinkThreshold && p.Economy.Used(VillageGood.Water) == beforeUse + 1 &&
            p.Economy.StartStock(VillageGood.Water) + p.Economy.Made(VillageGood.Water) == p.Economy.Present(VillageGood.Water) + p.Economy.Used(VillageGood.Water) &&
            p.Economy.Audit().Count == 0, "community-water-leaves-stock-and-quenches-the-patient");
        check(clocks.Archive.Get(patient.Identity).Knowledge(aid.Id).Origin == KnowledgeOrigin.Self &&
            clocks.Archive.Get(patient.Identity).RelationshipTo(helper.Identity).Gratitude > 0 &&
            clocks.Archive.Get("orsel")?.Knowledge(aid.Id) == null, "community-home-help-is-known-to-the-actual-patient");
    }

    private static void StoreDelivery(Action<bool, string> check, bool maraIsPresent)
    {
        var world = Village(1);
        var p = world.Progression;
        for (int i = 0; i < 4; i++) p.Social.TakeCampRation();
        var mara = world.Npcs.Single(n => n.Id == SocialLedger.MaraNpcId);
        var carrier = p.Economy.Residents[0];
        for (int i = 0; i < 30000 && carrier.Work != VillageWork.DeliverRations; i++) world.Step();
        int load = carrier.Held(VillageGood.Ration);
        check(load > 0, "community-actual-rations-on-way-to-the-store-" + maraIsPresent);
        long old = p.Economy.HandedOut(VillageGood.Ration);
        mara.Xmm = maraIsPresent ? VillageEconomyLayout.StandXmm(VillageSpot.Store) : 0;
        mara.Zmm = maraIsPresent ? VillageEconomyLayout.StandZmm(VillageSpot.Store) : 18000;
        for (int i = 0; i < 120 && p.Economy.HandedOut(VillageGood.Ration) == old; i++) world.Step();
        var receipt = p.Social.Forge.Archive.Events.Last(e => e.Context == VillageEconomyState.DeliveryContext);
        check(receipt.Actor == carrier.Identity && p.Social.Forge.Archive.Get(carrier.Identity).Knowledge(receipt.Id).Origin == KnowledgeOrigin.Self &&
            p.Economy.HandedOut(VillageGood.Ration) > old, "community-carrier-knows-the-real-deposit-" + maraIsPresent);
        check(maraIsPresent ? receipt.Target == "mara" && mara.Mind.Knowledge(receipt.Id)?.Origin == KnowledgeOrigin.Self :
            receipt.Target == "village.store" && mara.Mind.Knowledge(receipt.Id) == null, "community-only-a-present-mara-remembers-the-handover-" + maraIsPresent);
    }

    private static void ReportsComeHome(Action<bool, string> check)
    {
        var world = Village(1);
        var p = world.Progression;
        var r = p.Economy.Residents[0];
        check(p.Economy.ReportedSupply(VillageSource.MatureTrees) == -1, "community-unknown-is-distinct-from-depletion");
        for (int i = 0; i < 8000 && r.SiteSample == 0; i++) world.Step();
        int carried = r.SiteSample;
        ulong sampled = r.SiteSampleTick;
        check(carried > 0 && r.Site == VillageSite.Forest && r.Walking && p.Economy.ForestReport == 0, "community-scout-hasnt-told-the-village-from-the-road");
        check(CharacterProgression.TryLoad(p.SaveString(), out var loaded), "community-observation-in-transit-loads");
        var twin = new ArenaSimulation(77, loaded, 1, LootCatalog.Version1);
        for (int i = 0; i < VillageEconomyRules.ForestTravelTicks + 40 && p.Economy.ForestReport == 0; i++) { world.Step(); twin.Step(); }
        check(p.Economy.ForestReport == carried && p.Economy.ForestReportTick == sampled && p.Economy.ForestReporterOrdinal == r.Ordinal &&
            r.SiteSample == 0 && p.Economy.Token() == loaded.Economy.Token(), "community-only-a-returning-resident-publishes-the-original-observation");
    }

    private static void LocalGuard(Action<bool, string> check)
    {
        var world = Village(1);
        var p = world.Progression;
        var worker = p.Economy.Residents[0];
        // A food shortage makes a workbench step due in the village, with real wood reserved.
        for (int i = 0; i < 4; i++) p.Social.TakeCampRation();
        for (int i = 0; i < 6000 && worker.Work != VillageWork.MakeFrame; i++) world.Step();
        check(worker.Work == VillageWork.MakeFrame, "community-live-workbench-for-the-guard-test");
        long x = VillageEconomyLayout.StandXmm(VillageSpot.Workbench), z = VillageEconomyLayout.StandZmm(VillageSpot.Workbench);
        HunterEncounterTests.Move(world, x, z);
        var dorn = world.Npcs.Single(n => n.Id == 2);
        dorn.Xmm = x; dorn.Zmm = z;
        check(!world.VillageWorkUnsafe(worker) && world.InteractNpc(2, NpcAction.Attack).Accepted, "community-the-guard-responds-to-an-actual-local-assault");
        for (int i = 0; i < 15 && dorn.Behavior != NpcBehavior.Attack; i++) world.Step();
        check(dorn.Behavior == NpcBehavior.Attack && world.VillageWorkUnsafe(worker), "community-a-visible-fighting-guard-makes-this-workbench-unsafe");
        int remaining = worker.WorkTicks, held = worker.Held(VillageGood.Wood);
        for (int i = 0; i < 10; i++) world.Step();
        check(remaining == worker.WorkTicks && held == worker.Held(VillageGood.Wood) && p.Economy.Audit().Count == 0,
            "community-danger-pauses-the-actual-job-without-consuming-reserved-material");
        dorn.Xmm = -19000; dorn.Zmm = 16000;
        check(!world.VillageWorkUnsafe(worker), "community-distant-guard-cannot-freeze-the-dorf");
    }

    private static void EconomyTick(CharacterProgression p)
    {
        var forge = p.Social.Forge;
        forge.ProcessedTicks++;
        forge.Archive.AdvancePersonalTime(forge.ProcessedTicks);
        p.Economy.Advance(p);
    }

    private static void TheMillSendsItsOwnReport(Action<bool, string> check)
    {
        var p = CharacterProgression.CreateEntryProfile();
        for (int i = 0; i < 3; i++) p.DiscoverHungerClue();
        p.ChooseHungerSupply(HungerChoice.Protect);
        p.TravelHunger(4, false);
        p.DiscoverHungerClue();
        check(p.Village.UnlockOrselTeaches(p), "community-mill-opens-under-its-existing-quest");
        p.Social.EnableForge();
        p.Village.RecordMainQuestCompleted();
        for (int t = 0; t < 8 * (int)WorldSkyClock.TicksPerDay; t++)
        {
            EconomyTick(p);
            if (p.Economy.Residents.Count == 0) continue;
            var resident = p.Economy.Residents[0];
            if (resident.Site == VillageSite.Mill && resident.Walking && resident.SiteSample > 0) break;
        }
        var carrier = p.Economy.Residents[0];
        int herbs = carrier.SiteSample - 1;
        ulong survey = carrier.SiteSampleTick;
        check(carrier.Site == VillageSite.Mill && carrier.Walking && herbs == p.Economy.MillHerbs &&
            p.Economy.ReportedMillHerbs == -1, "community-herb-shelf-is-still-private-while-the-carrier-is-out");
        check(CharacterProgression.TryLoad(p.SaveString(), out var twin), "community-mill-report-in-transit-survives-load");
        for (int t = 0; t < VillageEconomyRules.MillTravelTicks + 40 && p.Economy.MillReportTick == 0; t++)
        {
            EconomyTick(p); EconomyTick(twin);
        }
        check(p.Economy.ReportedMillHerbs == herbs && p.Economy.MillReportTick == survey &&
            p.Economy.MillReporterOrdinal == carrier.Ordinal && p.Economy.Token() == twin.Economy.Token(),
            "community-first-person-mill-report-arrives-with-its-original-age-and-source");
    }
}
