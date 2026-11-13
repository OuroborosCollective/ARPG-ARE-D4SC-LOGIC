using System;
using System.IO;
using System.Linq;
using AreArpg.Core;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditorTools
{
    /// <summary>Build reviewed passive bindings from privately supplied art. Never changes the input models.</summary>
    public static class WoodlandArtBuilder
    {
        private const string Forest="Assets/PrivateArt/OwnerForestReview/";
        private const string Output="Assets/Resources/ARE/Visuals/";
        [MenuItem("ARE/Art/Build Woodland and Equipment Bindings")]
        public static void Build()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/PrivateArt/WoodlandWeapons"}))
                {
                    var importer=AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                    if(importer==null)continue;
                    importer.isReadable=false;importer.mipmapEnabled=true;importer.maxTextureSize=Math.Min(importer.maxTextureSize,1024);
                    var android=importer.GetPlatformTextureSettings("Android");android.name="Android";android.overridden=true;
                    android.maxTextureSize=1024;android.format=TextureImporterFormat.ASTC_6x6;android.compressionQuality=80;
                    importer.SetPlatformTextureSettings(android);importer.SaveAndReimport();
                }
                Directory.CreateDirectory(Output+"Weapons");AssetDatabase.Refresh();
                string[] sources={"ashen_tree_twisted_a","ashen_tree_burnt_b","ashen_shrub_thorny_01","ashen_fern_cold_01","ashen_mushroom_cluster_glow_01","ashen_basalt_rubble_cluster_01"};
                string[] keys={"WoodlandTreeA","WoodlandTreeB","WoodlandShrub","WoodlandFern","WoodlandMushroomMoon","WoodlandRock"};
                for(int i=0;i<sources.Length;i++)CopyPassive(Forest+"Prefabs/Generated/"+sources[i]+".prefab",Output+keys[i]+".prefab");
                CopyPassive(Forest+"Prefabs/ReviewVariants/ashen_mushroom_cluster_red_01.prefab",Output+"WoodlandMushroomRed.prefab");
                if(!AssetDatabase.LoadAssetAtPath<Material>(Output+"WoodlandGround.mat")&&!AssetDatabase.CopyAsset(Forest+"Materials/Generated/AK_Ground_AshenPBR.mat",Output+"WoodlandGround.mat"))throw new IOException("Missing forest ground material");
                var paths=AssetDatabase.FindAssets("t:Model",new[]{"Assets/PrivateArt/WoodlandWeapons","Assets/PrivateVendor"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
                for(int i=0;i<WeaponCatalog.Count;i++)
                {
                    var def=WeaponCatalog.Get(i);var path=paths.FirstOrDefault(p=>Path.GetFileNameWithoutExtension(p)==def.ModelStem);
                    if(path==null)throw new IOException("Missing model for "+def.Id+" / "+def.ModelStem);
                    BuildWeapon(path,def);
                }
                string bowSource="Assets/Resources/ARE/Visuals/Vendor/AnimationsStarterPack/Combat/BowShot.fbx";
                var bow=AssetDatabase.LoadAllAssetsAtPath(bowSource).OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__"));
                if(bow==null)throw new IOException("Missing bow animation");
                var clip=UnityEngine.Object.Instantiate(bow);clip.name="WeaponBowShot";
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),AreArpg.UnityClient.EntryWeaponGrip.WeightParameter),AnimationCurve.Constant(0,clip.length,0));
                var previous=AssetDatabase.LoadAssetAtPath<AnimationClip>(Output+"WeaponBowShot.anim");
                if(previous!=null){EditorUtility.CopySerialized(clip,previous);UnityEngine.Object.DestroyImmediate(clip);}else AssetDatabase.CreateAsset(clip,Output+"WeaponBowShot.anim");
                AssetDatabase.SaveAssets();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var report=new { catalog=WeaponCatalog.Count,forestBindings=7,modelBindings=Enumerable.Range(0,WeaponCatalog.Count).Select(i=>new {id=WeaponCatalog.Get(i).Id,source=WeaponCatalog.Get(i).ModelStem}).ToArray() };
                string evidence=Environment.GetEnvironmentVariable("ARE_WOODLAND_AUTHOR_REPORT");
                if(!string.IsNullOrEmpty(evidence))File.WriteAllText(evidence,Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
                if(Application.isBatchMode)EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
        }
        private static void Strip(GameObject go)
        {
            foreach(var c in go.GetComponentsInChildren<Component>(true))
                if(c!=null&&!(c is Transform||c is MeshFilter||c is MeshRenderer||c is SkinnedMeshRenderer||c is Animator||c is LODGroup))UnityEngine.Object.DestroyImmediate(c);
            foreach(var animator in go.GetComponentsInChildren<Animator>(true))animator.applyRootMotion=false;
        }
        private static void CopyPassive(string source,string output)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(source);if(prefab==null)throw new IOException(source);
            var go=UnityEngine.Object.Instantiate(prefab);try{Strip(go);if(PrefabUtility.SaveAsPrefabAsset(go,output)==null)throw new IOException(output);}finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        private static Bounds BoundsOf(GameObject go)
        {var rs=go.GetComponentsInChildren<Renderer>(true);if(rs.Length==0)throw new InvalidDataException("Empty weapon");var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
        private static void BuildWeapon(string source,WeaponDefinition definition)
        {
            var root=new GameObject(definition.Id);
            try
            {
                var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source),root.transform,false);Strip(model);
                var b=BoundsOf(root);
                if(b.size.z>b.size.y&&b.size.z>b.size.x)model.transform.localRotation=Quaternion.Euler(-90,0,0)*model.transform.localRotation;
                else if(b.size.x>b.size.y)model.transform.localRotation=Quaternion.Euler(0,0,90)*model.transform.localRotation;
                b=BoundsOf(root);
                float height=definition.Family==WeaponFamily.Dagger?.65f:definition.Family==WeaponFamily.Shield?.8f:definition.Family==WeaponFamily.Staff?1.9f:definition.Family==WeaponFamily.Bow?1.4f:definition.TwoHanded?1.55f:1.05f;
                model.transform.localScale*=height/Mathf.Max(.01f,b.size.y);b=BoundsOf(root);
                float grip=definition.Family==WeaponFamily.Shield||definition.Family==WeaponFamily.Bow?height*.5f:definition.Family==WeaponFamily.Staff?height*.4f:height*.15f;
                model.transform.localPosition+=new Vector3(-b.center.x,-b.min.y-grip,-b.center.z);
                if(PrefabUtility.SaveAsPrefabAsset(root,Output+definition.VisualKey+".prefab")==null)throw new IOException(definition.Id);
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
