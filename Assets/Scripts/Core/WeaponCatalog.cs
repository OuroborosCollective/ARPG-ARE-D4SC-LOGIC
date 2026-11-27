using System;
namespace AreArpg.Core
{
    /// <summary>Equipment catalog revision 1: one stable definition per reviewed private model. Freeze order and values.</summary>
    public static class WeaponCatalog
    {
        public const int Revision=1;
        private static readonly WeaponDefinition[] Definitions = {
            new WeaponDefinition("axe_1h_newbie.1","Waldaxt 1","SK_Axe_1H_Newbie_01",WeaponFamily.Axe,148,4,3000,false),
            new WeaponDefinition("axe_1h_newbie.2","Waldaxt 2","SK_Axe_1H_Newbie_02",WeaponFamily.Axe,154,4,3000,false),
            new WeaponDefinition("axe_1h_newbie.3","Waldaxt 3","SK_Axe_1H_Newbie_03",WeaponFamily.Axe,160,4,3000,false),
            new WeaponDefinition("axe_2hl_newbie.1","Spaltaxt 1","SK_Axe_2HL_Newbie_01",WeaponFamily.Axe,195,5,3800,true),
            new WeaponDefinition("bow_newbie.1","Jägerbogen 1","SK_Bow_Newbie_01",WeaponFamily.Bow,200,6,12000,true),
            new WeaponDefinition("bow_newbie.2","Jägerbogen 2","SK_Bow_Newbie_02",WeaponFamily.Bow,206,6,12000,true),
            new WeaponDefinition("bow_newbie.3","Jägerbogen 3","SK_Bow_Newbie_03",WeaponFamily.Bow,212,6,12000,true),
            new WeaponDefinition("dagger_1h_newbie.1","Waldläuferdolch 1","SK_Dagger_1H_Newbie_01",WeaponFamily.Dagger,80,2,2300,false),
            new WeaponDefinition("dagger_1h_newbie.2","Waldläuferdolch 2","SK_Dagger_1H_Newbie_02",WeaponFamily.Dagger,86,2,2300,false),
            new WeaponDefinition("dagger_1h_newbie.3","Waldläuferdolch 3","SK_Dagger_1H_Newbie_03",WeaponFamily.Dagger,92,2,2300,false),
            new WeaponDefinition("shield_newbie.1","Wachschild 1","SK_Shield_Newbie_01",WeaponFamily.Shield,600,3,3100,false),
            new WeaponDefinition("shield_newbie.2","Wachschild 2","SK_Shield_Newbie_02",WeaponFamily.Shield,700,3,3100,false),
            new WeaponDefinition("shield_newbie.3","Wachschild 3","SK_Shield_Newbie_03",WeaponFamily.Shield,800,3,3100,false),
            new WeaponDefinition("staff.air","Windstab","Air staff",WeaponFamily.Staff,235,7,9000,true),
            new WeaponDefinition("staff.earth","Erdenstab","Earth staff",WeaponFamily.Staff,240,7,9000,true),
            new WeaponDefinition("staff.fire","Glutstab","Fire staff",WeaponFamily.Staff,245,7,9000,true),
            new WeaponDefinition("staff.ice","Froststab","Ice staff",WeaponFamily.Staff,250,7,9000,true),
            new WeaponDefinition("staff.necromancy","Totenstab","Necromancy staff",WeaponFamily.Staff,255,7,9000,true),
            new WeaponDefinition("staff_newbie.1","Wanderstab 1","SK_Staff_Newbie_01",WeaponFamily.Staff,220,7,8500,true),
            new WeaponDefinition("staff_newbie.2","Wanderstab 2","SK_Staff_Newbie_02",WeaponFamily.Staff,226,7,8500,true),
            new WeaponDefinition("staff_newbie.3","Wanderstab 3","SK_Staff_Newbie_03",WeaponFamily.Staff,232,7,8500,true),
            new WeaponDefinition("staff_newbie.4","Wanderstab 4","SK_Staff_Newbie_04",WeaponFamily.Staff,238,7,8500,true),
            new WeaponDefinition("sword.artifact","Reliktklinge","ArtifactSword",WeaponFamily.Sword,100,3,3100,false),
            new WeaponDefinition("sword.bone","Knochenschwert","BoneSword",WeaponFamily.Sword,104,3,3100,false),
            new WeaponDefinition("sword.darkiron","Dunkeleisenklinge","DarkIronSword",WeaponFamily.Sword,108,3,3100,false),
            new WeaponDefinition("sword.dead","Grabesklinge","DeadSword",WeaponFamily.Sword,112,3,3100,false),
            new WeaponDefinition("sword.demonic","Dämonenklinge","DemonicSword",WeaponFamily.Sword,116,3,3100,false),
            new WeaponDefinition("sword.dragonic","Drachenklinge","DragonicSword",WeaponFamily.Sword,120,3,3100,false),
            new WeaponDefinition("sword.katsunesi","Geschwungene Klinge","KatsunesiSword",WeaponFamily.Sword,124,3,3100,false),
            new WeaponDefinition("sword.punk","Gezahnte Klinge","PunkSword",WeaponFamily.Sword,128,3,3100,false),
            new WeaponDefinition("sword.traveller","Wandererschwert","PT_Sword_01_a",WeaponFamily.Sword,102,3,3100,false),
            new WeaponDefinition("sword.wooden","Hölzernes Übungsschwert","WoodenSword",WeaponFamily.Sword,132,3,3100,false),
            new WeaponDefinition("sword_1h_newbie.1","Wachschwert 1","SK_Sword_1H_Newbie_01",WeaponFamily.Sword,112,3,3100,false),
            new WeaponDefinition("sword_1h_newbie.2","Wachschwert 2","SK_Sword_1H_Newbie_02",WeaponFamily.Sword,118,3,3100,false),
            new WeaponDefinition("sword_2h_newbie.1","Großschwert 1","SK_Sword_2H_Newbie_01",WeaponFamily.Sword,166,4,4100,true),
            new WeaponDefinition("sword_2h_newbie.2","Großschwert 2","SK_Sword_2H_Newbie_02",WeaponFamily.Sword,172,4,4100,true),
        };
        // Revision three appends this frozen pool. The original 36-entry catalog also serves legacy receipts.
        internal static readonly WeaponDefinition[] GlutDefinitions = {
            new WeaponDefinition("glut.01", "Witwenfunke", "glut.01", WeaponFamily.Dagger, 85, 2, 2300, false, visualFolder: "Glut/"),
            new WeaponDefinition("glut.02", "Eid im Ruß", "glut.02", WeaponFamily.Sword, 115, 3, 3100, false, visualFolder: "Glut/"),
            new WeaponDefinition("glut.03", "Chor der Asche", "glut.03", WeaponFamily.Sword, 165, 4, 3900, true, visualFolder: "Glut/"),
            new WeaponDefinition("glut.04", "Das letzte Ofentor", "glut.04", WeaponFamily.Sword, 179, 5, 4200, true, visualFolder: "Glut/"),
            new WeaponDefinition("glut.05", "Sichelmond von Nareth", "glut.05", WeaponFamily.Sword, 111, 3, 3000, false, visualFolder: "Glut/"),
            new WeaponDefinition("glut.06", "Schlackentide", "glut.06", WeaponFamily.Sword, 164, 4, 3900, true, visualFolder: "Glut/"),
            new WeaponDefinition("glut.07", "Roter Nachhall", "glut.07", WeaponFamily.Sword, 114, 3, 3150, false, visualFolder: "Glut/"),
            new WeaponDefinition("glut.08", "Vespernadel", "glut.08", WeaponFamily.Sword, 106, 3, 3350, false, visualFolder: "Glut/"),
            new WeaponDefinition("glut.09", "Wachtbeil", "glut.09", WeaponFamily.Axe, 153, 4, 3000, false, visualFolder: "Glut/"),
            new WeaponDefinition("glut.10", "Zwillingsesse", "glut.10", WeaponFamily.Axe, 196, 5, 3800, true, visualFolder: "Glut/"),
            new WeaponDefinition("glut.11", "Achtfaches Schweigen", "glut.11", WeaponFamily.Mace, 145, 4, 2900, false, visualFolder: "Glut/"),
            new WeaponDefinition("glut.12", "Schrein der Buße", "glut.12", WeaponFamily.Mace, 184, 5, 3650, true, visualFolder: "Glut/"),
            new WeaponDefinition("glut.15", "Kohlenlilie", "glut.15", WeaponFamily.Spear, 128, 4, 4100, true, visualFolder: "Glut/"),
            new WeaponDefinition("glut.16", "Standarte der Verlorenen", "glut.16", WeaponFamily.Spear, 142, 5, 4550, true, visualFolder: "Glut/"),
            new WeaponDefinition("glut.17", "Grenzvogt", "glut.17", WeaponFamily.Spear, 149, 5, 4250, true, visualFolder: "Glut/"),
            new WeaponDefinition("glut.20", "Witwenrippe", "glut.20", WeaponFamily.Bow, 203, 6, 12000, true, visualFolder: "Glut/"),
            new WeaponDefinition("glut.21", "Strebewerk", "glut.21", WeaponFamily.Bow, 218, 7, 12500, true, visualFolder: "Glut/"),
            new WeaponDefinition("glut.24", "Krone der Esse", "glut.24", WeaponFamily.Staff, 235, 7, 9000, true, visualFolder: "Glut/"),
            new WeaponDefinition("glut.28", "Glutauge", "glut.28", WeaponFamily.Shield, 600, 3, 3100, false, visualFolder: "Glut/"),
            new WeaponDefinition("glut.29", "Gebrochener Eid", "glut.29", WeaponFamily.Shield, 700, 3, 3100, false, visualFolder: "Glut/"),
            new WeaponDefinition("glut.30", "Tor der Heimkehr", "glut.30", WeaponFamily.Shield, 800, 3, 3100, false, visualFolder: "Glut/"),
        };
        private static readonly WeaponDefinition[] RoyalDefinitions = {
            new WeaponDefinition("schwarz.01", "Die erste Lüge", "schwarz.01", WeaponFamily.Dagger, 85, 2, 2300, false, visualFolder: "Schwarz/"),
            new WeaponDefinition("schwarz.02", "Des Königs Nachturteil", "schwarz.02", WeaponFamily.Sword, 115, 3, 3100, false, visualFolder: "Schwarz/"),
            new WeaponDefinition("schwarz.03", "Rabenvogt", "schwarz.03", WeaponFamily.Sword, 165, 4, 3900, true, visualFolder: "Schwarz/"),
            new WeaponDefinition("schwarz.04", "Die stille Bürde des Königs", "schwarz.04", WeaponFamily.Sword, 179, 5, 4200, true, visualFolder: "Schwarz/"),
            new WeaponDefinition("schwarz.05", "Mondsichel der Regentin", "schwarz.05", WeaponFamily.Sword, 111, 3, 3000, false, visualFolder: "Schwarz/"),
            new WeaponDefinition("schwarz.06", "Ernte des Schattenkönigs", "schwarz.06", WeaponFamily.Sword, 164, 4, 3900, true, visualFolder: "Schwarz/"),
            new WeaponDefinition("schwarz.07", "Schwarzschleier", "schwarz.07", WeaponFamily.Sword, 114, 3, 3150, false, visualFolder: "Schwarz/"),
            new WeaponDefinition("schwarz.08", "Nadel des stummen Hofs", "schwarz.08", WeaponFamily.Sword, 106, 3, 3350, false, visualFolder: "Schwarz/"),
            new WeaponDefinition("schwarz.09", "Mondpächter", "schwarz.09", WeaponFamily.Axe, 153, 4, 3000, false, visualFolder: "Schwarz/"),
            new WeaponDefinition("schwarz.10", "Die geteilte Krone", "schwarz.10", WeaponFamily.Axe, 196, 5, 3800, true, visualFolder: "Schwarz/"),
            new WeaponDefinition("schwarz.11", "Gefangener Stern", "schwarz.11", WeaponFamily.Mace, 145, 4, 2900, false, visualFolder: "Schwarz/"),
            new WeaponDefinition("schwarz.12", "Drei Siegel des Königs", "schwarz.12", WeaponFamily.Mace, 184, 5, 3650, true, visualFolder: "Schwarz/"),
            new WeaponDefinition("schwarz.15", "Die dritte Zinke", "schwarz.15", WeaponFamily.Spear, 128, 4, 4100, true, visualFolder: "Schwarz/"),
            new WeaponDefinition("schwarz.16", "Banner des leeren Throns", "schwarz.16", WeaponFamily.Spear, 142, 5, 4550, true, visualFolder: "Schwarz/"),
            new WeaponDefinition("schwarz.17", "Vogtrecht", "schwarz.17", WeaponFamily.Spear, 149, 5, 4250, true, visualFolder: "Schwarz/"),
            new WeaponDefinition("schwarz.20", "Bogen der Schleierwitwe", "schwarz.20", WeaponFamily.Bow, 203, 6, 12000, true, visualFolder: "Schwarz/"),
            new WeaponDefinition("schwarz.21", "Schwingen des Nachtkönigs", "schwarz.21", WeaponFamily.Bow, 218, 7, 12500, true, visualFolder: "Schwarz/"),
            new WeaponDefinition("schwarz.24", "Der verfinsterte Hof", "schwarz.24", WeaponFamily.Staff, 235, 7, 9000, true, visualFolder: "Schwarz/"),
            new WeaponDefinition("schwarz.28", "Rosette der Nacht", "schwarz.28", WeaponFamily.Shield, 600, 3, 3100, false, visualFolder: "Schwarz/"),
            new WeaponDefinition("schwarz.29", "Wappen des letzten Raben", "schwarz.29", WeaponFamily.Shield, 700, 3, 3100, false, visualFolder: "Schwarz/"),
            new WeaponDefinition("schwarz.30", "Schwarzschleiers Thronpforte", "schwarz.30", WeaponFamily.Shield, 800, 3, 3100, false, visualFolder: "Schwarz/"),
        };

        internal static WeaponDefinition RollFinalCrypt(LootItem receipt, DeterministicRng equipmentRng)
        {
            var sourceRng = new DeterministicRng(receipt.StableHash ^ 0x5357494CUL);
            uint denominator = (receipt.Flags & LootCatalog.BossFlag) != 0 ? 3u : 8u;
            return LootCatalog.Roll(sourceRng, denominator) == 0
                ? RoyalDefinitions[(int)LootCatalog.Roll(equipmentRng, (uint)RoyalDefinitions.Length)]
                : RollGlutCatalog(equipmentRng);
        }

        internal static WeaponDefinition RollGlutCatalog(DeterministicRng rng)
        {
            int index = (int)LootCatalog.Roll(rng, (uint)(Count + GlutDefinitions.Length));
            return index < Count ? Get(index) : GlutDefinitions[index - Count];
        }
        public static int Count => Definitions.Length;
        public static WeaponDefinition Get(int index) => index>=0&&index<Definitions.Length?Definitions[index]:throw new ArgumentOutOfRangeException(nameof(index));
    }
}
