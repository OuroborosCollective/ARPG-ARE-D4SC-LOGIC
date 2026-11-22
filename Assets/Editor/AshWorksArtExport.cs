using System;
using System.IO;
using System.Linq;
using UnityEditor;

namespace AreArpg.EditorTools
{
    public static class AshWorksArtExport
    {
        private static readonly string[] Folders = {
            "Assets/Resources/Equipment/AshWorks","Assets/Resources/Equipment/Werkarsenal",
            "Assets/Resources/ARE/Visuals/Werkarsenal","Assets/Resources/AshenRelics",
            "Assets/Resources/Equipment/Tiefscharrer"
        };
        public static void Export()
        {
            var output=Environment.GetEnvironmentVariable("ASHEN_ARTISAN_OUTPUT");
            if(string.IsNullOrEmpty(output) || Folders.Any(f=>!AssetDatabase.IsValidFolder(f)))
                throw new InvalidOperationException("Original source art folders and destination required.");
            var all=AssetDatabase.GetAllAssetPaths().Where(path=>Folders.Any(f=>path==f ||
                path.StartsWith(f+"/",StringComparison.Ordinal))).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            if(!all.Contains(Folders[4]+"/Tiefscharrer_World.prefab") ||
               !all.Contains(Folders[0]+"/Glockentraegerin_Fund.prefab"))throw new InvalidOperationException("Native prefabs missing");
            string package=Path.Combine(output,"Ashen-Crypt-Werkstatt.unitypackage");
            AssetDatabase.ExportPackage(all,package,ExportPackageOptions.Default);
            if(!File.Exists(package))throw new IOException(package);
        }
    }
}
