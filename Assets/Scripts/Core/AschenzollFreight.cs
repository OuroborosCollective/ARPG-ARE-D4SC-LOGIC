using System;
using System.Globalization;
using System.Text;

namespace AreArpg.Core
{
    public enum FreightChoice { Undecided, Secured, Released }

    /// <summary>Permanent evidence and local cargo motion, owned by the existing character save.</summary>
    public sealed class FreightState
    {
        public int Clues { get; internal set; }
        public FreightChoice Choice { get; internal set; }
        public int MotionTicks { get; internal set; }
        public bool OriginalCollected { get; internal set; }
        public bool ArrivalInspected { get; internal set; }
        public bool Reported { get; internal set; }
        public bool HasProgress => Clues != 0;
        public bool EvidenceReady => OriginalCollected || ArrivalInspected;
        public const int JourneyTicks = 100;
        public const long CargoX = 15500, StartZ = -4000, EndZ = 14000;
        public long CargoZ => StartZ + (EndZ - StartZ) * MotionTicks / JourneyTicks;

        internal string Token() => string.Join(",", "1", Clues.ToString(CultureInfo.InvariantCulture),
            ((int)Choice).ToString(CultureInfo.InvariantCulture), MotionTicks.ToString(CultureInfo.InvariantCulture),
            OriginalCollected ? "1" : "0", ArrivalInspected ? "1" : "0", Reported ? "1" : "0");

        internal static bool TryRead(string token, out FreightState state)
        {
            state = null; var f = token.Split(',');
            if (f.Length != 7 || f[0] != "1") return false;
            var n = new int[6];
            for (int i = 0; i < n.Length; i++)
                if (!int.TryParse(f[i+1], NumberStyles.None, CultureInfo.InvariantCulture, out n[i]) ||
                    n[i].ToString(CultureInfo.InvariantCulture) != f[i+1]) return false;
            if (n[0] < 1 || n[0] > 7 || n[1] > 2 || n[2] > JourneyTicks || n[3] > 1 || n[4] > 1 || n[5] > 1 ||
                (n[1] != 0 && n[0] != 7) || (n[1] != 2 && n[2] != 0) ||
                (n[3] != 0 && n[1] != 1) || (n[4] != 0 && (n[1] != 2 || n[2] != JourneyTicks)) ||
                (n[5] != 0 && n[3] == 0 && n[4] == 0)) return false;
            state = new FreightState { Clues=n[0], Choice=(FreightChoice)n[1], MotionTicks=n[2],
                OriginalCollected=n[3]==1, ArrivalInspected=n[4]==1, Reported=n[5]==1 };
            return true;
        }
    }

    public sealed partial class CharacterProgression
    {
        public FreightState Freight { get; private set; } = new FreightState();
        private string Payload()
        {
            string body = CompanionPayload();
            if (!Freight.HasProgress) return body;
            string previous = body + "|" + HashText(body).ToString("X16", CultureInfo.InvariantCulture);
            return "AC19|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(previous)) + "|" + Freight.Token();
        }
        private static bool TryLoadFreight(string saved, out CharacterProgression progress)
        {
            progress=null;
            if(saved.Length > 64*1024*1024) return false;
            var f=saved.Split('|');
            if(f.Length!=4 || f[3]!=HashText(string.Join("|",f,0,3)).ToString("X16",CultureInfo.InvariantCulture)) return false;
            try
            {
                var bytes=Convert.FromBase64String(f[1]);
                if(Convert.ToBase64String(bytes)!=f[1]) return false;
                string inner=new UTF8Encoding(false,true).GetString(bytes);
                if (!(inner.StartsWith("AC15|",StringComparison.Ordinal)||inner.StartsWith("AC16|",StringComparison.Ordinal)||
                      inner.StartsWith("AC17|",StringComparison.Ordinal)||inner.StartsWith("AC18|",StringComparison.Ordinal)) ||
                    !TryLoad(inner,out var p) || !p.Woodland.Active || !p.Woodland.ReachedAschenzoll ||
                    !FreightState.TryRead(f[2],out var freight)) return false;
                p.Freight=freight;
                if(p.SaveString()!=saved) return false;
                progress=p;return true;
            }
            catch(Exception e) when(e is FormatException || e is ArgumentException || e is OverflowException) { return false; }
        }
    }

    public sealed partial class ArenaSimulation
    {
        public bool InAschenzoll => Progression.EntryAct==10 && Progression.Woodland.Active;
        private void StepFreight()
        {
            var f=Progression.Freight;
            if(InAschenzoll && f.Choice==FreightChoice.Released && f.MotionTicks<FreightState.JourneyTicks) f.MotionTicks++;
        }
        private SocialResult ActOnFreight(HungerAction action)
        {
            var r=new SocialResult { Text="Gehe zur angegebenen Fundstelle. Die Rückfracht ist ein leerer Tierkäfig." };
            var f=Progression.Freight;
            if(GameOver || !Progression.Woodland.ReachedAschenzoll) return r;
            if(action==HungerAction.ReportFreightEvidence)
            {
                if(Progression.EntryAct!=2 || !f.EvidenceReady || f.Reported) return r;
                var contact=HungerContact(0);if(contact==null)return r;
                contact.InteractionCooldownTicks=10;f.Reported=true;
                r.SpeakerId=0;
                r.Text=f.OriginalCollected ? "Ilyan: Alte Mühle. Nordwechsel. Jetzt haben wir eine Strecke auf dem Originalbeleg. Wer die Lebendfracht bestellt hat, wissen wir noch nicht." :
                    "Ilyan: Du hast dieselbe Sendung an der oberen Rampe geprüft, dem Nordwechsel zugeordnet. Der Auftraggeber bleibt unbekannt.";
            }
            else if(!InAschenzoll) return r;
            else if(action==HungerAction.InspectFreightNotice && (f.Clues&1)==0 && Near(-5000,-12000,NpcInteractionRadiusMm))
            { f.Clues|=1;r.Text="Aushang: Handel bis zum Schlusszeichen. Aufenthalt oberhalb der Station nur mit Wohnrecht oder Erlaubnis."; }
            else if(action==HungerAction.InspectFreightCage && (f.Clues&2)==0 && Near(FreightState.CargoX,FreightState.StartZ,NpcInteractionRadiusMm))
            { f.Clues|=2;r.Text="Der Tierkäfig ist leer. Schwere Beschläge, von innen verbogene Streben. Am Rahmen steht Sendung 17. Das identifiziert noch nicht den Rattler."; }
            else if(action==HungerAction.ReadFreightManifest && (f.Clues&4)==0 && Near(-3000,-1000,NpcInteractionRadiusMm))
            { f.Clues|=4;r.Text="Sendung 17. Lebendfracht: eine Einheit. Abladeort: Alte Mühle. Leerbehälter zurück über Aschenzoll. Weitergabe: Nordwechsel. Das Auftraggeberfeld fehlt. Du hast den Beleg nur gelesen."; }
            else if((action==HungerAction.SecureFreight || action==HungerAction.ReleaseFreight) && f.Clues==7 && f.Choice==FreightChoice.Undecided && Near(-3000,-1000,NpcInteractionRadiusMm))
            {
                f.Choice=action==HungerAction.SecureFreight?FreightChoice.Secured:FreightChoice.Released;
                r.Text=f.Choice==FreightChoice.Secured ? "Der Notriegel hält die Plattform fest. Nimm jetzt den Originalbeleg vom Pult. Der seitliche Rückweg bleibt frei." :
                    "Der leere Käfig fährt mit dem verschlossenen Originalbeleg zur oberen Rampe. Folge dem seitlichen Fußweg und prüfe dort die Zuordnung.";
            }
            else if(action==HungerAction.TakeFreightOriginal && f.Choice==FreightChoice.Secured && !f.OriginalCollected && Near(-3000,-1000,NpcInteractionRadiusMm))
            { f.OriginalCollected=true;r.Text="Du sicherst den Originalbeleg der Sendung 17. Bringe Ilyan persönlich die Transportspur."; }
            else if(action==HungerAction.InspectFreightArrival && f.Choice==FreightChoice.Released && f.MotionTicks==FreightState.JourneyTicks && !f.ArrivalInspected && Near(FreightState.CargoX,FreightState.EndZ,NpcInteractionRadiusMm))
            { f.ArrivalInspected=true;r.Text="Obere Rampe: derselbe leere Käfig, Sendung 17, Abladezeichen Nordwechsel. Die Weitergabe ist vor Ort geprüft; den Originalbeleg besitzt du nicht. Berichte Ilyan persönlich."; }
            else return r;
            SaveWoodlandPlayer();r.Accepted=true;return r;
        }
        public string FreightObjective
        {
            get
            {
                var f=Progression.Freight;
                if(f.Reported) return f.OriginalCollected ? "Originalbeleg an Ilyan gemeldet. Die Spur führt zum Nordwechsel; der weitere Weg ist noch nicht zugänglich." : "Überprüfte Weitergabe an Ilyan gemeldet. Die Spur führt zum Nordwechsel; der weitere Weg ist noch nicht zugänglich.";
                if(f.EvidenceReady) return "Bringe Ilyan persönlich die Transportspur. Rückweg: Wald → Mühle → Aschestraße → Dorf.";
                if(f.Choice==FreightChoice.Secured) return "Die Fracht ist festgesetzt. Sichere den Originalbeleg am Pult; die Weiterfahrt kann nicht mehr beobachtet werden.";
                if(f.Choice==FreightChoice.Released) return f.MotionTicks<FreightState.JourneyTicks ? "Der leere Käfig fährt zur oberen Rampe. Folge dem freien Fußweg; der Originalbeleg bleibt bei der Fracht." : "Prüfe Sendungsnummer und Abladezeichen am Käfig an der oberen Rampe. Ankunft allein ist noch kein Beweis.";
                return f.Clues==7 ? "Am Pult wählen: Fracht festsetzen und Original sichern ODER Fracht freigeben und die Weitergabe prüfen. Die Wahl bleibt dauerhaft." : "Untersuche den Aushang im Süden, den leeren Käfig im Osten und den Frachtbeleg am Pult. Die Reihenfolge ist frei.";
            }
        }
    }
}
