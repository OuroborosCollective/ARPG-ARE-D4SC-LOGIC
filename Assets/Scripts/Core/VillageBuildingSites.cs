using System;
using System.Globalization;
using System.Text;

namespace AreArpg.Core
{
    /// <summary>How far a house of the village has come: a vacant lot, a lot with everything delivered, the house.</summary>
    public enum BuildStage { Site = 0, Raising = 1, Built = 2 }

    /// <summary>
    /// What the houses of Aschengrenze cost, who asks for them and what they are good for (owner decisions of
    /// 22.11.2026). Every house begins as a vacant lot. Somebody stands in front of it and asks the player for
    /// materials. With everything handed over that person says thanks and sets to work; the house stands when the
    /// player next comes to the village. The smith asks first, the farmer once the smithy stands. Orsel asks for the
    /// sick room after she has moved to the village: when her mill quest is done and the Rattler is defeated.
    /// </summary>
    public static class VillageBuildRules
    {
        /// <summary>Every house asks for two kinds of material.</summary>
        public const int NeedCount = 2;
        /// <summary>The person asking stands this far beside the door and this far in front of the wall.</summary>
        public const long AskerAsideMm = 1700, AskerOutMm = 1900;
        /// <summary>Materials are handed over within this distance of the person asking.</summary>
        public const long ReachMm = 2600;

        public readonly struct Need
        {
            public readonly VillageGood Good;
            public readonly int Count;
            public Need(VillageGood good, int count) { Good = good; Count = count; }
        }

        // In the order of VillagePlace. Only materials the player can carry: wood, ash-iron and ashen lilac.
        private static readonly Need[][] Needs =
        {
            new[] { new Need(VillageGood.Wood, 8), new Need(VillageGood.Herb, 4) },
            new[] { new Need(VillageGood.Wood, 10), new Need(VillageGood.Metal, 6) },
            new[] { new Need(VillageGood.Wood, 12), new Need(VillageGood.Metal, 4) },
        };

        private static int Index(VillagePlace place)
        {
            if (place < VillagePlace.SickRoom || (int)place > VillagePlaceLayout.PlaceCount) throw new ArgumentOutOfRangeException(nameof(place));
            return (int)place - 1;
        }

        public static Need NeedAt(VillagePlace place, int need)
        {
            if (need < 0 || need >= NeedCount) throw new ArgumentOutOfRangeException(nameof(need));
            return Needs[Index(place)][need];
        }

        /// <summary>Which of the two needs of a house a good answers, or -1.</summary>
        public static int NeedFor(VillagePlace place, VillageGood good)
        {
            Need[] needs = Needs[Index(place)];
            for (int i = 0; i < needs.Length; i++) if (needs[i].Good == good) return i;
            return -1;
        }

        /// <summary>Who stands at the lot and asks.</summary>
        public static string Asker(VillagePlace place)
        {
            switch (place)
            {
                case VillagePlace.SickRoom: return "Orsel";
                case VillagePlace.Smithy: return "Der Schmied";
                default: Index(place); return "Der Bauer";
            }
        }

        /// <summary>The material as the player carries it.</summary>
        public static string GoodName(VillageGood good)
        {
            switch (good)
            {
                case VillageGood.Wood: return "Holz";
                case VillageGood.Metal: return "Ascheisen";
                case VillageGood.Herb: return "Aschenflieder";
                default: return VillageEconomyRules.GoodName(good);
            }
        }

        /// <summary>How many units of a material the player has in the pack.</summary>
        public static int Carried(CharacterProgression p, VillageGood good)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            switch (good)
            {
                case VillageGood.Wood: return p.Crafting.MaterialCount(MaterialKind.Holz);
                case VillageGood.Metal: return p.Crafting.MaterialCount(MaterialKind.Ascheisen);
                case VillageGood.Herb: return p.Crafting.MaterialCount(MaterialKind.Ashenflieder);
                default: return 0;
            }
        }

        /// <summary>Why the person at the lot wants the house.</summary>
        public static string Request(VillagePlace place)
        {
            switch (place)
            {
                case VillagePlace.SickRoom:
                    return "An der Mühle liegen meine Kranken im Zug und im Dreck. Hilf mir, dann verlege ich die Krankenstube ins Dorf. " +
                        "Hier ist es sauberer und sicherer, für sie und für ihre Behandlung.";
                case VillagePlace.Smithy:
                    return "Ich schmiede unter freiem Himmel, und bei jedem Wind geht mir die Glut aus. Hilf mir, dann baue ich eine richtige Schmiede. " +
                        "Drinnen geht die Arbeit schneller und sicherer von der Hand.";
                default:
                    Index(place);
                    return "Korn und Saat liegen offen, die Asche verdirbt sie. Hilf mir, dann baue ich eine Scheune. " +
                        "Mit trockenem Lager und Gerät unter Dach schaffen wir auf dem Feld mehr.";
            }
        }

        /// <summary>Where the person asking stands: in front of the lot, on the side of the door away from the sign.</summary>
        public static long AskerXmm(VillagePlace place)
        { return VillagePlaceLayout.DoorXmm(place) + VillagePlaceLayout.DoorOutX(place) * AskerOutMm - VillagePlaceLayout.DoorOutZ(place) * AskerAsideMm; }
        public static long AskerZmm(VillagePlace place)
        { return VillagePlaceLayout.DoorZmm(place) + VillagePlaceLayout.DoorOutZ(place) * AskerOutMm + VillagePlaceLayout.DoorOutX(place) * AskerAsideMm; }

        /// <summary>The work a standing house makes quicker: it is done under a roof, with everything at hand.</summary>
        public static bool Speeds(VillagePlace place, VillageWork work)
        {
            switch (place)
            {
                case VillagePlace.Smithy: return work == VillageWork.RepairAxe || work == VillageWork.SmeltOre || work == VillageWork.ForgeFitting;
                case VillagePlace.SickRoom: return work == VillageWork.MakeBandage;
                case VillagePlace.Barn:
                    return work == VillageWork.SowField || work == VillageWork.HarvestField || work == VillageWork.MakeCompost || work == VillageWork.DressSoil;
                default: return false;
            }
        }

        /// <summary>
        /// How long a step of work takes. Without its house it takes what it always took in the open; in the house
        /// two thirds of that, kept on the economy's stride.
        /// </summary>
        public static int WorkTicks(VillageWorkRule rule, VillageBuildingState houses)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (houses == null || rule.Ticks <= VillageEconomyRules.Stride) return rule.Ticks;
            for (int i = 1; i <= VillagePlaceLayout.PlaceCount; i++)
                if (houses.Built((VillagePlace)i) && Speeds((VillagePlace)i, rule.Work))
                    return Math.Max(VillageEconomyRules.Stride, rule.Ticks * 2 / 3 / VillageEconomyRules.Stride * VillageEconomyRules.Stride);
            return rule.Ticks;
        }

        /// <summary>Orsel moves to the village once her mill quest is done and the Rattler is defeated.</summary>
        public static bool OrselReady(CharacterProgression p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            return p.Village.OrselTeachesUnlocked && p.Rattler.Active && p.Rattler.Creature.IsDead;
        }

        /// <summary>Somebody stands at this lot and asks: the smith from the start, the farmer after him, Orsel once she has come.</summary>
        public static bool Offered(CharacterProgression p, VillagePlace place)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            if (p.EntryAct < 2) return false;
            switch (place)
            {
                case VillagePlace.Smithy: return true;
                case VillagePlace.Barn: return p.Houses.Built(VillagePlace.Smithy);
                case VillagePlace.SickRoom: return p.Houses.OrselInVillage || OrselReady(p);
                default: Index(place); return false;
            }
        }

        /// <summary>What still has to be brought, in words: "10 Holz und 6 Ascheisen".</summary>
        public static string MissingWords(VillageBuildingState houses, VillagePlace place)
        {
            if (houses == null) throw new ArgumentNullException(nameof(houses));
            var b = new StringBuilder();
            for (int i = 0; i < NeedCount; i++)
            {
                int missing = houses.Missing(place, i);
                if (missing == 0) continue;
                if (b.Length != 0) b.Append(" und ");
                b.Append(missing.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(GoodName(NeedAt(place, i).Good));
            }
            return b.ToString();
        }

        /// <summary>What the person at the lot says right now.</summary>
        public static string Words(CharacterProgression p, VillagePlace place)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            string who = Asker(place) + ": ";
            switch (p.Houses.Stage(place))
            {
                case BuildStage.Built: return who + "Das Haus steht. Danke für deine Hilfe.";
                case BuildStage.Raising: return who + "Ich bin an der Arbeit. Komm später wieder.";
                default: return who + Request(place) + " Mir fehlen noch " + MissingWords(p.Houses, place) + ".";
            }
        }

        /// <summary>The thanks when the last piece is handed over.</summary>
        public static string Thanks(VillagePlace place) { return Asker(place) + ": Vielen Dank. Ich mache mich an die Arbeit. Komm später wieder."; }
    }

    /// <summary>
    /// The houses of the village as the save knows them: per house its stage and what has been delivered, and whether
    /// Orsel has come. Own AC22 section "houses"; a save without it has three vacant lots. Delivered materials are
    /// built into the house: they never enter the stocks of the village economy.
    /// </summary>
    public sealed class VillageBuildingState
    {
        private readonly int[] _stage = new int[VillagePlaceLayout.PlaceCount];
        private readonly int[] _delivered = new int[VillagePlaceLayout.PlaceCount * VillageBuildRules.NeedCount];

        /// <summary>Orsel has moved to the village. Once true it stays true, whatever happens to the Rattler's den later.</summary>
        public bool OrselInVillage { get; private set; }

        private static int Index(VillagePlace place)
        {
            if (place < VillagePlace.SickRoom || (int)place > VillagePlaceLayout.PlaceCount) throw new ArgumentOutOfRangeException(nameof(place));
            return (int)place - 1;
        }

        public BuildStage Stage(VillagePlace place) { return (BuildStage)_stage[Index(place)]; }
        /// <summary>The house stands. False for no house at all.</summary>
        public bool Built(VillagePlace place) { return place != VillagePlace.None && Stage(place) == BuildStage.Built; }

        public int Delivered(VillagePlace place, int need)
        {
            if (need < 0 || need >= VillageBuildRules.NeedCount) throw new ArgumentOutOfRangeException(nameof(need));
            return _delivered[Index(place) * VillageBuildRules.NeedCount + need];
        }

        public int Missing(VillagePlace place, int need) { return VillageBuildRules.NeedAt(place, need).Count - Delivered(place, need); }

        /// <summary>Anything differs from three untouched lots.</summary>
        public bool HasProgress
        {
            get
            {
                if (OrselInVillage) return true;
                foreach (int stage in _stage) if (stage != 0) return true;
                foreach (int count in _delivered) if (count != 0) return true;
                return false;
            }
        }

        /// <summary>Where work that belongs into a house is done while the house does not stand: out in the open, as before.</summary>
        public VillageSpot Outdoors(VillageSpot spot)
        {
            if (spot == VillageSpot.Smithy && !Built(VillagePlace.Smithy)) return VillageSpot.Workbench;
            if (spot == VillageSpot.SickRoom && !Built(VillagePlace.SickRoom)) return VillageSpot.Home;
            return spot;
        }

        /// <summary>Take materials for a lot. Returns how many were taken; the last piece sets the builder to work.</summary>
        internal int Deliver(VillagePlace place, int need, int count)
        {
            int i = Index(place);
            if (_stage[i] != (int)BuildStage.Site || count <= 0) return 0;
            int taken = Math.Min(count, Missing(place, need));
            _delivered[i * VillageBuildRules.NeedCount + need] += taken;
            bool complete = true;
            for (int n = 0; n < VillageBuildRules.NeedCount; n++) complete &= Missing(place, n) == 0;
            if (complete) _stage[i] = (int)BuildStage.Raising;
            return taken;
        }

        /// <summary>The player comes to the village again: every house that was being raised stands. True when one did.</summary>
        internal bool Raise(out bool sickRoom)
        {
            bool any = false; sickRoom = false;
            for (int i = 0; i < _stage.Length; i++)
            {
                if (_stage[i] != (int)BuildStage.Raising) continue;
                _stage[i] = (int)BuildStage.Built;
                any = true;
                sickRoom |= i == Index(VillagePlace.SickRoom);
            }
            return any;
        }

        internal void NoteOrsel() { OrselInVillage = true; }

        /// <summary>The finished house without the way there: for fixtures and for tools that stage a village.</summary>
        internal void Complete(VillagePlace place)
        {
            int i = Index(place);
            if (place == VillagePlace.SickRoom) OrselInVillage = true;
            if (place == VillagePlace.Barn && !Built(VillagePlace.Smithy)) Complete(VillagePlace.Smithy);
            for (int n = 0; n < VillageBuildRules.NeedCount; n++) _delivered[i * VillageBuildRules.NeedCount + n] = VillageBuildRules.NeedAt(place, n).Count;
            _stage[i] = (int)BuildStage.Built;
        }

        /// <summary>FNV-1a over the canonical text; mixed into the world hash once anything has happened at a lot.</summary>
        public ulong StableHash
        {
            get
            {
                ulong h = 14695981039346656037UL;
                unchecked { foreach (char c in Token()) { h ^= c; h *= 1099511628211UL; } }
                return h;
            }
        }

        private static string N(long value) { return value.ToString(CultureInfo.InvariantCulture); }

        /// <summary>"1;orsel;stage,a,b;stage,a,b;stage,a,b" in the order of VillagePlace.</summary>
        internal string Token()
        {
            var b = new StringBuilder("1;").Append(OrselInVillage ? '1' : '0');
            for (int i = 0; i < _stage.Length; i++)
            {
                b.Append(';').Append(N(_stage[i]));
                for (int n = 0; n < VillageBuildRules.NeedCount; n++) b.Append(',').Append(N(_delivered[i * VillageBuildRules.NeedCount + n]));
            }
            return b.ToString();
        }

        private static bool Number(string text, int max, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(text) || text.Length > 4) return false;
            foreach (char c in text) if (c < '0' || c > '9') return false;
            value = int.Parse(text, CultureInfo.InvariantCulture);
            return value <= max && N(value) == text;
        }

        /// <summary>
        /// Read a token and accept only a state the game itself can reach: nothing delivered beyond the need, a lot
        /// with everything delivered is no longer a lot, the barn only after the smithy, the sick room only with Orsel.
        /// </summary>
        internal static bool TryRead(string token, out VillageBuildingState state)
        {
            state = null;
            if (token == null) return false;
            string[] parts = token.Split(';');
            if (parts.Length != 2 + VillagePlaceLayout.PlaceCount || parts[0] != "1" || (parts[1] != "0" && parts[1] != "1")) return false;
            var s = new VillageBuildingState { OrselInVillage = parts[1] == "1" };
            for (int i = 0; i < VillagePlaceLayout.PlaceCount; i++)
            {
                var place = (VillagePlace)(i + 1);
                string[] f = parts[2 + i].Split(',');
                if (f.Length != 1 + VillageBuildRules.NeedCount || !Number(f[0], (int)BuildStage.Built, out int stage)) return false;
                bool complete = true, touched = stage != 0;
                for (int n = 0; n < VillageBuildRules.NeedCount; n++)
                {
                    if (!Number(f[1 + n], VillageBuildRules.NeedAt(place, n).Count, out int count)) return false;
                    s._delivered[i * VillageBuildRules.NeedCount + n] = count;
                    complete &= count == VillageBuildRules.NeedAt(place, n).Count;
                    touched |= count != 0;
                }
                if (complete != (stage != (int)BuildStage.Site)) return false;
                s._stage[i] = stage;
                if (touched && place == VillagePlace.SickRoom && !s.OrselInVillage) return false;
                if (touched && place == VillagePlace.Barn && !s.Built(VillagePlace.Smithy)) return false;
            }
            if (!s.HasProgress) return false;       // three untouched lots are written as no section at all
            state = s;
            return true;
        }
    }

    public sealed partial class CharacterProgression
    {
        private VillageBuildingState _houses = new VillageBuildingState();
        /// <summary>The houses of the village: vacant lots until the player has helped to build them.</summary>
        public VillageBuildingState Houses { get { return _houses; } }
        private const string HousesSectionName = "houses";

        private void SyncHousesSection()
        {
            if (_houses.HasProgress) SetSection(HousesSectionName, _houses.Token());
            else _sections.Remove(HousesSectionName);
        }

        /// <summary>A save without this section has three vacant lots: nothing is made up for it.</summary>
        private bool RestoreHousesSection()
        {
            if (!_sections.TryGetValue(HousesSectionName, out var token)) return true;
            if (!VillageBuildingState.TryRead(token, out var state) || EntryAct < 2) return false;
            _houses = state;
            return true;
        }

        /// <summary>
        /// The player comes to the village from somewhere else. Whoever was thanked and set to work has finished:
        /// the house stands. With the sick room Orsel brings her herbs along, and care moves into the village.
        /// </summary>
        internal void ArriveInVillage()
        {
            if (VillageBuildRules.OrselReady(this)) _houses.NoteOrsel();
            if (_houses.Raise(out bool sickRoom) && sickRoom) _economy.MoveCareToVillage();
        }
    }

    public sealed partial class ArenaSimulation
    {
        /// <summary>Somebody stands at this lot and asks for materials, or is at work on the house.</summary>
        public bool SiteOffered(VillagePlace place)
        {
            return IsVillage && place != VillagePlace.None && !Progression.Houses.Built(place) && VillageBuildRules.Offered(Progression, place);
        }

        /// <summary>The lot whose asker the player stands with, or none.</summary>
        public VillagePlace SiteInReach
        {
            get
            {
                if (GameOver || !IsVillage || InsidePlace != VillagePlace.None) return VillagePlace.None;
                for (int i = 1; i <= VillagePlaceLayout.PlaceCount; i++)
                {
                    var place = (VillagePlace)i;
                    if (SiteOffered(place) && DistanceSquared(PlayerXmm, PlayerZmm, VillageBuildRules.AskerXmm(place), VillageBuildRules.AskerZmm(place)) <=
                        FixedMath.Square(VillageBuildRules.ReachMm))
                        return place;
                }
                return VillagePlace.None;
            }
        }

        /// <summary>What the person at a lot says right now.</summary>
        public string SiteWords(VillagePlace place) { return VillageBuildRules.Words(Progression, place); }

        /// <summary>
        /// Hand a material from the pack to the person at the lot the player stands at: as much as is still missing
        /// and no more than the pack holds. Nothing is created: the pack loses exactly what the lot gains.
        /// </summary>
        public SocialResult GiveToSite(VillageGood good)
        {
            var result = new SocialResult();
            VillagePlace place = SiteInReach;
            if (place == VillagePlace.None) { result.Text = "Hier nimmt niemand Baumaterial an."; return result; }
            if (DoorsBarred) { result.Text = VillageBuildRules.Asker(place) + " will von dir nichts annehmen."; return result; }
            if (Progression.Houses.Stage(place) != BuildStage.Site) { result.Text = SiteWords(place); return result; }
            int need = VillageBuildRules.NeedFor(place, good);
            if (need < 0) { result.Text = VillageBuildRules.Asker(place) + ": Das kann ich für den Bau nicht brauchen."; return result; }
            int count = Math.Min(Progression.Houses.Missing(place, need), VillageBuildRules.Carried(Progression, good));
            if (count <= 0)
            {
                result.Text = Progression.Houses.Missing(place, need) == 0 ?
                    VillageBuildRules.Asker(place) + ": Davon habe ich genug." :
                    "Du hast kein " + VillageBuildRules.GoodName(good) + " bei dir. Nichts wurde übergeben.";
                return result;
            }
            if (!Progression.Crafting.TakeForVillage(good, count)) { result.Text = "Davon hast du nicht genug bei dir. Nichts wurde übergeben."; return result; }
            if (place == VillagePlace.SickRoom) Progression.Houses.NoteOrsel();
            Progression.Houses.Deliver(place, need, count);
            RememberVillageGift(good, count);
            result.Accepted = true;
            result.Text = Progression.Houses.Stage(place) == BuildStage.Raising ? VillageBuildRules.Thanks(place) :
                count.ToString(CultureInfo.InvariantCulture) + " × " + VillageBuildRules.GoodName(good) + " übergeben. " +
                VillageBuildRules.Asker(place) + ": Mir fehlen noch " + VillageBuildRules.MissingWords(Progression.Houses, place) + ".";
            return result;
        }

        /// <summary>
        /// Where a resident is seen in the village as it stands now: work and care that belong into a house are out
        /// in the open while that house is still a lot.
        /// </summary>
        public bool SeenInVillage(VillageResidentState resident, out VillageSpot spot)
        {
            bool seen = VillageEconomyLayout.Seen(resident, out spot);
            spot = Progression.Houses.Outdoors(spot);
            return seen;
        }

        private void MixHouses(ref ulong h)
        {
            // Only once something happened at a lot: every earlier profile keeps its exact hash.
            if (!Progression.Houses.HasProgress) return;
            Mix(ref h, 0x484F5553UL);
            Mix(ref h, Progression.Houses.StableHash);
        }
    }
}
