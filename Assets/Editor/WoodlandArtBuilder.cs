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
        private const string BowSource=Output+"Vendor/AnimationsStarterPack/Combat/BowShot.fbx";
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
                    var existing=importer.GetPlatformTextureSettings("Android");
                    if(!importer.isReadable&&importer.mipmapEnabled&&importer.maxTextureSize<=1024&&existing.overridden&&existing.maxTextureSize==1024&&existing.format==TextureImporterFormat.ASTC_6x6)continue;
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
                var roots=new[]{"Assets/PrivateArt/WoodlandWeapons","Assets/PrivateVendor"};
                // The supplied staff prefabs carry material overrides absent from the raw FBX (notably Ice).
                var paths=AssetDatabase.FindAssets("t:Prefab",roots).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p,StringComparer.Ordinal)
                    .Concat(AssetDatabase.FindAssets("t:Model",roots).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p,StringComparer.Ordinal)).ToArray();
                for(int i=0;i<WeaponCatalog.Count;i++)
                {
                    var def=WeaponCatalog.Get(i);var path=paths.FirstOrDefault(p=>Path.GetFileNameWithoutExtension(p)==def.ModelStem);
                    if(def.ModelStem=="PT_Sword_01_a")path=Output+"EntryWeapon.prefab"; // Existing reviewed URP palette and native grip.
                    if(path==null)throw new IOException("Missing model for "+def.Id+" / "+def.ModelStem);
                    BuildWeapon(path,def);
                }
                BuildBowAnimation();
                AssetDatabase.SaveAssets();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var report=new { catalog=WeaponCatalog.Count,forestBindings=7,modelBindings=Enumerable.Range(0,WeaponCatalog.Count).Select(i=>new {id=WeaponCatalog.Get(i).Id,source=WeaponCatalog.Get(i).ModelStem}).ToArray() };
                string evidence=Environment.GetEnvironmentVariable("ARE_WOODLAND_AUTHOR_REPORT");
                if(!string.IsNullOrEmpty(evidence))File.WriteAllText(evidence,Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
                if(Application.isBatchMode)EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
        }

        // This FBX also contains a one-frame tpose. Asset enumeration order is not a take contract.
        private static AnimationClip SelectBowTake(UnityEngine.Object[] assets)
        {
            var matches=assets.OfType<AnimationClip>().Where(c=>string.Equals(c.name,"BowShot",StringComparison.Ordinal)).ToArray();
            if(matches.Length!=1)throw new InvalidOperationException("Expected exactly one reviewed BowShot take in "+BowSource);
            var shot=matches[0];
            if(!shot.humanMotion||shot.legacy||shot.frameRate<=0||shot.length*shot.frameRate<=1.5f)
                throw new InvalidOperationException("BowShot must be the moving humanoid take, not a one-frame reference pose");
            return shot;
        }

        /// <summary>Rebuild only the shared bow projection; keep its identity and all private source models.</summary>
        [MenuItem("ARE/Art/Rebuild Shared Bow Animation")]
        public static void BuildBowAnimation()
        {
            var bow=SelectBowTake(AssetDatabase.LoadAllAssetsAtPath(BowSource));
            var clip=UnityEngine.Object.Instantiate(bow);
            try
            {
                clip.name="WeaponBowShot";
                AnimationUtility.SetAnimationEvents(clip,Array.Empty<AnimationEvent>());
                var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=false;
                AnimationUtility.SetAnimationClipSettings(clip,settings);
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),AreArpg.UnityClient.EntryWeaponGrip.WeightParameter),AnimationCurve.Constant(0,clip.length,0));
                string path=Output+"WeaponBowShot.anim";
                var previous=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if(previous!=null){EditorUtility.CopySerialized(clip,previous);EditorUtility.SetDirty(previous);AssetDatabase.SaveAssetIfDirty(previous);}
                else{AssetDatabase.CreateAsset(clip,path);AssetDatabase.SaveAssetIfDirty(clip);}
                string evidence=Environment.GetEnvironmentVariable("ARE_BOW_AUTHOR_REPORT");
                if(!string.IsNullOrEmpty(evidence))File.WriteAllText(evidence,Newtonsoft.Json.JsonConvert.SerializeObject(new
                {source=BowSource,take=bow.name,duration=bow.length,frameRate=bow.frameRate,output=path,guid=AssetDatabase.AssetPathToGUID(path)},Newtonsoft.Json.Formatting.Indented));
            }
            finally{if(!AssetDatabase.Contains(clip))UnityEngine.Object.DestroyImmediate(clip);}
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
