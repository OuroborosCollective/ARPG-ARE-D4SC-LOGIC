using System;

namespace AreArpg.Core
{
    /// <summary>Goods of the village economy. Order is persisted; never reorder without a migration.</summary>
    public enum VillageGood
    {
        Wood = 0, ThornBerry = 1, WoodFrame = 2, Bow = 3, Axe = 4, BrokenAxe = 5,
        Game = 6, Hide = 7, Ration = 8, Metal = 9, Seedling = 10, Water = 11, Herb = 12,
        SeedGrain = 13, Grain = 14, Flour = 15, Straw = 16, Compost = 17, Ore = 18,
        Stone = 19, Fitting = 20, Leather = 21, Bandage = 22
    }

    /// <summary>Natural stocks. They change only by named work or by their own written regeneration rule.</summary>
    public enum VillageSource
    {
        MatureTrees = 0, SeedCrop = 1, Wildlife = 2, Berries = 3, Well = 4, Herbs = 5,
        OreVein = 6, Rubble = 7, Soil = 8, CropTicks = 9, RipePlot = 10, Shelter = 11, Hearth = 12
    }

    /// <summary>Where a resident can be: the village, the forest edge, or Orsel's sick beds at the old mill.</summary>
    public enum VillageSite { Village = 0, Forest = 1, Mill = 2 }

    /// <summary>One step of a trade. Order is persisted.</summary>
    public enum VillageWork
    {
        None = 0, FellTree = 1, CollectSeedlings = 2, PlantSeedling = 3, GatherBerries = 4, Hunt = 5,
        MakeFrame = 6, MakeBow = 7, FetchWater = 8, Cook = 9, DeliverRations = 10, RepairAxe = 11, GoHome = 12,
        GatherHerbs = 13, BringHerbs = 14, SeekCare = 15, SowField = 16, HarvestField = 17,
        MillGrain = 18, BakeBread = 19, MakeCompost = 20, DressSoil = 21, RecoverOre = 22,
        GatherStone = 23, SmeltOre = 24, ForgeFitting = 25, TanHide = 26, MakeBandage = 27,
        RepairShelter = 28, TendHearth = 29, OfferWater = 30
    }

    /// <summary>What a resident is visibly doing; derived from the decision, for the projection and the village board.</summary>
    public enum VillageActivity { Idle = 0, Working = 1, Walking = 2, Resting = 3, Sleeping = 4, Eating = 5, Recovering = 6, Drinking = 7 }

    /// <summary>A good and an amount, used by the work table.</summary>
    public readonly struct GoodAmount
    {
        public readonly VillageGood Good;
        public readonly int Count;
        public GoodAmount(VillageGood good, int count) { Good = good; Count = count; }
    }

    /// <summary>
    /// One work step: where it happens, how long it takes, the tool it needs in hand, what it takes from the village
    /// commons when it starts (held back until it ends), what it takes from nature and what it yields when it ends.
    /// </summary>
    public sealed class VillageWorkRule
    {
        public readonly VillageWork Work;
        public readonly VillageSite Site;
        public readonly int Ticks;
        public readonly int Tool;            // VillageGood or -1
        public readonly GoodAmount[] Inputs;
        public readonly GoodAmount[] Outputs;
        public readonly int Source;          // VillageSource or -1
        public readonly int SourceUse;
        internal VillageWorkRule(VillageWork work, VillageSite site, int ticks, int tool, GoodAmount[] inputs, GoodAmount[] outputs, int source, int sourceUse)
        { Work = work; Site = site; Ticks = ticks; Tool = tool; Inputs = inputs; Outputs = outputs; Source = source; SourceUse = sourceUse; }
    }

    /// <summary>
    /// Versioned content of the village economy (owner concept "NPC-Wirtschaft und Lebenszyklus", 2026-11-21).
    /// Nothing appears from nowhere: every good comes from a named start stock, from nature through a work step, from
    /// another good through a recipe, or from the player. Numbers are content of this revision, not engine rules;
    /// changing them needs a new revision.
    /// </summary>
    public static class VillageEconomyRules
    {
        public const int Revision = 2;
        public const int GoodCount = 23;
        public const int SourceCount = 13;
        public const int WorkCount = 31;

        // One village plot. Sowing uses a measure of fertile soil; harvest keeps seed and straw
        // for the next season. The two rock deposits are finite, visible sources on the mill route.
        public const int CropGrowthTicks = (int)WorldSkyClock.TicksPerDay / 2;
        public const int SoilCapacity = 2, ShelterCapacity = 3, OreReserve = 12, StoneReserve = 12;
        public const int ShelterWearTicks = 2 * (int)WorldSkyClock.TicksPerDay;

        /// <summary>The economy is evaluated once per simulated second; every duration is a multiple of it.</summary>
        public const int Stride = 10;
        /// <summary>The way between the village and the forest edge, one direction.</summary>
        public const int ForestTravelTicks = 300;
        /// <summary>The way between the village and Orsel's sick beds at the old mill, one direction.</summary>
        public const int MillTravelTicks = 400;
        /// <summary>A resident carries this many goods at once; the tools in hand do not count.</summary>
        public const int CarryCapacity = 6;
        public const int AxeUses = 12;
        /// <summary>Seed trees that are never felled: without them the forest could not renew itself.</summary>
        public const int SeedTreeReserve = 6;
        public const int StandTarget = 24;
        /// <summary>A planted seedling is a tree that can be felled after three game days.</summary>
        public const ulong SaplingGrowthTicks = 3UL * WorldSkyClock.TicksPerDay;
        public const int WildlifeCap = 8, WildlifeBreeders = 2, WildlifeRegenTicks = 3600;
        public const int BerryCap = 8, BerryRegenTicks = 1200;
        public const int SeedCropRegenTicks = 1200;
        public const int WellCap = 12, WellRegenTicks = 100;
        public const int HerbCap = 6, HerbRegenTicks = 1800;
        /// <summary>The village keeps this many healing herbs with Orsel; her shelf holds no more than the cap.</summary>
        public const int HerbTarget = 3, MillHerbCap = 6, HerbLoad = 4;
        /// <summary>Orsel's care: with a healing herb one health per 10 ticks and one herb per 100 ticks; without herbs one per 30 ticks.</summary>
        public const int CareHealTicks = 10, CareHerbTicks = 100, PlainCareHealTicks = 30;
        /// <summary>The village cooks and delivers while the store and the food on its way hold fewer rations than this.</summary>
        public const int FoodTarget = 8;
        public const int WoodTarget = 8;
        public const int DeliveryLoad = 4;

        // Needs: the same scales as the three established villagers.
        public const int MaxHealth = NpcMemory.MaxHealth;
        public const int MaxNeed = 1000;
        public const int HungerTicks = 20;          // +1 hunger
        public const int EatHunger = 700;           // the established threshold for eating a camp ration
        public const int EatRelief = 600;
        public const int WorkFatigueTicks = 10;     // +1 fatigue while working or walking
        public const int RestRelief = 3;            // per tick, as for the established villagers
        /// <summary>Below this health a resident does not work; rest heals slowly.</summary>
        public const int WorkHealth = MaxHealth - 120;
        public const int RestHealTicks = 100;       // +1 health while resting
        /// <summary>Hunger alone never kills in this revision: it wears a resident down to this health and no further.</summary>
        public const int StarvingHealthFloor = 200;
        public const int StarvingTicks = 100;       // -1 health at full hunger
        /// <summary>One hunt in this many ends with an injury; decided by an integer mix of tick and resident.</summary>
        public const int HuntInjuryOneIn = 24;
        public const int HuntInjury = 200;

        /// <summary>Night: residents go home and sleep. Phase 0 is noon, half a day later is midnight.</summary>
        public const ulong NightStart = WorldSkyClock.TicksPerDay / 3;
        public const ulong NightEnd = WorldSkyClock.TicksPerDay * 2 / 3;

        private static GoodAmount G(VillageGood good, int count) { return new GoodAmount(good, count); }
        private static readonly GoodAmount[] Nothing = new GoodAmount[0];

        private static readonly VillageWorkRule[] Table =
        {
            new VillageWorkRule(VillageWork.None, VillageSite.Village, 0, -1, Nothing, Nothing, -1, 0),
            new VillageWorkRule(VillageWork.FellTree, VillageSite.Forest, 600, (int)VillageGood.Axe, Nothing, new[] { G(VillageGood.Wood, 3) }, (int)VillageSource.MatureTrees, 1),
            new VillageWorkRule(VillageWork.CollectSeedlings, VillageSite.Forest, 300, -1, Nothing, new[] { G(VillageGood.Seedling, 2) }, (int)VillageSource.SeedCrop, 2),
            new VillageWorkRule(VillageWork.PlantSeedling, VillageSite.Forest, 200, -1, new[] { G(VillageGood.Seedling, 1) }, Nothing, -1, 0),
            new VillageWorkRule(VillageWork.GatherBerries, VillageSite.Forest, 300, -1, Nothing, new[] { G(VillageGood.ThornBerry, 2) }, (int)VillageSource.Berries, 2),
            new VillageWorkRule(VillageWork.Hunt, VillageSite.Forest, 1500, (int)VillageGood.Bow, Nothing, new[] { G(VillageGood.Game, 1) }, (int)VillageSource.Wildlife, 1),
            // The two recipes the player already has: 2 wood -> 1 frame, 1 frame + 1 thorn berry -> 1 bow.
            new VillageWorkRule(VillageWork.MakeFrame, VillageSite.Village, 300, -1, new[] { G(VillageGood.Wood, 2) }, new[] { G(VillageGood.WoodFrame, 1) }, -1, 0),
            new VillageWorkRule(VillageWork.MakeBow, VillageSite.Village, 450, -1, new[] { G(VillageGood.WoodFrame, 1), G(VillageGood.ThornBerry, 1) }, new[] { G(VillageGood.Bow, 1) }, -1, 0),
            new VillageWorkRule(VillageWork.FetchWater, VillageSite.Village, 100, -1, Nothing, new[] { G(VillageGood.Water, 2) }, (int)VillageSource.Well, 2),
            // One animal is used once: two rations and a hide, cooked with water over a wood fire.
            new VillageWorkRule(VillageWork.Cook, VillageSite.Village, 400, -1, new[] { G(VillageGood.Game, 1), G(VillageGood.Water, 1), G(VillageGood.Wood, 1) }, new[] { G(VillageGood.Ration, 2), G(VillageGood.Hide, 1) }, -1, 0),
            new VillageWorkRule(VillageWork.DeliverRations, VillageSite.Village, 60, -1, Nothing, Nothing, -1, 0),
            new VillageWorkRule(VillageWork.RepairAxe, VillageSite.Village, 600, -1, new[] { G(VillageGood.BrokenAxe, 1), G(VillageGood.Metal, 1), G(VillageGood.Wood, 1) }, new[] { G(VillageGood.Axe, 1) }, -1, 0),
            new VillageWorkRule(VillageWork.GoHome, VillageSite.Village, 0, -1, Nothing, Nothing, -1, 0),
            new VillageWorkRule(VillageWork.GatherHerbs, VillageSite.Forest, 300, -1, Nothing, new[] { G(VillageGood.Herb, 2) }, (int)VillageSource.Herbs, 2),
            // Herbs are carried to the old mill and put on Orsel's shelf: a hand-over, nothing is made or used.
            new VillageWorkRule(VillageWork.BringHerbs, VillageSite.Mill, 60, -1, Nothing, Nothing, -1, 0),
            new VillageWorkRule(VillageWork.SeekCare, VillageSite.Mill, 0, -1, Nothing, Nothing, -1, 0),
            new VillageWorkRule(VillageWork.SowField, VillageSite.Village, 220, (int)VillageGood.Axe,
                new[] { G(VillageGood.SeedGrain, 1), G(VillageGood.Water, 1) }, Nothing, (int)VillageSource.Soil, 1),
            new VillageWorkRule(VillageWork.HarvestField, VillageSite.Village, 260, -1, Nothing,
                new[] { G(VillageGood.Grain, 4), G(VillageGood.SeedGrain, 1), G(VillageGood.Straw, 2) }, (int)VillageSource.RipePlot, 1),
            new VillageWorkRule(VillageWork.MillGrain, VillageSite.Mill, 280, -1, new[] { G(VillageGood.Grain, 2) }, new[] { G(VillageGood.Flour, 2) }, -1, 0),
            new VillageWorkRule(VillageWork.BakeBread, VillageSite.Village, 240, -1,
                new[] { G(VillageGood.Flour, 1), G(VillageGood.Water, 1), G(VillageGood.Wood, 1) }, new[] { G(VillageGood.Ration, 3) }, -1, 0),
            new VillageWorkRule(VillageWork.MakeCompost, VillageSite.Village, 180, -1,
                new[] { G(VillageGood.Straw, 1), G(VillageGood.Water, 1) }, new[] { G(VillageGood.Compost, 1) }, -1, 0),
            new VillageWorkRule(VillageWork.DressSoil, VillageSite.Village, 140, -1, new[] { G(VillageGood.Compost, 1) }, Nothing, -1, 0),
            new VillageWorkRule(VillageWork.RecoverOre, VillageSite.Forest, 420, -1, Nothing, new[] { G(VillageGood.Ore, 2) }, (int)VillageSource.OreVein, 2),
            new VillageWorkRule(VillageWork.GatherStone, VillageSite.Forest, 300, -1, Nothing, new[] { G(VillageGood.Stone, 2) }, (int)VillageSource.Rubble, 2),
            new VillageWorkRule(VillageWork.SmeltOre, VillageSite.Village, 340, -1,
                new[] { G(VillageGood.Ore, 1), G(VillageGood.Wood, 1) }, new[] { G(VillageGood.Metal, 1) }, -1, 0),
            new VillageWorkRule(VillageWork.ForgeFitting, VillageSite.Village, 240, -1,
                new[] { G(VillageGood.Metal, 1), G(VillageGood.Wood, 1), G(VillageGood.Water, 1) }, new[] { G(VillageGood.Fitting, 1) }, -1, 0),
            new VillageWorkRule(VillageWork.TanHide, VillageSite.Village, 320, -1,
                new[] { G(VillageGood.Hide, 1), G(VillageGood.Water, 1) }, new[] { G(VillageGood.Leather, 1) }, -1, 0),
            // Clean fibre padding held by a leather strip; healing herbs remain a separate supply.
            new VillageWorkRule(VillageWork.MakeBandage, VillageSite.Village, 140, -1,
                new[] { G(VillageGood.Leather, 1), G(VillageGood.Straw, 1), G(VillageGood.Water, 1) }, new[] { G(VillageGood.Bandage, 1) }, -1, 0),
            new VillageWorkRule(VillageWork.RepairShelter, VillageSite.Village, 400, -1,
                new[] { G(VillageGood.Wood, 2), G(VillageGood.Stone, 1), G(VillageGood.Fitting, 1) }, Nothing, -1, 0),
            new VillageWorkRule(VillageWork.TendHearth, VillageSite.Village, 90, -1, new[] { G(VillageGood.Wood, 1) }, Nothing, -1, 0),
            new VillageWorkRule(VillageWork.OfferWater, VillageSite.Village, 90, -1, new[] { G(VillageGood.Water, 1) }, Nothing, -1, 0),
        };

        /// <summary>Ticks for the way between two sites. Forest and mill are both reached through the village.</summary>
        public static int TravelTicks(VillageSite from, VillageSite to)
        {
            if (from == to) return 0;
            if (from == VillageSite.Forest || to == VillageSite.Forest) return from == VillageSite.Mill || to == VillageSite.Mill ? ForestTravelTicks + MillTravelTicks : ForestTravelTicks;
            return MillTravelTicks;
        }

        public static VillageWorkRule Rule(VillageWork work)
        {
            if (work < VillageWork.None || (int)work >= WorkCount) throw new ArgumentOutOfRangeException(nameof(work));
            return Table[(int)work];
        }

        /// <summary>The stated start stock of the commons when the first resident moves in. Limited on purpose.</summary>
        public static int StartStock(VillageGood good)
        {
            switch (good)
            {
                case VillageGood.Wood: return 4;
                case VillageGood.Axe: return 2;      // one to work with, one spare so a broken axe is no silent dead end
                case VillageGood.Metal: return 2;    // recovered scrap; further metal must be smelted or delivered
                case VillageGood.Water: return 2;
                case VillageGood.Seedling: return 4; // stands in the nursery at the forest edge
                case VillageGood.SeedGrain: return 2;
                default: return 0;
            }
        }

        public static int StartSource(VillageSource source)
        {
            switch (source)
            {
                case VillageSource.MatureTrees: return 24;
                case VillageSource.SeedCrop: return 2;
                case VillageSource.Wildlife: return 6;
                case VillageSource.Berries: return 8;
                case VillageSource.Well: return WellCap;
                case VillageSource.Herbs: return HerbCap;
                case VillageSource.OreVein: return OreReserve;
                case VillageSource.Rubble: return StoneReserve;
                case VillageSource.Soil: return SoilCapacity;
                case VillageSource.Shelter: return ShelterCapacity;
                case VillageSource.Hearth: return 1;
                case VillageSource.CropTicks:
                case VillageSource.RipePlot: return 0;
                default: throw new ArgumentOutOfRangeException(nameof(source));
            }
        }

        internal static bool ValidProductionSources(int[] s)
        {
            int ticks = s[(int)VillageSource.CropTicks];
            return s[(int)VillageSource.OreVein] <= OreReserve && s[(int)VillageSource.Rubble] <= StoneReserve &&
                s[(int)VillageSource.Soil] <= SoilCapacity && ticks <= CropGrowthTicks && ticks % Stride == 0 &&
                s[(int)VillageSource.RipePlot] <= 1 && (ticks == 0 || s[(int)VillageSource.RipePlot] == 0) &&
                s[(int)VillageSource.Shelter] <= ShelterCapacity && s[(int)VillageSource.Hearth] <= 1;
        }

        public static bool IsNight(ulong tick)
        {
            ulong phase = WorldSkyClock.PhaseTicks(tick);
            return phase >= NightStart && phase < NightEnd;
        }

        /// <summary>Integer mix for the rare hunting injury: same tick and resident, same result on every device.</summary>
        public static bool HuntInjures(ulong tick, int ordinal)
        {
            unchecked
            {
                ulong h = tick * 0x9E3779B97F4A7C15UL ^ ((ulong)ordinal + 0x632BE59BD9B4E019UL) * 0xC2B2AE3D27D4EB4FUL;
                h ^= h >> 29; h *= 0xBF58476D1CE4E5B9UL; h ^= h >> 32;
                return h % (ulong)HuntInjuryOneIn == 0;
            }
        }

        public static string GoodName(VillageGood good)
        {
            switch (good)
            {
                case VillageGood.Wood: return "Holz";
                case VillageGood.ThornBerry: return "Dornbeeren";
                case VillageGood.WoodFrame: return "Holzrahmen";
                case VillageGood.Bow: return "Bogen";
                case VillageGood.Axe: return "Axt";
                case VillageGood.BrokenAxe: return "defekte Axt";
                case VillageGood.Game: return "Wild";
                case VillageGood.Hide: return "Häute";
                case VillageGood.Ration: return "Rationen";
                case VillageGood.Metal: return "Metall";
                case VillageGood.Seedling: return "Setzlinge";
                case VillageGood.Water: return "Wasser";
                case VillageGood.Herb: return "Heilkräuter";
                case VillageGood.SeedGrain: return "Saatgut";
                case VillageGood.Grain: return "Getreide";
                case VillageGood.Flour: return "Mehl";
                case VillageGood.Straw: return "Stroh";
                case VillageGood.Compost: return "Kompost";
                case VillageGood.Ore: return "Erz";
                case VillageGood.Stone: return "Bruchstein";
                case VillageGood.Fitting: return "Beschläge";
                case VillageGood.Leather: return "Leder";
                case VillageGood.Bandage: return "Verbände";
                default: throw new ArgumentOutOfRangeException(nameof(good));
            }
        }

        public static string WorkName(VillageWork work)
        {
            switch (work)
            {
                case VillageWork.FellTree: return "fällt einen Baum";
                case VillageWork.CollectSeedlings: return "sammelt Setzlinge";
                case VillageWork.PlantSeedling: return "pflanzt einen Setzling";
                case VillageWork.GatherBerries: return "sammelt Dornbeeren";
                case VillageWork.Hunt: return "jagt";
                case VillageWork.MakeFrame: return "baut einen Holzrahmen";
                case VillageWork.MakeBow: return "baut einen Bogen";
                case VillageWork.FetchWater: return "holt Wasser";
                case VillageWork.Cook: return "bereitet Rationen zu";
                case VillageWork.DeliverRations: return "bringt Rationen zu Mara";
                case VillageWork.RepairAxe: return "repariert eine Axt";
                case VillageWork.GoHome: return "geht ins Dorf zurück";
                case VillageWork.GatherHerbs: return "sammelt Heilkräuter";
                case VillageWork.BringHerbs: return "bringt Heilkräuter zu Orsel";
                case VillageWork.SeekCare: return "geht zu Orsel an die Mühle";
                case VillageWork.SowField: return "bestellt das Feld";
                case VillageWork.HarvestField: return "erntet Getreide";
                case VillageWork.MillGrain: return "mahlt Getreide";
                case VillageWork.BakeBread: return "backt Brot für Maras Lager";
                case VillageWork.MakeCompost: return "setzt Kompost an";
                case VillageWork.DressSoil: return "bringt Kompost aufs Feld";
                case VillageWork.RecoverOre: return "birgt Erz";
                case VillageWork.GatherStone: return "sammelt Bruchstein";
                case VillageWork.SmeltOre: return "verhüttet Erz";
                case VillageWork.ForgeFitting: return "schmiedet Beschläge";
                case VillageWork.TanHide: return "gerbt eine Haut";
                case VillageWork.MakeBandage: return "bindet einen Verband";
                case VillageWork.RepairShelter: return "repariert das Dach";
                case VillageWork.TendHearth: return "schürt das Herdfeuer";
                case VillageWork.OfferWater: return "bringt Wasser zur Krankenstube";
                default: return "wartet";
            }
        }
    }
}
