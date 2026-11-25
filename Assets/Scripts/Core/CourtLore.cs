using System;

namespace AreArpg.Core
{
    /// <summary>One place at Aschenwacht that tells a piece of the wanderer's past when he looks at it.</summary>
    public readonly struct CourtTrace
    {
        public readonly int Id;
        public readonly long Xmm, Zmm, RadiusMm;
        public readonly string Title, Text;
        public CourtTrace(int id, long xmm, long zmm, long radiusMm, string title, string text)
        {
            Id = id; Xmm = xmm; Zmm = zmm; RadiusMm = radiusMm; Title = title; Text = text;
        }
    }

    /// <summary>
    /// The beginning of the story (owner decision 2026-11-25: the game grew out of a tech demo and had no beginning; the place
    /// between the crypt and the village is free to tell it). The bell of Aschenwacht kept the ash rain beyond the ridge while
    /// it rang. The bell order died out, the Aschbeter desecrated the forecourt and bolted the crypt. The order had laid its
    /// last ringer not in a grave but into the ash sleep, to wake when the bell falls silent. It has fallen silent: the
    /// wanderer wakes in the crypt, fights his way out and finds out here who he was. The pilgrim path leads down to the
    /// village the order once protected. Eight traces tell it; none of them changes a rule.
    /// </summary>
    public static class CourtLore
    {
        public const string Contract = "AshenCrypt.Lore.Aschenwacht.v1";
        public const long TraceRadiusMm = 2400L;

        public const string Prologue = "AKT I: Du erwachst in der alten Krypta unter Aschenwacht. Über dir schweigt die Glocke, und die Toten " +
            "haben sich erhoben. Besiege ihre Wächter und sichere die Beute.";

        public const string Arrival = "Du trittst durch das Kryptenportal in den entweihten Vorhof von Aschenwacht. Sieh dich um: Der Pilgerweg " +
            "führt durch das Tor zum Glockenhof und von dort hinab zum Dorf.";

        public const string Objective = "Erkunde Aschenwacht und folge dem Pilgerweg durch das Tor und den Glockenhof hinab zum Dorf.";

        public static readonly CourtTrace[] Traces =
        {
            new CourtTrace(0, 0L, -38600L, TraceRadiusMm, "Das Kryptenportal",
                "Das Portal, durch das du gekommen bist. Außen hängt ein zerbrochener Eisenriegel: Jemand hat die Krypta von außen verschlossen. " +
                "Die Toten sollten drinnen bleiben, und du mit ihnen."),
            new CourtTrace(1, -8100L, -38700L, TraceRadiusMm, "Das Banner des Ordens",
                "Eine Glocke unter einem Spitzbogen, Rot auf Grau: das Zeichen des Glockenordens von Aschenwacht. Du erkennst es, bevor du " +
                "weißt, woher."),
            new CourtTrace(2, 19200L, 1000L, 3500L, "Die entweihte Taufschale", // read from outside its ring of votive candles
                "Hier weihte der Orden seine Läuter mit Wasser aus der Glockenschale. Die Schale ist geborsten und voller Asche, ringsum stehen " +
                "Kerzen. Jemand hat hier zur Asche gebetet statt zur Glocke."),
            new CourtTrace(3, -11520L, 9780L, TraceRadiusMm, "Das leere Grab",
                "Eine Grabplatte ohne Namen, nur eine Glocke ist eingemeißelt, und darunter liegt niemand. Der Orden legte seinen letzten Läuter " +
                "nicht ins Grab, sondern in den Aschenschlaf: Er sollte erwachen, wenn die Glocke verstummt. Die Glocke ist verstummt. Du bist erwacht."),
            new CourtTrace(4, 0L, 8000L, 3000L, "Der Aschbeter",
                "Eine verhüllte Gestalt in schwarzer Kutte, das Gesicht zur Krypta gewandt. Sie murmelt immer dieselben Worte: „Lass die Glocke " +
                "schweigen, lass die Asche kommen.“ Die Aschbeter haben den Vorhof entweiht und das Portal verriegelt. Komm ihr nicht zu nahe."),
            new CourtTrace(5, -5650L, 34500L, TraceRadiusMm, "Die Steinwächter am Tor",
                "Zwei steinerne Wächter bewachen die Treppe zum Tor. Sie sollten Pilger hereinlassen und die Asche draußen halten. In ihren Augen " +
                "glimmt nur noch ein Rest der alten Glut."),
            new CourtTrace(6, -400L, 58000L, TraceRadiusMm, "Die letzte Wache",
                "Ein Glockenwächter in voller Rüstung und ein Dochtpilger mit seiner Laterne stehen reglos auf dem Pilgerweg, grau von Asche. Sie " +
                "stehen noch dort, wo sie standen, als die Glocke verstummte."),
            new CourtTrace(7, -1600L, 59200L, TraceRadiusMm, "Die schweigende Glocke",
                "Oben im Turm hängt die Glocke von Aschenwacht. Ihr Klöppel ist herausgebrochen. Solange sie läutete, blieb der Ascheregen " +
                "jenseits des Grats. Durch die Turmtür führt der Pilgerweg hinab nach Aschengrenze, in das Dorf, das der Orden einst schützte.")
        };

        public const int AllTraces = (1 << 8) - 1;

        /// <summary>How many traces a mask holds.</summary>
        public static int Count(int mask)
        {
            int count = 0;
            for (int bits = mask & AllTraces; bits != 0; bits &= bits - 1) count++;
            return count;
        }
    }

    public sealed partial class CharacterProgression
    {
        /// <summary>The traces of Aschenwacht the wanderer has read (bit = trace id); kept while he is there.</summary>
        public int CourtTraces { get; private set; }

        internal bool ReadCourtTrace(int id)
        {
            if (!InCourt || id < 0 || id >= CourtLore.Traces.Length) return false;
            CourtTraces |= 1 << id;
            return true;
        }
    }

    public sealed partial class ArenaSimulation
    {
        /// <summary>The trace within reach, the nearest first; null when none is.</summary>
        public CourtTrace? NearCourtTrace
        {
            get
            {
                if (!InCourt || GameOver) return null;
                CourtTrace? best = null; long bestDistance = long.MaxValue;
                foreach (CourtTrace trace in CourtLore.Traces)
                {
                    long distance = DistanceSquared(PlayerXmm, PlayerZmm, trace.Xmm, trace.Zmm);
                    if (distance <= FixedMath.Square(trace.RadiusMm) && distance < bestDistance) { best = trace; bestDistance = distance; }
                }
                return best;
            }
        }

        private SocialResult ReadCourtTrace()
        {
            CourtTrace? trace = NearCourtTrace;
            if (trace == null) return new SocialResult { Text = "Hier gibt es nichts zu lesen." };
            Progression.ReadCourtTrace(trace.Value.Id);
            return new SocialResult { Accepted = true, Text = trace.Value.Title + ": " + trace.Value.Text };
        }
    }
}
