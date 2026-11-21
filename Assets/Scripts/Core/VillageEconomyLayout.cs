using System;

namespace AreArpg.Core
{
    /// <summary>Places of the village economy inside Aschengrenze.</summary>
    public enum VillageSpot
    {
        Store = 0, Kitchen = 1, Woodyard = 2, Workbench = 3, Well = 4, Home = 5, Gate = 6, Board = 7, DryingRack = 8, GameRack = 9,
        Field = 10, Smithy = 11, SickRoom = 12
    }

    /// <summary>
    /// Where the work of the village is seen: for every place the point a resident stands at and the centre of its
    /// workplace, in millimetres like the village's building footprints. The workplaces are set dressing without
    /// collision; the Core's movement rules for the village are unchanged. Only the village board matters to the
    /// rules: the player hands goods over within reach of it. The smithy and the sick room are rooms of their own
    /// (see <see cref="VillagePlaceLayout"/>): their points lie in the room, and from the village only the house
    /// door is seen.
    /// </summary>
    public static class VillageEconomyLayout
    {
        public const int SpotCount = 13;
        /// <summary>The player reads the board and hands goods over within this distance of it.</summary>
        public const long BoardReachMm = 3500;
        /// <summary>A resident leaving for the forest or the mill is seen for this long on the way to the gate, and again arriving.</summary>
        public const int GateWalkTicks = 100;

        // Stand point (x, z) and workplace centre (x, z). The store is Mara's camp, the home the door of the centre
        // house in the east and the gate the north passage. The others were put where the village has free ground,
        // with the worker to the south of the workplace so that the work is seen from the road.
        private static readonly long[] Table =
        {
            /* Store      */  -3700,  -7000,   -4500,  -6000,
            /* Kitchen    */  -7700,  -1100,   -8600,   -400,
            /* Woodyard   */   5000,  -2600,    6200,  -1600,
            /* Workbench  */   8200,  -5000,    8200,  -3900,
            /* Well       */  -6100, -10900,   -7400, -10800,
            /* Home       */  10500,  -3000,   10500,  -3000,
            /* Gate       */      0,  22500,       0,  22500,
            /* Board      */   1500,  -6500,    2400,  -5500,
            /* DryingRack */  11100,  -9600,   11100,  -8400,
            /* GameRack   */  -9900,    500,  -10700,   1300,
            // The field lies in the open north-west of the square. Its place is the first row, the rows run north from it.
            /* Field      */ -11000,  11000,  -11000,  12500,
            // Inside the smithy: the smith east of the anvil. Inside the sick room: the carer at the herb table.
            /* Smithy     */ VillagePlaceLayout.FirstRoomXmm + VillagePlaceLayout.RoomSpacingMm - 1250, 1100,
                             VillagePlaceLayout.FirstRoomXmm + VillagePlaceLayout.RoomSpacingMm - 2200, 1100,
            /* SickRoom   */ VillagePlaceLayout.FirstRoomXmm + 2100, 0, VillagePlaceLayout.FirstRoomXmm + 3300, 0,
        };

        private static long At(VillageSpot spot, int column)
        {
            if (spot < VillageSpot.Store || (int)spot >= SpotCount) throw new ArgumentOutOfRangeException(nameof(spot));
            return Table[(int)spot * 4 + column];
        }

        public static long StandXmm(VillageSpot spot) { return At(spot, 0); }
        public static long StandZmm(VillageSpot spot) { return At(spot, 1); }
        public static long PlaceXmm(VillageSpot spot) { return At(spot, 2); }
        public static long PlaceZmm(VillageSpot spot) { return At(spot, 3); }

        /// <summary>The house a place of work lies in, or none for the places out in the village.</summary>
        public static VillagePlace HouseOf(VillageSpot spot)
        {
            return spot == VillageSpot.Smithy ? VillagePlace.Smithy : spot == VillageSpot.SickRoom ? VillagePlace.SickRoom : VillagePlace.None;
        }

        /// <summary>Where a resident bound for this place is seen in the village: at the place, or at the door of its house.</summary>
        public static long VillageXmm(VillageSpot spot)
        { VillagePlace house = HouseOf(spot); return house == VillagePlace.None ? StandXmm(spot) : VillagePlaceLayout.LeaveXmm(house); }
        public static long VillageZmm(VillageSpot spot)
        { VillagePlace house = HouseOf(spot); return house == VillagePlace.None ? StandZmm(spot) : VillagePlaceLayout.LeaveZmm(house); }

        /// <summary>The place a village work step is done at.</summary>
        public static VillageSpot SpotFor(VillageWork work)
        {
            switch (work)
            {
                case VillageWork.MakeFrame: case VillageWork.MakeBow: return VillageSpot.Workbench;
                case VillageWork.RepairAxe: case VillageWork.SmeltOre: case VillageWork.ForgeFitting: return VillageSpot.Smithy;
                case VillageWork.MakeBandage: return VillageSpot.SickRoom;
                case VillageWork.Cook: case VillageWork.BakeBread: case VillageWork.TendHearth: return VillageSpot.Kitchen;
                case VillageWork.SowField: case VillageWork.HarvestField: case VillageWork.MakeCompost: case VillageWork.DressSoil:
                    return VillageSpot.Field;
                case VillageWork.RepairShelter: case VillageWork.OfferWater: return VillageSpot.Home;
                case VillageWork.TanHide: return VillageSpot.DryingRack;
                case VillageWork.FetchWater: return VillageSpot.Well;
                case VillageWork.DeliverRations: return VillageSpot.Store;
                default: return VillageSpot.Woodyard;
            }
        }

        /// <summary>
        /// Where a resident is seen in the village, derived from the Core state alone. False while away in the
        /// forest or at the mill, on the far part of the way, and asleep in the house. On the way out the resident
        /// is seen walking to the gate; coming home, from the gate to the wood yard where the load is put down.
        /// Someone too hurt to work lies in the sick room.
        /// </summary>
        public static bool Seen(VillageResidentState resident, out VillageSpot spot)
        {
            if (resident == null) throw new ArgumentNullException(nameof(resident));
            spot = VillageSpot.Home;
            if (resident.Walking)
            {
                int whole = VillageEconomyRules.TravelTicks(resident.Site, resident.Destination);
                if (resident.Site == VillageSite.Village)
                {
                    spot = VillageSpot.Gate;
                    return resident.TravelTicks > whole - GateWalkTicks;
                }
                spot = VillageSpot.Woodyard;
                return resident.Destination == VillageSite.Village && resident.TravelTicks <= GateWalkTicks;
            }
            if (resident.Site != VillageSite.Village) return false;
            switch (resident.Activity)
            {
                case VillageActivity.Sleeping: return false;
                case VillageActivity.Eating: spot = VillageSpot.Store; return true;
                case VillageActivity.Drinking: spot = VillageSpot.Well; return true;
                case VillageActivity.Working: spot = SpotFor(resident.Work); return true;
                case VillageActivity.Recovering: spot = VillageSpot.SickRoom; return true;
                case VillageActivity.Resting: spot = VillageSpot.Home; return true;
                default: spot = VillageSpot.Board; return true;
            }
        }
    }

    public sealed partial class ArenaSimulation
    {
        /// <summary>The player stands at the village board: stocks and needs can be read and goods handed over.</summary>
        public bool VillageBoardInReach
        {
            get
            {
                return !GameOver && IsVillage &&
                    DistanceSquared(PlayerXmm, PlayerZmm, VillageEconomyLayout.PlaceXmm(VillageSpot.Board), VillageEconomyLayout.PlaceZmm(VillageSpot.Board)) <=
                    FixedMath.Square(VillageEconomyLayout.BoardReachMm);
            }
        }
    }
}
