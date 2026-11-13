using System;

namespace AreArpg.Core
{
    public sealed partial class ArenaSimulation
    {
        public bool InWoodlandRegion => Progression.EntryAct>=8&&Progression.EntryAct<=10;
        public bool InForest => Progression.EntryAct==8;
        public const long MillForestXmm=-20000,MillForestZmm=18000;
        private bool Near(long x,long z,long radius=PassageRadiusMm) => DistanceSquared(PlayerXmm,PlayerZmm,x,z)<=FixedMath.Square(radius);
        public int WoodlandPassage
        {
            get
            {
                if(!Progression.Woodland.Active) return 0;
                int act=Progression.EntryAct;
                if(act==4&&Near(MillForestXmm,MillForestZmm))return 8;
                if(act==8) { if(Near(0,-48000))return 4; if(Near(0,48000))return 10; if(Near(-36000,0))return 9; }
                if((act==9||act==10)&&Near(0,-18000))return 8;
                return 0;
            }
        }
        public bool AtWoodlandPassage => WoodlandPassage!=0;
        public static bool CanStandInWoodland(int act,WoodlandLayout map,long x,long z)
        {
            if(act==8)return map.CanStand(x,z);
            if(act==9)return Math.Abs(x)<=10000&&z>=-20000&&z<=14000;
            return act==10&&Math.Abs(x)<=14000&&z>=-20000&&z<=20000;
        }
        private bool CombatSight(long ax,long az,long bx,long bz) => InForest?Progression.Woodland.Layout.HasSight(ax,az,bx,bz):!InDungeon||Progression.Dungeon.Current.Layout.HasSight(ax,az,bx,bz);
        private void InitializeWoodland()
        {
            var w=Progression.Woodland;
            PlayerXmm=PrevPlayerXmm=0;
            PlayerZmm=PrevPlayerZmm=InForest?-45000:-15000;
            CurrentTick=w.Tick;
            if(InForest) { GroundLoot.AddRange(w.Loot); Monsters.AddRange(w.Monsters); }
            if(w.SavedAct!=Progression.EntryAct)return;
            PlayerXmm=PrevPlayerXmm=w.SavedX; PlayerZmm=PrevPlayerZmm=w.SavedZ;
            PlayerHealth=Math.Min(MaxPlayerHealth,w.SavedHealth);Resource=w.SavedResource;
            for(int i=0;i<4;i++)Cooldowns[i]=w.SavedCooldowns[i];_rng.Restore(w.Rng0,w.Rng1);
        }
        private void SaveWoodlandPlayer()
        {
            if(!InWoodlandRegion)return;
            var w=Progression.Woodland;w.Tick=CurrentTick;
            w.SavedAct=Progression.EntryAct;w.SavedX=PlayerXmm;w.SavedZ=PlayerZmm;w.SavedHealth=PlayerHealth;w.SavedResource=Resource;
            for(int i=0;i<4;i++)w.SavedCooldowns[i]=Cooldowns[i];w.Rng0=_rng.S0;w.Rng1=_rng.S1;
            if(InForest) { w.Loot.Clear();w.Loot.AddRange(GroundLoot); }
        }
        private ArenaSimulation TraverseWoodland()
        {
            int target=WoodlandPassage,from=Progression.EntryAct;
            if(target==0||GameOver||GroundLoot.Count!=0)return null;
            SaveWoodlandPlayer();
            if(!CharacterProgression.TryLoad(Progression.SaveString(),out var p))return null;
            var w=p.Woodland;
            if(from==8&&(target==4||target==10))w.NeedsNewRun=true;
            if(target==10)w.ReachedAschenzoll=true;
            if(target==8&&from!=9)w.Enter();
            p.TravelHunger(target,false);
            var next=new ArenaSimulation(_runSeed,p,CryptDepth,_lootCatalog);
            long x=0,z=-15000;
            if(target==8) { x=from==9?-33000:0; z=from==4?-45000:from==10?45000:0; }
            if(target==4) { x=MillForestXmm+3000;z=MillForestZmm; }
            next.PlayerXmm=next.PrevPlayerXmm=x;next.PlayerZmm=next.PrevPlayerZmm=z;
            next.PlayerHealth=Math.Min(PlayerHealth,next.MaxPlayerHealth);next.Resource=Resource;
            for(int i=0;i<4;i++)next.Cooldowns[i]=Cooldowns[i];
            next.SaveWoodlandPlayer();next.SaveRoadPlayer();next.SaveInvestigationPlayer();
            return next;
        }
        private void ReceiveDamage(int amount)
        { PlayerHealth=Math.Max(0,PlayerHealth-(Progression.Woodland.ArmorCrafted?Math.Max(1,amount*90/100):amount)); }
        public string BearFamilyContext => !Progression.Road.Started ? "Die Spuren führen zur Bärin an der Aschestraße. Über ihr Schicksal weißt du noch nichts." :
            Progression.Road.Bear.IsDead ? "Es sind die Jungen der Bärin von der Aschestraße. Ihre Mutter ist tot." : "Es sind die Jungen der Bärin von der Aschestraße. Ihre Mutter lebt.";
        private SocialResult ActOnWoodland(HungerAction action)
        {
            var result=new SocialResult {Text="Gehe zur passenden Person oder Fundstelle."};
            if(GameOver)return result;
            var w=Progression.Woodland;int act=Progression.EntryAct;
            if(action==HungerAction.AcceptTransportLead&&!w.Active&&act==2&&Progression.Rattler.Reported)
            {
                var npc=HungerContact(0);if(npc==null)return result;
                npc.InteractionCooldownTicks=10;w.Accept(_rng.Next64());
                result.Text="Ilyan: Verfolge den Transportweg. Westlich der Mühle führt der Waldpfad nach Aschenzoll. Dort werden die Frachtwagen abgefertigt.";
            }
            else if(!w.Active)return result;
            else if(action>=HungerAction.ForestChest0&&action<=HungerAction.ForestChest3&&InForest)
            {
                int index=(int)action-(int)HungerAction.ForestChest0;w.Layout.Chest(index,out var x,out var z);
                if(!w.Layout.HasChest(index)||(w.OpenChests&(1<<index))!=0||!Near(x,z,NpcInteractionRadiusMm)||!CombatSight(PlayerXmm,PlayerZmm,x,z))return result;
                w.OpenChests|=1<<index;GroundLoot.Add(new LootState {Id=300+index,Xmm=x,Zmm=z,WeaponBonus=12});
                result.Text="Unter dem verrotteten Deckel liegen Schmiedefragmente. Hebe sie auf.";
            }
            else if(act==9&&Near(0,5000,NpcInteractionRadiusMm))
            {
                if(action==HungerAction.InspectCubs&&!w.CubsInspected) {w.CubsInspected=true;result.Text=BearFamilyContext+" Drei Junge drängen sich im Nest zusammen.";}
                else if(w.CubsInspected&&w.Choice==CubChoice.Undecided&&action==HungerAction.SpareCubs)
                {w.Choice=CubChoice.Spared;result.Text="Du lässt die Jungen leben. Sprich persönlich mit Mara, um ihren Schutz zu organisieren.";}
                else if(w.CubsInspected&&w.Choice==CubChoice.Undecided&&action==HungerAction.SkinCubs)
                {w.Choice=CubChoice.Skinned;result.Text="Du tötest und häutest die drei Jungen. Du erhältst einmalig drei Felle. Mara kann daraus eine Fellrüstung herstellen; diese Jungen werden niemals Kriegsbären.";}
                else return result;
            }
            else if(act==2)
            {
                var npc=HungerContact(SocialLedger.MaraNpcId);if(npc==null)return result;
                if(action==HungerAction.ReportCubs&&w.Choice==CubChoice.Spared&&!w.CubsReported)
                {w.CubsReported=true;result.Text="Mara kennt nun den Standort der lebenden Jungen. Vereinbare noch ihre Versorgung.";}
                else if(action==HungerAction.OrganizeCubProtection&&w.CubsReported&&!w.ProtectionOrganized)
                {w.ProtectionOrganized=true;result.Text="Mara organisiert Futter und Schutz für die Jungen. Diese Hilfe kann später einer Fraktion Zugang zu Kriegsbären eröffnen.";}
                else if(action==HungerAction.CraftPeltArmor&&w.Pelts==3)
                {w.ArmorCrafted=true;result.Text="Mara verarbeitet deine drei Felle. Die Fellrüstung ist angelegt und verringert erlittenen Schaden um 10 %.";}
                else return result;
                npc.InteractionCooldownTicks=10;
            }
            else return result;
            SaveWoodlandPlayer();result.Accepted=true;return result;
        }
        private string WoodlandObjective
        {
            get
            {
                var w=Progression.Woodland;
                if(Progression.EntryAct==9)return !w.CubsInspected?"Untersuche das Nest in der Bärenhöhle.":w.Choice==CubChoice.Undecided?"Entscheide am Nest: Junge verschonen und Schutz suchen oder töten und Felle nehmen.":"Die Entscheidung bleibt bestehen. Kehre über den Höhleneingang in denselben Wald zurück.";
                if(w.Choice==CubChoice.Spared&&!w.ProtectionOrganized)return w.CubsReported?"Vereinbare bei Mara den Schutz der Bärenjungen.":"Berichte Mara persönlich von den verschonten Bärenjungen.";
                if(w.Pelts>0)return "Bringe Mara die drei Felle und lasse eine Fellrüstung herstellen.";
                if(Progression.EntryAct==10)return "Aschenzoll erreicht. Der Frachtplatz liegt vor dir; der südliche Weg führt zurück in den Wald.";
                if(InForest)return "Folge dem Waldweg nach Norden bis Aschenzoll. Eine Bärenhöhle liegt westlich der mittleren Lichtungen. Erkunde Abzweige und sichere Beute.";
                return "Folge von der Mühle dem westlichen Waldpfad nach Aschenzoll.";
            }
        }
    }
}
