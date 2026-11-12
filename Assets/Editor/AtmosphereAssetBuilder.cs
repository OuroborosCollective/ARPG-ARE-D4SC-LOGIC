#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AreArpg.UnityClient;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditorTools
{
    /// <summary>Bind reviewed private clips without importing or executing vendor gameplay scripts.</summary>
    [InitializeOnLoad]
    public static class AtmosphereAssetBuilder
    {
        public const string SourceRoot = "Assets/PrivateArt/AshenAudio";
        private const string Target = "Assets/PrivateArt/Resources/AshenAtmosphere.asset";
        static AtmosphereAssetBuilder() { EditorApplication.delayCall += BuildIfAbsent; }

        private static void BuildIfAbsent()
        {
            if (!Directory.Exists(SourceRoot) || AssetDatabase.LoadAssetAtPath<AtmosphereLibrary>(Target) != null) return;
            Build();
        }

        [MenuItem("Ashen Crypt/Build reviewed audio library")]
        public static void Build()
        {
            if (!Directory.Exists(SourceRoot)) throw new InvalidOperationException("Reviewed local audio is absent.");
            var entries = new List<AtmosphereLibrary.ClipEntry>();
            var paths = Directory.GetFiles(SourceRoot, "*", SearchOption.TopDirectoryOnly); Array.Sort(paths, StringComparer.Ordinal);
            foreach (string raw in paths)
            {
                string path = raw.Replace('\\', '/'); string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension != ".wav" && extension != ".ogg") continue;
                string key = Path.GetFileNameWithoutExtension(path);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) continue;
                var settings = importer.defaultSampleSettings;
                bool stream = key.StartsWith("music_", StringComparison.Ordinal) || key.StartsWith("ambient_", StringComparison.Ordinal) || key == "water_mill";
                var desired = stream ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                if (settings.loadType != desired || settings.compressionFormat != AudioCompressionFormat.Vorbis || !importer.forceToMono)
                {
                    settings.loadType = desired; settings.compressionFormat = AudioCompressionFormat.Vorbis; settings.quality = .65f;
                    importer.forceToMono = true; importer.defaultSampleSettings = settings; importer.SaveAndReimport();
                }
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) entries.Add(new AtmosphereLibrary.ClipEntry { Key = key, Clip = clip });
            }
            if (entries.Count == 0) throw new InvalidOperationException("No reviewed audio clips imported.");
            Directory.CreateDirectory(Path.GetDirectoryName(Target)); AssetDatabase.Refresh();
            var library = AssetDatabase.LoadAssetAtPath<AtmosphereLibrary>(Target);
            if (library == null) { library = ScriptableObject.CreateInstance<AtmosphereLibrary>(); AssetDatabase.CreateAsset(library, Target); }
            library.Clips = entries.ToArray(); EditorUtility.SetDirty(library); AssetDatabase.SaveAssets();
            Debug.Log("Ashen audio library: " + entries.Count + " reviewed local clips.");
        }
    }
}
#endif
