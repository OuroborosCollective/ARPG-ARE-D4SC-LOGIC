using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AreArpg.EditorTools
{
    public static class RelicWorkshop
    {
        private const string Root = "Assets/Resources/AshenRelics/";
        private static Shader Lit => Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        private static Material Surface(string name, Color tint, float metal, float smooth, Color glow = default)
        {
            string path = Root + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) {
                var shader = Lit;
                if (!shader || !shader.isSupported) throw new InvalidOperationException("Missing supported project shader.");
                mat = new Material(shader); AssetDatabase.CreateAsset(mat,path);
            }
            mat.color = tint;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic",metal);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness",smooth);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness",smooth);
            if (glow.maxColorComponent > 0 && mat.HasProperty("_EmissionColor")) {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor",glow);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        [MenuItem("ARE ARPG/Art/Bake Grubenlicht and Tastkranz")]
        public static void Bake()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string src = Root + "Grubenlicht.fbx";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(src);
            if (!source) throw new FileNotFoundException(src);
            var tone = Surface("Ascheton",Color.white,0,.25f);
            string colorPath=Root+"Grubenlicht_Ton.png", normalPath=Root+"Grubenlicht_Poren.png";
            var normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (normalImporter == null) throw new FileNotFoundException(normalPath);
            if (normalImporter.textureType != TextureImporterType.NormalMap) {
                normalImporter.textureType = TextureImporterType.NormalMap; normalImporter.SaveAndReimport();
            }
            var color = AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (!color || !normal) throw new InvalidOperationException("Missing clay textures");
            tone.mainTexture = color;
            if (tone.HasProperty("_BumpMap")) { tone.SetTexture("_BumpMap",normal);tone.EnableKeyword("_NORMALMAP"); }
            var materials = new [] {
                tone, Surface("Hammereisen",new Color(.25f,.26f,.26f),.74f,.43f),
                Surface("Griffmessing",new Color(.57f,.44f,.28f),.8f,.53f),
                Surface("Oelgarn",new Color(.25f,.20f,.16f),0,.1f),
                Surface("Kohledocht",new Color(.10f,.13f,.09f),0,.17f),
                Surface("Gruenspan",new Color(.39f,.61f,.26f),0,.54f,new Color(.37f,.92f,.19f)*1.8f)
            };
            GameObject staged = UnityEngine.Object.Instantiate(source);
            try {
                staged.name="Grubenlicht"; var levels=new LOD[3];
                foreach (var old in staged.GetComponentsInChildren<LODGroup>(true)) UnityEngine.Object.DestroyImmediate(old);
                for (int i=0;i<levels.Length;i++) {
                    string name = "Grubenlicht_LOD"+i;
                    var r=staged.GetComponentsInChildren<MeshRenderer>(true).Single(s=>s.name==name);
                    var original=r.sharedMaterials;
                    if (original.Length!=6) throw new InvalidOperationException(name+" material slots "+original.Length);
                    var remapped=new Material[original.Length];
                    for(int j=0;j<original.Length;j++) {
                        var n=original[j] ? original[j].name : "";
                        if (n.Length<2 || !int.TryParse(n.Substring(0,2),out var idx) || idx<1 || idx>6)
                            throw new InvalidOperationException("Unknown FBX slot "+name+" / "+n);
                        remapped[j]=materials[idx-1];
                    }
                    r.sharedMaterials=remapped; r.enabled=true; r.gameObject.SetActive(true);
                    if(r.bounds.size.y<.3f || r.bounds.size.y>.6f) throw new InvalidOperationException("Lantern outside metre scale: "+r.bounds);
                    levels[i]=new LOD(new[]{.21f,.074f,.004f}[i],new Renderer[]{r});
                }
                var group=staged.AddComponent<LODGroup>();group.SetLODs(levels);group.RecalculateBounds();
                if(!PrefabUtility.SaveAsPrefabAsset(staged,Root+"Grubenlicht_World.prefab")) throw new IOException("Grubenlicht prefab");
            } finally {UnityEngine.Object.DestroyImmediate(staged);}
            var skin=Surface("Tastballenhaut",new Color(.47f,.23f,.21f),0,.34f);
            var ridge=Surface("Tastballenfalten",new Color(.52f,.28f,.24f),0,.31f);
            string nosePath=Root+"Tastkranz.fbx";
            var importer=AssetImporter.GetAtPath(nosePath) as ModelImporter;
            if(importer==null) throw new FileNotFoundException(nosePath);
            var nose=AssetDatabase.LoadAssetAtPath<GameObject>(nosePath);
            var parts=nose.GetComponentsInChildren<MeshRenderer>(true);
            if(parts.Length!=1)throw new InvalidOperationException("Tastkranz mesh");
            var originals=parts[0].sharedMaterials;
            if(originals.Length!=2 || originals.Any(x=>!x)) throw new InvalidOperationException("Tastkranz materials");
            for(int i=0;i<originals.Length;i++) {
                var original=originals[i];
                var target=i==0?skin:ridge;
                if(original==target)continue;
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),original.name),target);
            }
            importer.SaveAndReimport();
            AssetDatabase.SaveAssets();
            var baked=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Grubenlicht_World.prefab");
            if(!baked || baked.GetComponent<LODGroup>().lodCount!=3)throw new InvalidOperationException("Persistent LOD binding");
        }
    }
}
