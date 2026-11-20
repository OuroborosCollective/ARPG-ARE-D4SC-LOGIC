using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace AshenCrypt.WorldForge.EditorTools
{
    public enum WFBlenderJobState { Unknown, Queued, Ready, Failed, Incomplete }

    public static class WFBlenderIntake
    {
        public const string Root = "/var/lib/unityarpg/.local/share/are-arpg/worldforge-blender";
        public const string AssetRoot = "Assets/PrivateArt/WorldForgeBlender";
        const string Cli = "/usr/local/bin/worldforge-blender";

        [Serializable]
        class Receipt { public string schema; public string jobId; public ExportFile[] files; }
        [Serializable]
        class ExportFile { public string path; public long size; public string sha256; }

        public static bool IsJobId(string id)
        {
            return id != null && id.Length == 64 && id.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
        }

        static void ValidateId(string id)
        {
            if (!IsJobId(id)) throw new ArgumentException("Eine Auftrags-ID benötigt genau 64 kleine Hexadezimalzeichen.");
        }

        // Run on a background thread. Only the fixed executable receives the chosen path.
        public static string Submit(string source)
        {
            if (string.IsNullOrEmpty(source) || !source.StartsWith("/", StringComparison.Ordinal) ||
                source.Any(char.IsControl))
                throw new ArgumentException("Bitte einen absoluten lokalen Dateipfad auswählen.");
            string ext = Path.GetExtension(source).ToLowerInvariant();
            if (ext != ".blend" && ext != ".fbx" && ext != ".glb")
                throw new ArgumentException("Unterstützt werden gepackte .blend, eigenständige .fbx und .glb.");
            if (!File.Exists(source)) throw new FileNotFoundException("Quelldatei fehlt.", source);
            string stdout = RunCli("submit", source);
            if (stdout.Length != 65 || stdout[64] != '\n' || !IsJobId(stdout.Substring(0, 64)))
                throw new InvalidDataException("Der Blender-Dienst lieferte keine gültige Auftrags-ID.");
            return stdout.Substring(0, 64);
        }

        public static string Retry(string id)
        {
            ValidateId(id);
            RunCli("retry", id);
            return id;
        }

        static string RunCli(string command, string argument)
        {
            var info = new ProcessStartInfo(Cli) { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true };
            info.ArgumentList.Add(command);
            info.ArgumentList.Add(argument);
            using (var process = Process.Start(info))
            {
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                string stdout = output.GetAwaiter().GetResult();
                string stderr = error.GetAwaiter().GetResult();
                if (process.ExitCode != 0) throw new IOException("Blender-Auftrag fehlgeschlagen: " + stderr.Trim());
                return stdout;
            }
        }

        public static WFBlenderJobState State(string id)
        {
            ValidateId(id);
            string folder = Path.Combine(Root, "results", id);
            if (Directory.Exists(folder))
            {
                return IsCompleteResult(folder, id) ? WFBlenderJobState.Ready : WFBlenderJobState.Incomplete;
            }
            if (File.Exists(Path.Combine(Root, "failed", id, "error.txt"))) return WFBlenderJobState.Failed;
            return Directory.Exists(Path.Combine(Root, "queue", id)) ? WFBlenderJobState.Queued : WFBlenderJobState.Unknown;
        }

        // Poll the receipt and filesystem metadata. Hash FBX bytes only on an explicit import.
        public static bool IsCompleteResult(string folder, string id)
        {
            return ReadFbxRecord(folder, id) != null;
        }

        static ExportFile ReadFbxRecord(string folder, string id)
        {
            if (!IsJobId(id)) return null;
            try
            {
                RejectLink(folder);
                string receiptPath = Path.Combine(folder, "receipt.json");
                RejectLink(receiptPath);
                if (new FileInfo(receiptPath).Length == 0) return null;
                var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(receiptPath));
                if (receipt == null || receipt.schema != "worldforge.blender-export.v1" ||
                    receipt.jobId != id || receipt.files == null) return null;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                ExportFile fbx = null;
                foreach (var file in receipt.files)
                {
                    if (file == null || string.IsNullOrEmpty(file.path) || file.path == "receipt.json" ||
                        file.path.Contains("\\") || Path.IsPathRooted(file.path) || file.path.Any(char.IsControl) ||
                        !seen.Add(file.path) || file.size <= 0 || !IsJobId(file.sha256)) return null;
                    string path = folder;
                    foreach (string part in file.path.Split('/'))
                    {
                        if (part.Length == 0 || part == "." || part == ".." || Path.IsPathRooted(part)) return null;
                        path = Path.Combine(path, part);
                        RejectLink(path);
                    }
                    if (!File.Exists(path) || new FileInfo(path).Length != file.size) return null;
                    if (file.path == "model.fbx") fbx = file;
                }
                return seen.Contains("model.glb") && seen.Contains("model.gltf") ? fbx : null;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException ||
                                          error is ArgumentException)
            {
                return null;
            }
        }

        static void RejectLink(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Symbolischer Link im Importpfad: " + path);
        }

        static string Fingerprint(string file)
        {
            using (var input = File.OpenRead(file))
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }

        static string EnsureFolder(string parent, string name)
        {
            string folder = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(folder) && string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, name)))
                throw new IOException("Unity konnte den privaten Ordner nicht anlegen: " + folder);
            RejectLink(folder);
            return folder;
        }

        public static GameObject ImportFbx(string id)
        {
            ValidateId(id);
            string results = Path.Combine(Root, "results");
            RejectLink(Root);
            RejectLink(results);
            string job = Path.Combine(results, id);
            var fbx = ReadFbxRecord(job, id);
            if (fbx == null) throw new InvalidDataException("Der Blender-Export ist unvollständig oder beschädigt.");
            string source = Path.Combine(job, "model.fbx");

            string root = EnsureFolder(EnsureFolder("Assets", "PrivateArt"), "WorldForgeBlender");
            string folder = EnsureFolder(root, id);
            string destination = folder + "/model.fbx";
            bool copied = false;
            if (File.Exists(destination))
            {
                RejectLink(destination);
                if (Fingerprint(destination) != fbx.sha256 || Fingerprint(source) != fbx.sha256)
                    throw new InvalidDataException("FBX-Prüfsumme weicht ab. Vorhandenes Modell bleibt erhalten.");
            }
            else
            {
                // A temporary name ending in ~ is ignored by the Unity asset importer.
                string pending = Path.Combine(folder, Guid.NewGuid().ToString("N") + "~");
                try
                {
                    File.Copy(source, pending);
                    if (Fingerprint(pending) != fbx.sha256)
                        throw new InvalidDataException("FBX-Prüfsumme stimmt nicht mit dem Exportbeleg überein.");
                    File.Move(pending, destination);
                    copied = true;
                }
                finally { if (File.Exists(pending)) File.Delete(pending); }
            }

            try
            {
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(destination);
                if (model == null || !(AssetImporter.GetAtPath(destination) is ModelImporter))
                    throw new InvalidDataException("Unity konnte das FBX-Modell nicht laden.");
                return model;
            }
            catch
            {
                if (copied) AssetDatabase.DeleteAsset(destination);
                throw;
            }
        }
    }
}
