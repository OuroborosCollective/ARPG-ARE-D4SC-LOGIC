using System;

namespace AreArpg.Core
{
    /// <summary>Houses of Aschengrenze that have a room of their own behind their door.</summary>
    public enum VillagePlace { None = 0, SickRoom = 1, Smithy = 2, Barn = 3 }

    /// <summary>
    /// The doors of the village houses that can be entered and the rooms behind them, in millimetres. A room is a
    /// walkable island far outside the village square: nothing of the village is in reach or in sight from inside,
    /// and a position in the village is never mistaken for one in a room. Passing a door moves the player between
    /// the door outside and the door inside. Which of the two is shown follows from where the player stands, so
    /// the rooms add no saved state: a game saved inside a room is saved in front of its house.
    /// </summary>
    public static class VillagePlaceLayout
    {
        public const int PlaceCount = 3;
        /// <summary>
        /// A door is used by walking into it. Its doormat reaches this far to either side of the threshold and this
        /// far in front of it: deep enough that a body which cannot come closer to the wall still stands on it.
        /// </summary>
        public const long DoorHalfWidthMm = 800, DoorDepthMm = 1500;
        /// <summary>After passing a door the player stands this far beyond the threshold, clear of the doormat.</summary>
        public const long DoorStepMm = 2300;
        /// <summary>Centre of the first room; the others follow to the east at this spacing.</summary>
        public const long FirstRoomXmm = 200000, RoomSpacingMm = 60000;
        private const long RoomClaimMm = 20000;
        /// <summary>
        /// The direction that leads out through the door of a room. Rooms are never rotated: the door is in the
        /// middle of the north wall. The house doors face north as well, so whoever walks into a house walks on into
        /// its room, and whoever walks out of the room walks on away from the house. No door is ever entered on the
        /// heading that leads straight back out through the next one.
        /// </summary>
        public const int RoomOutX = 0, RoomOutZ = 1;

        /// <summary>A piece of furniture as the rules see it: a block nobody walks through.</summary>
        public readonly struct Furniture
        {
            public readonly string Id;
            /// <summary>Centre and half extents relative to the middle of the room.</summary>
            public readonly long X, Z, HalfWidth, HalfDepth;
            public Furniture(string id, long x, long z, long halfWidth, long halfDepth)
            { Id = id; X = x; Z = z; HalfWidth = halfWidth; HalfDepth = halfDepth; }
        }

        private sealed class Room
        {
            public string Name, House;
            // Threshold of the house door in the village and the direction that leads away from the house.
            public long DoorX, DoorZ; public int OutX, OutZ;
            public long HalfWidth, HalfDepth;
            public Furniture[] Furniture;
        }

        // Every room has its door in the middle of the north wall, and its furniture leaves the way from that door
        // free. The houses are three of the village's existing buildings: the smithy and the stable already stand
        // there, the sick room takes the house west of the hall. All three have their front to the north: the porch
        // of the house, the open forge shed, the open stalls. The thresholds lie where those fronts are, not in the
        // middle of the wall.
        private static readonly Room[] Rooms =
        {
            new Room
            {
                Name = "Krankenstube", House = "home-west-centre", DoorX = -22000, DoorZ = -2800, OutX = 0, OutZ = 1,
                HalfWidth = 4000, HalfDepth = 3500,
                Furniture = new[]
                {
                    new Furniture("bed0", -2900, 2200, 1050, 500), new Furniture("bed1", -2900, 400, 1050, 500),
                    new Furniture("bed2", -2900, -1400, 1050, 500), new Furniture("hearth", 2150, 3050, 900, 450),
                    new Furniture("herbrack", -2050, 3250, 800, 250), new Furniture("table", 3300, 0, 700, 1100),
                    new Furniture("shelf", 3600, 2700, 400, 700), new Furniture("basin", 3300, -2400, 450, 450),
                }
            },
            new Room
            {
                Name = "Schmiede", House = "home-east-north", DoorX = 10900, DoorZ = 14200, OutX = 0, OutZ = 1,
                HalfWidth = 4500, HalfDepth = 4000,
                Furniture = new[]
                {
                    new Furniture("forge", -2450, 3400, 1150, 600), new Furniture("bellows", -4050, 3300, 450, 650),
                    new Furniture("anvil", -2200, 1100, 400, 400), new Furniture("quench", -3900, 1300, 400, 400),
                    new Furniture("bench", 4000, 800, 500, 1300), new Furniture("toolrack", -4200, -1300, 300, 1000),
                    new Furniture("orebin", 2050, 3400, 800, 550), new Furniture("ingots", 3650, 3400, 800, 550),
                    new Furniture("coal", -3300, -3200, 600, 600),
                }
            },
            new Room
            {
                Name = "Feldscheune", House = "home-west-north", DoorX = -19800, DoorZ = 13200, OutX = 0, OutZ = 1,
                HalfWidth = 5000, HalfDepth = 4000,
                Furniture = new[]
                {
                    new Furniture("grain", -3700, 2900, 1200, 1000), new Furniture("flour", 2150, 3400, 900, 550),
                    new Furniture("straw", 3800, 1100, 1200, 1700), new Furniture("seedbin", -4400, -300, 550, 700),
                    new Furniture("tools", -4650, -2400, 300, 800), new Furniture("cart", 2600, -2300, 1100, 650),
                }
            },
        };

        private static Room Of(VillagePlace place)
        {
            if (place < VillagePlace.SickRoom || (int)place > PlaceCount) throw new ArgumentOutOfRangeException(nameof(place));
            return Rooms[(int)place - 1];
        }

        /// <summary>What the house is called on the door and in the title of the area.</summary>
        public static string Name(VillagePlace place) { return Of(place).Name; }
        /// <summary>The village building (see <see cref="EntryWorldLayout"/>) whose door this is.</summary>
        public static string House(VillagePlace place) { return Of(place).House; }

        public static long DoorXmm(VillagePlace place) { return Of(place).DoorX; }
        public static long DoorZmm(VillagePlace place) { return Of(place).DoorZ; }
        /// <summary>Where the player stands after coming out of the house.</summary>
        public static long LeaveXmm(VillagePlace place) { Room r = Of(place); return r.DoorX + r.OutX * DoorStepMm; }
        public static long LeaveZmm(VillagePlace place) { Room r = Of(place); return r.DoorZ + r.OutZ * DoorStepMm; }
        /// <summary>The direction that leads away from the house door, one of the four compass steps.</summary>
        public static int DoorOutX(VillagePlace place) { return Of(place).OutX; }
        public static int DoorOutZ(VillagePlace place) { return Of(place).OutZ; }

        /// <summary>Middle of the room. Rooms are never rotated: the door is in the north wall (see <see cref="RoomOutZ"/>).</summary>
        public static long RoomXmm(VillagePlace place) { Of(place); return FirstRoomXmm + ((int)place - 1) * RoomSpacingMm; }
        public const long RoomZmm = 0;
        public static long HalfWidthMm(VillagePlace place) { return Of(place).HalfWidth; }
        public static long HalfDepthMm(VillagePlace place) { return Of(place).HalfDepth; }
        /// <summary>Threshold of the door seen from inside.</summary>
        public static long RoomDoorXmm(VillagePlace place) { return RoomXmm(place) + RoomOutX * Of(place).HalfWidth; }
        public static long RoomDoorZmm(VillagePlace place) { return RoomZmm + RoomOutZ * Of(place).HalfDepth; }
        /// <summary>Where the player stands after coming in.</summary>
        public static long EnterXmm(VillagePlace place) { return RoomDoorXmm(place) - RoomOutX * DoorStepMm; }
        public static long EnterZmm(VillagePlace place) { return RoomDoorZmm(place) - RoomOutZ * DoorStepMm; }

        public static int FurnitureCount(VillagePlace place) { return Of(place).Furniture.Length; }
        public static Furniture FurnitureAt(VillagePlace place, int index) { return Of(place).Furniture[index]; }
        /// <summary>The named piece, or a piece without an id when the room has none of that name.</summary>
        public static Furniture Find(VillagePlace place, string id)
        {
            foreach (Furniture piece in Of(place).Furniture) if (piece.Id == id) return piece;
            return default;
        }

        /// <summary>The house door in the village whose doormat the point is on, or none.</summary>
        public static VillagePlace DoorAt(long x, long z)
        {
            for (int i = 1; i <= PlaceCount; i++)
            {
                Room r = Rooms[i - 1];
                long outward = (x - r.DoorX) * r.OutX + (z - r.DoorZ) * r.OutZ;
                long along = (x - r.DoorX) * r.OutZ - (z - r.DoorZ) * r.OutX;
                if (outward >= 0 && outward <= DoorDepthMm && Math.Abs(along) <= DoorHalfWidthMm) return (VillagePlace)i;
            }
            return VillagePlace.None;
        }

        /// <summary>The room a point belongs to, or none: every point of the village square is outside all rooms.</summary>
        public static VillagePlace RoomAt(long x, long z)
        {
            if (x < FirstRoomXmm - RoomClaimMm || Math.Abs(z - RoomZmm) > RoomClaimMm) return VillagePlace.None;
            long index = (x - FirstRoomXmm + RoomClaimMm) / RoomSpacingMm;
            if (index >= PlaceCount) return VillagePlace.None;
            return Math.Abs(x - (FirstRoomXmm + index * RoomSpacingMm)) <= RoomClaimMm ? (VillagePlace)(index + 1) : VillagePlace.None;
        }

        /// <summary>Inside the walls and beside the furniture, with room for a body.</summary>
        public static bool CanStandInRoom(VillagePlace place, long x, long z)
        {
            Room r = Of(place);
            long dx = x - RoomXmm(place), dz = z - RoomZmm, body = ArenaSimulation.PlayerRadiusMm;
            if (Math.Abs(dx) > r.HalfWidth - body || Math.Abs(dz) > r.HalfDepth - body) return false;
            foreach (Furniture piece in r.Furniture)
                if (Math.Abs(dx - piece.X) < piece.HalfWidth + body && Math.Abs(dz - piece.Z) < piece.HalfDepth + body) return false;
            return true;
        }

        /// <summary>On the doormat inside the room: a step towards the door leads back into the village.</summary>
        public static bool AtRoomDoor(VillagePlace place, long x, long z)
        {
            long dx = x - RoomDoorXmm(place), dz = z - RoomDoorZmm(place);
            long inward = -(dx * RoomOutX + dz * RoomOutZ), along = dx * RoomOutZ - dz * RoomOutX;
            return inward >= 0 && inward <= DoorDepthMm && Math.Abs(along) <= DoorHalfWidthMm;
        }
    }

    public sealed partial class ArenaSimulation
    {
        /// <summary>The room the player is in, or none while out in the village or anywhere else.</summary>
        public VillagePlace InsidePlace
        {
            get { return Progression.EntryAct == 2 ? VillagePlaceLayout.RoomAt(PlayerXmm, PlayerZmm) : VillagePlace.None; }
        }

        /// <summary>The house door whose doormat the player stands on.</summary>
        public VillagePlace DoorInReach
        {
            get
            {
                return GameOver || Progression.EntryAct != 2 || InsidePlace != VillagePlace.None ?
                    VillagePlace.None : VillagePlaceLayout.DoorAt(PlayerXmm, PlayerZmm);
            }
        }

        /// <summary>Inside, on the doormat of the door that leads back out.</summary>
        public bool AtRoomDoor
        {
            get { VillagePlace place = InsidePlace; return !GameOver && place != VillagePlace.None && VillagePlaceLayout.AtRoomDoor(place, PlayerXmm, PlayerZmm); }
        }

        /// <summary>The house the player is walking into right now: on its doormat and heading for the door, which opens.</summary>
        public VillagePlace WalkingIntoHouse
        {
            get
            {
                VillagePlace place = DoorInReach;
                if (place == VillagePlace.None || DoorsBarred) return VillagePlace.None;
                long heading = _directionXbp * VillagePlaceLayout.DoorOutX(place) + _directionZbp * VillagePlaceLayout.DoorOutZ(place);
                return heading < 0 ? place : VillagePlace.None;
            }
        }

        /// <summary>The player is walking out through the door of the room.</summary>
        public bool WalkingOutOfRoom
        {
            get { return AtRoomDoor && _directionXbp * VillagePlaceLayout.RoomOutX + _directionZbp * VillagePlaceLayout.RoomOutZ > 0; }
        }

        /// <summary>Nobody opens a door to someone the village is up in arms against.</summary>
        public bool DoorsBarred
        {
            get
            {
                foreach (NpcAgent npc in Npcs)
                    if (npc.Memory.Health > 0 && npc.Mood == NpcDisposition.Hostile) return true;
                return false;
            }
        }

        /// <summary>Step through the house door the player stands at. False when there is none or it stays shut.</summary>
        public bool TryEnterPlace()
        {
            VillagePlace place = DoorInReach;
            if (place == VillagePlace.None || DoorsBarred) return false;
            PassDoor(VillagePlaceLayout.EnterXmm(place), VillagePlaceLayout.EnterZmm(place));
            return true;
        }

        /// <summary>Step back out through the door of the room. False when the player is not at it.</summary>
        public bool TryLeavePlace()
        {
            if (!AtRoomDoor) return false;
            VillagePlace place = InsidePlace;
            PassDoor(VillagePlaceLayout.LeaveXmm(place), VillagePlaceLayout.LeaveZmm(place));
            return true;
        }

        // Only the player passes the door. A companion waits in front of the house (see StepCompanion), and nothing
        // that is saved ever holds a position inside a room (see CaptureForgeVillage): saves keep their old limits.
        private void PassDoor(long x, long z)
        {
            PlayerXmm = PrevPlayerXmm = x;
            PlayerZmm = PrevPlayerZmm = z;
        }
    }
}
