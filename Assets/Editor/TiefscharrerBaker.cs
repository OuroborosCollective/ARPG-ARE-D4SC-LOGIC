using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AreArpg.EditorTools
{
    public static class TiefscharrerBaker
    {
        private const string Folder="Assets/Resources/Equipment/Tiefscharrer/";
        // Place support points on the authored skin in bind pose. A paw joint lies above the
        // sole and cannot tell the running projection where the clay floor actually is.
        private static void BakeGroundContacts(Transform form, SkinnedMeshRenderer skin)
        {
            var posed = new Mesh();
            try {
                skin.BakeMesh(posed);
                var toForm = form.worldToLocalMatrix * skin.transform.localToWorldMatrix;
                var points = posed.vertices;
                for (int i = 0; i < points.Length; i++) points[i] = toForm.MultiplyPoint3x4(points[i]);
                var bones = skin.bones;
                foreach (string pawName in new[] { "ForePaw_L", "ForePaw_R", "HindPaw_L", "HindPaw_R" }) {
                    var paw = bones.Single(b => b.name == "TS_" + pawName);
                    var center = form.InverseTransformPoint(paw.position);
                    // Cover the front, heel and both sides. During roll the edge in contact changes.
                    var support = new Vector3[4];
                    for (int i = 0; i < support.Length; i++) support[i].y = float.PositiveInfinity;
                    foreach (var point in points) {
                        float x = point.x - center.x, z = point.z - center.z;
                        if (Mathf.Abs(x) > .27f || Mathf.Abs(z) > .32f) continue;
                        int sector = (x < 0 ? 0 : 1) + (z < 0 ? 0 : 2);
                        if (point.y < support[sector].y) support[sector] = point;
                    }
                    for (int i = 0; i < support.Length; i++) {
                        if (float.IsInfinity(support[i].y)) throw new InvalidOperationException("Missing sole surface " + pawName);
                        var contact = new GameObject("TS_Sole_" + pawName + "_" + i).transform;
                        contact.SetParent(paw, false);
                        contact.position = form.TransformPoint(support[i]);
                    }
                }
            } finally { UnityEngine.Object.DestroyImmediate(posed); }
        }

        [MenuItem("ARE ARPG/Art/Bake Tiefscharrer")]
        public static void Bake()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(Folder+"Tiefscharrer.fbx") as ModelImporter;
            if (!importer)throw new FileNotFoundException(Folder+"Tiefscharrer.fbx");
            bool change=importer.optimizeGameObjects || importer.generateSecondaryUV || !importer.isReadable ||
                importer.importAnimation || !importer.bakeAxisConversion;
            if (change) {
                importer.optimizeGameObjects=false;importer.generateSecondaryUV=false;importer.isReadable=true;
                importer.importAnimation=false;importer.bakeAxisConversion=true;importer.SaveAndReimport();
            }
            const string maps=Folder+"Tiefscharrer_";
            Texture2D Texture(string suffix, bool linear=false, bool normal=false)
            {
                var path=maps+suffix+".png";var ti=AssetImporter.GetAtPath(path) as TextureImporter;
                if(!ti)throw new FileNotFoundException(path);
                if(ti.sRGBTexture==linear || (normal && ti.textureType!=TextureImporterType.NormalMap)) {
                    ti.sRGBTexture=!linear;if(normal)ti.textureType=TextureImporterType.NormalMap;ti.SaveAndReimport();
                }
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if(!texture)throw new IOException(path);
                return texture;
            }
            var albedo=Texture("BaseColor");var roughMetal=Texture("MetalSmooth",true);var normalMap=Texture("Normal",true,true);
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Tiefscharrer_Atlas.mat");
            if(!mat) {
                var shader=Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if(!shader || !shader.isSupported)throw new InvalidOperationException("Missing supported lit shader.");
                mat=new Material(shader);AssetDatabase.CreateAsset(mat,Folder+"Tiefscharrer_Atlas.mat");
            }
            mat.mainTexture=albedo;mat.color=Color.white;
            if(mat.HasProperty("_BumpMap")){mat.SetTexture("_BumpMap",normalMap);mat.EnableKeyword("_NORMALMAP");}
            if(mat.HasProperty("_MetallicGlossMap")){
                mat.SetTexture("_MetallicGlossMap",roughMetal);mat.EnableKeyword("_METALLICSPECGLOSSMAP");mat.EnableKeyword("_METALLICGLOSSMAP");
            }
            if(mat.HasProperty("_Smoothness"))mat.SetFloat("_Smoothness",.74f);
            if(mat.HasProperty("_BumpScale"))mat.SetFloat("_BumpScale",.68f);
            EditorUtility.SetDirty(mat);
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"Tiefscharrer.fbx");var instance=UnityEngine.Object.Instantiate(model);
            try{
                instance.name="Tiefscharrer_World";
                foreach(var group in instance.GetComponentsInChildren<LODGroup>(true)) UnityEngine.Object.DestroyImmediate(group);
                foreach(var animator in instance.GetComponentsInChildren<Animator>(true))UnityEngine.Object.DestroyImmediate(animator);
                var transforms=instance.GetComponentsInChildren<Transform>(true);
                foreach(string n in new[]{"TS_Root","TS_Nose","TS_Brand","TS_ForePaw_L","TS_ForePaw_R","TS_HindPaw_L","TS_HindPaw_R"})
                    if(transforms.Count(t=>t.name==n)!=1)throw new InvalidOperationException("Missing unique rest socket "+n);
                var renderers=instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var levels=new LOD[3];long previous=long.MaxValue;
                for(int i=0;i<3;i++){
                    string key="Tiefscharrer_LOD"+i;
                    var skin=renderers.Single(s=>s.name==key);var mesh=skin.sharedMesh;
                    if(!mesh || skin.bones.Length<16 || !mesh.HasVertexAttribute(VertexAttribute.TexCoord0) ||
                       (i<2 && !mesh.HasVertexAttribute(VertexAttribute.TexCoord1)))throw new InvalidOperationException("Invalid skinned source "+key);
                    long triangles=Enumerable.Range(0,mesh.subMeshCount).Sum(sub=>(long)mesh.GetIndexCount(sub)/3);
                    if(triangles<=0 || triangles>=previous)throw new InvalidOperationException("Invalid LOD geometry "+key);
                    previous=triangles;skin.sharedMaterials=Enumerable.Repeat(mat,mesh.subMeshCount).ToArray();
                    skin.enabled=true;skin.gameObject.SetActive(true);
                    // Unity's renderer culling uses this bound even when the live driver is on a sibling.
                    // The imported animal extends its front paws and lowers its jaw during the source clips.
                    var bound=skin.localBounds;bound.Expand(bound.size*.30f);skin.localBounds=bound;
                    levels[i]=new LOD(new[]{.16f,.055f,.001f}[i],new Renderer[]{skin});
                }
                BakeGroundContacts(instance.transform, renderers.Single(r => r.name == "Tiefscharrer_LOD0"));
                var lod=instance.AddComponent<LODGroup>();lod.SetLODs(levels);lod.RecalculateBounds();
                if(!PrefabUtility.SaveAsPrefabAsset(instance,Folder+"Tiefscharrer_World.prefab"))throw new IOException("Creature prefab");
                AssetDatabase.SaveAssets();
            } finally {UnityEngine.Object.DestroyImmediate(instance);}
        }
    }
}
