using System;
using System.Collections.Generic;
using System.Linq;
using AreArpg.Core;

namespace AreArpg.PlayMode.Tests
{
    // Drives only public simulation inputs. Actor positions, HP, inventory and quest flags belong to the game.
    internal static class AshWorksJourney
    {
        private const int Cell = 700;
        private const int Move = 10000;
        private static readonly (int, int)[] Axes = { (0,1), (1,0), (0,-1), (-1,0) };

        public static void ClearEntry(ArenaSimulation game)
        {
            if (game.Progression.EntryAct != 1 || game.Monsters.Count == 0 ||
                game.Monsters.Any(m => m.IsDead || m.Health != m.MaxHealth))
                throw new InvalidOperationException("Expected the live entry encounter at full health.");
            for (int tick=0; tick<500 && !game.GameOver && !game.CryptCleared; tick++)
            {
                var m=game.Monsters.Where(e=>!e.IsDead).OrderBy(e=>Range2(game,e.Xmm,e.Zmm)).FirstOrDefault();
                if (m==null) break;
                long dx=m.Xmm-game.PlayerXmm, dz=m.Zmm-game.PlayerZmm;
                game.SetMoveInput((int)dx,(int)dz);
                if (game.PlayerHealth<game.MaxPlayerHealth*2/3) game.ActivateSkill(2);
                game.ActivateSkill(3);game.ActivateSkill(1);game.ActivateSkill(0);
                game.Step();
            }
            game.SetMoveInput(0,0);
            if (game.GameOver || !game.CryptCleared)
                throw new InvalidOperationException("The player did not defeat the live entry encounter.");
            while (game.GroundLoot.Count!=0)
            {
                var item=game.GroundLoot.OrderBy(e=>Range2(game,e.Xmm,e.Zmm)).First();
                Walk(game,item.Xmm,item.Zmm,750);
                if (!game.TryCollectLoot()) throw new InvalidOperationException("Entry pickup failed.");
            }
        }

        private static long Range2(ArenaSimulation game,long x,long z)
        {
            long dx=x-game.PlayerXmm,dz=z-game.PlayerZmm;return dx*dx+dz*dz;
        }

        // Actors may move while approached. Follow the actor and use the same obstacle map as real input.
        public static void Meet(ArenaSimulation game,int id)
        {
            var npc=game.Npcs.Single(n=>n.Id==id);
            for (int i=0;i<18;i++)
            {
                if(Range2(game,npc.Xmm,npc.Zmm)<=1600L*1600 &&
                    EntryWorldLayout.HasSight(game.PlayerXmm,game.PlayerZmm,npc.Xmm,npc.Zmm)&&npc.InteractionCooldownTicks==0)
                    return;
                if(npc.InteractionCooldownTicks!=0 && Range2(game,npc.Xmm,npc.Zmm)<1500L*1500)
                { game.SetMoveInput(0,0);game.Step(); }
                else Walk(game,npc.Xmm,npc.Zmm,1200,true);
            }
            throw new InvalidOperationException("Could not reach NPC "+id+" at "+npc.Xmm+","+npc.Zmm+" from "+game.PlayerXmm+","+game.PlayerZmm);
        }

        public static void Walk(ArenaSimulation game,long x,long z,long radius=660,bool sight=false)
        {
            for (int i=0;i<235;i++)
            {
                if (game.GameOver) break;
                if (Range2(game,x,z)<=radius*radius && (!sight || EntryWorldLayout.HasSight(game.PlayerXmm,game.PlayerZmm,x,z)))
                { game.SetMoveInput(0,0);return; }
                var next=Next(game,x,z,radius,sight);
                game.SetMoveInput(next.Item1*Move,next.Item2*Move);game.Step();
            }
            throw new InvalidOperationException("Path failed in "+game.ChapterName+" to "+x+","+z+" from "+game.PlayerXmm+","+game.PlayerZmm+".");
        }

        private static (int,int) Next(ArenaSimulation game,long tx,long tz,long radius,bool sight)
        {
            var start=(0,0);var queue=new Queue<(int,int)>();queue.Enqueue(start);
            var first=new Dictionary<(int,int),(int,int)>();first.Add(start,start);
            while (queue.Count!=0)
            {
                var c=queue.Dequeue();
                long x=game.PlayerXmm+c.Item1*Cell,z=game.PlayerZmm+c.Item2*Cell;
                long dx=tx-x,dz=tz-z;
                if (c!=start && dx*dx+dz*dz<=radius*radius && (!sight || EntryWorldLayout.HasSight(x,z,tx,tz))) return first[c];
                foreach (var a in Axes)
                {
                    var child=(c.Item1+a.Item1,c.Item2+a.Item2);
                    if (first.ContainsKey(child)) continue;
                    long nx=x+a.Item1*Cell,nz=z+a.Item2*Cell;
                    if(Math.Abs(nx)>27000 || Math.Abs(nz)>27000 || !game.CanStand(nx,nz) ||
                       !game.CanStand((nx+x)/2,(nz+z)/2)) continue;
                    first.Add(child,c==start?a:first[c]);queue.Enqueue(child);
                }
            }
            throw new InvalidOperationException("Unreachable fixture input target "+tx+","+tz+" in "+game.ChapterName);
        }

        public static void DefeatTiefscharrer(ArenaSimulation game)
        {
            var m=game.Progression.Rattler.Creature;
            if(m.IsDead || game.GameOver) throw new InvalidOperationException("Expected the live Rattler.");
            for(int i=0;i<1500&&!game.GameOver&&!m.IsDead;i++)
            {
                if(m.BossWindupTicks>0)
                {
                    game.SetMoveInput(0,-Move);
                    if(game.CombatV2) game.StartDodge(0,-Move); // combat v2: roll back out of the announced strike
                }
                else
                {
                    long dx=m.Xmm-game.PlayerXmm,dz=m.Zmm-game.PlayerZmm;
                    game.SetMoveInput(Math.Abs(dx)>1100?Math.Sign(dx)*Move:0,Math.Abs(dz)>1700?Math.Sign(dz)*Move:0);
                    if(!game.CombatV2&&game.PlayerHealth<game.MaxPlayerHealth*2/3) game.ActivateSkill(2);
                    // Combat v2 commits to each swing: Ruin's long windup only when it ends before the next strike.
                    if(!game.CombatV2||m.AttackCooldownTicks>CombatV2Rules.Ruin.Startup) game.ActivateSkill(3);
                    game.ActivateSkill(1);game.ActivateSkill(0);
                }
                game.Step();
            }
            game.SetMoveInput(0,0);
            if(game.GameOver || !m.IsDead) throw new InvalidOperationException("The Rattler fight did not finish normally.");
        }
    }
}
