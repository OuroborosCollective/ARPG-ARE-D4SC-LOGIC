using System;
using System.Linq;
using AshenCrypt.MemoryForge;

namespace AreArpg.Core
{
    public sealed partial class SocialLedger
    {
        private static string RoadIdentity(int id) => id<3?NpcForgeState.Identity(id):id==3?"hunter":"road.traveler";
        private void RememberRoadFact(RoadFact fact,bool travelerWitness,bool hunterDirect)
        {
            if(Forge==null)return;
            Forge.Ensure("hunter","companion");Forge.Ensure("road.traveler","traveler");
            var kind=fact==RoadFact.Rescue?MemoryKind.Rescue:fact==RoadFact.Death?MemoryKind.Death:fact==RoadFact.CorpseTheft?MemoryKind.Theft:MemoryKind.Quest;
            var impact=fact==RoadFact.Rescue?new RelationshipChange(trust:6000,gratitude:10000,respect:4000):
                fact==RoadFact.CorpseTheft?new RelationshipChange(trust:-3000,mistrust:4000,hostility:3000):
                fact==RoadFact.RefusedHelp?new RelationshipChange(trust:-1200,mistrust:2000):new RelationshipChange(fear:5000);
            string actor=fact==RoadFact.Death?"road.bear":"player";
            var e=Forge.Archive.Append(kind,actor,"hunter",Forge.ProcessedTicks,10000,true,"road.fact."+RoadEventId(fact),impact);
            if(hunterDirect)Forge.Archive.Learn("hunter",e.Id,KnowledgeOrigin.Self,"hunter",Forge.ProcessedTicks);
            if(travelerWitness)Forge.Archive.Learn("road.traveler",e.Id,KnowledgeOrigin.Witnessed,"road.traveler",Forge.ProcessedTicks,6000);
        }
        private void RememberRoadReport(int source,int recipient,RoadFact fact)
        {
            if(Forge==null)return;
            var e=Forge.Archive.Events.FirstOrDefault(x=>x.Context=="road.fact."+RoadEventId(fact));
            // Legacy road knowledge remains in its original owner; unknown historical ticks are never invented.
            if(e==null)return;
            Forge.Ensure(RoadIdentity(source),source==3?"companion":source==4?"traveler":source==0?"traveler":source==1?"merchant":"guard");
            Forge.Ensure(RoadIdentity(recipient),recipient==3?"companion":recipient==4?"traveler":recipient==0?"traveler":recipient==1?"merchant":"guard");
            Forge.Archive.Report(RoadIdentity(source),RoadIdentity(recipient),e.Id,Forge.ProcessedTicks);
        }
    }
}
