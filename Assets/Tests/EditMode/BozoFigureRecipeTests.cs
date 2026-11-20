using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace AreArpg.EditMode.Tests
{
    /// <summary>
    /// Owner rule 2026-11-19: every NPC, merchant and human figure comes from one character system (BoZo pack).
    /// The recipes are plain data and are checked without the purchased pack.
    /// </summary>
    public sealed class BozoFigureRecipeTests
    {
        const string RecipePath = "Assets/Editor/BozoCharacters/figures.json";

        static MethodInfo Parse()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType("AreArpg.EditorTools.BozoFigureRecipes");
                if (type != null) return type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static);
            }
            Assert.Fail("BozoFigureRecipes is missing");
            return null;
        }

        static string Defect(string json)
        {
            try { Parse().Invoke(null, new object[] { json }); return null; }
            catch (TargetInvocationException e) when (e.InnerException is FormatException) { return e.InnerException.Message; }
        }

        [Test]
        public void OutfitColoursAllowOnlyTrailingEmptyEntries()
        {
            Assert.IsNull(Defect(Probe.Replace("[\"#6f6c5c\"]", "[\"#6f6c5c\",\"\",\"\"]")));
            StringAssert.Contains("empty", Rejection(Probe.Replace("[\"#6f6c5c\"]", "[\"\",\"#6f6c5c\"]")));
        }

        [Test]
        public void EveryHumanFigureOfTheGameHasARoleRecipe()
        {
            string json = File.ReadAllText(RecipePath);
            Assert.IsNull(Defect(json), "figures.json must be valid");
            var figures = (Array)Parse().Invoke(null, new object[] { json });
            Assert.GreaterOrEqual(figures.Length, 8);
            foreach (string key in new[] { "\"EntryNpc0\"", "\"EntryNpc1\"", "\"EntryNpc2\"", "\"RoadHunter\"", "\"HeroSource\"", "\"PatientSource0\"" })
                StringAssert.Contains(key, json);
            foreach (object figure in figures)
            {
                string role = (string)figure.GetType().GetField("role").GetValue(figure);
                Assert.IsFalse(string.IsNullOrWhiteSpace(role), "each figure is designed for a role");
            }
        }

        /// <summary>A minimal valid recipe; the checks below must not depend on the wording of the real figures.</summary>
        const string Probe = "{\"schema\":\"ashen.bozo-figures.v1\",\"figures\":[{\"id\":\"probe\",\"role\":\"Probe\",\"base\":\"Fantasy/Hero\","
            + "\"height\":1.9,\"texture\":1024,\"motion\":\"m\","
            + "\"keys\":[\"EntryNpc0\",\"EntryNpc1\",\"EntryNpc2\",\"Npc0\",\"Npc1\",\"Npc2\",\"RoadHunter\",\"Hero\"],"
            + "\"sources\":[\"HeroSource\",\"PatientSource0\",\"PatientSource1\",\"PatientSource2\"],"
            + "\"outfits\":[{\"slot\":\"Top\",\"id\":\"Top_RusticTunic\",\"colors\":[\"#6f6c5c\"]}]}]}";

        [Test]
        public void BrokenRecipesAreRejected()
        {
            Assert.IsNull(Defect(Probe), "The probe itself must be a valid recipe");
            StringAssert.Contains("schema", Defect(Probe.Replace("ashen.bozo-figures.v1", "other")));
            StringAssert.Contains("produced twice", Defect(Probe.Replace("\"RoadHunter\"", "\"EntryNpc0\"")));
            StringAssert.Contains("required figure RoadHunter", Defect(Probe.Replace("\"RoadHunter\"", "\"OtherHunter\"")));
            StringAssert.Contains("required figure Hero", Defect(Probe.Replace("\"Hero\"]", "\"OtherHero\"]")));
            StringAssert.Contains("required source figure HeroSource", Defect(Probe.Replace("\"HeroSource\"", "\"OtherSource\"")));
            StringAssert.Contains("#rrggbb", Defect(Probe.Replace("#6f6c5c", "grau")));
            StringAssert.Contains("at most nine", Defect(Probe.Replace("[\"#6f6c5c\"]",
                "[\"#000000\",\"#000000\",\"#000000\",\"#000000\",\"#000000\",\"#000000\",\"#000000\",\"#000000\",\"#000000\",\"#000000\"]")));
            StringAssert.Contains("height", Defect(Probe.Replace("\"height\":1.9", "\"height\":9.5")));
            StringAssert.Contains("texture", Defect(Probe.Replace("\"texture\":1024", "\"texture\":3000")));
            StringAssert.Contains("role", Defect(Probe.Replace("\"role\":\"Probe\"", "\"role\":\"\"")));
            StringAssert.Contains("visual key", Defect(Probe.Replace("\"Hero\"]", "\"../x\"]")));
            StringAssert.Contains("figure id", Defect(Probe.Replace("\"id\":\"probe\"", "\"id\":\"../probe\"")));
            StringAssert.Contains("reserved", Defect(Probe.Replace("\"id\":\"probe\"", "\"id\":\"sources\"")));
            StringAssert.Contains("base preset", Defect(Probe.Replace("Fantasy/Hero", "../../Secrets")));
            StringAssert.Contains("outfit entry", Defect(Probe.Replace("Top_RusticTunic", "../Top")));
            StringAssert.Contains("light", Defect(Probe.Replace("\"figures\"", "\"light\":0.1,\"figures\"")));
        }

        /// <summary>
        /// Values no comparison catches: "not a number" passes every smaller/greater check and would scale a figure
        /// or its colour atlas into garbage; a negative brightening is a typo, not "use the default".
        /// </summary>
        [Test]
        public void NumbersThatAreNotNumbersAreRejected()
        {
            StringAssert.Contains("height", Rejection(Probe.Replace("\"height\":1.9", "\"height\":NaN")));
            StringAssert.Contains("light", Rejection(Probe.Replace("\"figures\"", "\"light\":NaN,\"figures\"")));
            StringAssert.Contains("light", Rejection(Probe.Replace("\"figures\"", "\"light\":-0.6,\"figures\"")));
        }

        static string Rejection(string json)
        {
            string defect = Defect(json);
            Assert.IsNotNull(defect, "The recipe was accepted");
            return defect;
        }

        /// <summary>
        /// The playable hero and the resting mill patients are built by their own tools from a source figure (weapon
        /// socket, combat controller, lying pose). A recipe that names one of those visuals as its key would replace
        /// it with a plain walking figure.
        /// </summary>
        [Test]
        public void ARecipeCannotClaimAVisualBuiltByAnotherTool()
        {
            foreach (string key in new[] { "EntryCombatHero", "EntryHero", "MillPatient0", "MillPatient2" })
                StringAssert.Contains("built by another tool", Rejection(Probe.Replace("\"Hero\"]", "\"Hero\",\"" + key + "\"]")), key);
        }
    }
}
