using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AreArpg.Core
{
    /// <summary>Continuity for the existing village actors and player, serialized by CharacterProgression.</summary>
    internal sealed class ForgeVillageCheckpoint
    {
        internal ulong Tick,Rng0,Rng1,Seed;
        internal long X,Z,PreviousX,PreviousZ,DirectionX,DirectionZ;
        internal int Health,Resource;
        internal readonly int[] Cooldowns=new int[4];
        internal readonly long[][] People=new long[3][];
        internal string Token()
        {
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream))
            {
                w.Write(1);w.Write(Seed);w.Write(Tick);w.Write(Rng0);w.Write(Rng1);
                foreach(long n in new[]{X,Z,PreviousX,PreviousZ,DirectionX,DirectionZ})w.Write(n);
                w.Write(Health);w.Write(Resource);foreach(int n in Cooldowns)w.Write(n);
                foreach(var person in People)foreach(long n in person)w.Write(n);
                return Convert.ToBase64String(stream.ToArray());
            }
        }
        internal static bool TryRead(string token,out ForgeVillageCheckpoint checkpoint)
        {
            checkpoint=null;if(token=="-")return true;
            try
            {
                byte[] bytes=Convert.FromBase64String(token);if(bytes.Length!=324||Convert.ToBase64String(bytes)!=token)return false;
                using(var r=new BinaryReader(new MemoryStream(bytes,false)))
                {
                    if(r.ReadInt32()!=1)return false;
                    var c=new ForgeVillageCheckpoint{Seed=r.ReadUInt64(),Tick=r.ReadUInt64(),Rng0=r.ReadUInt64(),Rng1=r.ReadUInt64(),X=r.ReadInt64(),Z=r.ReadInt64(),PreviousX=r.ReadInt64(),PreviousZ=r.ReadInt64(),DirectionX=r.ReadInt64(),DirectionZ=r.ReadInt64(),Health=r.ReadInt32(),Resource=r.ReadInt32()};
                    if((c.Rng0|c.Rng1)==0||c.Health<0||c.Resource<0||c.Resource>100||!Position(c.X,c.Z)||!Position(c.PreviousX,c.PreviousZ)||c.DirectionX < -10000||c.DirectionX>10000||c.DirectionZ < -10000||c.DirectionZ>10000)return false;
                    for(int i=0;i<4;i++){c.Cooldowns[i]=r.ReadInt32();if(c.Cooldowns[i]<0||c.Cooldowns[i]>1000)return false;}
                    for(int i=0;i<3;i++)
                    {
                        var n=new long[9];for(int j=0;j<9;j++)n[j]=r.ReadInt64();
                        if(!Position(n[0],n[1])||!Position(n[2],n[3])||n[4]<0||n[4]>15||n[5]<0||n[5]>10||n[6]<0||n[6]>1||n[7]<0||n[7]>8||n[8]!=i)return false;
                        c.People[i]=n;
                    }
                    checkpoint=c;return c.Token()==token;
                }
            }
            catch(Exception e) when(e is FormatException||e is IOException||e is ArgumentException){return false;}
        }
        private static bool Position(long x,long z)=>x>=-30000&&x<=30000&&z>=-30000&&z<=30000;
    }
    public sealed partial class ArenaSimulation
    {
        private void BindForge()
        {
            var forge=Progression.Social.Forge;if(forge==null)return;
            foreach(var npc in Npcs)npc.Mind=forge.Archive.Get(NpcForgeState.Identity(npc.Id));
            if(Progression.EntryAct==2&&ForgeVillageCheckpoint.TryRead(forge.Checkpoint,out var c)&&c!=null&&c.Seed==_runSeed)
            {
                CurrentTick=c.Tick;_rng.Restore(c.Rng0,c.Rng1);PlayerXmm=c.X;PlayerZmm=c.Z;PrevPlayerXmm=c.PreviousX;PrevPlayerZmm=c.PreviousZ;
                _directionXbp=c.DirectionX;_directionZbp=c.DirectionZ;PlayerHealth=c.Health;Resource=c.Resource;Array.Copy(c.Cooldowns,Cooldowns,4);
                foreach(var npc in Npcs)
                {
                    var n=c.People[npc.Id];npc.Xmm=n[0];npc.Zmm=n[1];npc.PreviousXmm=n[2];npc.PreviousZmm=n[3];npc.AttackCooldownTicks=(int)n[4];npc.InteractionCooldownTicks=(int)n[5];npc.PatrolIndex=(int)n[6];npc.Behavior=(NpcBehavior)n[7];
                }
            }
            forge.Capture=CaptureForgeVillage;
            BindCompanion();
            BindForgePopulation();
        }
        private string CaptureForgeVillage()
        {
            if(Progression.EntryAct!=2)return "-";
            var c=new ForgeVillageCheckpoint{Tick=CurrentTick,Seed=_runSeed,Rng0=_rng.S0,Rng1=_rng.S1,X=PlayerXmm,Z=PlayerZmm,PreviousX=PrevPlayerXmm,PreviousZ=PrevPlayerZmm,DirectionX=_directionXbp,DirectionZ=_directionZbp,Health=PlayerHealth,Resource=Resource};
            Array.Copy(Cooldowns,c.Cooldowns,4);
            foreach(var npc in Npcs)c.People[npc.Id]=new[]{npc.Xmm,npc.Zmm,npc.PreviousXmm,npc.PreviousZmm,npc.AttackCooldownTicks,npc.InteractionCooldownTicks,npc.PatrolIndex,(long)npc.Behavior,npc.Id};
            return c.Token();
        }
    }
}
