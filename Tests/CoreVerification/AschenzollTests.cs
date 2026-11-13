using System;
using System.Linq;
using AreArpg.Core;
using AshenCrypt.MemoryForge;

internal static class AschenzollTests
{
    internal static ArenaSimulation Station()
    {
        var forest=WoodlandTests.Enter();
        var p=forest.Progression;
        // Authored station fixture; subsequent movement, interactions and saves use production paths.
        p.Woodland.ReachedAschenzoll=true;p.Woodland.NeedsNewRun=true;
        p.TravelHunger(10,false);p.Social.EnableForge();
        return new ArenaSimulation(71,p,1,LootCatalog.Version1);
    }
    public static void Run(Action<bool,string> check)
    {
        var envelope=Station().Progression.SaveString();
        string inner=Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(envelope));
        foreach(string token in new[]{"1,7,-1,0,0,0,0","1,7,2,-1,0,0,0","1,7,-2147483648,0,0,0,0","1,7,2,-2147483648,0,0,0","1,7,+1,0,0,0,0","1,7,2,+1,0,0,0"})
            check(!CharacterProgression.TryLoad(SignFreight("AC19|"+inner+"|"+token),out _),"freight-rechecksummed-signed-values-rejected-"+token);
        check(CharacterProgression.TryLoad(SignFreight("AC19|"+inner+"|1,7,0,0,0,0,0"),out _),"freight-rechecksummed-unsigned-control-loads");
        foreach(bool release in new[]{false,true})
        {
            var w=Station();var experience=w.Progression.TotalExperience;string legacy=w.Progression.SaveString();
            // Pending optional objectives must not hide the station investigation or its recorded result.
            w.Progression.Woodland.CubsInspected=true;
            w.Progression.Woodland.Choice=release?CubChoice.Skinned:CubChoice.Spared;
            check(w.HungerObjective.StartsWith(w.FreightObjective,StringComparison.Ordinal)&&w.HungerObjective.Contains("Mara"),"freight-station-objective-before-unfinished-cubs-"+release);
            check(!legacy.StartsWith("AC19|")&&CharacterProgression.TryLoad(legacy,out var old)&&!old.Freight.HasProgress,"freight-old-save-remains-unchanged-"+release);
            check(!w.ActOnHunger(HungerAction.InspectFreightNotice).Accepted,"freight-distant-reading-forbidden-"+release);
            HunterEncounterTests.Move(w,-3000,-1000);
            check(!w.ActOnHunger(HungerAction.ReleaseFreight).Accepted&&!w.ActOnHunger(HungerAction.SecureFreight).Accepted,"freight-requires-three-independent-clues-"+release);
            check(w.ActOnHunger(HungerAction.ReadFreightManifest).Accepted&&!w.Progression.Freight.OriginalCollected,"freight-reading-is-not-possession-"+release);
            HunterEncounterTests.Move(w,-5000,-12000);
            check(w.ActOnHunger(HungerAction.InspectFreightNotice).Accepted,"aschentoll-read-real-station-notice-"+release);
            string once=w.Progression.SaveString();int events=w.Progression.Social.Forge.Archive.Events.Count;
            check(!w.ActOnHunger(HungerAction.InspectFreightNotice).Accepted&&w.Progression.SaveString()==once,"freight-repeat-does-not-add-memory-or-reward-"+release);
            HunterEncounterTests.Move(w,13500,-4000);
            var cage=w.ActOnHunger(HungerAction.InspectFreightCage);
            check(cage.Accepted&&cage.Text.Contains("leer")&&w.Progression.Freight.Clues==7,"freight-empty-cage-not-unidentified-prisoners-"+release);
            check(w.Progression.Social.Forge.Archive.Get("ilyan").Known.Count==0&&w.Progression.Social.Forge.Archive.Get("mara").Known.Count==0,"freight-offscreen-npcs-do-not-know-findings-"+release);
            HunterEncounterTests.Move(w,-3000,-1000);
            check(w.ActOnHunger(release?HungerAction.ReleaseFreight:HungerAction.SecureFreight).Accepted,"freight-choice-at-real-desk-"+release);
            check(!w.ActOnHunger(release?HungerAction.SecureFreight:HungerAction.ReleaseFreight).Accepted,"freight-exclusive-permanent-branch-"+release);
            var f=w.Progression.Freight;
            check(!f.EvidenceReady&&!w.ActOnHunger(HungerAction.ReportFreightEvidence).Accepted,"freight-choice-alone-is-not-completed-evidence-"+release);
            if(release)
            {
                check(!w.ActOnHunger(HungerAction.TakeFreightOriginal).Accepted,"freight-released-original-stays-with-shipment");
                for(int i=0;i<11;i++)w.Step();
                int progress=f.MotionTicks;w.Progression.TravelHunger(4,false);
                var away=new ArenaSimulation(71,w.Progression,1,LootCatalog.Version1);
                for(int i=0;i<30;i++)away.Step();
                check(f.MotionTicks==progress,"freight-offscreen-motion-pauses");
                w.Progression.TravelHunger(10,false);w=new ArenaSimulation(71,w.Progression,1,LootCatalog.Version1);
                for(int i=0;i<7;i++)w.Step();
                var clone=WoodlandTests.Reload(w);
                for(int i=0;i<15;i++){w.Step();clone.Step();check(w.Progression.SaveString()==clone.Progression.SaveString(),"freight-mid-motion-save-replay-"+i);}
                w=clone;f=w.Progression.Freight;
                for(int i=0;i<100;i++)w.Step();
                check(f.MotionTicks==100&&f.CargoZ==FreightState.EndZ&&!f.ArrivalInspected,"freight-arrival-is-not-investigation");
                check(!w.ActOnHunger(HungerAction.InspectFreightArrival).Accepted,"freight-upper-inspection-needs-local-presence");
                HunterEncounterTests.Move(w,13500,14000);
                check(w.ActOnHunger(HungerAction.InspectFreightArrival).Accepted&&f.ArrivalInspected&&!f.OriginalCollected,"freight-actual-upper-ramp-investigation");
            }
            else
            {
                check(w.ActOnHunger(HungerAction.TakeFreightOriginal).Accepted&&f.OriginalCollected,"freight-separate-original-collection");
                for(int i=0;i<110;i++)w.Step();
                check(f.MotionTicks==0&&f.CargoZ==FreightState.StartZ&&!f.ArrivalInspected,"freight-secured-platform-stays-put");
                HunterEncounterTests.Move(w,11000,14000);HunterEncounterTests.Move(w,11000,-16000);HunterEncounterTests.Move(w,0,-18000);
                check(w.AtWoodlandPassage&&w.TryTraversePassage()!=null,"freight-blocked-platform-never-blocks-footpath-or-exit");
            }
            w=WoodlandTests.Reload(w);f=w.Progression.Freight;
            check(f.EvidenceReady&&!f.Reported&&w.Progression.SaveString().StartsWith("AC19|"),"freight-evidence-persists-before-report-"+release);
            var p=w.Progression;p.TravelHunger(2,false);w=new ArenaSimulation(71,p,1,LootCatalog.Version1);
            check(!w.ActOnHunger(HungerAction.ReportFreightEvidence).Accepted,"freight-no-remote-report-"+release);
            var ilyan=w.Npcs.First(n=>n.Id==0);ilyan.Xmm=w.PlayerXmm+1000;ilyan.Zmm=w.PlayerZmm;
            var report=w.ActOnHunger(HungerAction.ReportFreightEvidence);
            check(report.Accepted&&f.Reported&&report.Text.Contains(release?"oberen Rampe":"Originalbeleg"),"freight-personal-branch-specific-report-"+release);
            check(w.HungerObjective.Contains("Nordwechsel")&&w.HungerObjective.Contains(release?"Weitergabe":"Originalbeleg"),"freight-journal-retains-completed-branch-in-village-"+release);
            check(w.HungerObjective.Contains("Mara")&&!w.Progression.Woodland.ProtectionOrganized&&!w.Progression.Woodland.ArmorCrafted,"freight-preserves-unfinished-cub-followup-after-report-"+release);
            var mind=p.Social.Forge.Archive.Get("ilyan");
            check(mind.Known.Count==1&&mind.Known[0].Origin==KnowledgeOrigin.Self&&mind.Known[0].Event.Context=="story.reportfreightevidence","freight-ilyan-remembers-only-personal-report-"+release);
            check(p.Social.Forge.Archive.Get("dorn").Known.Count==0&&p.Social.Forge.Archive.Get("mara").Known.Count==0,"freight-report-is-not-worldwide-knowledge-"+release);
            string reported=p.SaveString();check(!w.ActOnHunger(HungerAction.ReportFreightEvidence).Accepted&&p.SaveString()==reported,"freight-report-idempotent-"+release);
            w=WoodlandTests.Reload(w);
            check(w.HungerObjective.StartsWith(w.FreightObjective,StringComparison.Ordinal)&&w.HungerObjective.Contains("Mara"),"freight-and-cub-journal-priority-survives-restore-"+release);
            check(w.Progression.Freight.Reported&&w.Progression.Social.Forge.Archive.Get("ilyan").Known.Count==1,"freight-report-and-memory-restore-together-"+release);
            check(w.Progression.TotalExperience==experience,"freight-investigation-does-not-invent-xp-"+release);
        }
        foreach(string invalid in new[]{"1,7,0,1,0,0,0","1,7,1,0,0,1,0","1,7,2,99,0,1,0","1,7,2,100,1,1,1","1,3,1,0,1,0,0","1,0,0,0,0,0,0","1,7,0,0,0,0,1","1,7,2,-1,0,0,0"})
            check(!FreightState.TryRead(invalid,out _),"freight-impossible-state-rejected-"+invalid);
    }
    private static string SignFreight(string body)
    {
        ulong hash=14695981039346656037UL;
        unchecked { foreach(byte value in System.Text.Encoding.UTF8.GetBytes(body)){hash^=value;hash*=1099511628211UL;} }
        return body+"|"+hash.ToString("X16",System.Globalization.CultureInfo.InvariantCulture);
    }
}
