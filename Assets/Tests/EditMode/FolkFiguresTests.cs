using System.Collections.Generic;
using System.Linq;
using AreArpg.UnityClient;
using NUnit.Framework;

namespace AreArpg.EditMode.Tests
{
    /// <summary>
    /// How residents get their look from the figure pool (owner direction 2026-11-22): a figure of their own first,
    /// then the same bodies with heads of other figures of the same motion set and skin.
    /// </summary>
    public sealed class FolkFiguresTests
    {
        // Two kinds per motion set and skin, two looks each: eight figures, four per head group.
        const string Index = "# pool\n" +
            "FolkBauer0\tbauer\tarbeit\tm\thell\nFolkBauer1\tbauer\tarbeit\tm\thell\nFolkKnecht0\tknecht\tarbeit\tm\thell\nFolkKnecht1\tknecht\tarbeit\tm\thell\n" +
            "FolkMagd0\tmagd\tarbeit\tf\tdunkel\nFolkMagd1\tmagd\tarbeit\tf\tdunkel\nFolkAlte0\talte\tarbeit\tf\tdunkel\nFolkAlte1\talte\tarbeit\tf\tdunkel\n" +
            "FolkWache0\twache\twache\tm\thell\n";

        [Test]
        public void TheIndexKeepsOnlyWellFormedLinesWithSafeKeys()
        {
            string hostile = Index +
                "../Hero\tbauer\tarbeit\tm\thell\n" +           // a path out of the visuals folder
                "FolkX/../Hero\tbauer\tarbeit\tm\thell\n" +
                "Hero\tbauer\tarbeit\tm\thell\n" +              // not a pool visual
                "FolkBauer9\tbauer\tarbeit\tx\thell\n" +        // unknown motion set
                "FolkBauer8\tbauer\tarbeit\tm\n" +              // a field missing
                "FolkBauer7\tbau er\tarbeit\tm\thell\n" +        // a kind that is no word
                "FolkBauer0\tknecht\twache\tf\tdunkel\r\n";     // a key twice: the first line counts
            List<FolkFigures.Entry> entries = FolkFigures.Parse(hostile);
            Assert.AreEqual(9, entries.Count);
            Assert.IsTrue(entries.All(e => FolkFigures.IsKey(e.Key)));
            FolkFigures.Entry first = entries.Single(e => e.Key == "FolkBauer0");
            Assert.AreEqual("arbeit", first.Group); Assert.AreEqual("m", first.Motion);
            Assert.IsEmpty(FolkFigures.Parse(null));
        }

        [Test]
        public void TheFirstResidentsEachGetAFigureOfTheirOwnInAFixedOrder()
        {
            List<FolkFigures.Entry> entries = FolkFigures.Parse(Index);
            var bodies = new HashSet<string>();
            for (int ordinal = 0; ordinal < 8; ordinal++)
            {
                FolkFigures.Look look;
                Assert.IsTrue(FolkFigures.TryLook(entries, FolkFigures.Workers, ordinal, out look));
                Assert.IsTrue(look.OwnHead, "the first round keeps the figures as they were made");
                Assert.IsTrue(bodies.Add(look.Body), "nobody of the first round looks like another");
                FolkFigures.Look again;
                FolkFigures.TryLook(entries, FolkFigures.Workers, ordinal, out again);
                Assert.AreEqual(look.Body, again.Body, "the same resident looks the same every time");
            }
            CollectionAssert.DoesNotContain(bodies, "FolkWache0", "guards are no village workers");
            FolkFigures.Look none;
            Assert.IsFalse(FolkFigures.TryLook(entries, "krieger", 0, out none));
            Assert.IsFalse(FolkFigures.TryLook(new FolkFigures.Entry[0], FolkFigures.Workers, 0, out none));
        }

        [Test]
        public void FiguresAreDealtKindByKindSoTheFirstResidentsDiffer()
        {
            // Four kinds with six looks each, named as the pool names them: keys that differ only in their last digit.
            string[] kinds = { "foersterin", "holzfaeller", "magd", "bauer" };
            string[] skins = { "hell", "mittel", "dunkel" };
            string index = string.Concat(kinds.SelectMany(kind => Enumerable.Range(0, 6).Select(n =>
                "Folk" + char.ToUpperInvariant(kind[0]) + kind.Substring(1) + n + "\t" + kind + "\tarbeit\t" + (kind == "magd" || kind == "foersterin" ? "f" : "m") + "\t" + skins[n % 3] + "\n")));
            List<FolkFigures.Entry> entries = FolkFigures.Parse(index);
            Assert.AreEqual(24, entries.Count);
            var dealt = new List<FolkFigures.Entry>();
            for (int number = 0; number < 24; number++)
            {
                FolkFigures.Look look;
                Assert.IsTrue(FolkFigures.TryLook(entries, FolkFigures.Workers, number, out look));
                dealt.Add(entries.Single(e => e.Key == look.Body));
            }
            for (int round = 0; round < 6; round++)
                CollectionAssert.AreEquivalent(kinds, dealt.Skip(round * 4).Take(4).Select(e => e.Kind), "round " + round + " brings every kind once");
            CollectionAssert.AllItemsAreUnique(dealt.Take(4).Select(e => e.Skin).Take(3), "the first residents do not share one skin");
            CollectionAssert.AllItemsAreUnique(dealt.Select(e => e.Key), "every look comes once before any comes again");
            CollectionAssert.AreEqual(dealt.Select(e => e.Key), FolkFigures.DealingOrder(entries, FolkFigures.Workers).Select(e => e.Key));
        }

        [Test]
        public void LaterResidentsWearTheSameBodiesWithOtherHeadsOfTheirMotionSetAndSkin()
        {
            List<FolkFigures.Entry> entries = FolkFigures.Parse(Index);
            var byKey = entries.ToDictionary(e => e.Key);
            var looks = new HashSet<string>();
            for (int ordinal = 0; ordinal < 32; ordinal++)
            {
                FolkFigures.Look look;
                Assert.IsTrue(FolkFigures.TryLook(entries, FolkFigures.Workers, ordinal, out look));
                FolkFigures.Entry body = byKey[look.Body], head = byKey[look.Head];
                Assert.AreEqual(body.Motion, head.Motion, "a head only goes on a body of its own motion set");
                Assert.AreEqual(body.Skin, head.Skin, "head and body have one skin");
                Assert.AreEqual(ordinal >= 8, !look.OwnHead, "from the second round on, heads are swapped");
                looks.Add(look.Body + "+" + look.Head);
            }
            Assert.AreEqual(32, looks.Count, "eight bodies with four heads each give thirty-two different people");
            FolkFigures.Look negative;
            Assert.IsTrue(FolkFigures.TryLook(entries, FolkFigures.Workers, -3, out negative), "an odd ordinal still gets a figure");
        }
    }
}
