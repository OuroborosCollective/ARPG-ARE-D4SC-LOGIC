using System.IO;
using UnityEditor;
using UnityEngine;
namespace AreArpg.Editor
{
    public static class FieldPauldronExport
    {
        public static void Export()
        {
            var output=System.Environment.GetEnvironmentVariable("ASHEN_SHOULDER_OUTPUT");
            if(string.IsNullOrEmpty(output)) throw new System.InvalidOperationException("Missing review folder");
            const string folder="Assets/Resources/Equipment/Feldschulter";
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/Feldschulter_Wearable.prefab"))
                throw new FileNotFoundException("Baked pauldron missing");
            AssetDatabase.ExportPackage(folder,Path.Combine(output,"Ashen-Crypt-Feldschulter.unitypackage"),ExportPackageOptions.Recurse);
        }
    }
}
