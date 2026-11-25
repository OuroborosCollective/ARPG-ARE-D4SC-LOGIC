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

        /// <summary>
        /// A second colour of a figure shares the mesh of its model figure. That is only right when both are the same
        /// shape, so a recipe may name a model only if it matches it in everything but colours.
        /// </summary>
        [Test]
        public void AColourVariantMustMatchItsModelInEverythingButColour()
        {
            const string tail = "]}";
            Assert.IsTrue(Probe.EndsWith(tail));
            Func<string, string> with = second => Probe.Substring(0, Probe.Length - tail.Length) + "," + second + tail;
            const string twin = "{\"id\":\"probe_b\",\"role\":\"Probe, zweite Farbe\",\"base\":\"Fantasy/Hero\",\"height\":1.8,\"texture\":512,\"motion\":\"m\","
                + "\"keys\":[\"ProbeB\"],\"sameShapeAs\":\"probe\",\"outfits\":[{\"slot\":\"Top\",\"id\":\"Top_RusticTunic\",\"colors\":[\"#445566\"]}]}";
            Assert.IsNull(Defect(with(twin)), "Another colour, another height and another name are a variant");
            StringAssert.Contains("names no other figure", Defect(with(twin.Replace("\"sameShapeAs\":\"probe\"", "\"sameShapeAs\":\"nobody\""))));
            StringAssert.Contains("names no other figure", Defect(with(twin.Replace("\"sameShapeAs\":\"probe\"", "\"sameShapeAs\":\"probe_b\""))));
            StringAssert.Contains("(outfits)", Defect(with(twin.Replace("Top_RusticTunic", "Top_VillagerTunic"))));
            StringAssert.Contains("(outfits)", Defect(with(twin.Replace("\"outfits\":[", "\"outfits\":[{\"slot\":\"Hat\",\"id\":\"Hat_RusticHood\",\"colors\":[]},"))));
            StringAssert.Contains("(body)", Defect(with(twin.Replace("\"keys\":[\"ProbeB\"]", "\"keys\":[\"ProbeB\"],\"body\":[{\"id\":\"Muscle\",\"value\":40}]"))));
            StringAssert.Contains("(face)", Defect(with(twin.Replace("\"keys\":[\"ProbeB\"]", "\"keys\":[\"ProbeB\"],\"face\":[{\"id\":\"Stern\",\"value\":10}]"))));
            StringAssert.Contains("(remove)", Defect(with(twin.Replace("\"keys\":[\"ProbeB\"]", "\"keys\":[\"ProbeB\"],\"remove\":[\"Neck\"]"))));
            StringAssert.Contains("(base)", Defect(with(twin.Replace("Fantasy/Hero", "Fantasy/Ranger"))));
            StringAssert.Contains("(parts)", Defect(with(twin.Replace("\"sameShapeAs\":\"probe\"", "\"sameShapeAs\":\"probe\",\"parts\":true"))));
            // No chains: a variant names a figure that has a mesh of its own.
            string third = twin.Replace("probe_b", "probe_c").Replace("ProbeB", "ProbeC").Replace("\"sameShapeAs\":\"probe\"", "\"sameShapeAs\":\"probe_b\"");
            StringAssert.Contains("mesh of its own", Defect(with(twin + "," + third)));
        }

        /// <summary>A figure built in two parts sorts every outfit type of the pack to the head or the body, and no type to both.</summary>
        [Test]
        public void EveryOutfitTypeBelongsToTheHeadOrTheBody()
        {
            Type recipes = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) recipes = recipes ?? assembly.GetType("AreArpg.EditorTools.BozoFigureRecipes");
            MethodInfo isHead = recipes.GetMethod("IsHeadSlot", BindingFlags.Public | BindingFlags.Static);
            Func<string, bool?> side = type => (bool?)isHead.Invoke(null, new object[] { type });
            foreach (string head in new[] { "Head", "HairFront", "HairBack", "Hat", "Eyes", "EyeBrows", "LowerFace", "UpperFace", "Ears", "Teeth" })
                Assert.AreEqual(true, side(head), head + " belongs to the head");
            foreach (string body in new[] { "Body", "Top", "Bottom", "Feet", "Gloves", "Shoulders", "Neck", "Leggings", "UnderUpper", "UnderLower" })
                Assert.AreEqual(false, side(body), body + " belongs to the body");
            Assert.IsNull(side("SomethingNew"), "A type nobody sorted is not guessed");
            // Every type the installed pack defines is sorted.
            string folder = "Assets/BoZo_StylizedModularCharacters/OutfitTypes";
            if (!System.IO.Directory.Exists(folder)) return;
            foreach (string file in System.IO.Directory.GetFiles(folder, "*.asset"))
            {
                string type = System.IO.Path.GetFileNameWithoutExtension(file);
                Assert.IsNotNull(side(type), "The pack's outfit type " + type + " is sorted to the head or the body");
            }
        }

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
        /// A figure may carry things (staff, lantern, pouches, jewellery): each names a model inside the project, one of
        /// the trunk or limb bones and a place on the standing figure. Anything else would hang art from nowhere.
        /// </summary>
        [Test]
        public void WhatAFigureCarriesNamesAModelABoneAndAPlace()
        {
            const string staff = "{\"name\":\"Staff\",\"asset\":\"Assets/PrivateArt/Props/Staff 01.prefab\",\"bone\":\"Chest\","
                + "\"position\":[0,1.1,-0.2],\"rotation\":[0,0,24],\"size\":1.5,\"anchor\":\"center\",\"tint\":\"#c9b79f\"}";
            string carrying = Probe.Replace("\"outfits\":", "\"props\":[" + staff + "],\"outfits\":");
            Assert.IsNull(Defect(carrying), "A complete prop is accepted");
            Assert.IsNull(Defect(carrying.Replace("\"rotation\":[0,0,24],", "")), "The turn is optional");
            StringAssert.Contains("unknown bone", Rejection(carrying.Replace("\"Chest\"", "\"LeftIndexDistal\"")));
            StringAssert.Contains("model path", Rejection(carrying.Replace("Assets/PrivateArt/Props/Staff 01.prefab", "Assets/../../etc/Staff.prefab")));
            StringAssert.Contains("model path", Rejection(carrying.Replace("Staff 01.prefab", "Staff 01.cs")));
            StringAssert.Contains("position", Rejection(carrying.Replace("[0,1.1,-0.2]", "[0,1.1]")));
            StringAssert.Contains("position", Rejection(carrying.Replace("[0,1.1,-0.2]", "[0,11,0]")));
            StringAssert.Contains("rotation", Rejection(carrying.Replace("[0,0,24]", "[0,24]")));
            StringAssert.Contains("size", Rejection(carrying.Replace("\"size\":1.5", "\"size\":9")));
            StringAssert.Contains("anchor", Rejection(carrying.Replace("\"center\"", "\"left\"")));
            StringAssert.Contains("tint", Rejection(carrying.Replace("#c9b79f", "braun")));
            StringAssert.Contains("glow", Rejection(carrying.Replace("\"tint\":\"#c9b79f\"", "\"glow\":\"hell\"")));
            StringAssert.Contains("listed twice", Rejection(carrying.Replace("[" + staff + "]", "[" + staff + "," + staff + "]")));
            StringAssert.Contains("prop name", Rejection(carrying.Replace("\"name\":\"Staff\"", "\"name\":\"../Staff\"")));
            StringAssert.Contains("reserved", Rejection(Probe.Replace("\"id\":\"probe\"", "\"id\":\"props\"")), "The props share a folder beside the figures");
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
