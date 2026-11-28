namespace AreArpg.Core
{
    public sealed partial class ArenaSimulation
    {
        public bool CanTakeWorldLoot(LootState loot) => !GameOver && loot != null &&
            Near(loot.Xmm, loot.Zmm, PickupRadiusMm) && GroundLoot.Contains(loot);

        public bool CanTakeWorldSupply(SupplyDrop drop) => !GameOver && drop != null &&
            Near(drop.Xmm, drop.Zmm, PickupRadiusMm) && SupplyIndex(drop) >= 0;

        public bool TryTakeWorldLoot(LootState loot) => CollectLoot(loot, null, false);
        public bool TryTakeWorldSupply(SupplyDrop drop) => CollectLoot(null, drop, false);

        private int SupplyIndex(SupplyDrop drop)
        {
            var ground = Progression.Supplies.Ground;
            for (int i = 0; i < ground.Count; i++)
                if (ReferenceEquals(ground[i], drop)) return i;
            return -1;
        }

        public bool CanBreakWorldVase(int id)
        {
            if (!InKilnYard || GameOver || id < 0 || id >= KilnYard.Vases.Length ||
                Progression.KilnVaseBroken(id)) return false;
            var vase = KilnYard.Vases[id];
            return Near(vase.Xmm, vase.Zmm, KilnYard.VaseReachMm);
        }

        public SocialResult BreakWorldVase(int id)
        {
            if (!CanBreakWorldVase(id) || !Progression.BreakKilnVase(id))
                return new SocialResult { Text = "Diese Vase ist nicht mehr in Reichweite." };
            return new SocialResult { Accepted = true, Text = KilnYard.VaseBroken };
        }

        public bool CanReadWorldTrace(CourtTrace trace) => InCourt && !GameOver &&
            (Progression.CourtTraces & (1 << trace.Id)) == 0 && Near(trace.Xmm, trace.Zmm, trace.RadiusMm);

        public SocialResult ReadWorldTrace(int id)
        {
            if (id < 0 || id >= CourtLore.Traces.Length || !CanReadWorldTrace(CourtLore.Traces[id]))
                return new SocialResult { Text = "Diese Spur ist nicht mehr verfügbar." };
            var trace = CourtLore.Traces[id];
            Progression.ReadCourtTrace(id);
            return new SocialResult { Accepted = true, Text = trace.Title + ": " + trace.Text };
        }

        public bool TryGetWorldRelic(RegionalRelic id, out long x, out long z) =>
            TryGetRegionalRelicSource(id, out x, out z) && Near(x, z, NpcInteractionRadiusMm) &&
            CombatSight(PlayerXmm, PlayerZmm, x, z);

        public SocialResult ClaimWorldRelic(RegionalRelic id)
        {
            if (!TryGetWorldRelic(id, out _, out _) || !Progression.Equipment.ClaimRegionalRelic(Progression, id))
                return new SocialResult { Text = "Dieses Fundstück ist nicht mehr in Reichweite." };
            return new SocialResult { Accepted = true, Text = RegionalRelics.Name(id) + " erhalten." };
        }

        public bool CanTalkToWorldNpc(int id) => !GameOver && !ArrivalScenePending && HungerContact(id) != null;

        /// <summary>Report the location of a currently actionable projected story event.</summary>
        public bool TryGetWorldStorySite(HungerAction action, out long x, out long z, out int speaker)
        {
            x = z = 0; speaker = -1;
            if (GameOver || InCourt || InKilnYard || InNordwechselRift) return false;
            var p = Progression;
            int act = p.EntryAct, stage = p.HungerStage;
            var c = p.Investigation;
            var w = p.Woodland;
            var f = p.Freight;
            var d = p.Dungeon;
            var r = p.Rattler;
            bool available;
            bool requireSight = false;
            switch (action)
            {
                case HungerAction.HearIlyan: speaker = 0; available = act == 2 && stage == 0; break;
                case HungerAction.Protect: speaker = 0; available = act == 2 && stage == 3 && p.SupplyChoice == HungerChoice.Undecided; break;
                case HungerAction.Reveal:
                case HungerAction.Replace:
                    speaker = SocialLedger.MaraNpcId;
                    available = act == 2 && stage == 3 && p.SupplyChoice == HungerChoice.Undecided &&
                        (action != HungerAction.Replace || p.Social.Provisions >= SocialLedger.CampCapacity - p.Social.CampProvisions);
                    break;
                case HungerAction.ReportCrypt: speaker = 0; available = act == 2 && stage == 4 && c.Active && c.TraceFound && !c.Reported; break;
                case HungerAction.ReportRoyalBrand: speaker = 0; available = act == 2 && r.BrandExamined && !r.Reported; break;
                case HungerAction.AcceptTransportLead: speaker = 0; available = act == 2 && r.Reported && !w.Active; break;
                case HungerAction.ReportFreightEvidence: speaker = 0; available = act == 2 && f.EvidenceReady && !f.Reported; break;
                case HungerAction.ReportCubs: speaker = SocialLedger.MaraNpcId; available = act == 2 && w.Active && w.Choice == CubChoice.Spared && !w.CubsReported; break;
                case HungerAction.OrganizeCubProtection: speaker = SocialLedger.MaraNpcId; available = act == 2 && w.Active && w.CubsReported && !w.ProtectionOrganized; break;
                case HungerAction.CraftPeltArmor: speaker = SocialLedger.MaraNpcId; available = act == 2 && w.Active && w.Pelts == 3; break;
                case HungerAction.InspectTrail: x = AshRoadBandageXmm; z = AshRoadBandageZmm; available = act == 3 && stage == 1; break;
                case HungerAction.InspectShelter: x = -4000; z = -2000; available = act == 4 && stage == 2; break;
                case HungerAction.InspectBeds: x = -4000; z = 5000; available = act == 4 && stage == 3 && p.SupplyChoice != HungerChoice.Undecided; break;
                case HungerAction.StoreBandage: x = -6500; available = act == 5 && c.Active && stage == 4 && (c.Clues & 1) == 0; break;
                case HungerAction.StoreTracks: x = 6500; z = -3000; available = act == 5 && c.Active && stage == 4 && (c.Clues & 2) == 0; break;
                case HungerAction.InspectSeal: z = 18000; available = act == 5 && c.Active && stage == 4 && (c.Clues & 4) == 0; break;
                case HungerAction.OpenDungeonSeal:
                case HungerAction.BreakDungeonSeal:
                    z = 18000; available = act == 5 && c.Active && stage == 4 && c.Approach == SealApproach.Undecided &&
                        (c.Clues & 4) != 0 && (action == HungerAction.BreakDungeonSeal || c.Clues == 7); break;
                case HungerAction.BeginDungeonRun: z = 18000; available = act == 5 && !d.Active && c.Approach != SealApproach.Undecided; break;
                case HungerAction.InspectCryptTrace: z = 15000; available = act == 6 && c.Active && stage == 4 && (!InDungeon || d.Depth == 2) && !c.TraceFound; break;
                case HungerAction.OpenInnerDoor: z = 15000; available = act == 6 && (!InDungeon || d.Depth == 2) && c.TraceFound && !r.Active; break;
                case HungerAction.InspectRoyalBrand:
                    x = r.Creature.Xmm; z = r.Creature.Zmm; available = act == 7 && r.Creature.IsDead && !r.BrandExamined; break;
                case HungerAction.OpenWestChest:
                case HungerAction.OpenEastChest:
                    int chest = action == HungerAction.OpenWestChest ? 0 : 1;
                    if (InJourney)
                    {
                        var floor = p.Journey.Current;
                        if (chest >= floor.Geometry.ChestCount || (floor.OpenChests & (1 << chest)) != 0) return false;
                        floor.Geometry.Chest(chest, out x, out z);
                        return Near(x, z, NpcInteractionRadiusMm) && floor.Geometry.HasSight(PlayerXmm, PlayerZmm, x, z);
                    }
                    if (!InDungeon || !d.Current.Layout.HasChest(chest) || (d.Current.OpenChests & (1 << chest)) != 0) return false;
                    d.Current.Layout.Chest(chest, out x, out z);
                    return Near(x, z, NpcInteractionRadiusMm) && d.Current.Layout.HasSight(PlayerXmm, PlayerZmm, x, z);
                case HungerAction.ForestChest0: case HungerAction.ForestChest1: case HungerAction.ForestChest2: case HungerAction.ForestChest3:
                    int forestChest = action - HungerAction.ForestChest0;
                    if (!InForest || !w.Layout.HasChest(forestChest) || (w.OpenChests & (1 << forestChest)) != 0) return false;
                    w.Layout.Chest(forestChest, out x, out z); available = true; requireSight = true; break;
                case HungerAction.InspectCubs: z = 5000; available = act == 9 && w.Active && !w.CubsInspected; break;
                case HungerAction.SpareCubs:
                case HungerAction.SkinCubs: z = 5000; available = act == 9 && w.Active && w.CubsInspected && w.Choice == CubChoice.Undecided; break;
                case HungerAction.InspectFreightNotice: x = -5000; z = -12000; available = InAschenzoll && (f.Clues & 1) == 0; break;
                case HungerAction.InspectFreightCage: x = FreightState.CargoX; z = FreightState.StartZ; available = InAschenzoll && (f.Clues & 2) == 0; break;
                case HungerAction.ReadFreightManifest: x = -3000; z = -1000; available = InAschenzoll && (f.Clues & 4) == 0; break;
                case HungerAction.SecureFreight:
                case HungerAction.ReleaseFreight: x = -3000; z = -1000; available = InAschenzoll && f.Clues == 7 && f.Choice == FreightChoice.Undecided; break;
                case HungerAction.TakeFreightOriginal: x = -3000; z = -1000; available = InAschenzoll && f.Choice == FreightChoice.Secured && !f.OriginalCollected; break;
                case HungerAction.InspectFreightArrival:
                    x = FreightState.CargoX; z = FreightState.EndZ;
                    available = InAschenzoll && f.Choice == FreightChoice.Released && f.MotionTicks == FreightState.JourneyTicks && !f.ArrivalInspected; break;
                default: return false;
            }
            if (!available || InJourney) return false;
            if (speaker >= 0)
            {
                if (ArrivalScenePending) return false;
                var npc = HungerContact(speaker);
                if (npc == null) return false;
                x = npc.Xmm; z = npc.Zmm;
                return true;
            }
            return Near(x, z, NpcInteractionRadiusMm) && (!requireSight || CombatSight(PlayerXmm, PlayerZmm, x, z));
        }
    }
}
