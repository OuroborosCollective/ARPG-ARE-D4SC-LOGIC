using System;
using System.Collections.Generic;
using System.Linq;
using AreArpg.Core;

internal static class SchwarzLootTests
{
    public static void Run(Action<bool,string> check)
    {
        var ids = new HashSet<string>();
        int bossCount=0, regularCount=0, regularDrops=0;
        for (ulong seed = 1; seed <= 2500; seed++)
        {
            var member = new CryptDungeonLayout(seed, 3).Monster(0);
            var ordinary = LootCatalog.Version4.GenerateFlagged(seed, 3, 50, member.Id, member.Kind, seed, LootCatalog.FinalCryptFlag);
            var boss = LootCatalog.Version4.GenerateFlagged(seed,3,50,42,2,seed,LootCatalog.FinalCryptFlag | LootCatalog.BossFlag);
            foreach (var receipt in new[]{ordinary,boss})
            {
                if (receipt == null) continue;
                bool isBoss = ReferenceEquals(receipt,boss);
                if (!isBoss) regularDrops++;
                if (!LootItem.TryRead(receipt.Token(),out var copy)) throw new Exception("Broken final crypt receipt");
                var item=new WeaponItem(receipt);
                if (!item.Definition.IsSchwarzArmory) continue;
                if (new WeaponItem(copy).Definition.Id != item.Definition.Id || new WeaponItem(copy).AffixDescription != item.AffixDescription || item.Value<=0)
                    throw new Exception("Royal replay mismatch");
                ids.Add(item.Definition.Id);
                if (isBoss) bossCount++; else regularCount++;
            }
            var outside=LootCatalog.Version4.Generate(seed,3,50,member.Id,member.Kind,seed);
            var previous=LootCatalog.Version3.Generate(seed,3,50,member.Id,member.Kind,seed);
            if (outside?.Token()!=previous?.Token()) throw new Exception("Equivalent depth outside final crypt changed");
        }
        check(ids.Count==21 && bossCount>720 && bossCount<950 && regularCount>regularDrops*.08 && regularCount<regularDrops*.18,"schwarz-tables-and-all-21-models");
        Console.WriteLine("SCHWARZ_SAMPLE regular="+regularCount+"/"+regularDrops+" boss="+bossCount+"/2500 models="+ids.Count);
        check(!LootCatalog.ValidFinalCryptKill(1,2,42,2,6) && !LootCatalog.ValidFinalCryptKill(1,3,1,0,4) &&
            !LootCatalog.ValidFinalCryptKill(1,3,42,2,4), "schwarz-rejects-wrong-floor-source-and-boss-flag");
        var world=CryptDungeonTests.Enter().Progression;
        var game=new ArenaSimulation(123,world,1,LootCatalog.Version4);
        bool terminal=false;
        for (int depth=1;depth<=3;depth++)
        {
            CryptDungeonTests.ClearFloor(game);
            string save=world.SaveString();
            if (!CharacterProgression.TryLoad(save,out var loaded) || loaded.SaveString()!=save) throw new Exception("Crypt save floor "+depth);
            if (depth<3 && world.Items.Any(item=>item.CatalogRevision==4)) throw new Exception("Early royal eligibility");
            if (depth==2 && !world.Rattler.Active)
            {
                CryptDungeonTests.WalkFight(game,0,15000);
                if (!game.ActOnHunger(HungerAction.InspectCryptTrace).Accepted || !game.ActOnHunger(HungerAction.OpenInnerDoor).Accepted)
                    throw new Exception("Closed lower floor");
            }
            if (depth<3)
            {
                foreach (var supply in world.Supplies.Ground.ToArray())
                {
                    CryptDungeonTests.WalkFight(game, supply.Xmm, supply.Zmm);
                    game.TryCollectLoot();
                }
                CryptDungeonTests.WalkFight(game,0,24000);
                var next = game.TryTraversePassage();
                if (next == null) throw new Exception("Stair refused: depth=" + depth + " forward=" + game.DungeonForwardOpen + " ground=" + game.LootOnGround + " save=" + CharacterProgression.TryLoad(world.SaveString(), out _));
                game=next; world=game.Progression;
            }
            else terminal=world.Items.Any(i=>i.CatalogRevision==4) && world.Rattler.Creature.IsDead;
        }
        check(terminal,"schwarz-real-stairs-level-three-kill-collection-save");
    }
}
