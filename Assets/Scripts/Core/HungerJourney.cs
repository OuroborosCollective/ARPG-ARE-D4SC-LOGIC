using System;
using System.Globalization;

namespace AreArpg.Core
{
    public enum HungerChoice { Undecided = 0, Reveal = 1, Protect = 2, Replace = 3 }
    public enum HungerAction { HearIlyan = 0, InspectTrail = 1, InspectShelter = 2, Reveal = 3, Protect = 4, Replace = 5, InspectBeds = 6, StoreBandage = 7, StoreTracks = 8, InspectSeal = 9, OpenSeal = 10, BreakSeal = 11, InspectCryptTrace = 12, ReportCrypt = 13, OpenInnerDoor = 14, ToggleSneak = 15, InspectRoyalBrand = 16, ReportRoyalBrand = 17 }

    public sealed partial class CharacterProgression
    {
        /// <summary>Monotonic, authored clues: unknown, Ilyan, road, shelter, missing patients.</summary>
        public int HungerStage { get; private set; }
        public HungerChoice SupplyChoice { get; private set; }
        public bool EntryFromNorth { get; private set; }

        /// <summary>Advance exactly one discovered clue; no reward or second inventory is created.</summary>
        internal void DiscoverHungerClue() { HungerStage++; }

        /// <summary>Record the first resolved supply decision after its real local transaction.</summary>
        internal void ChooseHungerSupply(HungerChoice choice) { SupplyChoice = choice; }

        /// <summary>Change only the cloned entry checkpoint for a validated adjacent passage.</summary>
        internal void TravelHunger(int act, bool fromNorth) { EntryAct = act; EntryFromNorth = fromNorth; Road.ResumePlayer = false; Investigation.RestartPlayer(); if (act >= 5) Investigation.Active = true; }

        /// <summary>AC9 leaves every unused AC8/legacy profile byte-stable until the first story action.</summary>
        private string HungerToken()
        {
            return "1," + EntryAct.ToString(CultureInfo.InvariantCulture) + "," + (EntryFromNorth ? "1" : "0") +
                "," + HungerStage.ToString(CultureInfo.InvariantCulture) + "," + ((int)SupplyChoice).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Reject impossible paths and noncanonical numbers even under a valid checksum.</summary>
        private static bool ReadHungerToken(string text, out int act, out bool north, out int stage, out HungerChoice choice, bool extended = false, bool rattler = false)
        {
            act = 0; north = false; stage = 0; choice = HungerChoice.Undecided;
            string[] f = text.Split(',');
            if (f.Length != 5 || f[0] != "1" || f[1].Length != 1 || f[2].Length != 1 ||
                f[3].Length != 1 || f[4].Length != 1) return false;
            act = f[1][0] - '0'; stage = f[3][0] - '0'; int decision = f[4][0] - '0';
            if (act < 2 || act > (rattler ? 7 : extended ? 6 : 4) || (f[2] != "0" && f[2] != "1") || stage < 1 || stage > 4 ||
                decision < 0 || decision > 3 || (act == 4 && (stage < 2 || (f[2] == "1" && (!extended || stage != 4)))) ||
                (act >= 5 && stage != 4) || ((act == 7 || (act == 6 && !rattler)) && f[2] == "1") ||
                (stage < 3 && decision != 0) || (stage == 4 && decision == 0)) return false;
            north = f[2] == "1"; choice = (HungerChoice)decision;
            return true;
        }
    }

    public sealed partial class ArenaSimulation
    {
        public const long ReturnPassageZmm = -24000L;
        public bool AtReturnPassage { get { return Progression.EntryAct >= 3 &&
            DistanceSquared(PlayerXmm, PlayerZmm, 0, ReturnPassageZmm) <= FixedMath.Square(PassageRadiusMm); } }
        public bool AtAnyPassage { get { return AtWorldPassage || AtReturnPassage; } }
        /// <summary>Authored shelter encounter: one patient is missing on the return visit, on every supply branch.</summary>
        public int HungerShelterPatients { get { return Progression.EntryAct != 4 ? 0 :
            Progression.SupplyChoice == HungerChoice.Undecided ? 3 : 2; } }
        public bool HungerForwardOpen { get { return Progression.EntryAct == 2 ? Progression.HungerStage >= 1 :
            Progression.EntryAct == 3 ? Progression.HungerStage >= 2 :
            Progression.EntryAct == 4 ? Progression.HungerStage == 4 :
            Progression.EntryAct == 5 ? Progression.Investigation.Approach != SealApproach.Undecided :
            Progression.EntryAct == 6 && Progression.Rattler.Active; } }

        /// <summary>Return edges remain open; forward edges require the clue in the actual region.</summary>
        private ArenaSimulation TraverseHunger(bool backwards)
        {
            if (GameOver || GroundLoot.Count != 0 || (backwards ? Progression.EntryAct < 3 : !HungerForwardOpen)) return null;
            if (!CharacterProgression.TryLoad(Progression.SaveString(), out var next)) return null;
            next.TravelHunger(Progression.EntryAct + (backwards ? -1 : 1), backwards);
            return new ArenaSimulation(_runSeed, next, CryptDepth, _lootCatalog);
        }

        /// <summary>Core-sized mill footprint also supplies the visible structure's dimensions.</summary>
        public static bool CanStandInHungerRegion(int act, long x, long z)
        {
            return act != 4 || Math.Abs(x - 10500L) > 3500L + PlayerRadiusMm ||
                Math.Abs(z - 5000L) > 5500L + PlayerRadiusMm;
        }

        /// <summary>Require actual conscious, nonhostile local contact; offscreen NPCs learn nothing.</summary>
        private NpcAgent HungerContact(int id)
        {
            var npc = Npcs.Find(n => n.Id == id);
            return npc != null && npc.Memory.Health > 0 && npc.InteractionCooldownTicks == 0 &&
                npc.Memory.Disposition(npc.Personality) != NpcDisposition.Hostile &&
                DistanceSquared(PlayerXmm, PlayerZmm, npc.Xmm, npc.Zmm) <= FixedMath.Square(NpcInteractionRadiusMm) &&
                EntryWorldLayout.HasSight(PlayerXmm, PlayerZmm, npc.Xmm, npc.Zmm) ? npc : null;
        }

        /// <summary>Apply one versioned story action at its real location; repeated requests cannot duplicate effects.</summary>
        public SocialResult ActOnHunger(HungerAction action)
        {
            if (action >= HungerAction.OpenInnerDoor) return ActOnRattler(action);
            if (action >= HungerAction.StoreBandage) return ActOnInvestigation(action);
            var result = new SocialResult { Text = "Hier gibt es dafuer noch keinen neuen Hinweis." };
            if (GameOver || Progression.EntryAct < 2 || Progression.EntryAct > 4) return result;
            int stage = Progression.HungerStage;
            NpcAgent contact = null;
            if (action == HungerAction.HearIlyan && Progression.EntryAct == 2 && stage == 0)
            {
                contact = HungerContact(0);
                if (contact == null) { result.Text = "Sprich direkt mit Ilyan, wenn er ansprechbar ist."; return result; }
                Progression.DiscoverHungerClue();
                result.Text = "Ilyan: Ich bringe Verwundeten heimlich Essen. Folge der Aschestrasse zur alten Muehle.";
            }
            else if (action == HungerAction.InspectTrail && Progression.EntryAct == 3 && stage == 1 &&
                DistanceSquared(PlayerXmm, PlayerZmm, 0, -2000) <= FixedMath.Square(NpcInteractionRadiusMm))
            {
                Progression.DiscoverHungerClue();
                result.Text = "Ein Verband haengt am Wegzeichen. Fussspuren fuehren nach Norden zur Muehle.";
            }
            else if (action == HungerAction.InspectShelter && Progression.EntryAct == 4 && stage == 2 &&
                DistanceSquared(PlayerXmm, PlayerZmm, -4000, -2000) <= FixedMath.Square(NpcInteractionRadiusMm))
            {
                Progression.DiscoverHungerClue();
                result.Text = "Verbandszeug und Essensreste belegen Ilyans Hilfe. Kehre zurueck: offenlegen, schuetzen oder Vorrat ersetzen.";
            }
            else if (action >= HungerAction.Reveal && action <= HungerAction.Replace && Progression.EntryAct == 2 &&
                stage == 3 && Progression.SupplyChoice == HungerChoice.Undecided)
            {
                contact = HungerContact(action == HungerAction.Protect ? 0 : SocialLedger.MaraNpcId);
                if (contact == null) { result.Text = "Diese Entscheidung braucht ein direktes Gespraech mit Ilyan bzw. Mara."; return result; }
                if (action == HungerAction.Replace)
                {
                    int missing = SocialLedger.CampCapacity - Progression.Social.CampProvisions;
                    if (Progression.Social.Provisions < missing)
                    { result.Text = "Zum Auffuellen fehlen dir Rationen. Kosten: " + missing + ". Nichts wurde verbraucht."; return result; }
                    for (int i = 0; i < missing; i++) Progression.Social.DonateCampRation(SocialLedger.MaraNpcId);
                    Progression.ChooseHungerSupply(HungerChoice.Replace);
                    result.Text = "Das Lager ist versorgt. Mara kennt Ilyans Geheimnis nicht. Kehre zum Verletztenlager zurueck.";
                }
                else if (action == HungerAction.Protect)
                {
                    contact.Memory.Record(NpcAction.Help, 0);
                    Progression.ChooseHungerSupply(HungerChoice.Protect);
                    result.Text = "Ilyan dankt dir fuer dein Schweigen. Der Fehlbestand bleibt. Kehre zum Verletztenlager zurueck.";
                }
                else
                {
                    contact.Memory.Record(NpcAction.Greet, 0);
                    Progression.ChooseHungerSupply(HungerChoice.Reveal);
                    result.Text = "Du berichtest Mara von Ilyans Hilfe. Sie weiss es jetzt; Dorn wurde nicht informiert. Kehre zur Muehle zurueck.";
                }
            }
            else if (action == HungerAction.InspectBeds && Progression.EntryAct == 4 && stage == 3 &&
                Progression.SupplyChoice != HungerChoice.Undecided &&
                DistanceSquared(PlayerXmm, PlayerZmm, -4000, 5000) <= FixedMath.Square(NpcInteractionRadiusMm))
            {
                Progression.DiscoverHungerClue();
                result.Text = "Eine Lagerstatt ist leer. Die anderen berichten von weiteren Vermissten. Verband und Schleifspuren weisen zum alten Speicher.";
            }
            else return result;
            if (contact != null) contact.InteractionCooldownTicks = 10;
            result.Accepted = true;
            return result;
        }

        /// <summary>Readable objective derived only from persisted discoveries and the selected branch.</summary>
        public string HungerObjective
        {
            get
            {
                switch (Progression.HungerStage)
                {
                    case 0: return "Ilyan verbirgt etwas. Sprich mit ihm in der Aschengrenze.";
                    case 1: return "Folge dem Nordweg. Untersuche das Wegzeichen auf der Aschestrasse.";
                    case 2: return "Die Spur fuehrt zur alten Muehle. Untersuche das Verbandszeug westlich des Weges.";
                    case 3: return Progression.SupplyChoice == HungerChoice.Undecided ?
                        "Kehre ins Dorf zurueck: Mara informieren, Ilyan schuetzen oder Maras Fehlbestand aus eigenem Proviant ersetzen." :
                        "Kehre zur Muehle zurueck. Untersuche die verlassenen Lagerstaetten nordwestlich des Weges.";
                    default: return InvestigationObjective;
                }
            }
        }
    }
}
