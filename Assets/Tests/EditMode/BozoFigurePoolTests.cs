using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace AreArpg.EditMode.Tests
{
    /// <summary>
    /// The figure pool (owner direction 2026-11-22): ready-made people for residents and roles, each kind with a few
    /// looks of one model. Plain data, checked without the purchased pack.
    /// </summary>
    public sealed class BozoFigurePoolTests
    {
        const string PoolPath = "Assets/Editor/BozoCharacters/pool.json";

        static Type Pool()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType("AreArpg.EditorTools.BozoFigurePool");
                if (type != null) return type;
            }
            Assert.Fail("BozoFigurePool is missing");
            return null;
        }

        static object Field(object o, string name) { return o.GetType().GetField(name).GetValue(o); }
        static string Text(object o, string name) { return (string)Field(o, name); }
        static string[] Texts(object o, string name) { return (string[])Field(o, name); }
        static object[] Items(object o, string name) { return ((Array)Field(o, name)).Cast<object>().ToArray(); }

        static object[] Expand(string json)
        {
            return ((Array)Pool().GetMethod("Expand").Invoke(null, new object[] { json })).Cast<object>().ToArray();
        }

        static string Defect(string json)
        {
            try { Expand(json); return null; }
            catch (TargetInvocationException e) when (e.InnerException is FormatException) { return e.InnerException.Message; }
        }

        // Checked out with Windows line ends, the text probes below still find their lines.
        static string Json { get { return File.ReadAllText(PoolPath).Replace("\r\n", "\n"); } }

        [Test]
        public void EachKindIsOneModelWithColourVariantsBuiltInTwoParts()
        {
            object[] figures = Expand(Json);
            Assert.GreaterOrEqual(figures.Length, 150, "the owner asked for about two hundred ready-made people");
            var models = figures.Where(f => Text(f, "sameShapeAs") == "").ToArray();
            Assert.GreaterOrEqual(models.Length, 20);
            foreach (object f in figures)
            {
                StringAssert.StartsWith("folk_", Text(f, "id"));
                Assert.IsTrue((bool)Field(f, "parts"), Text(f, "id") + " must be built in two parts so heads can be swapped");
                Assert.AreEqual(512, (int)Field(f, "texture"), Text(f, "id"));
                string model = Text(f, "sameShapeAs");
                if (model == "") continue;
                Assert.IsTrue(models.Any(m => Text(m, "id") == model), Text(f, "id") + " names no model");
                StringAssert.EndsWith("_0", model, "the first look of a kind is its model");
            }
            Assert.AreEqual(figures.Length, figures.Select(f => Texts(f, "keys").Single()).Distinct().Count(), "every look has its own visual");
        }

        [Test]
        public void EveryWornPartGetsAColourForEachOfItsChannelsAndThePresetKeepsNothingElse()
        {
            string[] wearable = { "Top", "Bottom", "Feet", "Gloves", "Neck", "Shoulders", "Hat", "HairFront", "HairBack", "LowerFace" };
            var hex = new Regex("^#[0-9a-fA-F]{6}$");
            foreach (object f in Expand(Json))
            {
                var outfits = Items(f, "outfits");
                var slots = outfits.Select(o => Text(o, "slot")).ToArray();
                foreach (string must in new[] { "Head", "Body", "Eyes", "EyeBrows", "Top", "Bottom", "Feet" })
                    CollectionAssert.Contains(slots, must, Text(f, "id"));
                foreach (object o in outfits)
                {
                    string[] colours = Texts(o, "colors");
                    Assert.That(colours.Length, Is.InRange(1, 9), Text(f, "id") + " " + Text(o, "id"));
                    foreach (string c in colours) StringAssert.IsMatch(hex.ToString(), c, Text(f, "id") + " " + Text(o, "id"));
                }
                foreach (string slot in wearable)
                    Assert.AreNotEqual(slots.Contains(slot), Texts(f, "remove").Contains(slot), Text(f, "id") + ": " + slot + " is either worn or removed");
            }
        }

        [Test]
        public void ThePoolHoldsWorkersCraftsfolkGuardsFightersMessengersTailorsAndForesters()
        {
            object[] figures = Expand(Json);
            Func<string, object[]> kind = name => figures.Where(f => Text(f, "id").StartsWith("folk_" + name + "_")).ToArray();
            foreach (string name in new[] { "bauer", "baeuerin", "wache", "waechterin", "kaempfer", "bergwache", "bote", "botin", "schneider", "schneiderin", "foerster", "foersterin", "baecker", "schmied", "arzt" })
                Assert.IsNotEmpty(kind(name), name);
            Assert.AreEqual("f", Text(kind("magd")[0], "motion"));
            Assert.AreEqual("m", Text(kind("knecht")[0], "motion"));
        }

        [Test]
        public void ThePoolRefusesWhatCannotBeBuilt()
        {
            Assert.IsNull(Defect(Json));
            string json = Json;
            Func<string, string, string> changed = (from, to) =>
            {
                Assert.IsTrue(json.Contains(from), "probe text missing: " + from);
                return json.Replace(from, to);
            };
            StringAssert.Contains("has no colour roles", Defect(changed("\"wear\": [\n    \"Top_VillagerTunic\",", "\"wear\": [\n    \"Top_Unknown\",")));
            StringAssert.Contains("unknown palette", Defect(changed("\"palette\": \"erde\"", "\"palette\": \"gold\"")));
            StringAssert.Contains("unknown colour role", Defect(changed("\"roles\": [\n    \"metal\"\n   ]", "\"roles\": [\n    \"silver\"\n   ]")));
            StringAssert.Contains("reserved for the hair", Defect(changed("\"black\"\n ],", "\"black\",\n  \"hair\"\n ],")));
            StringAssert.Contains("Unknown figure pool schema", Defect(changed("ashen.bozo-pool.v1", "ashen.bozo-pool.v0")));
            StringAssert.Contains("nobody walks around without", Defect(changed("\n    \"Feet_VillagerBoots\",", "")));
            StringAssert.Contains("two looks are the same", Defect(changed("\"looks\": [\n    {\n     \"tone\": \"hell\",\n     \"hair\": \"braun\",\n     \"palette\": \"erde\",\n     \"eyes\": \"braun\"\n    },",
                "\"looks\": [\n    {\n     \"tone\": \"hell\",\n     \"hair\": \"braun\",\n     \"palette\": \"erde\",\n     \"eyes\": \"braun\"\n    },\n    {\n     \"tone\": \"hell\",\n     \"hair\": \"braun\",\n     \"palette\": \"erde\",\n     \"eyes\": \"braun\"\n    },")));
            // Another palette alone changes the clothes, not the head: a swapped head would look like the other one.
            StringAssert.Contains("share skin, hair and eyes", Defect(changed("\"looks\": [\n    {\n     \"tone\": \"hell\",\n     \"hair\": \"braun\",\n     \"palette\": \"erde\",\n     \"eyes\": \"braun\"\n    },",
                "\"looks\": [\n    {\n     \"tone\": \"hell\",\n     \"hair\": \"braun\",\n     \"palette\": \"erde\",\n     \"eyes\": \"braun\"\n    },\n    {\n     \"tone\": \"hell\",\n     \"hair\": \"braun\",\n     \"palette\": \"wein\",\n     \"eyes\": \"braun\"\n    },")));
        }

        static string[] IndexLines(string existing, string[] rebuilt, HashSet<string> baked)
        {
            string text = (string)Pool().GetMethod("UpdateIndex").Invoke(null, new object[] { existing, rebuilt, (Func<string, bool>)baked.Contains });
            StringAssert.StartsWith("#", text);
            return text.Split('\n').Where(l => l != "" && !l.StartsWith("#")).ToArray();
        }

        [Test]
        public void TheIndexListsOnlyBakedFiguresWithKindGroupMotionAndSkin()
        {
            var baked = new HashSet<string> { "FolkBauer0", "FolkMagd2" };
            string[] lines = IndexLines("", new string[0], baked);
            Assert.AreEqual(2, lines.Length, string.Join("\n", lines));
            CollectionAssert.AreEqual(new[] { "FolkBauer0", "bauer", "arbeit", "m", "hell" }, lines[0].Split('\t'));
            CollectionAssert.AreEqual(new[] { "FolkMagd2", "magd", "arbeit", "f", "dunkel" }, lines[1].Split('\t'));
        }

        [Test]
        public void TheIndexDescribesEachVisualAsItWasBuilt()
        {
            // The index says FolkBauer0 was built with dark skin, the pool now says light skin.
            string existing = "# index\nFolkBauer0\tbauer\tarbeit\tm\tdunkel\nFolkGone0\tgone\tarbeit\tm\thell\nFolkOld0\told\tarbeit\tf\tmittel\n";
            var baked = new HashSet<string> { "FolkBauer0", "FolkOld0", "FolkMagd2" };
            string[] lines = IndexLines(existing, new string[0], baked);
            CollectionAssert.Contains(lines, "FolkBauer0\tbauer\tarbeit\tm\tdunkel", "a figure that was not rebuilt keeps the line of its visual");
            CollectionAssert.Contains(lines, "FolkMagd2\tmagd\tarbeit\tf\tdunkel", "a visual the index never had gets its line from the pool");
            CollectionAssert.Contains(lines, "FolkOld0\told\tarbeit\tf\tmittel", "a baked visual of a kind no longer in the pool keeps its line");
            Assert.IsFalse(lines.Any(l => l.StartsWith("FolkGone0")), "a visual that is gone loses its line");
            lines = IndexLines(existing, new[] { "folk_bauer_0" }, baked);
            CollectionAssert.Contains(lines, "FolkBauer0\tbauer\tarbeit\tm\thell", "a successful rebuild writes the pool's line");
            Assert.AreEqual(lines.Length, lines.Select(l => l.Split('\t')[0]).Distinct().Count(), "one line per visual");
            Assert.IsEmpty(IndexLines(null, null, new HashSet<string>()), "nothing baked, nothing listed");
        }
    }
}
