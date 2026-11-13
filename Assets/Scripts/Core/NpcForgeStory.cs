using System;
using AshenCrypt.MemoryForge;

namespace AreArpg.Core
{
    public sealed partial class ArenaSimulation
    {
        private void RememberStoryAction(HungerAction action)
        {
            var forge=Progression.Social.Forge;if(forge==null||action==HungerAction.ToggleSneak||action==HungerAction.BeginDungeonRun||
                action==HungerAction.OpenWestChest||action==HungerAction.OpenEastChest||action>=HungerAction.ForestChest0&&action<=HungerAction.ForestChest3)return;
            string contact=null;
            if(action==HungerAction.HearIlyan||action==HungerAction.Protect||action==HungerAction.ReportCrypt||action==HungerAction.ReportRoyalBrand||action==HungerAction.AcceptTransportLead)contact="ilyan";
            if(action==HungerAction.Reveal||action==HungerAction.Replace||action==HungerAction.ReportCubs||action==HungerAction.OrganizeCubProtection||action==HungerAction.CraftPeltArmor)contact="mara";
            var impact=action==HungerAction.Protect?new RelationshipChange(trust:2000,gratitude:3000):action==HungerAction.Replace?new RelationshipChange(trust:1000,gratitude:1500):new RelationshipChange();
            string target=contact??(Progression.EntryAct==9?"bear.cubs":Progression.EntryAct==4?"world.mill":Progression.EntryAct==3?"world.road":"world.crypt");
            var e=forge.Archive.Append(MemoryKind.Quest,"player",target,forge.ProcessedTicks,8000,true,"story."+action.ToString().ToLowerInvariant(),impact);
            if(contact!=null)forge.Archive.Learn(contact,e.Id,KnowledgeOrigin.Self,contact,forge.ProcessedTicks);
            var c=Progression.Companion;
            if(CompanionPresent&&c.Health>0&&DistanceSquared(PlayerXmm,PlayerZmm,c.Xmm,c.Zmm)<=FixedMath.Square(8000)&&CombatSight(PlayerXmm,PlayerZmm,c.Xmm,c.Zmm))
                forge.Archive.Learn("hunter",e.Id,KnowledgeOrigin.Witnessed,"hunter",forge.ProcessedTicks,6000);
            // These are memories of the actual conversation/choice, not facts asserted by a report.
            // Existing quest owners remain the sole authority on patients, brand discovery and cub survival.
        }
    }
}
