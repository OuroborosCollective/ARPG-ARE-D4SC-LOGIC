using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AreArpg.Core;

/// <summary>The houses of Aschengrenze that can be entered: their doors, their rooms and what the rules do with them.</summary>
internal static class VillagePlaceTests
{
    private static readonly VillagePlace[] Places = { VillagePlace.SickRoom, VillagePlace.Smithy, VillagePlace.Barn };

    public static void Run(Action<bool, string> check)
    {
        Layout(check);
        ThroughEveryDoorAndBack(check);
        ABarredDoor(check);
        TheCompanionWaitsOutside(check);
        OnlyInTheVillage(check);
        WhoIsSeenWhere(check);
    }

    private static ArenaSimulation Village(ulong seed)
    {
        var p = CharacterProgression.CreateEntryProfile(); p.EnterVillage(); p.Social.EnableForge();
        // These checks are about houses that stand; how a lot becomes a house is checked in VillageBuildingSiteTests.
        foreach (VillagePlace place in Places) p.Houses.Complete(place);
        return new ArenaSimulation(seed, p, BigInteger.One, LootCatalog.Version1);
    }

    private static void Layout(Action<bool, string> check)
    {
        bool onTheWall = true, matIsFree = true, putDownFree = true, comeInFree = true, furnitureInside = true, apart = true, named = true;
        foreach (VillagePlace place in Places)
        {
            EntryWorldLayout.Footprint house = default; bool found = false;
            for (int i = 0; i < EntryWorldLayout.BuildingCount; i++)
                if (EntryWorldLayout.Building(i).Id == VillagePlaceLayout.House(place)) { house = EntryWorldLayout.Building(i); found = true; }
            named &= found && VillagePlaceLayout.Name(place).Length > 0;
            long dx = VillagePlaceLayout.DoorXmm(place), dz = VillagePlaceLayout.DoorZmm(place);
            int ox = VillagePlaceLayout.DoorOutX(place), oz = VillagePlaceLayout.DoorOutZ(place);
            // The threshold lies on the wall that faces the way out, within the width of that wall.
            onTheWall &= Math.Abs(ox) + Math.Abs(oz) == 1 &&
                (ox != 0 ? dx == house.X + ox * house.HalfWidth && Math.Abs(dz - house.Z) <= house.HalfDepth - VillagePlaceLayout.DoorHalfWidthMm
                         : dz == house.Z + oz * house.HalfDepth && Math.Abs(dx - house.X) <= house.HalfWidth - VillagePlaceLayout.DoorHalfWidthMm);
            // Anyone who walks against the wall at the door still stands on the doormat, however long the last stride was.
            long body = ArenaSimulation.PlayerRadiusMm, stride = ArenaSimulation.PlayerSpeedMmPerTick;
            for (long step = body; step <= body + stride; step += 10)
                matIsFree &= EntryWorldLayout.CanStand(dx + ox * step, dz + oz * step) && VillagePlaceLayout.DoorAt(dx + ox * step, dz + oz * step) == place;
            long lx = VillagePlaceLayout.LeaveXmm(place), lz = VillagePlaceLayout.LeaveZmm(place);
            putDownFree &= EntryWorldLayout.CanStand(lx, lz) && VillagePlaceLayout.DoorAt(lx, lz) == VillagePlace.None &&
                Math.Abs(lx) <= 25000 && Math.Abs(lz) <= 25000 && VillagePlaceLayout.RoomAt(lx, lz) == VillagePlace.None;
            long ex = VillagePlaceLayout.EnterXmm(place), ez = VillagePlaceLayout.EnterZmm(place);
            comeInFree &= VillagePlaceLayout.RoomAt(ex, ez) == place && VillagePlaceLayout.CanStandInRoom(place, ex, ez) &&
                !VillagePlaceLayout.AtRoomDoor(place, ex, ez);
            // Inside as outside: a body at the wall beside the door stands on the doormat, across its whole width.
            for (long step = body; step <= body + stride; step += 10)
                for (long aside = -VillagePlaceLayout.DoorHalfWidthMm; aside <= VillagePlaceLayout.DoorHalfWidthMm; aside += 400)
                {
                    long px = VillagePlaceLayout.RoomDoorXmm(place) - VillagePlaceLayout.RoomOutX * step + VillagePlaceLayout.RoomOutZ * aside;
                    long pz = VillagePlaceLayout.RoomDoorZmm(place) - VillagePlaceLayout.RoomOutZ * step - VillagePlaceLayout.RoomOutX * aside;
                    comeInFree &= VillagePlaceLayout.CanStandInRoom(place, px, pz) && VillagePlaceLayout.AtRoomDoor(place, px, pz);
                }
            for (int a = 0; a < VillagePlaceLayout.FurnitureCount(place); a++)
            {
                var piece = VillagePlaceLayout.FurnitureAt(place, a);
                furnitureInside &= piece.Id.Length > 0 && Math.Abs(piece.X) + piece.HalfWidth <= VillagePlaceLayout.HalfWidthMm(place) &&
                    Math.Abs(piece.Z) + piece.HalfDepth <= VillagePlaceLayout.HalfDepthMm(place) && VillagePlaceLayout.Find(place, piece.Id).Id == piece.Id;
                for (int b = a + 1; b < VillagePlaceLayout.FurnitureCount(place); b++)
                {
                    var other = VillagePlaceLayout.FurnitureAt(place, b);
                    apart &= Math.Abs(piece.X - other.X) >= piece.HalfWidth + other.HalfWidth || Math.Abs(piece.Z - other.Z) >= piece.HalfDepth + other.HalfDepth;
                }
            }
        }
        check(named, "every-house-with-a-room-is-a-building-of-the-village");
        check(onTheWall, "every-door-lies-in-the-wall-of-its-house");
        check(matIsFree, "a-body-at-the-wall-stands-on-the-doormat");
        check(putDownFree, "coming-out-one-stands-free-and-off-the-doormat");
        check(comeInFree, "coming-in-one-stands-free-and-off-the-doormat");
        check(furnitureInside, "all-furniture-stands-inside-the-walls");
        check(apart, "no-two-pieces-of-furniture-overlap");
        check(VillagePlaceLayout.Find(VillagePlace.Smithy, "no such thing").Id == null, "an-unknown-piece-of-furniture-is-none");

        // The square of the village and its surroundings never belong to a room, and no room to another.
        bool villageIsNoRoom = true;
        for (long x = -30000; x <= 30000; x += 1500)
            for (long z = -30000; z <= 30000; z += 1500) villageIsNoRoom &= VillagePlaceLayout.RoomAt(x, z) == VillagePlace.None;
        check(villageIsNoRoom, "no-point-of-the-village-lies-in-a-room");
        bool ownRoom = true;
        foreach (VillagePlace place in Places)
            for (long x = -VillagePlaceLayout.HalfWidthMm(place); x <= VillagePlaceLayout.HalfWidthMm(place); x += 500)
                for (long z = -VillagePlaceLayout.HalfDepthMm(place); z <= VillagePlaceLayout.HalfDepthMm(place); z += 500)
                    ownRoom &= VillagePlaceLayout.RoomAt(VillagePlaceLayout.RoomXmm(place) + x, VillagePlaceLayout.RoomZmm + z) == place;
        check(ownRoom, "every-point-of-a-room-belongs-to-that-room");

        // Everything a room is for can be walked to from its door.
        bool reachable = true;
        foreach (VillagePlace place in Places)
        {
            var free = Reachable(place);
            for (int a = 0; a < VillagePlaceLayout.FurnitureCount(place); a++)
                reachable &= Beside(place, VillagePlaceLayout.FurnitureAt(place, a), free);
        }
        foreach (VillageSpot spot in new[] { VillageSpot.Smithy, VillageSpot.SickRoom })
        {
            VillagePlace house = VillageEconomyLayout.HouseOf(spot);
            reachable &= Reachable(house).Contains(Cell(house, VillageEconomyLayout.StandXmm(spot), VillageEconomyLayout.StandZmm(spot)));
        }
        check(reachable, "every-piece-of-furniture-and-every-workplace-can-be-walked-to");
        bool threw = false;
        try { VillagePlaceLayout.Name(VillagePlace.None); } catch (ArgumentOutOfRangeException) { threw = true; }
        check(threw, "no-house-is-refused");
    }

    private const long Grid = 250;
    private static long Cell(VillagePlace place, long x, long z)
    { return ((x - VillagePlaceLayout.RoomXmm(place) + 10000) / Grid) * 1000 + (z - VillagePlaceLayout.RoomZmm + 10000) / Grid; }

    /// <summary>The cells of a room a body can walk to from where it comes in.</summary>
    private static HashSet<long> Reachable(VillagePlace place)
    {
        var seen = new HashSet<long>(); var open = new Queue<(long, long)>();
        open.Enqueue((VillagePlaceLayout.EnterXmm(place), VillagePlaceLayout.EnterZmm(place)));
        seen.Add(Cell(place, VillagePlaceLayout.EnterXmm(place), VillagePlaceLayout.EnterZmm(place)));
        while (open.Count > 0)
        {
            var (x, z) = open.Dequeue();
            foreach (var (sx, sz) in new[] { (Grid, 0L), (-Grid, 0L), (0L, Grid), (0L, -Grid) })
            {
                long nx = x + sx, nz = z + sz;
                if (!VillagePlaceLayout.CanStandInRoom(place, nx, nz) || !seen.Add(Cell(place, nx, nz))) continue;
                open.Enqueue((nx, nz));
            }
        }
        return seen;
    }

    /// <summary>Somewhere within a step of the piece one can stand.</summary>
    private static bool Beside(VillagePlace place, VillagePlaceLayout.Furniture piece, HashSet<long> free)
    {
        long cx = VillagePlaceLayout.RoomXmm(place) + piece.X, cz = VillagePlaceLayout.RoomZmm + piece.Z, reach = ArenaSimulation.PlayerRadiusMm + 500;
        for (long x = cx - piece.HalfWidth - reach; x <= cx + piece.HalfWidth + reach; x += Grid / 2)
            for (long z = cz - piece.HalfDepth - reach; z <= cz + piece.HalfDepth + reach; z += Grid / 2)
                if (VillagePlaceLayout.CanStandInRoom(place, x, z) && free.Contains(Cell(place, x, z))) return true;
        return false;
    }

    /// <summary>The player walks there on free ground, step by real step: around buildings, never through them.</summary>
    internal static void Walk(ArenaSimulation world, long tx, long tz)
    {
        const long cell = 500;
        Func<long, long, long> key = (x, z) => ((x + 400000) / cell) * 100000 + (z + 400000) / cell;
        long startX = world.PlayerXmm / cell * cell, startZ = world.PlayerZmm / cell * cell, goalX = tx / cell * cell, goalZ = tz / cell * cell;
        var from = new Dictionary<long, (long, long)>(); var open = new Queue<(long, long)>();
        open.Enqueue((startX, startZ)); from[key(startX, startZ)] = (startX, startZ);
        while (open.Count > 0 && !from.ContainsKey(key(goalX, goalZ)))
        {
            var (x, z) = open.Dequeue();
            foreach (var (sx, sz) in new[] { (cell, 0L), (-cell, 0L), (0L, cell), (0L, -cell) })
            {
                long nx = x + sx, nz = z + sz;
                if (from.ContainsKey(key(nx, nz)) || !world.CanStand(nx, nz)) continue;
                from[key(nx, nz)] = (x, z); open.Enqueue((nx, nz));
            }
        }
        if (!from.ContainsKey(key(goalX, goalZ))) throw new Exception("FAIL no way from " + world.PlayerXmm + "," + world.PlayerZmm + " to " + tx + "," + tz);
        var way = new List<(long, long)> { (tx, tz) };
        for (var at = (goalX, goalZ); at != (startX, startZ); at = from[key(at.Item1, at.Item2)]) way.Add(at);
        way.Reverse();
        foreach (var (wx, wz) in way)
            for (int t = 0; t < 40; t++)
            {
                long dx = wx - world.PlayerXmm, dz = wz - world.PlayerZmm;
                if (Math.Abs(dx) <= 350 && Math.Abs(dz) <= 350) break;
                long reach = Math.Max(Math.Abs(dx), Math.Abs(dz));
                world.SetMoveInput(dx * 10000 / reach, dz * 10000 / reach);
                world.Step();
            }
        world.SetMoveInput(0, 0);
        if (Math.Abs(world.PlayerXmm - tx) > 700 || Math.Abs(world.PlayerZmm - tz) > 700)
            throw new Exception("FAIL the player did not reach " + tx + "," + tz + " but stands at " + world.PlayerXmm + "," + world.PlayerZmm);
    }

    private static void ThroughEveryDoorAndBack(Action<bool, string> check)
    {
        var world = Village(11); var twin = Village(11);
        bool entered = true, placed = true, noVillage = true, walls = true, furniture = true, left = true, noSave = true, savedAtTheDoor = true, hashMoves = true, noPingPong = true, sameAsTwin = true, heading = true;
        foreach (VillagePlace place in Places)
        {
            foreach (var w in new[] { world, twin })
            {
                Walk(w, VillagePlaceLayout.LeaveXmm(place), VillagePlaceLayout.LeaveZmm(place));
                heading &= w.DoorInReach == VillagePlace.None && w.WalkingIntoHouse == VillagePlace.None && !w.TryEnterPlace();
                // Walking away from the house on its doormat opens nothing; walking into the door does.
                w.SetMoveInput(-VillagePlaceLayout.DoorOutX(place) * 10000, -VillagePlaceLayout.DoorOutZ(place) * 10000);
                for (int t = 0; t < 12 && w.WalkingIntoHouse == VillagePlace.None; t++) w.Step();
                entered &= w.DoorInReach == place && w.WalkingIntoHouse == place;
                w.SetMoveInput(VillagePlaceLayout.DoorOutX(place) * 10000, VillagePlaceLayout.DoorOutZ(place) * 10000);
                heading &= w.DoorInReach == place && w.WalkingIntoHouse == VillagePlace.None;
                w.SetMoveInput(-VillagePlaceLayout.DoorOutX(place) * 10000, -VillagePlaceLayout.DoorOutZ(place) * 10000);
                ulong hash = w.ComputeWorldHash();
                entered &= w.TryEnterPlace();
                hashMoves &= hash != w.ComputeWorldHash();
                savedAtTheDoor &= CharacterProgression.TryLoad(w.Progression.SaveString(), out var kept) && InFrontOf(new ArenaSimulation(11, kept, BigInteger.One, LootCatalog.Version1), place);
                placed &= w.InsidePlace == place && w.PlayerXmm == VillagePlaceLayout.EnterXmm(place) && w.PlayerZmm == VillagePlaceLayout.EnterZmm(place) &&
                    w.PrevPlayerXmm == w.PlayerXmm && w.PrevPlayerZmm == w.PlayerZmm && w.IsVillage;
                noPingPong &= !w.AtRoomDoor && !w.WalkingOutOfRoom && !w.TryLeavePlace() && w.DoorInReach == VillagePlace.None && !w.TryEnterPlace();
                noVillage &= !w.VillageBoardInReach && w.Npcs.All(n => Math.Abs(n.Xmm - w.PlayerXmm) > 100000);
            }
            long cx = VillagePlaceLayout.RoomXmm(place), hw = VillagePlaceLayout.HalfWidthMm(place), hd = VillagePlaceLayout.HalfDepthMm(place);
            // Against every wall the player stops inside the room.
            foreach (var (mx, mz) in new[] { (-10000L, 0L), (10000L, 0L), (0L, 10000L), (0L, -10000L) })
            {
                Walk(world, VillagePlaceLayout.EnterXmm(place), VillagePlaceLayout.EnterZmm(place));
                world.SetMoveInput(mx, mz);
                for (int t = 0; t < 40; t++) world.Step();
                walls &= world.InsidePlace == place && Math.Abs(world.PlayerXmm - cx) <= hw - ArenaSimulation.PlayerRadiusMm &&
                    Math.Abs(world.PlayerZmm) <= hd - ArenaSimulation.PlayerRadiusMm;
            }
            for (int a = 0; a < VillagePlaceLayout.FurnitureCount(place); a++)
            {
                var piece = VillagePlaceLayout.FurnitureAt(place, a);
                furniture &= !world.CanStand(cx + piece.X, piece.Z) && !world.CanStand(cx + piece.X + piece.HalfWidth - 1, piece.Z + piece.HalfDepth - 1);
            }
            walls &= !world.CanStand(cx + hw, 0) && !world.CanStand(cx, hd) && !world.CanStand(cx, -hd) && !world.CanStand(cx - hw - 5000, 0) &&
                world.CanStand(cx, hd - ArenaSimulation.PlayerRadiusMm) && world.CanStand(cx, -hd + ArenaSimulation.PlayerRadiusMm);
            // Out again: onto the doormat and towards the door.
            foreach (var w in new[] { world, twin })
            {
                Walk(w, VillagePlaceLayout.EnterXmm(place), VillagePlaceLayout.EnterZmm(place));
                w.SetMoveInput(VillagePlaceLayout.RoomOutX * 10000, VillagePlaceLayout.RoomOutZ * 10000);
                for (int t = 0; t < 12 && !w.WalkingOutOfRoom; t++) w.Step();
                left &= w.AtRoomDoor && w.WalkingOutOfRoom;
                w.SetMoveInput(-VillagePlaceLayout.RoomOutX * 10000, -VillagePlaceLayout.RoomOutZ * 10000);
                heading &= w.AtRoomDoor && !w.WalkingOutOfRoom;
                w.SetMoveInput(0, 0);
                string before = w.Progression.SaveString();
                left &= w.TryLeavePlace() && w.InsidePlace == VillagePlace.None && w.PlayerXmm == VillagePlaceLayout.LeaveXmm(place) &&
                    w.PlayerZmm == VillagePlaceLayout.LeaveZmm(place) && w.PrevPlayerXmm == w.PlayerXmm && w.PrevPlayerZmm == w.PlayerZmm;
                noSave &= before == w.Progression.SaveString();
                noPingPong &= w.DoorInReach == VillagePlace.None && !w.TryEnterPlace() && !w.TryLeavePlace();
                w.SetMoveInput(0, 0);
            }
            sameAsTwin &= world.CurrentTick != twin.CurrentTick || world.ComputeWorldHash() == twin.ComputeWorldHash();
        }
        check(entered, "walking-into-a-house-door-leads-inside");
        check(heading, "a-door-opens-only-to-someone-walking-into-it");
        check(placed, "inside-one-stands-a-step-beyond-the-door");
        check(noVillage, "nothing-of-the-village-is-in-reach-from-inside");
        check(walls, "the-walls-of-a-room-hold");
        check(furniture, "nobody-walks-through-furniture");
        check(left, "walking-out-of-the-door-leads-back-in-front-of-the-house");
        check(noPingPong, "a-door-does-not-swing-straight-back");
        check(savedAtTheDoor, "a-save-from-inside-a-room-is-valid-and-holds-the-place-in-front-of-the-house");
        check(noSave, "stepping-out-changes-nothing-a-save-knows");
        check(hashMoves, "the-world-hash-knows-on-which-side-of-the-door-the-player-is");

        // The same steps in a second world give the same world: rooms are part of the deterministic simulation.
        var first = Village(23); var second = Village(23);
        foreach (var w in new[] { first, second })
        {
            Walk(w, VillagePlaceLayout.LeaveXmm(VillagePlace.Smithy), VillagePlaceLayout.LeaveZmm(VillagePlace.Smithy));
            w.SetMoveInput(-VillagePlaceLayout.DoorOutX(VillagePlace.Smithy) * 10000, -VillagePlaceLayout.DoorOutZ(VillagePlace.Smithy) * 10000);
            for (int t = 0; t < 12 && w.WalkingIntoHouse == VillagePlace.None; t++) w.Step();
            w.TryEnterPlace();
            w.SetMoveInput(6000, 8000);
            for (int t = 0; t < 30; t++) w.Step();
        }
        check(first.InsidePlace == VillagePlace.Smithy && first.CurrentTick == second.CurrentTick && first.ComputeWorldHash() == second.ComputeWorldHash() &&
            first.Progression.SaveString() == second.Progression.SaveString() && sameAsTwin, "the-same-steps-give-the-same-world-inside-a-room");
        check(CharacterProgression.TryLoad(first.Progression.SaveString(), out var loadedInside) &&
            InFrontOf(new ArenaSimulation(23, loadedInside, BigInteger.One, LootCatalog.Version1), VillagePlace.Smithy), "a-save-from-inside-a-room-loads-in-front-of-the-house");

        // Whoever keeps walking the way they came through a door walks on into the room, or on away from the house.
        // The game lets a door open the moment it can (as the running game does every tick): held on its heading,
        // the player passes each door exactly once.
        var held = Village(29);
        bool onward = true, differentHeadings = true; int passes = 0;
        Func<bool> useDoors = () =>
        {
            if (held.WalkingIntoHouse != VillagePlace.None) return held.TryEnterPlace();
            return held.WalkingOutOfRoom && held.TryLeavePlace();
        };
        foreach (VillagePlace place in Places)
        {
            int inX = -VillagePlaceLayout.DoorOutX(place), inZ = -VillagePlaceLayout.DoorOutZ(place);
            differentHeadings &= inX != VillagePlaceLayout.RoomOutX || inZ != VillagePlaceLayout.RoomOutZ;
            Walk(held, VillagePlaceLayout.LeaveXmm(place), VillagePlaceLayout.LeaveZmm(place));
            held.SetMoveInput(inX * 10000, inZ * 10000);
            int through = 0;
            for (int t = 0; t < 80; t++) { held.Step(); if (useDoors()) through++; }
            onward &= through == 1 && held.InsidePlace == place;
            passes += through;
            held.SetMoveInput(0, 0);
            if (held.InsidePlace != place) { onward = false; break; }
            Walk(held, VillagePlaceLayout.EnterXmm(place), VillagePlaceLayout.EnterZmm(place));
            held.SetMoveInput(VillagePlaceLayout.RoomOutX * 10000, VillagePlaceLayout.RoomOutZ * 10000);
            through = 0;
            for (int t = 0; t < 80; t++) { held.Step(); if (useDoors()) through++; }
            onward &= through == 1 && held.InsidePlace == VillagePlace.None;
            passes += through;
            held.SetMoveInput(0, 0);
        }
        check(differentHeadings, "no-house-is-entered-on-the-heading-that-leads-out-of-its-room");
        check(onward && passes == 2 * Places.Length, "held-on-its-heading-the-player-passes-each-door-once (" + passes + " passes)");
    }

    private static bool InFrontOf(ArenaSimulation world, VillagePlace place)
    {
        return world.InsidePlace == VillagePlace.None && world.PlayerXmm == VillagePlaceLayout.LeaveXmm(place) && world.PlayerZmm == VillagePlaceLayout.LeaveZmm(place) &&
            world.PrevPlayerXmm == world.PlayerXmm && world.PrevPlayerZmm == world.PlayerZmm;
    }

    /// <summary>A companion does not come into the house. It waits at the door, and the save stays one every reader accepts.</summary>
    private static void TheCompanionWaitsOutside(Action<bool, string> check)
    {
        var road = CompanionForgeTests.RescueFixture();
        if (!road.RecruitHunter().Accepted) throw new Exception("FAIL the hunter did not join");
        var p = road.Progression; p.TravelHunger(2, true);
        p.Houses.Complete(VillagePlace.Smithy);
        var world = new ArenaSimulation(53, p, BigInteger.One, LootCatalog.Version1);
        var c = p.Companion; VillagePlace place = VillagePlace.Smithy;
        Walk(world, VillagePlaceLayout.LeaveXmm(place), VillagePlaceLayout.LeaveZmm(place));
        for (int t = 0; t < 30; t++) world.Step();
        bool followed = world.CompanionPresent && c.Status == CompanionStatus.Following && Math.Abs(c.Xmm - world.PlayerXmm) < 4000 && Math.Abs(c.Zmm - world.PlayerZmm) < 4000;
        world.SetMoveInput(-VillagePlaceLayout.DoorOutX(place) * 10000, -VillagePlaceLayout.DoorOutZ(place) * 10000);
        for (int t = 0; t < 12 && world.WalkingIntoHouse == VillagePlace.None; t++) world.Step();
        bool entered = world.TryEnterPlace();
        long waitX = c.Xmm, waitZ = c.Zmm;
        world.SetMoveInput(3000, 10000);
        for (int t = 0; t < 60; t++) world.Step();
        world.SetMoveInput(0, 0);
        bool waited = c.Xmm == waitX && c.Zmm == waitZ && c.PreviousXmm == waitX && c.PreviousZmm == waitZ && c.Status == CompanionStatus.Following &&
            Math.Abs(c.Xmm) <= 27000 && Math.Abs(c.Zmm) <= 27000 && world.InsidePlace == place;
        check(followed && entered && waited, "a-companion-waits-in-front-of-the-house");
        string save = p.SaveString();
        check(CharacterProgression.TryLoad(save, out var loaded) && InFrontOf(new ArenaSimulation(53, loaded, BigInteger.One, LootCatalog.Version1), place) &&
            loaded.Companion.Xmm == waitX && loaded.Companion.Zmm == waitZ, "a-save-with-a-waiting-companion-loads-in-front-of-the-house");
        // Out again, and the companion follows as before.
        Walk(world, VillagePlaceLayout.EnterXmm(place), VillagePlaceLayout.EnterZmm(place));
        world.SetMoveInput(VillagePlaceLayout.RoomOutX * 10000, VillagePlaceLayout.RoomOutZ * 10000);
        for (int t = 0; t < 12 && !world.WalkingOutOfRoom; t++) world.Step();
        bool outside = world.TryLeavePlace();
        Walk(world, VillagePlaceLayout.LeaveXmm(place) - VillagePlaceLayout.DoorOutX(place) * -6000, VillagePlaceLayout.LeaveZmm(place) + VillagePlaceLayout.DoorOutZ(place) * 6000);
        for (int t = 0; t < 40; t++) world.Step();
        check(outside && (c.Xmm != waitX || c.Zmm != waitZ) && Math.Abs(c.Xmm - world.PlayerXmm) < 4000 && Math.Abs(c.Zmm - world.PlayerZmm) < 4000,
            "the-companion-follows-again-once-the-player-is-out");
    }

    private static void ABarredDoor(Action<bool, string> check)
    {
        var world = Village(37);
        VillagePlace place = VillagePlace.SickRoom;
        Walk(world, VillagePlaceLayout.LeaveXmm(place), VillagePlaceLayout.LeaveZmm(place));
        var ilyan = world.Npcs.First(n => n.Id == 0);
        ilyan.Xmm = world.PlayerXmm + 1000; ilyan.Zmm = world.PlayerZmm;
        bool attacked = world.InteractNpc(0, NpcAction.Attack).Accepted && ilyan.Mood == NpcDisposition.Hostile;
        world.SetMoveInput(-VillagePlaceLayout.DoorOutX(place) * 10000, -VillagePlaceLayout.DoorOutZ(place) * 10000);
        for (int t = 0; t < 12 && world.DoorInReach == VillagePlace.None; t++) world.Step();
        check(attacked && world.DoorInReach == place && world.DoorsBarred && world.WalkingIntoHouse == VillagePlace.None && !world.TryEnterPlace() &&
            world.InsidePlace == VillagePlace.None, "no-door-opens-while-a-villager-is-hostile");
    }

    private static void OnlyInTheVillage(Action<bool, string> check)
    {
        var crypt = new ArenaSimulation(41, CharacterProgression.CreateEntryProfile(), BigInteger.One, LootCatalog.Version1);
        long roomX = VillagePlaceLayout.EnterXmm(VillagePlace.Smithy), roomZ = VillagePlaceLayout.EnterZmm(VillagePlace.Smithy);
        check(!crypt.IsVillage && crypt.InsidePlace == VillagePlace.None && crypt.DoorInReach == VillagePlace.None && !crypt.CanStand(roomX, roomZ) &&
            !crypt.TryEnterPlace() && !crypt.TryLeavePlace() && !crypt.AtRoomDoor, "rooms-exist-only-in-the-village");
        var village = Village(43);
        check(village.CanStand(roomX, roomZ) && village.InsidePlace == VillagePlace.None, "in-the-village-the-rooms-can-be-stood-in");
    }

    private static void WhoIsSeenWhere(Action<bool, string> check)
    {
        check(VillageEconomyLayout.SpotFor(VillageWork.SmeltOre) == VillageSpot.Smithy && VillageEconomyLayout.SpotFor(VillageWork.ForgeFitting) == VillageSpot.Smithy &&
            VillageEconomyLayout.SpotFor(VillageWork.RepairAxe) == VillageSpot.Smithy, "ore-fittings-and-the-axe-are-worked-in-the-smithy");
        check(VillageEconomyLayout.SpotFor(VillageWork.MakeBandage) == VillageSpot.SickRoom, "dressings-are-made-in-the-sick-room");
        check(VillageEconomyLayout.SpotFor(VillageWork.SowField) == VillageSpot.Field && VillageEconomyLayout.SpotFor(VillageWork.HarvestField) == VillageSpot.Field &&
            VillageEconomyLayout.SpotFor(VillageWork.DressSoil) == VillageSpot.Field && VillageEconomyLayout.SpotFor(VillageWork.MakeCompost) == VillageSpot.Field,
            "sowing-harvest-soil-and-compost-are-done-at-the-field");
        check(VillageEconomyLayout.SpotFor(VillageWork.MakeFrame) == VillageSpot.Workbench && VillageEconomyLayout.SpotFor(VillageWork.MakeBow) == VillageSpot.Workbench &&
            VillageEconomyLayout.SpotFor(VillageWork.Cook) == VillageSpot.Kitchen && VillageEconomyLayout.SpotFor(VillageWork.BakeBread) == VillageSpot.Kitchen,
            "woodwork-and-cooking-stay-where-they-were");
        check(VillageEconomyLayout.HouseOf(VillageSpot.Smithy) == VillagePlace.Smithy && VillageEconomyLayout.HouseOf(VillageSpot.SickRoom) == VillagePlace.SickRoom &&
            VillageEconomyLayout.HouseOf(VillageSpot.Field) == VillagePlace.None && VillageEconomyLayout.HouseOf(VillageSpot.Workbench) == VillagePlace.None,
            "smithy-and-sick-room-are-houses-the-field-is-not");
        foreach (VillageSpot spot in new[] { VillageSpot.Smithy, VillageSpot.SickRoom })
        {
            VillagePlace house = VillageEconomyLayout.HouseOf(spot);
            check(VillageEconomyLayout.VillageXmm(spot) == VillagePlaceLayout.LeaveXmm(house) && VillageEconomyLayout.VillageZmm(spot) == VillagePlaceLayout.LeaveZmm(house) &&
                VillagePlaceLayout.RoomAt(VillageEconomyLayout.StandXmm(spot), VillageEconomyLayout.StandZmm(spot)) == house,
                "from-the-village-a-worker-in-the-" + spot.ToString().ToLowerInvariant() + "-is-seen-at-its-door");
        }
        check(VillageEconomyLayout.VillageXmm(VillageSpot.Field) == VillageEconomyLayout.StandXmm(VillageSpot.Field) &&
            VillageEconomyLayout.VillageZmm(VillageSpot.Kitchen) == VillageEconomyLayout.StandZmm(VillageSpot.Kitchen), "out-in-the-village-a-worker-is-seen-at-the-workplace");

        // Someone too hurt to work lies in the sick room; someone resting sits at home.
        var p = CharacterProgression.CreateEntryProfile(); p.EnterVillage(); p.Social.EnableForge();
        p.Village.RecordMainQuestCompleted();
        var forge = p.Social.Forge;
        for (int i = 0; i < 20 && p.Economy.Residents.Count == 0; i++) { forge.ProcessedTicks++; forge.Archive.AdvancePersonalTime(forge.ProcessedTicks); p.Economy.Advance(p); }
        var resident = p.Economy.Residents[0];
        var seen = new Dictionary<VillageActivity, VillageSpot>();
        foreach (VillageActivity activity in new[] { VillageActivity.Recovering, VillageActivity.Resting })
        {
            resident.Site = resident.Destination = VillageSite.Village; resident.TravelTicks = 0; resident.Activity = activity;
            if (VillageEconomyLayout.Seen(resident, out var spot)) seen[activity] = spot;
        }
        check(seen.Count == 2 && seen[VillageActivity.Recovering] == VillageSpot.SickRoom && seen[VillageActivity.Resting] == VillageSpot.Home,
            "the-hurt-lie-in-the-sick-room-the-tired-rest-at-home");

        // What happens in the village reaches somebody in a house at its door: an attacker in front of the smithy
        // stops the work inside, as it stopped the work at the bench before the smithy had a room.
        p.Houses.Complete(VillagePlace.Smithy);
        var world = new ArenaSimulation(61, p, BigInteger.One, LootCatalog.Version1);
        resident.Site = resident.Destination = VillageSite.Village; resident.TravelTicks = 0;
        resident.Activity = VillageActivity.Working; resident.Work = VillageWork.SmeltOre;
        var attacker = world.Npcs.First(n => n.Memory.Health > 0);
        NpcBehavior calm = attacker.Behavior; long homeX = attacker.Xmm, homeZ = attacker.Zmm;
        attacker.Xmm = VillagePlaceLayout.LeaveXmm(VillagePlace.Smithy) + 1500; attacker.Zmm = VillagePlaceLayout.LeaveZmm(VillagePlace.Smithy) + 1000;
        bool safeWhileCalm = !world.VillageWorkUnsafe(resident);
        attacker.Behavior = NpcBehavior.Attack;
        bool unsafeAtTheDoor = world.VillageWorkUnsafe(resident);
        attacker.Xmm = homeX; attacker.Zmm = homeZ;
        bool safeFarAway = Math.Abs(homeX - VillagePlaceLayout.LeaveXmm(VillagePlace.Smithy)) + Math.Abs(homeZ - VillagePlaceLayout.LeaveZmm(VillagePlace.Smithy)) < 9000 ||
            !world.VillageWorkUnsafe(resident);
        attacker.Behavior = calm;
        check(safeWhileCalm && unsafeAtTheDoor && safeFarAway, "an-attacker-at-the-house-door-stops-the-work-inside");
    }
}
