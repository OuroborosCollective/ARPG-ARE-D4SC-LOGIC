namespace AreArpg.Core
{
    public sealed partial class ArenaSimulation
    {
        public bool InDungeon { get { return Progression.Dungeon.Active && (Progression.EntryAct==6 || Progression.EntryAct==7); } }
        public bool DungeonForwardOpen { get { return InDungeon && Progression.Dungeon.Depth<3 &&
            Progression.Dungeon.Current.Cleared && (Progression.Dungeon.Depth==1 || Progression.Rattler.Active); } }

        private SocialResult ActOnDungeon(HungerAction action)
        {
            if(action==HungerAction.OpenDungeonSeal || action==HungerAction.BreakDungeonSeal)
            {
                var r=ActOnInvestigation(action==HungerAction.OpenDungeonSeal ? HungerAction.OpenSeal : HungerAction.BreakSeal);
                if(r.Accepted) { Progression.Dungeon.Begin(_runSeed); }
                return r;
            }
            var result=new SocialResult { Text="Gehe nahe an eine noch geschlossene Truhe." };
            if(!InDungeon || GameOver) return result;
            int index=action==HungerAction.OpenWestChest?0:action==HungerAction.OpenEastChest?1:-1;
            if(index<0) return result;
            var d=Progression.Dungeon; int bit=1<<index;
            if(!d.Current.Layout.HasChest(index)) return result;
            d.Current.Layout.Chest(index,out var x,out var z);
            if((d.Current.OpenChests&bit)!=0 || DistanceSquared(PlayerXmm,PlayerZmm,x,z)>FixedMath.Square(NpcInteractionRadiusMm) ||
                !d.Current.Layout.HasSight(PlayerXmm,PlayerZmm,x,z)) return result;
            d.Current.OpenChests|=bit;
            GroundLoot.Add(new LootState { Id=900+d.Depth*2+index,Xmm=x,Zmm=z,WeaponBonus=12 });
            SaveDungeonPlayer(); result.Accepted=true; result.Text="In der Truhe liegen Schmiedefragmente. Sammle die Beute ein, um deine Waffe zu verbessern.";
            return result;
        }

        private void SaveDungeonPlayer()
        { if(InDungeon) { Progression.Dungeon.Tick=CurrentTick; SaveInvestigationPlayer(); } }

        private ArenaSimulation TraverseDungeon(bool backwards)
        {
            if(GameOver || GroundLoot.Count!=0 || (!backwards && !DungeonForwardOpen)) return null;
            if(!CharacterProgression.TryLoad(Progression.SaveString(),out var p)) return null;
            int depth=p.Dungeon.Depth+(backwards?-1:1);
            if(depth==0) { p.TravelHunger(5,true); p.Dungeon.NeedsNewRun=true; }
            else
            {
                bool firstBoss=depth==3 && p.Dungeon.UnlockedDepth<3;
                p.Dungeon.Depth=depth; p.Dungeon.UnlockedDepth=System.Math.Max(depth,p.Dungeon.UnlockedDepth);
                p.TravelHunger(depth==3?7:6,backwards);
                if(firstBoss)
                {
                    var r=p.Rattler; r.Creature.Xmm=r.Creature.PreviousXmm=0;
                    r.Creature.Zmm=r.Creature.PreviousZmm=17000; r.TargetX=0; r.TargetZ=17000;
                }
            }
            return new ArenaSimulation(_runSeed,p,CryptDepth,_lootCatalog);
        }

        public string DungeonObjective
        {
            get
            {
                var d=Progression.Dungeon;
                if(d.Depth==3) return !Progression.Rattler.Creature.IsDead ? "Ebene III: Erkunde die Kammern und besiege den MaulwurfRattler im Nordsaal." :
                    !Progression.Rattler.BrandExamined ? RattlerObjective : "Der Rattler ist besiegt. Sichere deine Beute. Ein neuer Run beginnt nach dem Rueckzug in den Speicher und erneutem Abstieg.";
                if(!d.Current.Cleared) return "Ebene "+d.Depth+"/3: Erkunde die Seitenkammern und besiege die Waechter. Halte nach Truhen mit optionaler Beute Ausschau.";
                if(GroundLoot.Count>0) return "Sichere die gefallene Beute, bevor du die Ebene verlaesst.";
                if(d.Depth==2 && !Progression.Rattler.Active) return "Untersuche den frischen Verband im Nordsaal und oeffne den Riegel zur dritten Ebene.";
                return "Die Waechter sind besiegt. Der Abstieg im Norden fuehrt tiefer; im Sueden liegt dein Rueckweg.";
            }
        }
    }
}
