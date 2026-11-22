using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    public sealed class LivingChronicleHudTests
    {
        [TestCase(1007f, 601f)]
        [TestCase(1920f, 1080f)]
        public void ChronicleTouchTargetsStayInsideLogicalModal(float width, float height)
        {
            var layout = new ArenaHudLayout(width, height, new Rect(0, 0, width, height));
            Rect tab = ArenaRuntime.ChronicleTabRect(layout);
            Rect previous = ArenaRuntime.ChroniclePreviousRect(layout);
            Rect next = ArenaRuntime.ChronicleNextRect(layout);
            Assert.GreaterOrEqual(tab.width, 80f);
            Assert.GreaterOrEqual(tab.height, 30f);
            Assert.GreaterOrEqual(previous.width, 65f);
            Assert.GreaterOrEqual(next.width, 65f);
            Assert.Greater(tab.xMin, 0f);
            Assert.Greater(tab.yMin, 0f);
            Assert.LessOrEqual(tab.xMax, layout.Width);
            Assert.LessOrEqual(next.xMax, layout.Width);
            Assert.LessOrEqual(next.yMax, layout.Height);
            Assert.IsFalse(previous.Overlaps(next));
            Assert.IsFalse(tab.Overlaps(previous));
            Assert.IsFalse(tab.Overlaps(next));
            foreach (Rect rect in new[] { tab, previous, next })
            {
                Vector2 center = rect.center;
                Vector2 native = layout.ToScreenTouch(center);
                Vector2 recovered = layout.ToGuiTouch(native);
                Assert.That(recovered.x, Is.EqualTo(center.x).Within(.1f));
                Assert.That(recovered.y, Is.EqualTo(center.y).Within(.1f));
            }
        }
    }
}
