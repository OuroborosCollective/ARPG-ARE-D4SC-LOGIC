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
            new WeaponDefinition("shield_newbie.1","Wachschild 1","SK_Shield_Newbie_01",WeaponFamily.Shield,0,3,3100,false),
            new WeaponDefinition("shield_newbie.2","Wachschild 2","SK_Shield_Newbie_02",WeaponFamily.Shield,6,3,3100,false),
            new WeaponDefinition("shield_newbie.3","Wachschild 3","SK_Shield_Newbie_03",WeaponFamily.Shield,12,3,3100,false),
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
        public static int Count => Definitions.Length;
        public static WeaponDefinition Get(int index) => index>=0&&index<Definitions.Length?Definitions[index]:throw new ArgumentOutOfRangeException(nameof(index));
    }
}
