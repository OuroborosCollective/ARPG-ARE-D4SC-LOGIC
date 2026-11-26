using AreArpg.Core;
using UnityEngine;
using Fit = AreArpg.EditorTools.AschenwachtGearBuilder.HeadFit;

namespace AreArpg.EditorTools
{
    /// <summary>
    /// Owner request 2026-11-26: the hunter's leather set Rotfährte and the sorcerer's linen set Rußkantor from the owner's
    /// archives, fitted to the hero (<see cref="ArchiveSetBuilder"/>). Both models were reduced for the tablet with
    /// Tools/Art/aschenwacht/convert.py (55,144 and 51,114 triangles to about 26,000 each, the weapons kept as they are).
    /// Their hoods are open at the face. The hunter's back quiver with its arrows is a fitted picture of its own,
    /// <see cref="Quiver"/>, worn while the longbow is in hand.
    /// </summary>
    public static class HunterCantorBuilder
    {
        public const string Quiver = "F_Koecher";

        public static readonly ArchiveSetBuilder.Spec Rotfaehrte = new ArchiveSetBuilder.Spec
        {
            Label = "Rotfaehrte",
            LayOver = true,
            Root = "Assets/PrivateArt/Rotfaehrte/",
            Model = "Rotfaehrte.fbx",
            Textured = new[] { "Feder", "Filz", "Holz", "Krapp", "Messing", "Moorleder", "Rauchleder", "Riemen", "Schiefer" },
            Plain = new[] { ("Abrieb", new Color(.26f, .19f, .135f), 0f, .83f), ("Schatten", new Color(.018f, .018f, .021f), 0f, .83f) },
            Glowing = new[] { ("Glut", 2.65f, .48f) },
            Pieces = new[]
            {
                (nameof(RegionalRelic.F_Gugel), new[] { "03_Faehrtengugel", "04_Halstuch" }, Fit.Open),
                (nameof(RegionalRelic.F_Wams), new[] { "01_Wams", "13_Wamsnaehte", "12_Kreuzriemen" }, Fit.None),
                (nameof(RegionalRelic.F_Wetterkragen), new[] { "05_Wetterkragen", "14_Laubmantel" }, Fit.None),
                (nameof(RegionalRelic.F_Aermel), new[] { "08_Aermel.L", "08_Aermel.R" }, Fit.None),
                (nameof(RegionalRelic.F_Handschuhe), new[] { "09_Handschuh.L", "09_Handschuh.R" }, Fit.None),
                (nameof(RegionalRelic.F_Koppel), new[] { "10_Feldkoppel", "11_Vorratstaschen", "02_Jagdschoesse" }, Fit.None),
                (nameof(RegionalRelic.F_Beinkleid), new[] { "06_Beinkleid.L", "06_Beinkleid.R" }, Fit.None),
                (nameof(RegionalRelic.F_Stiefel), new[] { "07_Stiefel.L", "07_Stiefel.R" }, Fit.None),
                (Quiver, new[] { "Rueckenkoecher", "Koecherfuellung" }, Fit.None)
            },
            Unused = new[] { "Schwarzschiefer_Langbogen", "Glutspitzenpfeil" },
            Weapons = new[] { (RegionalRelic.F_Langbogen, "Schwarzschiefer_Langbogen.fbx") }
        };

        public static readonly ArchiveSetBuilder.Spec Russkantor = new ArchiveSetBuilder.Spec
        {
            Label = "Russkantor",
            LayOver = true,
            Root = "Assets/PrivateArt/Russkantor/",
            Model = "Russkantor.fbx",
            Textured = new[] { "Borte", "Dunkelbuche", "Flachs", "Kohleinen", "Krapp", "Messing", "Riemen", "Schieferholz" },
            Plain = new[] { ("Abrieb", new Color(.26f, .19f, .135f), 0f, .83f), ("Schatten", new Color(.018f, .018f, .021f), 0f, .83f) },
            Glowing = new[] { ("Glut", 2.65f, .48f) },
            Pieces = new[]
            {
                (nameof(RegionalRelic.K_Kapuze), new[] { "03_Kantorenkapuze", "04_Wickelkragen" }, Fit.Open),
                (nameof(RegionalRelic.K_Kittel), new[] { "01_Leinenkittel", "12_Kantorenstola", "13_Siegelkordel" }, Fit.None),
                (nameof(RegionalRelic.K_Pelerine), new[] { "14_Aschenpelerine", "15_Rueckensiegel" }, Fit.None),
                (nameof(RegionalRelic.K_Aermel), new[] { "08_Aermel.L", "08_Aermel.R" }, Fit.None),
                (nameof(RegionalRelic.K_Handschuhe), new[] { "09_Handschuh.L", "09_Handschuh.R" }, Fit.None),
                (nameof(RegionalRelic.K_Gurt), new[] { "10_Knotengurt", "11_Vorratstaschen", "02_Leinenrock" }, Fit.None),
                (nameof(RegionalRelic.K_Beinkleid), new[] { "06_Beinkleid.L", "06_Beinkleid.R" }, Fit.None),
                (nameof(RegionalRelic.K_Stiefel), new[] { "07_Stiefel.L", "07_Stiefel.R" }, Fit.None)
            },
            Unused = new[] { "QuirlDunkelbuchen_Langstab", "Schwarzschieferholz_Kurzstab" },
            Weapons = new[] { (RegionalRelic.K_Kurzstab, "Schwarzschieferholz_Kurzstab.fbx"), (RegionalRelic.K_Langstab, "QuirlDunkelbuchen_Langstab.fbx") }
        };

        public static bool TryBuild() { return ArchiveSetBuilder.Build(Rotfaehrte) & ArchiveSetBuilder.Build(Russkantor); }
    }
}
