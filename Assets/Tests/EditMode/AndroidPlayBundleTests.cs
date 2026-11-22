using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace AreArpg.EditMode.Tests
{
    /// <summary>
    /// The signed Google Play bundle (owner decision 2026-11-22): version codes always rise, and the upload key can only
    /// come from a signing file outside the project, so it can never be committed to the public repository.
    /// </summary>
    public sealed class AndroidPlayBundleTests
    {
        static Type Tools()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType("AreArpg.EditorTools.ArpgBuildTools");
                if (type != null) return type;
            }
            Assert.Fail("ArpgBuildTools is missing");
            return null;
        }

        static int VersionCode(DateTime utc) { return (int)Tools().GetMethod("PlayVersionCode").Invoke(null, new object[] { utc }); }

        static string Refusal()
        {
            try { Tools().GetMethod("ReadSigning", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null); return null; }
            catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { return e.InnerException.Message; }
        }

        [Test]
        public void EveryLaterBundleCarriesAHigherVersionCode()
        {
            var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(1, VersionCode(start.AddMinutes(1)));
            Assert.Less(VersionCode(new DateTime(2026, 11, 22, 21, 0, 0, DateTimeKind.Utc)), VersionCode(new DateTime(2026, 11, 22, 21, 1, 0, DateTimeKind.Utc)));
            Assert.Less(VersionCode(new DateTime(2080, 1, 1, 0, 0, 0, DateTimeKind.Utc)), 2100000000, "Google Play's largest version code is far away");
        }

        [Test]
        public void TheUploadKeyOnlyComesFromASigningFileOutsideTheProject()
        {
            string variable = (string)Tools().GetField("SigningVariable").GetValue(null);
            string before = Environment.GetEnvironmentVariable(variable);
            string outside = Path.Combine(Path.GetTempPath(), "ashen-signing-test-" + Guid.NewGuid().ToString("N"));
            string inside = Path.GetFullPath(Path.Combine("Temp", "ashen-signing-test-" + Guid.NewGuid().ToString("N") + ".json"));
            Directory.CreateDirectory(outside);
            try
            {
                Environment.SetEnvironmentVariable(variable, null);
                StringAssert.Contains(variable, Refusal(), "without a signing file nothing is signed");

                string key = Path.Combine(outside, "upload.p12");
                File.WriteAllText(key, "not a real key");
                string json = "{\"keystore\":\"" + key.Replace("\\", "\\\\") + "\",\"alias\":\"a\",\"storePassword\":\"s\",\"keyPassword\":\"k\"}";
                Directory.CreateDirectory(Path.GetDirectoryName(inside));
                File.WriteAllText(inside, json);
                Environment.SetEnvironmentVariable(variable, inside);
                StringAssert.Contains("inside the project", Refusal(), "a signing file in the project could be committed");

                string file = Path.Combine(outside, "signing.json");
                File.WriteAllText(file, json);
                Environment.SetEnvironmentVariable(variable, file);
                Assert.IsNull(Refusal(), "a complete signing file outside the project is accepted");

                string keyInside = Path.GetFullPath(Path.Combine("Temp", "ashen-upload-test.p12"));
                File.WriteAllText(keyInside, "not a real key");
                File.WriteAllText(file, json.Replace(key.Replace("\\", "\\\\"), keyInside.Replace("\\", "\\\\")));
                StringAssert.Contains("inside the project", Refusal(), "an upload key in the project could be committed");
                File.Delete(keyInside);

                File.WriteAllText(file, "{\"keystore\":\"" + key.Replace("\\", "\\\\") + "\",\"alias\":\"a\",\"storePassword\":\"\",\"keyPassword\":\"k\"}");
                StringAssert.Contains("incomplete", Refusal());
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, before);
                if (File.Exists(inside)) File.Delete(inside);
                Directory.Delete(outside, true);
            }
        }

        [Test]
        public void APathIntoTheProjectIsRecognisedWhereTheFileSystemIgnoresCase()
        {
            MethodInfo inside = Tools().GetMethod("InsideFolder", BindingFlags.NonPublic | BindingFlags.Static);
            Func<string, string, bool, bool> check = (path, folder, ignoreCase) => (bool)inside.Invoke(null, new object[] { path, folder, ignoreCase });
            string project = Path.Combine(Path.GetTempPath(), "AreProject");
            string otherCase = Path.Combine(project.ToUpperInvariant(), "signing.json");
            Assert.IsTrue(check(otherCase, project, true), "Windows and macOS: another spelling of the project path is still the project");
            Assert.IsFalse(check(otherCase, project, false), "Linux: another spelling is another folder");
            Assert.IsTrue(check(Path.Combine(project, "Temp", "signing.json"), project, false));
            Assert.IsFalse(check(Path.Combine(project + "Sibling", "signing.json"), project, true), "a sibling folder with the same prefix stays outside");
        }

        [Test]
        public void AfterASignedBuildNoKeyPasswordStaysInTheGeneratedGradleFiles()
        {
            string folder = Path.Combine(Path.GetTempPath(), "ashen-gradle-test-" + Guid.NewGuid().ToString("N"));
            string launcher = Path.Combine(folder, "launcher");
            Directory.CreateDirectory(launcher);
            try
            {
                // The shape Unity generates: both passwords as plain text in the launcher's build file.
                File.WriteAllText(Path.Combine(launcher, "build.gradle"),
                    "signingConfigs { release { storeFile file('/outside/upload.p12')\n storePassword 'Store-Secret-1'\n keyAlias 'upload'\n keyPassword 'Key-Secret-2' } }");
                File.WriteAllText(Path.Combine(folder, "gradle.properties"), "unity.keyPassword=Key-Secret-2\n");
                MethodInfo clean = Tools().GetMethod("RemoveKeyPasswords", BindingFlags.NonPublic | BindingFlags.Static);
                object[] secrets = { folder, new[] { "Store-Secret-1", "Key-Secret-2" } };
                Assert.AreEqual(2, (int)clean.Invoke(null, secrets), "both Gradle files held a password");
                string gradle = File.ReadAllText(Path.Combine(launcher, "build.gradle"));
                string properties = File.ReadAllText(Path.Combine(folder, "gradle.properties"));
                StringAssert.DoesNotContain("Secret", gradle + properties);
                StringAssert.Contains("storePassword ''", gradle, "the build file keeps its shape");
                StringAssert.Contains("keyAlias 'upload'", gradle, "only the passwords are removed");
                Assert.AreEqual(0, (int)clean.Invoke(null, secrets), "a second pass finds nothing");
                Assert.AreEqual(0, (int)clean.Invoke(null, new object[] { Path.Combine(folder, "missing"), new[] { "x" } }), "no Gradle project, nothing to do");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void TheBundleSizeWarningOnlyAppearsAboveThreeGigabytes()
        {
            var settings = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            Assert.IsNotEmpty(settings);
            var serialized = new UnityEditor.SerializedObject(settings[0]);
            Assert.IsNotNull(serialized.FindProperty("AndroidAppBundleSizeToValidate"), "Unity keeps the bundle size warning in the player settings");
            int before = serialized.FindProperty("AndroidAppBundleSizeToValidate").intValue;
            try
            {
                int raised = (int)Tools().GetMethod("RaiseBundleSizeWarning", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                Assert.AreEqual(Math.Max(before, 3072), raised, "the owner accepts bundles up to 3 GB");
                serialized.Update();
                Assert.AreEqual(raised, serialized.FindProperty("AndroidAppBundleSizeToValidate").intValue);
            }
            finally
            {
                serialized.Update();
                serialized.FindProperty("AndroidAppBundleSizeToValidate").intValue = before;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
