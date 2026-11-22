using System;
using System.Linq;
using System.Numerics;
using AreArpg.Core;

/// <summary>
/// The lots of Aschengrenze: who asks for which house, what it costs, how a lot becomes a house when the player
/// comes to the village again, and what a save may claim about it.
/// </summary>
internal static class VillageBuildingSiteTests
{
    private static readonly VillagePlace[] Places = { VillagePlace.SickRoom, VillagePlace.Smithy, VillagePlace.Barn };

    public static void Run(Action<bool, string> check)
    {
        Rules(check);
        FromLotToHouse(check);
        OrselComes(check);
        WhatASaveMayClaim(check);
        QuickerUnderARoof(check);
    }

    private static ArenaSimulation Village(ulong seed)
    {
        var p = CharacterProgression.CreateEntryProfile(); p.EnterVillage(); p.Social.EnableForge();
        return new ArenaSimulation(seed, p, BigInteger.One, LootCatalog.Version1);
    }

    private static void Rules(Action<bool, string> check)
    {
        // The costs the owner decided on (22.11.2026), in materials the player can carry.
        Func<VillagePlace, int, VillageGood, int, bool> costs = (place, need, good, count) =>
            VillageBuildRules.NeedAt(place, need).Good == good && VillageBuildRules.NeedAt(place, need).Count == count;
        check(costs(VillagePlace.SickRoom, 0, VillageGood.Wood, 8) && costs(VillagePlace.SickRoom, 1, VillageGood.Herb, 4) &&
            costs(VillagePlace.Smithy, 0, VillageGood.Wood, 10) && costs(VillagePlace.Smithy, 1, VillageGood.Metal, 6) &&
            costs(VillagePlace.Barn, 0, VillageGood.Wood, 12) && costs(VillagePlace.Barn, 1, VillageGood.Metal, 4), "the-houses-cost-what-was-decided");
        check(VillageBuildRules.Asker(VillagePlace.SickRoom) == "Orsel" && VillageBuildRules.Asker(VillagePlace.Smithy) == "Der Schmied" &&
            VillageBuildRules.Asker(VillagePlace.Barn) == "Der Bauer", "the-right-person-asks-for-each-house");
        bool carried = true, free = true, apart = true, said = true;
        var p = CharacterProgression.CreateEntryProfile();
        foreach (VillagePlace place in Places)
        {
            for (int need = 0; need < VillageBuildRules.NeedCount; need++)
            {
                VillageGood good = VillageBuildRules.NeedAt(place, need).Good;
                carried &= VillageBuildRules.NeedFor(place, good) == need && VillageBuildRules.Carried(p, good) == 0 && VillageBuildRules.GoodName(good).Length > 0;
            }
            long ax = VillageBuildRules.AskerXmm(place), az = VillageBuildRules.AskerZmm(place);
            // The person stands on free ground in front of the lot, beside the way to the door and not on its doormat.
            free &= EntryWorldLayout.CanStand(ax, az) && VillagePlaceLayout.DoorAt(ax, az) == VillagePlace.None &&
                VillagePlaceLayout.RoomAt(ax, az) == VillagePlace.None && Math.Abs(ax) <= 25000 && Math.Abs(az) <= 25000;
            // Whoever comes out of the house, or stands where the door will be, is within talking distance.
            long lx = VillagePlaceLayout.LeaveXmm(place), lz = VillagePlaceLayout.LeaveZmm(place);
            free &= (lx - ax) * (lx - ax) + (lz - az) * (lz - az) <= VillageBuildRules.ReachMm * VillageBuildRules.ReachMm;
            foreach (VillagePlace other in Places)
                if (other != place)
                {
                    long dx = ax - VillageBuildRules.AskerXmm(other), dz = az - VillageBuildRules.AskerZmm(other);
                    apart &= dx * dx + dz * dz > 4 * VillageBuildRules.ReachMm * VillageBuildRules.ReachMm;
                }
            said &= VillageBuildRules.Request(place).Length > 40 && VillageBuildRules.Thanks(place).StartsWith(VillageBuildRules.Asker(place) + ": Vielen Dank.") &&
                VillageBuildRules.Thanks(place).EndsWith("Komm später wieder.");
        }
        check(carried, "every-material-asked-for-is-one-the-player-carries");
        check(free, "the-person-asking-stands-free-in-front-of-the-lot");
        check(apart, "no-two-lots-are-in-reach-at-once");
        check(said, "everybody-says-why-and-says-thanks");
        // Away from the board the village board still stands clear of every lot.
        bool boardApart = true;
        foreach (VillagePlace place in Places)
        {
            long dx = VillageBuildRules.AskerXmm(place) - VillageEconomyLayout.StandXmm(VillageSpot.Board), dz = VillageBuildRules.AskerZmm(place) - VillageEconomyLayout.StandZmm(VillageSpot.Board);
            boardApart &= dx * dx + dz * dz > 4 * VillageBuildRules.ReachMm * VillageBuildRules.ReachMm;
        }
        check(boardApart, "the-village-board-is-not-in-reach-of-a-lot");
        bool threw = false;
        try { VillageBuildRules.NeedAt(VillagePlace.None, 0); } catch (ArgumentOutOfRangeException) { threw = true; }
        check(threw && !new VillageBuildingState().Built(VillagePlace.None), "no-house-has-no-cost-and-does-not-stand");
    }

    private static void FromLotToHouse(Action<bool, string> check)
    {
        var world = Village(71); var p = world.Progression; var houses = p.Houses;
        VillagePlace smithy = VillagePlace.Smithy;
        p.SaveString();
        check(!houses.HasProgress && !p.TryGetSection("houses", out _), "three-untouched-lots-are-no-part-of-a-save");
        check(world.SiteOffered(smithy) && !world.SiteOffered(VillagePlace.Barn) && !world.SiteOffered(VillagePlace.SickRoom) && world.SiteInReach == VillagePlace.None,
            "the-smith-asks-first-and-nobody-else");
        check(VillageBoard.LocalNeeds(p).First().Contains("Der Schmied bittet am Bauplatz") && VillageBoard.LocalNeeds(p).First().Contains("10 Holz und 6 Ascheisen"),
            "the-board-names-the-lot-first");

        // A lot has no door: walking into where the door will be leads nowhere.
        VillagePlaceTests.Walk(world, VillagePlaceLayout.LeaveXmm(smithy), VillagePlaceLayout.LeaveZmm(smithy));
        world.SetMoveInput(-VillagePlaceLayout.DoorOutX(smithy) * 10000, -VillagePlaceLayout.DoorOutZ(smithy) * 10000);
        bool door = false;
        for (int t = 0; t < 14; t++) { world.Step(); door |= world.DoorInReach != VillagePlace.None || world.WalkingIntoHouse != VillagePlace.None || world.TryEnterPlace(); }
        world.SetMoveInput(0, 0);
        check(!door && world.InsidePlace == VillagePlace.None && VillagePlaceLayout.DoorAt(world.PlayerXmm, world.PlayerZmm) == smithy, "a-lot-has-no-door");

        VillagePlaceTests.Walk(world, VillageBuildRules.AskerXmm(smithy) + 900, VillageBuildRules.AskerZmm(smithy) + 500);
        check(world.SiteInReach == smithy && world.SiteWords(smithy).StartsWith("Der Schmied: ") && world.SiteWords(smithy).EndsWith("Mir fehlen noch 10 Holz und 6 Ascheisen."),
            "at-the-lot-the-smith-says-what-he-needs");
        string before = p.SaveString(); ulong hash = world.ComputeWorldHash();
        p.Crafting.AddMaterial(MaterialKind.Ashenflieder, 3);
        string withHerbs = p.SaveString();
        check(!world.GiveToSite(VillageGood.Wood).Accepted && !world.GiveToSite(VillageGood.Herb).Accepted && !world.GiveToSite(VillageGood.Game).Accepted &&
            p.SaveString() == withHerbs && before != withHerbs && world.ComputeWorldHash() == hash && !houses.HasProgress,
            "an-empty-pack-and-the-wrong-material-change-nothing");

        p.Crafting.AddMaterial(MaterialKind.Holz, 4);
        var first = world.GiveToSite(VillageGood.Wood);
        check(first.Accepted && houses.Delivered(smithy, 0) == 4 && p.Crafting.MaterialCount(MaterialKind.Holz) == 0 && houses.Stage(smithy) == BuildStage.Site &&
            first.Text.Contains("4 × Holz") && first.Text.EndsWith("Mir fehlen noch 6 Holz und 6 Ascheisen."), "what-the-pack-holds-is-handed-over");
        check(world.ComputeWorldHash() != hash && houses.HasProgress, "the-world-hash-knows-what-lies-at-a-lot");
        p.Crafting.AddMaterial(MaterialKind.Holz, 20);
        bool rest = world.GiveToSite(VillageGood.Wood).Accepted;
        check(rest && houses.Delivered(smithy, 0) == 10 && p.Crafting.MaterialCount(MaterialKind.Holz) == 14 && !world.GiveToSite(VillageGood.Wood).Accepted &&
            p.Crafting.MaterialCount(MaterialKind.Holz) == 14 && houses.Stage(smithy) == BuildStage.Site, "nothing-is-taken-beyond-the-need");
        check(VillageBoard.LocalNeeds(p).First().Contains("um 6 Ascheisen."), "the-board-follows-what-is-still-missing");

        p.Crafting.AddMaterial(MaterialKind.Ascheisen, 6);
        var last = world.GiveToSite(VillageGood.Metal);
        check(last.Accepted && houses.Stage(smithy) == BuildStage.Raising && last.Text == "Der Schmied: Vielen Dank. Ich mache mich an die Arbeit. Komm später wieder." &&
            p.Crafting.MaterialCount(MaterialKind.Ascheisen) == 0, "with-the-last-piece-the-smith-says-thanks-and-sets-to-work");
        var again = world.GiveToSite(VillageGood.Wood);
        check(!again.Accepted && again.Text == "Der Schmied: Ich bin an der Arbeit. Komm später wieder." && p.Crafting.MaterialCount(MaterialKind.Holz) == 14 &&
            world.SiteOffered(smithy) && world.SiteInReach == smithy, "while-he-builds-he-takes-nothing-more");
        check(VillageBoard.LocalNeeds(p).First() == "Der Schmied baut: Schmiede. Komm später wieder ins Dorf.", "the-board-says-that-the-smithy-is-being-built");

        // The house does not appear out of nothing: however long the player stays, the lot stays a lot.
        bool popped = false;
        for (int t = 0; t < 900; t++) { world.Step(); popped |= houses.Built(smithy) || world.DoorInReach != VillagePlace.None; }
        check(!popped && houses.Stage(smithy) == BuildStage.Raising && !world.SiteOffered(VillagePlace.Barn), "the-house-does-not-appear-while-the-player-is-there");
        string raising = p.SaveString();
        check(CharacterProgression.TryLoad(raising, out var kept) && kept.Houses.Stage(smithy) == BuildStage.Raising && kept.SaveString() == raising &&
            !new ArenaSimulation(71, kept, BigInteger.One, LootCatalog.Version1).Progression.Houses.Built(smithy), "saving-and-loading-builds-nothing");

        // Away and back again: now the smithy stands, and the farmer asks for the barn.
        kept.TravelHunger(3, false);
        bool away = kept.Houses.Stage(smithy) == BuildStage.Raising;
        kept.TravelHunger(2, true);
        var back = new ArenaSimulation(71, kept, BigInteger.One, LootCatalog.Version1);
        check(away && kept.Houses.Built(smithy) && !back.SiteOffered(smithy) && back.SiteOffered(VillagePlace.Barn) && !back.SiteOffered(VillagePlace.SickRoom),
            "coming-back-the-smithy-stands-and-the-farmer-asks-next");
        VillagePlaceTests.Walk(back, VillagePlaceLayout.LeaveXmm(smithy), VillagePlaceLayout.LeaveZmm(smithy));
        check(back.SiteInReach == VillagePlace.None, "nobody-asks-at-a-house-that-stands");
        back.SetMoveInput(-VillagePlaceLayout.DoorOutX(smithy) * 10000, -VillagePlaceLayout.DoorOutZ(smithy) * 10000);
        for (int t = 0; t < 14 && back.WalkingIntoHouse == VillagePlace.None; t++) back.Step();
        check(back.WalkingIntoHouse == smithy && back.TryEnterPlace() && back.InsidePlace == smithy, "the-finished-smithy-can-be-entered");
        check(CharacterProgression.TryLoad(kept.SaveString(), out var built) && built.Houses.Built(smithy) && built.SaveString() == kept.SaveString(), "the-finished-house-is-saved");

        // A villager the player has turned against takes nothing.
        var hostile = Village(72);
        VillagePlaceTests.Walk(hostile, VillageBuildRules.AskerXmm(smithy) + 900, VillageBuildRules.AskerZmm(smithy) + 500);
        hostile.Progression.Crafting.AddMaterial(MaterialKind.Holz, 5);
        var ilyan = hostile.Npcs.First(n => n.Id == 0);
        ilyan.Xmm = hostile.PlayerXmm + 1000; ilyan.Zmm = hostile.PlayerZmm;
        bool attacked = hostile.InteractNpc(0, NpcAction.Attack).Accepted && hostile.DoorsBarred;
        check(attacked && !hostile.GiveToSite(VillageGood.Wood).Accepted && hostile.Progression.Crafting.MaterialCount(MaterialKind.Holz) == 5 &&
            !hostile.Progression.Houses.HasProgress, "nobody-builds-with-someone-the-village-is-up-in-arms-against");
    }

    /// <summary>The mill quest is done as in the game: clues found, the supply decided, the shelter beds inspected.</summary>
    private static CharacterProgression MillQuestDone()
    {
        var p = CharacterProgression.CreateEntryProfile();
        for (int i = 0; i < 3; i++) p.DiscoverHungerClue();
        p.ChooseHungerSupply(HungerChoice.Protect);
        p.TravelHunger(4, false);
        p.DiscoverHungerClue();
        if (!p.Village.UnlockOrselTeaches(p)) throw new Exception("FAIL the mill quest does not unlock Orsel");
        p.Social.EnableForge();
        return p;
    }

    /// <summary>The Rattler lies dead in its den, with everything the investigation needed to get there.</summary>
    private static void DefeatRattler(CharacterProgression p)
    {
        p.Investigation.Active = true; p.Investigation.Clues = 7; p.Investigation.Approach = SealApproach.Investigated;
        p.Investigation.TraceFound = true; p.Investigation.Reported = true;
        p.Rattler.Active = true; p.Rattler.Creature.Health = 0; p.Rattler.Creature.LootGranted = true;
        p.Rattler.BrandExamined = true; p.Rattler.Reported = true;
    }

    private static void OrselComes(Action<bool, string> check)
    {
        VillagePlace sickRoom = VillagePlace.SickRoom;
        var p = MillQuestDone();
        p.TravelHunger(3, true); p.TravelHunger(2, true);
        check(!VillageBuildRules.OrselReady(p) && !VillageBuildRules.Offered(p, sickRoom) && !p.Houses.OrselInVillage, "with-the-rattler-alive-orsel-stays-at-the-mill");
        var early = CharacterProgression.CreateEntryProfile(); early.EnterVillage();
        DefeatRattler(early);
        check(!VillageBuildRules.OrselReady(early) && !VillageBuildRules.Offered(early, sickRoom), "without-her-mill-quest-orsel-does-not-come");

        // The Rattler falls. Orsel is in the village the next time the player gets there, and stays.
        p.TravelHunger(7, false);
        DefeatRattler(p);
        check(VillageBuildRules.OrselReady(p) && !p.Houses.OrselInVillage, "with-mill-and-rattler-done-orsel-is-ready-to-move");
        p.TravelHunger(2, true);
        check(p.Houses.OrselInVillage && VillageBuildRules.Offered(p, sickRoom), "after-mill-and-rattler-orsel-asks-for-the-sick-room");
        string arrived = p.SaveString();
        check(CharacterProgression.TryLoad(arrived, out var rerun) && rerun.SaveString() == arrived && rerun.Houses.OrselInVillage, "orsels-arrival-is-saved");
        rerun.Rattler.RestartDungeonRun();
        check(!rerun.Rattler.Creature.IsDead && VillageBuildRules.Offered(rerun, sickRoom), "a-new-run-of-the-den-does-not-send-orsel-back");

        // Her lot does not wait for the smith: the sick room can be the first house of the village.
        var world = new ArenaSimulation(73, p, BigInteger.One, LootCatalog.Version1);
        VillagePlaceTests.Walk(world, VillageBuildRules.AskerXmm(sickRoom) + 900, VillageBuildRules.AskerZmm(sickRoom) + 500);
        p.Crafting.AddMaterial(MaterialKind.Holz, 8); p.Crafting.AddMaterial(MaterialKind.Ashenflieder, 9);
        check(world.SiteInReach == sickRoom && world.SiteWords(sickRoom).StartsWith("Orsel: An der Mühle liegen meine Kranken") &&
            world.SiteWords(sickRoom).EndsWith("Mir fehlen noch 8 Holz und 4 Aschenflieder."), "orsel-says-why-she-wants-to-move");
        bool wood = world.GiveToSite(VillageGood.Wood).Accepted;
        var herbs = world.GiveToSite(VillageGood.Herb);
        check(wood && herbs.Accepted && herbs.Text == "Orsel: Vielen Dank. Ich mache mich an die Arbeit. Komm später wieder." &&
            p.Houses.Stage(sickRoom) == BuildStage.Raising && p.Crafting.MaterialCount(MaterialKind.Ashenflieder) == 5 && p.Houses.Stage(VillagePlace.Smithy) == BuildStage.Site,
            "orsel-takes-wood-and-ashen-lilac-and-sets-to-work");
        check(CharacterProgression.TryLoad(p.SaveString(), out var kept) && kept.SaveString() == p.SaveString() && kept.Houses.OrselInVillage, "orsels-lot-is-saved");
        kept.TravelHunger(3, false); kept.TravelHunger(2, true);
        check(kept.Houses.Built(sickRoom) && !VillageBuildRules.Offered(kept, VillagePlace.Barn) && VillageBuildRules.Offered(kept, VillagePlace.Smithy),
            "coming-back-the-sick-room-stands");
        check(kept.Houses.Outdoors(VillageSpot.SickRoom) == VillageSpot.SickRoom && kept.Houses.Outdoors(VillageSpot.Smithy) == VillageSpot.Workbench &&
            new VillageBuildingState().Outdoors(VillageSpot.SickRoom) == VillageSpot.Home && kept.Houses.Outdoors(VillageSpot.Field) == VillageSpot.Field,
            "work-and-care-are-in-the-open-until-their-house-stands");
    }

    private static void WhatASaveMayClaim(Action<bool, string> check)
    {
        Func<string, bool> reads = token => VillageBuildingState.TryRead(token, out _);
        check(reads("1;0;0,0,0;0,4,0;0,0,0") && reads("1;0;0,0,0;1,10,6;0,0,0") && reads("1;0;0,0,0;2,10,6;0,3,1") && reads("1;1;0,8,3;0,0,0;0,0,0") &&
            reads("1;1;2,8,4;2,10,6;2,12,4"), "every-state-the-game-can-reach-is-read");
        foreach (string token in new[] { "1;1;2,8,4;2,10,6;2,12,4", "1;0;0,0,0;0,4,0;0,0,0", "1;1;0,0,0;0,0,0;0,0,0" })
            check(VillageBuildingState.TryRead(token, out var state) && state.Token() == token, "a-token-is-written-as-it-was-read " + token);
        string[] refused =
        {
            null, "", "1", "2;0;0,0,0;0,4,0;0,0,0", "1;0;0,0,0;0,0,0;0,0,0",          // unknown version, three untouched lots
            "1;2;0,0,0;0,4,0;0,0,0", "1;0;0,0,0;0,4,0", "1;0;0,0,0;0,4,0;0,0,0;0,0,0",   // no such flag, a lot missing, a lot too many
            "1;0;0,0,0;0,11,0;0,0,0", "1;0;0,0,0;0,10,7;0,0,0",                         // more delivered than asked for
            "1;0;0,0,0;1,10,5;0,0,0", "1;0;0,0,0;2,9,6;0,0,0", "1;0;0,0,0;0,10,6;0,0,0", // raised or built without everything, complete but still a lot
            "1;0;0,0,0;3,10,6;0,0,0",                                                   // no such stage
            "1;0;0,0,0;0,4,0;0,1,0", "1;0;0,0,0;1,10,6;0,1,0", "1;0;0,0,0;0,0,0;2,12,4", // the barn before the smithy stands
            "1;0;0,1,0;0,0,0;0,0,0", "1;0;2,8,4;0,0,0;0,0,0",                           // the sick room without Orsel
            "1;0;0,0,0;0,04,0;0,0,0", "1;0;0,0,0;0,+4,0;0,0,0", "1;0;0,0,0;0,-1,0;0,0,0", "1;0;0,0,0;0,4,0;0,0,0 ", "1;0;0,0,0;0,4;0,0,0", "1;0;0,0,0;0,a,0;0,0,0",
            "1;0;0,0,0;0,99999,0;0,0,0",
        };
        bool all = true; string slipped = "";
        foreach (string token in refused) if (reads(token)) { all = false; slipped += " [" + token + "]"; }
        check(all, "a-state-the-game-cannot-reach-is-refused" + slipped);

        // A whole save with a lot in it loads only into the village era of the journey.
        var world = Village(75); var p = world.Progression;
        p.Crafting.AddMaterial(MaterialKind.Holz, 3);
        VillagePlaceTests.Walk(world, VillageBuildRules.AskerXmm(VillagePlace.Smithy) + 900, VillageBuildRules.AskerZmm(VillagePlace.Smithy) + 500);
        check(world.GiveToSite(VillageGood.Wood).Accepted && p.TryGetSection("houses", out _) == false, "the-section-is-written-with-the-save-not-before");
        string save = p.SaveString();
        check(p.TryGetSection("houses", out string section) && section == "1;0;0,0,0;0,3,0;0,0,0" && CharacterProgression.TryLoad(save, out var loaded) &&
            loaded.Houses.Delivered(VillagePlace.Smithy, 0) == 3 && loaded.SaveString() == save, "a-lot-with-three-logs-is-saved-as-exactly-that");
        // Whole saves written by somebody else, with a correct checksum: the game still reads only what it could have written.
        check(Forge(save, "houses", section) == save && CharacterProgression.TryLoad(Forge(save, "houses", "1;0;0,0,0;0,7,2;0,0,0"), out var other) &&
            other.Houses.Delivered(VillagePlace.Smithy, 1) == 2, "the-forging-helper-writes-saves-the-game-reads");
        check(!CharacterProgression.TryLoad(Forge(save, "houses", "1;0;0,0,0;2,9,6;0,0,0"), out _) &&
            !CharacterProgression.TryLoad(Forge(save, "houses", "1;0;0,0,0;0,0,0;0,0,0"), out _) &&
            !CharacterProgression.TryLoad(Forge(save, "houses", "1;0;2,8,4;0,3,0;0,0,0"), out _) &&
            !CharacterProgression.TryLoad(Forge(save, "houses", ""), out _), "a-save-with-an-impossible-lot-is-refused-whole");
        // Orsel's lot needs Orsel's quest: a village that never finished the mill cannot have her in it.
        check(!CharacterProgression.TryLoad(Forge(save, "houses", "1;1;0,8,3;0,3,0;0,0,0"), out _) &&
            !CharacterProgression.TryLoad(Forge(save, "houses", "1;1;0,0,0;0,3,0;0,0,0"), out _), "orsel-in-the-village-without-her-mill-quest-is-refused");
        var crypt = CharacterProgression.CreateEntryProfile();
        check(CharacterProgression.TryLoad(crypt.SaveString(), out _) && !CharacterProgression.TryLoad(Forge(crypt.SaveString(), "houses", "1;0;0,0,0;0,3,0;0,0,0"), out _),
            "a-lot-before-the-village-is-reached-is-refused");
    }

    /// <summary>A save with one section put in or replaced by hand and the checksum made right again.</summary>
    private static string Forge(string save, string name, string value)
    {
        var sections = new System.Collections.Generic.SortedDictionary<string, string>(StringComparer.Ordinal);
        string legacy = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(save));
        if (save.StartsWith("AC22|", StringComparison.Ordinal))
        {
            string[] f = save.Split('|');
            legacy = f[2];
            for (int i = 4; i < f.Length - 1; i++) sections[f[i].Substring(0, f[i].IndexOf('='))] = f[i].Substring(f[i].IndexOf('=') + 1);
        }
        sections[name] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        string body = "AC22|1|" + legacy + "|" + sections.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        foreach (var pair in sections) body += "|" + pair.Key + "=" + pair.Value;
        return body + "|" + LootCatalog.HashText(body).ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void QuickerUnderARoof(Action<bool, string> check)
    {
        var lots = new VillageBuildingState();
        var all = new VillageBuildingState();
        foreach (VillagePlace place in Places) all.Complete(place);
        bool slowerOutside = true, twoThirds = true, onStride = true, othersUntouched = true;
        foreach (VillageWork work in (VillageWork[])Enum.GetValues(typeof(VillageWork)))
        {
            if (work == VillageWork.None) continue;
            VillageWorkRule rule;
            try { rule = VillageEconomyRules.Rule(work); } catch (Exception) { continue; }
            if (rule == null) continue;
            bool housed = Places.Any(place => VillageBuildRules.Speeds(place, work));
            int outside = VillageBuildRules.WorkTicks(rule, lots), inside = VillageBuildRules.WorkTicks(rule, all);
            slowerOutside &= outside == rule.Ticks;
            onStride &= inside % VillageEconomyRules.Stride == 0 && inside <= rule.Ticks && (rule.Ticks == 0 || inside > 0);
            if (housed) twoThirds &= inside < outside && inside >= rule.Ticks * 2 / 3 - VillageEconomyRules.Stride && inside <= rule.Ticks * 2 / 3;
            else othersUntouched &= inside == outside;
        }
        check(slowerOutside, "without-its-house-work-takes-what-it-always-took");
        check(twoThirds, "in-its-house-the-work-takes-two-thirds");
        check(onStride, "work-in-a-house-stays-on-the-economys-stride");
        check(othersUntouched, "work-that-belongs-to-no-house-is-not-changed");
        var smithyOnly = new VillageBuildingState(); smithyOnly.Complete(VillagePlace.Smithy);
        check(VillageBuildRules.WorkTicks(VillageEconomyRules.Rule(VillageWork.SmeltOre), smithyOnly) < VillageEconomyRules.Rule(VillageWork.SmeltOre).Ticks &&
            VillageBuildRules.WorkTicks(VillageEconomyRules.Rule(VillageWork.SowField), smithyOnly) == VillageEconomyRules.Rule(VillageWork.SowField).Ticks &&
            VillageBuildRules.WorkTicks(VillageEconomyRules.Rule(VillageWork.SowField), all) < VillageEconomyRules.Rule(VillageWork.SowField).Ticks,
            "each-house-speeds-its-own-work-only");
    }
}
