using System;
using System.Linq;
using AshenCrypt.MemoryForge;

namespace AreArpg.Core
{
    /// <summary>
    /// Owner rule 2026-11-15: after a hostile act a villager first flees, then calms down without further violence;
    /// greetings and help speed up reconciliation. Calming is a recorded, persisted memory event, never a silent reset,
    /// and the deed itself stays remembered (legacy counters and lost trust are untouched).
    /// </summary>
    public sealed partial class ArenaSimulation
    {
        /// <summary>Peaceful time after the last harm or calming before the next calming step (60 s at 100 ms).</summary>
        public const ulong CalmIntervalTicks = 600;
        internal const int CalmHostility = 1000, CalmFear = 600;
        internal const int GreetHostility = 200, GreetFear = 150, HelpHostility = 800, HelpFear = 500;
        /// <summary>Greetings cost nothing, so they reconcile at most once per 30 s; help spends real provisions.</summary>
        internal const ulong GreetReconcileIntervalTicks = 300;

        /// <summary>Checked once per simulated second; derives everything from the persisted memory archive.</summary>
        private void CalmVillagers()
        {
            var forge = Progression.Social.Forge;
            if (forge == null || forge.ProcessedTicks % 10 != 0) return;
            // Saves from before the side task existed: a camp the player already refilled is credited once.
            if (!Progression.Social.CampRefilled && Progression.Social.CampProvisions >= SocialLedger.CampCapacity) RecordCampRefill();
            foreach (NpcAgent npc in Npcs)
            {
                if (npc.Id > 2 || npc.Memory.Health == 0 || npc.Mind == null) continue;
                var relation = npc.Mind.RelationshipTo("player");
                bool grudge = npc.Memory.Disposition(npc.Personality) == NpcDisposition.Hostile && !NpcAgent.CalmedSinceHarm(npc.Mind);
                if (relation.Hostility == 0 && relation.Fear == 0 && !grudge) continue;
                if (forge.ProcessedTicks < NpcAgent.LastHarmOrCalm(npc.Mind) + CalmIntervalTicks) continue;
                Soothe(forge, npc, NpcAgent.CalmContext, CalmHostility, CalmFear);
            }
        }

        /// <summary>A friendly greeting or real help eases a remaining grudge; it never erases the deed.</summary>
        private void Reconcile(NpcAgent npc, NpcAction action)
        {
            var forge = Progression.Social.Forge;
            if (forge == null || npc.Mind == null || npc.Memory.Health == 0 || action != NpcAction.Greet && action != NpcAction.Help) return;
            var relation = npc.Mind.RelationshipTo("player");
            if (relation.Hostility == 0 && relation.Fear == 0) return;
            if (action == NpcAction.Help) { Soothe(forge, npc, NpcAgent.ReconcileContext, HelpHostility, HelpFear); return; }
            ulong lastGreeting = 0;
            foreach (var known in npc.Mind.Known)
                if (known.Event.Actor == "player" && known.Event.Context == NpcAgent.GreetReconcileContext) lastGreeting = Math.Max(lastGreeting, known.Event.Tick);
            if (lastGreeting > 0 && forge.ProcessedTicks < lastGreeting + GreetReconcileIntervalTicks) return;
            Soothe(forge, npc, NpcAgent.GreetReconcileContext, GreetHostility, GreetFear);
        }

        private static void Soothe(NpcForgeState forge, NpcAgent npc, string context, int hostility, int fear)
        {
            var relation = npc.Mind.RelationshipTo("player");
            var impact = new RelationshipChange(fear: -Math.Min(fear, relation.Fear), hostility: -Math.Min(hostility, relation.Hostility));
            var ev = forge.Archive.Append(MemoryKind.Visit, "player", npc.Mind.Id, forge.ProcessedTicks, 100, false, context, impact);
            forge.Archive.Learn(npc.Mind.Id, ev.Id, KnowledgeOrigin.Self, npc.Mind.Id, forge.ProcessedTicks);
        }

        /// <summary>Mara's camp can take a ration: village, conscious supplier, stock below capacity and own provisions.</summary>
        public bool CanDonateCampRation
        {
            get
            {
                if (!IsVillage || GameOver) return false;
                var mara = Npcs.Find(n => n.Id == SocialLedger.MaraNpcId);
                return mara != null && mara.Memory.Health > 0 && !Progression.Social.Provisions.IsZero &&
                    Progression.Social.CampProvisions < SocialLedger.CampCapacity;
            }
        }

        /// <summary>Side task text derived from persisted stock and the recorded completion.</summary>
        public string CampSideTask
        {
            get
            {
                int missing = SocialLedger.CampCapacity - Progression.Social.CampProvisions;
                if (Progression.Social.CampRefilled) return "Erledigt: Maras Lager aufgefüllt. Mara dankt dir.";
                return missing > 0 ? "Nebenaufgabe: Bring Mara " + missing + (missing == 1 ? " Ration" : " Rationen") + " für ihr Lager." :
                    "Maras Lager ist voll.";
            }
        }

        /// <summary>
        /// Record the one-time side task completion when the player's own rations filled Mara's camp. Only a net
        /// contribution counts: Mara's persisted memory records every handed-over ration as help and every camp theft or
        /// assault as a hostile act, in old saves from before the memory archive as well. Returning a stolen ration is no help.
        /// </summary>
        private bool RecordCampRefill()
        {
            var forge = Progression.Social.Forge;
            if (forge == null || Progression.Social.CampRefilled || Progression.Social.CampProvisions < SocialLedger.CampCapacity) return false;
            var mara = Progression.Social.GetMemory(SocialLedger.MaraNpcId);
            if (mara.HelpfulActions <= mara.HostileActions) return false;
            var ev = forge.Archive.Append(MemoryKind.Quest, "player", "mara", forge.ProcessedTicks, 6000, true, SocialLedger.CampRefillContext,
                new RelationshipChange(gratitude: 1000, trust: 500));
            forge.Archive.Learn("mara", ev.Id, KnowledgeOrigin.Self, "mara", forge.ProcessedTicks);
            return true;
        }
    }

    public sealed partial class SocialLedger
    {
        internal const string CampRefillContext = "quest.camp.refilled";
        /// <summary>True once the player's own rations refilled Mara's camp; stock may later be eaten again.</summary>
        public bool CampRefilled => Forge != null && Forge.Archive.Events.Any(e => e.Kind == MemoryKind.Quest && e.Context == CampRefillContext);
    }

    public sealed partial class NpcAgent
    {
        /// <summary>Forge hostility at which a villager treats the player as a threat (shared with the decision step).</summary>
        public const int FleeHostility = 2000;
        internal const string CalmContext = "npc.calmed", ReconcileContext = "npc.reconciled", GreetReconcileContext = "npc.reconciled.greeting";

        /// <summary>
        /// Current stance used for flight, quest contact, greetings and the HUD. A strong remembered threat keeps the NPC
        /// hostile; an old grudge is set aside once the NPC has calmed down since the last harm.
        /// </summary>
        public NpcDisposition Mood
        {
            get
            {
                if (Mind != null && Mind.RelationshipTo("player").Hostility >= FleeHostility) return NpcDisposition.Hostile;
                NpcDisposition legacy = Memory.Disposition(Personality);
                return legacy == NpcDisposition.Hostile && Mind != null && CalmedSinceHarm(Mind) ? NpcDisposition.Wary : legacy;
            }
        }

        internal static bool CalmedSinceHarm(NpcMind mind)
        {
            ulong harm = 0, calm = 0; bool calmed = false;
            foreach (var known in mind.Known)
            {
                var e = known.Event;
                if (e.Actor != "player") continue;
                if (e.Kind == MemoryKind.Assault || e.Kind == MemoryKind.Theft) harm = Math.Max(harm, e.Tick);
                else if (e.Context == CalmContext || e.Context == ReconcileContext || e.Context == GreetReconcileContext) { calm = Math.Max(calm, e.Tick); calmed = true; }
            }
            return calmed && calm >= harm;
        }

        internal static ulong LastHarmOrCalm(NpcMind mind)
        {
            ulong last = 0;
            foreach (var known in mind.Known)
            {
                var e = known.Event;
                if (e.Actor == "player" && (e.Kind == MemoryKind.Assault || e.Kind == MemoryKind.Theft || e.Context == CalmContext))
                    last = Math.Max(last, e.Tick);
            }
            return last;
        }
    }
}
