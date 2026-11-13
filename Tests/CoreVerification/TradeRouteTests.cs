using System;
using System.Linq;
using System.Text;
using AreArpg.Core;

internal static class TradeRouteTests
{
    // A completed previous chapter is arranged; all new interactions, movement and saves use production code.
    internal static ArenaSimulation Village(bool reported = true)
    {
        var p = RattlerEncounterTests.Den().Progression;
        p.Rattler.Creature.Health = 0; p.Rattler.Creature.LootGranted = true;
        p.Rattler.BrandExamined = true; p.Rattler.Reported = reported; p.Investigation.Reported = reported;
        p.TravelHunger(2, false);
        return new ArenaSimulation(123, p);
    }
    internal static void Contact(ArenaSimulation w)
    {
        var n = w.Npcs.Find(x => x.Id == 0);
        n.Xmm = w.PlayerXmm + 1000; n.Zmm = w.PlayerZmm; n.InteractionCooldownTicks = 0;
    }
    internal static ArenaSimulation Reload(ArenaSimulation w)
    {
        if (!CharacterProgression.TryLoad(w.Progression.SaveString(), out var p)) throw new Exception("Invalid trade checkpoint");
        return new ArenaSimulation(123, p);
    }
    internal static string Resign(string[] parts)
    {
        string payload = string.Join("|", parts.Take(parts.Length - 1));
        ulong h = 14695981039346656037UL;
        unchecked { foreach (byte b in Encoding.UTF8.GetBytes(payload)) { h ^= b; h *= 1099511628211UL; } }
        return payload + "|" + h.ToString("X16");
    }
    internal static void Run(Action<bool, string> check)
    {
        var w = Village(); string legacy = w.Progression.SaveString();
        check(legacy.StartsWith("AC12|") && Reload(w).Progression.SaveString() == legacy, "trade-unused-ac12-byte-stable");
        check(!w.ActOnHunger(HungerAction.AskTransport).Accepted, "trade-no-remote-acceptance");
        var unreported = Village(false); Contact(unreported);
        check(!unreported.ActOnHunger(HungerAction.AskTransport).Accepted, "trade-brand-examination-alone-insufficient");
        Contact(w); var ilyan = w.Npcs.Find(n => n.Id == 0); ilyan.InteractionCooldownTicks = 1;
        check(!w.ActOnHunger(HungerAction.AskTransport).Accepted, "trade-contact-cooldown-enforced");
        ilyan.InteractionCooldownTicks = 0; ilyan.Memory.Health = 0;
        check(!w.ActOnHunger(HungerAction.AskTransport).Accepted, "trade-unconscious-contact-rejected");
        ilyan.Memory.Health = 900;
        string social = w.Progression.Social.Token();
        check(w.ActOnHunger(HungerAction.AskTransport).Accepted && w.Progression.TradeRoute.Accepted, "trade-personal-acceptance");
        check(!w.ActOnHunger(HungerAction.AskTransport).Accepted && social == w.Progression.Social.Token(), "trade-no-repeat-or-offscreen-knowledge");
        string saved = w.Progression.SaveString();
        check(saved.StartsWith("AC13|") && Reload(w).Progression.SaveString() == saved, "trade-ac13-roundtrip");
        var parts = saved.Split('|'); var rattler = parts[19].Split(','); rattler[3] = "0"; parts[19] = string.Join(",", rattler);
        check(!CharacterProgression.TryLoad(Resign(parts), out _), "trade-accepted-without-report-rejected");
    }
}
