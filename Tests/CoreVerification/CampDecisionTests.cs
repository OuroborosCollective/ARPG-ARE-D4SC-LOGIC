using System;
using System.Linq;
using System.Numerics;
using AreArpg.Core;

/// <summary>
/// Owner rule 2026-11-15: Mara's missing rations are decided exactly once — by refilling the camp, by stealing from it
/// or by asking Ilyan. The decision is persisted, the ration panel then disappears for good and the story continues.
/// </summary>
internal static class CampDecisionTests
{
    public static void Run(Action<bool,string> check)
    {
        // While the arrival scene is pending nothing can be decided yet, and nothing changes.
        var a=Village(101,out var pending);
        Near(pending,0);Near(pending,SocialLedger.MaraNpcId);
        string before=a.SaveString();
        check(pending.ArrivalScenePending&&!pending.CampDecisionOpen&&!pending.CampPanelVisible&&!pending.CanDonateCampRation,"camp-decision-hidden-during-arrival-scene");
        var heard=pending.ActOnHunger(HungerAction.HearIlyan);var donated=pending.DonateCampRation();var taken=pending.TakeCampRation();
        check(!heard.Accepted&&!donated.Accepted&&!taken.Accepted&&a.SaveString()==before,"camp-decision-pending-scene-rejects-every-way");
        check(heard.Text==ArenaSimulation.ListenFirst&&donated.Text==ArenaSimulation.ListenFirst&&taken.Text==ArenaSimulation.ListenFirst,"camp-decision-pending-scene-explains-why");
        check(pending.CompleteArrivalScene()&&pending.CampDecisionOpen&&pending.CampPanelVisible&&pending.CanDonateCampRation,"camp-decision-opens-after-the-scene");

        // Way 1: refilling the camp. A partial donation keeps the decision open; the full camp closes it for good.
        var r=Village(102,out var refill);refill.CompleteArrivalScene();
        Near(refill,SocialLedger.MaraNpcId);
        check(refill.DonateCampRation().Accepted&&refill.CampDecisionOpen&&refill.CampPanelVisible&&Decisions(r)=="","camp-decision-partial-donation-keeps-it-open");
        Near(refill,SocialLedger.MaraNpcId);
        check(refill.DonateCampRation().Accepted&&r.CampDecisionMade&&!refill.CampDecisionOpen&&!refill.CampPanelVisible&&!refill.CanDonateCampRation,"camp-decision-refill-closes-the-panel");
        check(Decisions(r)=="camp.decision.replaced","camp-decision-refill-recorded-once");
        Near(refill,SocialLedger.MaraNpcId);
        string replaced=r.SaveString();
        var again=refill.DonateCampRation();var steal=refill.TakeCampRation();
        check(!again.Accepted&&!steal.Accepted&&again.Text==ArenaSimulation.CampAlreadyDecided&&steal.Text==ArenaSimulation.CampAlreadyDecided&&r.SaveString()==replaced,"camp-decision-refill-rejects-later-ways");
        check(Reloaded(replaced,102,out var refillLoaded)&&refillLoaded.Progression.CampDecisionMade&&!refillLoaded.CampPanelVisible&&!refillLoaded.ArrivalScenePending,"camp-decision-refill-survives-load");
        Near(refill,0);
        check(refill.ActOnHunger(HungerAction.HearIlyan).Accepted&&r.HungerStage==1&&Decisions(r)=="camp.decision.replaced","camp-decision-story-continues-after-refill");

        // Way 2: stealing. One theft decides; nothing further can be taken or given.
        var t=Village(103,out var theft);theft.CompleteArrivalScene();
        Near(theft,SocialLedger.MaraNpcId);
        check(theft.TakeCampRation().Accepted&&t.CampDecisionMade&&!theft.CampPanelVisible&&!theft.CanDonateCampRation,"camp-decision-theft-closes-the-panel");
        check(Decisions(t)=="camp.decision.stolen","camp-decision-theft-recorded-once");
        var events = t.Social.Forge.Archive.Events;
        var cause = events.Single(e => e.Context == "camp.theft");
        var decision = events.Single(e => e.Context == "camp.decision.stolen");
        check(cause.Id < decision.Id, "causal-camp-theft-precedes-its-story-consequence");
        Near(theft,SocialLedger.MaraNpcId);
        string stolen=t.SaveString();
        check(!theft.TakeCampRation().Accepted&&!theft.DonateCampRation().Accepted&&t.SaveString()==stolen,"camp-decision-theft-rejects-later-ways");
        check(Reloaded(stolen,103,out var theftLoaded)&&theftLoaded.Progression.CampDecisionMade&&!theftLoaded.CampPanelVisible,"camp-decision-theft-survives-load");
        Near(theft,0);
        check(theft.ActOnHunger(HungerAction.HearIlyan).Accepted&&t.HungerStage==1,"camp-decision-story-continues-after-theft");

        // Way 3: asking Ilyan starts the investigation; the ration panel disappears as well.
        var i=Village(104,out var ask);ask.CompleteArrivalScene();
        Near(ask,0);
        check(ask.ActOnHunger(HungerAction.HearIlyan).Accepted&&i.CampDecisionMade&&!ask.CampPanelVisible&&!ask.CanDonateCampRation,"camp-decision-ilyan-closes-the-panel");
        Near(ask,SocialLedger.MaraNpcId);
        string asked=i.SaveString();
        check(!ask.DonateCampRation().Accepted&&!ask.TakeCampRation().Accepted&&i.SaveString()==asked&&Decisions(i)=="","camp-decision-ilyan-rejects-ration-ways");

        // The legacy arena village keeps its open camp; the one-time rule belongs to the story village.
        var legacy=new ArenaSimulation(105,new CharacterProgression(),BigInteger.One,LootCatalog.Version1);
        check(legacy.IsVillage&&legacy.CampPanelVisible&&!legacy.CampDecisionOpen&&!legacy.ArrivalScenePending,"camp-decision-legacy-village-keeps-its-camp");
        for(int k=0;k<2;k++){Near(legacy,SocialLedger.MaraNpcId);check(legacy.DonateCampRation().Accepted,"camp-decision-legacy-donation-"+k);}
        check(legacy.CampPanelVisible&&!legacy.CanDonateCampRation,"camp-decision-legacy-full-camp-stays-visible");
    }

    private static CharacterProgression Village(ulong seed,out ArenaSimulation world)
    {
        var p=CharacterProgression.CreateEntryProfile();p.EnterVillage();p.Social.EnableForge();
        world=new ArenaSimulation(seed,p,BigInteger.One,LootCatalog.Version1);
        return p;
    }

    private static void Near(ArenaSimulation world,int id)
    {
        var npc=world.Npcs.First(n=>n.Id==id);
        npc.Xmm=world.PlayerXmm+1000;npc.Zmm=world.PlayerZmm;npc.InteractionCooldownTicks=0;
    }

    private static bool Reloaded(string save,ulong seed,out ArenaSimulation world)
    {
        world=null;
        if(!CharacterProgression.TryLoad(save,out var loaded))return false;
        world=new ArenaSimulation(seed,loaded,BigInteger.One,LootCatalog.Version1);
        return true;
    }

    private static string Decisions(CharacterProgression p)=>string.Join(",",p.Social.Forge.Archive.Events
        .Where(e=>e.Context.StartsWith(CharacterProgression.CampDecisionPrefix,StringComparison.Ordinal)).Select(e=>e.Context));
}
