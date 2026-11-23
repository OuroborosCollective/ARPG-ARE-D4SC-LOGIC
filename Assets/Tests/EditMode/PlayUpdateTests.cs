using System;
using System.Reflection;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AreArpg.EditMode.Tests
{
    /// <summary>
    /// The update check at game start (owner request 2026-11-23): the game only offers an update Google Play reports,
    /// starts Play's update only when the player asks for it, and every Android build carries Play's update library.
    /// </summary>
    public sealed class PlayUpdateTests
    {
        private sealed class FakeSource : IPlayUpdateSource
        {
            public PlayUpdateState Answer;
            public int Checks, Starts;
            public void Check(Action<PlayUpdateState> done) { Checks++; done(Answer); }
            public void StartUpdate() { Starts++; }
        }

        private static PlayUpdateState Available { get { return new PlayUpdateState(PlayUpdateState.UpdateAvailable, true, 28300000); } }

        [Test]
        public void TheGameOnlyOffersAnUpdateWhenPlayReportsOne()
        {
            Assert.AreEqual(PlayUpdateAction.Offer, PlayUpdatePolicy.Decide(Available));
            Assert.AreEqual(PlayUpdateAction.Resume, PlayUpdatePolicy.Decide(new PlayUpdateState(PlayUpdateState.UpdateInProgress, true, 28300000)),
                "an update the player already started continues");
            Assert.AreEqual(PlayUpdateAction.None, PlayUpdatePolicy.Decide(new PlayUpdateState(PlayUpdateState.UpdateNotAvailable, true, 0)));
            Assert.AreEqual(PlayUpdateAction.None, PlayUpdatePolicy.Decide(new PlayUpdateState(PlayUpdateState.UpdateAvailable, false, 28300000)),
                "only Play's full-screen update is used");
            Assert.AreEqual(PlayUpdateAction.None, PlayUpdatePolicy.Decide(PlayUpdateState.Failed("not installed from Google Play")),
                "without an answer from Play the game simply starts");
            Assert.AreEqual(PlayUpdateAction.None, PlayUpdatePolicy.Decide(default(PlayUpdateState)));
        }

        [Test]
        public void ThePanelOffersTheNewVersionAndStartsPlaysUpdateOnlyOnRequest()
        {
            bool hadEventSystem = UnityEngine.Object.FindObjectOfType<EventSystem>() != null;
            var source = new FakeSource();
            PlayUpdatePrompt prompt = null;
            try
            {
                source.Answer = new PlayUpdateState(PlayUpdateState.UpdateNotAvailable, true, 0);
                prompt = PlayUpdatePrompt.Install(source);
                prompt.RunCheck();
                Assert.IsFalse(prompt.PromptVisible, "no newer version, no panel");
                UnityEngine.Object.DestroyImmediate(prompt.gameObject);

                source.Answer = Available;
                prompt = PlayUpdatePrompt.Install(source);
                prompt.RunCheck();
                prompt.RunCheck();
                Assert.AreEqual(2, source.Checks, "each game start asks Play exactly once");
                Assert.IsTrue(prompt.PromptVisible);
                Assert.AreEqual(PlayUpdatePolicy.Body, prompt.BodyText.text);
                Assert.AreEqual(PlayUpdatePolicy.UpdateNow, prompt.UpdateButton.GetComponentInChildren<Text>().text);
                Assert.AreEqual(PlayUpdatePolicy.Later, prompt.LaterButton.GetComponentInChildren<Text>().text);
                Assert.IsTrue(prompt.GetComponentInChildren<Canvas>().sortingOrder > 20, "the panel lies above HUD and menus");
                prompt.LaterButton.onClick.Invoke();
                Assert.IsFalse(prompt.PromptVisible, "later closes the panel");
                Assert.AreEqual(0, source.Starts, "later never starts an update");
                UnityEngine.Object.DestroyImmediate(prompt.gameObject);

                prompt = PlayUpdatePrompt.Install(source);
                prompt.RunCheck();
                prompt.UpdateButton.onClick.Invoke();
                Assert.AreEqual(1, source.Starts, "update now hands over to Play");
                Assert.IsFalse(prompt.PromptVisible);
                UnityEngine.Object.DestroyImmediate(prompt.gameObject);

                source.Answer = new PlayUpdateState(PlayUpdateState.UpdateInProgress, true, 28300000);
                prompt = PlayUpdatePrompt.Install(source);
                prompt.RunCheck();
                Assert.AreEqual(2, source.Starts, "a started update resumes without asking again");
                Assert.IsFalse(prompt.PromptVisible);
            }
            finally
            {
                if (prompt != null) UnityEngine.Object.DestroyImmediate(prompt.gameObject);
                if (!hadEventSystem)
                {
                    EventSystem created = UnityEngine.Object.FindObjectOfType<EventSystem>();
                    if (created != null) UnityEngine.Object.DestroyImmediate(created.gameObject);
                }
            }
        }

        private static MethodInfo AddDependency()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType("AreArpg.EditorTools.PlayAppUpdateGradle");
                if (type != null) return type.GetMethod("AddDependency");
            }
            Assert.Fail("PlayAppUpdateGradle is missing");
            return null;
        }

        private static string Add(string gradle) { return (string)AddDependency().Invoke(null, new object[] { gradle, "com.google.android.play:app-update:2.1.0" }); }

        [Test]
        public void TheUpdateLibraryJoinsTheGeneratedGradleProjectOnce()
        {
            const string Line = "implementation 'com.google.android.play:app-update:2.1.0'";
            string unity = "apply plugin: 'com.android.library'\n\nbuildscript {\n    dependencies {\n        classpath 'x:y:1'\n    }\n}\n\n" +
                           "dependencies {\n    implementation fileTree(dir: 'libs', include: ['*.jar'])\n    implementation 'com.google.android.play:asset-delivery:2.3.0'\n}\n\nandroid {\n}\n";
            string once = Add(unity);
            Assert.AreEqual(1, Count(once, Line));
            int block = once.IndexOf("\ndependencies {", StringComparison.Ordinal), line = once.IndexOf(Line, StringComparison.Ordinal);
            Assert.IsTrue(line > block && line < once.IndexOf("\n}", block + 1, StringComparison.Ordinal), "inside the top-level dependencies block");
            Assert.IsTrue(line > once.IndexOf("classpath", StringComparison.Ordinal), "never in the buildscript block");
            Assert.AreEqual(once, Add(once), "a second build step changes nothing");
            string other = unity.Replace("asset-delivery:2.3.0", "app-update:2.0.1");
            Assert.AreEqual(other, Add(other), "a library already declared in another version is left alone");
            Assert.IsNull(Add("android {\n}\n"), "no dependencies block: the build fails loudly instead of shipping without updates");
            string crlf = Add(unity.Replace("\n", "\r\n"));
            Assert.AreEqual(1, Count(crlf, Line + "\r\n"));
            Assert.AreEqual(0, Count(crlf.Replace("\r\n", ""), "\n"), "Windows line endings stay consistent");
        }

        private static int Count(string text, string part)
        {
            int count = 0;
            for (int i = text.IndexOf(part, StringComparison.Ordinal); i >= 0; i = text.IndexOf(part, i + part.Length, StringComparison.Ordinal)) count++;
            return count;
        }
    }
}
