using System;
using System.Linq;

namespace AreArpg.Core
{
    public sealed partial class ArenaSimulation
    {
        private readonly JourneyMapCatalog _journeyMaps;
        public bool InJourney=>Progression.Journey.Active&&Progression.Journey.Location!=0;
        public bool InJourneyCave=>InJourney&&Progression.Journey.Location==2;
        public int CurrentAreaId=>InJourney?(InJourneyCave?12:11):Progression.EntryAct;
        public int PresentationLocation=>CurrentAreaId*10+(InJourneyCave?Progression.Journey.Depth:0);
        public JourneyGeometry JourneyGeometry=>InJourney?Progression.Journey.Current.Geometry:null;
        private ulong JourneySeed=>InJourneyCave?Progression.Journey.CaveSeed:Progression.Journey.RouteSeed;
        public int JourneyPassage
        {
            get
            {
                if(GameOver)return 0;
                if(!InJourney)return InAschenzoll&&Progression.Freight.Reported&&Near(0,18000)?1:0;
                var g=JourneyGeometry;
                if(Near(g.ReturnX,g.ReturnZ))return -1;
                if(!InJourneyCave){var route=(JourneyRouteLayout)g;if(Near(route.CaveX,route.CaveZ))return 2;}
                return InJourneyCave&&Progression.Journey.Depth<Progression.Journey.FloorCount&&Near(g.ForwardX,g.ForwardZ)?3:0;
            }
        }
        public bool AtJourneyPassage=>JourneyPassage!=0;
        public bool JourneyForwardOpen=>InJourneyCave&&Progression.Journey.Depth<Progression.Journey.FloorCount&&Progression.Journey.Current.Cleared;
        private void InitializeJourney()
        {
            var f=Progression.Journey.Current;
            GroundLoot.AddRange(f.Loot);Monsters.AddRange(f.Monsters);
            if(!f.Visited)
            {
                PlaceJourneyArrival(f.Geometry.ReturnX,f.Geometry.ReturnZ);
                SaveJourneyPlayer();return;
            }
            CurrentTick=f.Tick;PlayerXmm=f.X;PlayerZmm=f.Z;PrevPlayerXmm=f.PreviousX;PrevPlayerZmm=f.PreviousZ;
            PlayerHealth=f.Health;Resource=f.Resource;Kills=f.Kills;_directionXbp=f.DirectionX;_directionZbp=f.DirectionZ;
            _rng.Restore(f.Rng0,f.Rng1);for(int i=0;i<4;i++)Cooldowns[i]=f.Cooldowns[i];
        }
        private void SaveJourneyPlayer()
        {
            if(!InJourney)return;
            var f=Progression.Journey.Current;f.Visited=true;f.X=PlayerXmm;f.Z=PlayerZmm;f.PreviousX=PrevPlayerXmm;f.PreviousZ=PrevPlayerZmm;
            f.Health=PlayerHealth;f.Resource=Resource;f.Kills=Kills;f.Tick=CurrentTick;f.Rng0=_rng.S0;f.Rng1=_rng.S1;f.DirectionX=_directionXbp;f.DirectionZ=_directionZbp;
            for(int i=0;i<4;i++)f.Cooldowns[i]=Cooldowns[i];
            f.Loot.Clear();f.Loot.AddRange(GroundLoot);
        }
        private void PlaceJourneyArrival(long x,long z)
        {
            // Arrivals are outside the automatic passage radius and use the same actor clearance as movement.
            foreach(var offset in new[]{(0L,3000L),(0L,-3000L),(3000L,0L),(-3000L,0L),(0L,2000L),(0L,-2000L),(2000L,0L),(-2000L,0L)})
                if(CanStand(x+offset.Item1,z+offset.Item2)){PlayerXmm=PrevPlayerXmm=x+offset.Item1;PlayerZmm=PrevPlayerZmm=z+offset.Item2;return;}
            throw new InvalidOperationException("Journey passage has no safe arrival");
        }
        private ArenaSimulation TraverseJourney()
        {
            int passage=JourneyPassage;if(passage==0||LootOnGround||_journeyMaps==null)return null;
            if(passage==3&&!JourneyForwardOpen)return null;
            SaveJourneyPlayer();SaveWoodlandPlayer();
            if(!CharacterProgression.TryLoad(Progression.SaveString(),_journeyMaps,out var p))return null;
            var j=p.Journey;bool wasCave=InJourneyCave;int oldDepth=j.Depth;long x=0,z=15000;
            if(!InJourney)
            {
                if(!j.Active)j.Begin(_journeyMaps,new DeterministicRng(_runSeed^0x4A4F55524E4559UL).Next64());
                j.EnterRoute();x=j.Route.Geometry.ReturnX;z=j.Route.Geometry.ReturnZ;
            }
            else if(!wasCave&&passage==-1){j.Location=0;j.RouteNeedsNewRun=true;p.Woodland.RestartPlayer();}
            else if(!wasCave&&passage==2){j.EnterCave();x=j.Current.Geometry.ReturnX;z=j.Current.Geometry.ReturnZ;}
            else if(wasCave&&passage==-1&&oldDepth==1)
            {j.Location=1;j.Depth=1;j.CaveNeedsNewRun=true;var route=(JourneyRouteLayout)j.Route.Geometry;x=route.CaveX;z=route.CaveZ;}
            else
            {
                j.Depth+=passage==3?1:-1;j.UnlockedDepth=Math.Max(j.Depth,j.UnlockedDepth);
                x=passage==3?j.Current.Geometry.ReturnX:j.Current.Geometry.ForwardX;z=passage==3?j.Current.Geometry.ReturnZ:j.Current.Geometry.ForwardZ;
            }
            var next=new ArenaSimulation(_runSeed,p,CryptDepth,_lootCatalog,_journeyMaps);
            if(next.InJourney)next.PlaceJourneyArrival(x,z);else{next.PlayerXmm=next.PrevPlayerXmm=x;next.PlayerZmm=next.PrevPlayerZmm=z;}
            next.PlayerHealth=Math.Min(PlayerHealth,next.MaxPlayerHealth);next.Resource=Resource;
            for(int i=0;i<4;i++)next.Cooldowns[i]=Cooldowns[i];
            next._directionXbp=next._directionZbp=0;next.BindCompanion(true);next.SaveJourneyPlayer();next.SaveWoodlandPlayer();
            return next;
        }
        public SocialResult OpenJourneyChest(int index)
        {
            var r=new SocialResult{Text="Gehe an eine geschlossene Truhe."};
            if(!InJourney||GameOver||index<0||index>=JourneyGeometry.ChestCount)return r;
            var f=Progression.Journey.Current;int bit=1<<index;
            JourneyGeometry.Chest(index,out var x,out var z);
            if((f.OpenChests&bit)!=0||!Near(x,z,NpcInteractionRadiusMm)||!CombatSight(PlayerXmm,PlayerZmm,x,z))return r;
            f.OpenChests|=bit;int value=12*(InJourneyCave?Progression.Journey.Depth:1);
            GroundLoot.Add(new LootState{Id=9000+index,Xmm=x,Zmm=z,WeaponBonus=value});SaveJourneyPlayer();
            r.Accepted=true;r.Text="Die Truhe enthält "+value+" Schmiedepunkte. Hebe die Beute auf.";return r;
        }
        public void RestartJourneyPlayer()
        {
            if(!InJourney)return;
            PlayerHealth=MaxPlayerHealth;Resource=100;Array.Clear(Cooldowns,0,Cooldowns.Length);_directionXbp=_directionZbp=0;
            PlaceJourneyArrival(JourneyGeometry.ReturnX,JourneyGeometry.ReturnZ);SaveJourneyPlayer();
        }
        public string JourneyObjective=>InJourneyCave?
            "Seitenhöhle – Ebene "+Progression.Journey.Depth+"/"+Progression.Journey.FloorCount+". "+
            (!Progression.Journey.Current.Cleared?"Erkunde die Kammern und besiege ihre Wächter. Truhen liegen in den Seitenkammern.":LootOnGround?"Sichere deine Beute.":JourneyForwardOpen?"Im Norden führt der Abstieg tiefer. Der Rückweg liegt im Süden.":"Die tiefste Ebene ist frei. Kehre über die Aufgänge zum Wald zurück."):
            "Folge dem Waldweg weiter nach Norden. Westlich der Rastlichtung liegt eine optionale Höhle mit vier Ebenen; im Osten wartet eine Schatzabzweigung. Erkunde die Rastlichtung und sichere deine Beute.";
    }
    public sealed partial class CharacterProgression
    {
        internal Action JourneyCapture;
    }
}
