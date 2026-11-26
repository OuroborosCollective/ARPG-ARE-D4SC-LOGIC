using AreArpg.Core;
using UnityEngine;
using Fit = AreArpg.EditorTools.AschenwachtGearBuilder.HeadFit;

namespace AreArpg.EditorTools
{
    /// <summary>
    /// Owner request 2026-11-26: the Nachtvesper harness from the owner's archive becomes rare loot, fitted to the hero. Its
    /// private pictures come from the tablet version of the modular model (Tools/Art/aschenwacht/convert.py, about 28,000
    /// triangles from 69,412) and the archive's seven tileable surfaces, the faceted garnet and the matte visor insert; the
    /// eighteen meshes are grouped into the eight regional pieces (<see cref="ArchiveSetBuilder"/>).
    /// </summary>
    public static class NachtvesperBuilder
    {
        public const string Root = "Assets/PrivateArt/Nachtvesper/";

        public static readonly ArchiveSetBuilder.Spec Spec = new ArchiveSetBuilder.Spec
        {
            Label = "Nachtvesper",
            Root = Root,
            Model = "Nachtvesper.fbx",
            Textured = new[] { "Eisen", "Geflecht", "Leder", "Messing", "Samt", "Schwarzstahl", "Silber" },
            Plain = new[] { ("Granat", new Color(.19f, .009f, .019f), .35f, .18f), ("Schatten", new Color(.008f, .006f, .009f), .02f, .85f) },
            Pieces = new[]
            {
                (nameof(RegionalRelic.N_Schaller), new[] { "04_Schaller" }, Fit.Closed),
                (nameof(RegionalRelic.N_Kuerass), new[] { "01_Hauberk", "02_Kuerass", "03_Ringkragen" }, Fit.None),
                (nameof(RegionalRelic.N_Schulterstuecke), new[] { "08_Schulter.L", "08_Schulter.R" }, Fit.None),
                (nameof(RegionalRelic.N_Armschienen), new[] { "07_Armschutz.L", "07_Armschutz.R" }, Fit.None),
                (nameof(RegionalRelic.N_Handschuhe), new[] { "09_Handschuh.L", "09_Handschuh.R" }, Fit.None),
                (nameof(RegionalRelic.N_Guertung), new[] { "05_Guertung", "06_Wappenbahnen", "11_Hueftschurz.L", "11_Hueftschurz.R" }, Fit.None),
                (nameof(RegionalRelic.N_Beinzeug), new[] { "10_Beinzeug.L", "10_Beinzeug.R" }, Fit.None),
                (nameof(RegionalRelic.N_Panzerschuhe), new[] { "12_Panzerschuh.L", "12_Panzerschuh.R" }, Fit.None)
            }
        };

        public static bool TryBuild() { return ArchiveSetBuilder.Build(Spec); }
    }
}
