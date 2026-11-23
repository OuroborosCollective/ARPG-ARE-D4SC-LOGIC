#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
#if UNITY_ANDROID
using UnityEditor.Android;
using UnityEditor.Build;
#endif

namespace AreArpg.EditorTools
{
    /// <summary>
    /// Google Play's In-App Update library for the update check at game start. Every Android build adds it to the
    /// generated unityLibrary Gradle project; Gradle fetches it from Google's Maven repository during the build.
    /// </summary>
    public static class PlayAppUpdateGradle
    {
        public const string Dependency = "com.google.android.play:app-update:2.1.0";

        private static readonly Regex TopLevelDependencies = new Regex(@"^dependencies\s*\{[ \t]*\r?$", RegexOptions.Multiline);

        /// <summary>
        /// The build file with the dependency in its top-level dependencies block; unchanged when that library is already
        /// declared in any version; null when the file has no top-level dependencies block.
        /// </summary>
        public static string AddDependency(string buildGradle, string coordinate)
        {
            if (buildGradle == null) return null;
            string library = coordinate.Substring(0, coordinate.LastIndexOf(':') + 1);
            // Only an active declaration counts; a commented-out line must not keep the library out of the build.
            var declared = new Regex(@"^[ \t]*(?:implementation|api|compileOnly|runtimeOnly)[ \t]*\(?[ \t]*['""]" + Regex.Escape(library), RegexOptions.Multiline);
            if (declared.IsMatch(buildGradle)) return buildGradle;
            Match block = TopLevelDependencies.Match(buildGradle);
            if (!block.Success) return null;
            string newline = buildGradle.Contains("\r\n") ? "\r\n" : "\n";
            int lineEnd = buildGradle.IndexOf('\n', block.Index);
            int at = lineEnd < 0 ? buildGradle.Length : lineEnd + 1;
            string line = "    implementation '" + coordinate + "'" + newline;
            return lineEnd < 0 ? buildGradle + newline + line : buildGradle.Insert(at, line);
        }
    }

#if UNITY_ANDROID
    public sealed class PlayAppUpdateGradleStep : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder { get { return 20; } }

        /// <param name="path">The unityLibrary module of the generated Gradle project.</param>
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string file = Path.Combine(path, "build.gradle");
            string text = File.ReadAllText(file);
            string patched = PlayAppUpdateGradle.AddDependency(text, PlayAppUpdateGradle.Dependency);
            if (patched == null) throw new BuildFailedException("Play update check: " + file + " has no dependencies block.");
            if (patched != text) File.WriteAllText(file, patched);
        }
    }
#endif
}
#endif
