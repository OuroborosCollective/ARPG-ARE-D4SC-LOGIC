using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AshenCrypt.ArtReview
{
    public static class AshenEnvironmentProof
    {
        const string Dir = "Assets/PrivateArt/ArtTechniqueReview";
        [Serializable] class Measurement
        {
            public string unity, renderer, scene;
            public int width, height, changedPixels, totalPixels, warmOn, warmOff, sparks, repeatDifference;
            public float maskCoverage;
        }
        static void Check(bool ok, string detail) { if (!ok) throw new InvalidOperationException(detail); }
        static Material Mat(string asset, string shaderName)
        {
            var shader=Shader.Find(shaderName);
            Check(shader && !ShaderUtil.ShaderHasError(shader),shaderName);
            string path=Dir+"/"+asset+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m=new Material(shader); AssetDatabase.CreateAsset(m,path); }
            m.shader=shader; return m;
        }
        static Texture2D Map(string key, bool normal=false)
        {
            string path=Dir+"/"+key+".png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=!normal; importer.alphaSource=TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency=false; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static GameObject ObjectWith(string name, PrimitiveType shape, Vector3 xyz, Vector3 scale, Material mat)
        {
            var go=GameObject.CreatePrimitive(shape); go.name=name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position=xyz; go.transform.localScale=scale; go.GetComponent<Renderer>().sharedMaterial=mat;return go;
        }
        static Mesh Ribbon()
        {
            var vs=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
            for(int i=0;i<=48;i++){
                float z=-3.8f+i*.17f;
                float x=.62f+Mathf.Sin(z*1.35f)*.26f+.14f*Mathf.Sin(z*3.4f);
                float width=(.10f+.055f*(1+Mathf.Sin(z*4.1f)))*(.2f+.8f*Mathf.Sin(i*Mathf.PI/48f));
                for(int j=0;j<2;j++){vs.Add(new Vector3(x+(j==0?-width:width),.016f,z));uv.Add(new Vector2(j,i*.115f));}
                if(i>0){int k=i*2;indices.AddRange(new[]{k-2,k,k-1,k-1,k,k+1});}
            }
            var mesh=new Mesh {name="Erstarrte Fließkante"};mesh.SetVertices(vs);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
        static void Place(GameObject asset, Vector3 xyz, Vector3 scale,float yaw)
        {
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset);
            instance.transform.SetPositionAndRotation(xyz,Quaternion.Euler(0,yaw,0));instance.transform.localScale=scale;
        }
        static Light LightAt(string name,LightType type,Vector3 pos,Vector3 angles,Color color,float intensity,float range)
        {
            var go=new GameObject(name);go.transform.position=pos;go.transform.rotation=Quaternion.Euler(angles);
            var light=go.AddComponent<Light>();light.type=type;light.color=color;light.intensity=intensity;light.range=range;return light;
        }
        static Texture2D Capture(Camera cam,string path)
        {
            var old=RenderTexture.active;var rt=RenderTexture.GetTemporary(960,600,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            cam.targetTexture=rt;
            try{
                cam.Render();RenderTexture.active=rt;
                var img=new Texture2D(960,600,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,960,600),0,0);img.Apply();
                File.WriteAllBytes(path,img.EncodeToPNG());return img;
            }
            finally { cam.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt); }
        }
        static bool Warm(Color32 p) {return p.r>95 && p.r>p.g*1.34f && p.g>p.b*1.18f;}
        public static void Run()
        {
            string output=Environment.GetEnvironmentVariable("ASHEN_ENV_CAPTURE");
            Check(!string.IsNullOrWhiteSpace(output),"Capture directory");
            Directory.CreateDirectory(output);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ShaderUtil.allowAsyncCompilation=false;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.32f,.35f,.40f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.050f;RenderSettings.fogColor=new Color(.26f,.30f,.36f);
            var ground=Mat("Schieferasche","ARE/World/Schneeasche");
            ground.SetTexture("_Mask",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AshenCrypt/BorderlandArt/AshSnowMask.png"));
            Check(ground.GetTexture("_Mask"),"Project ash mask");ground.SetFloat("_Metres",3.2f);
            ObjectWith("Schneeasche",PrimitiveType.Plane,Vector3.zero,new Vector3(6,1,6),ground);
            var lava=Mat("Lavasaum","AshenCrypt/Art/Crusted Lava");
            lava.SetTexture("_Crust",Map("CrustHeat"));lava.SetTexture("_BumpMap",Map("CrustNormal",true));lava.SetFloat("_Phase",.7f);
            var ribbon=new GameObject("Schmale Lavaader");string meshPath=Dir+"/Lavasaum.asset";
            var curve=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(!curve){curve=Ribbon();AssetDatabase.CreateAsset(curve,meshPath);}
            ribbon.AddComponent<MeshFilter>().sharedMesh=curve;ribbon.AddComponent<MeshRenderer>().sharedMaterial=lava;
            var specimen=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"/Ossuar-Werkprobe.prefab");
            Check(specimen,"Intake assemblage");
            var master=(GameObject)PrefabUtility.InstantiatePrefab(specimen);
            master.name="Modelle aus dem Asset-Eingang";
            // Environment review uses only the existing imported prop instances.
            var actor=master.transform.Find("Aschenhorcher");actor.gameObject.SetActive(false);
            var fungus=master.transform.Find("Gluthelmlinge");fungus.localPosition=new Vector3(-1.18f,0,.01f);fungus.localScale=Vector3.one*.80f;
            var originalSparks=fungus.GetComponentInChildren<ParticleSystem>();originalSparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var ashRock=master.transform.Find("Schieferbruch").gameObject;
            TransformScatter(ashRock,master.transform);
            var pine=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ARE/Borderland/AshPine.prefab");
            if(pine) for(int i=0;i<15;i++) Place(pine,new Vector3(-13.2f+i*1.7f,0,9.2f+3*Mathf.Sin(i*.6f)),Vector3.one*(.75f+.22f*Mathf.Sin(i*2f)),i*57f);
            var snag=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ARE/Borderland/EmberSnag.prefab");
            if(snag) Place(snag,new Vector3(-2.4f,0,2.6f),Vector3.one*.68f,32);
            var flame=Mat("Kohlenfeuer","AshenCrypt/Flame");flame.SetFloat("_Seed",3.2f);
            var left=ObjectWith("Feuer an der Bruchkante",PrimitiveType.Quad,new Vector3(-1.67f,.55f,1.25f),new Vector3(.76f,1.12f,1),flame);
            var atmosphere=Mat("Glutflug","AshenCrypt/Atmosphere");atmosphere.SetTexture("_MainTex",Map("EmberFleck"));atmosphere.SetFloat("_Glow",.82f);
            var ps=new GameObject("Glutflug über der Bruchkante").AddComponent<ParticleSystem>();ps.transform.position=left.transform.position;ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.useAutoRandomSeed=false;ps.randomSeed=7619;
            var main=ps.main;main.startLifetime=2.1f;main.startSpeed=.32f;main.startSize=.035f;main.startColor=new Color(1,.55f,.23f);main.maxParticles=14;
            var em=ps.emission;em.rateOverTime=5;var sh=ps.shape;sh.shapeType=ParticleSystemShapeType.Sphere;sh.radius=.15f;
            var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.y=new ParticleSystem.MinMaxCurve(.30f);velocity.x=new ParticleSystem.MinMaxCurve(.06f);
            var pr=ps.GetComponent<ParticleSystemRenderer>();pr.sharedMaterial=atmosphere;ps.Simulate(1.4f,true,true,true);
            var sun=LightAt("Bedeckter Himmel",LightType.Directional,Vector3.zero,new Vector3(42,-44,0),new Color(.83f,.88f,1),1.15f,0);
            RenderSettings.sun=sun;var glow=LightAt("Feuerschein",LightType.Point,left.transform.position,Vector3.zero,new Color(1,.32f,.10f),2.2f,2.7f);
            var cam=new GameObject("Werkstattkamera").AddComponent<Camera>();cam.transform.position=new Vector3(2.75f,1.72f,-4.65f);cam.transform.LookAt(new Vector3(-.13f,.72f,1.1f));cam.fieldOfView=50;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=RenderSettings.fogColor;cam.nearClipPlane=.05f;cam.farClipPlane=95;cam.allowHDR=false;
            string scenePath=Dir+"/Aschepass.unity";
            flame.SetFloat("_Intensity",1.18f);lava.SetFloat("_Heat",1.36f);
            foreach(var m in new[]{ground,lava,flame,atmosphere}){EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);}
            Check(EditorSceneManager.SaveScene(scene,scenePath),"Save projection");
            var warmup=Capture(cam,Path.Combine(output,"Warmup.png"));UnityEngine.Object.DestroyImmediate(warmup);
            // An untimed second draw confirms shader/LOD resources have settled before comparing.
            var stable=Capture(cam,Path.Combine(output,"Stability.png"));
            var on=Capture(cam,Path.Combine(output,"Aschepass-Unity.png"));
            flame.SetFloat("_Intensity",0);lava.SetFloat("_Heat",0);pr.enabled=false;glow.enabled=false;
            var off=Capture(cam,Path.Combine(output,"Aschepass-ohne-Glut.png"));
            // Isolate the lava contribution from flame, point light and particles.
            lava.SetFloat("_Heat",1.36f);
            var lavaOnly=Capture(cam,Path.Combine(output,"Aschepass-Lavasaum.png"));
            var a=on.GetPixels32();var b=off.GetPixels32();var l=lavaOnly.GetPixels32();var stablePixels=stable.GetPixels32();int changed=0,mask=0,wo=0,wf=0,repeat=0;
            for(int i=0;i<a.Length;i++){
                if(Math.Max(Math.Abs(a[i].r-stablePixels[i].r),Math.Abs(a[i].g-stablePixels[i].g))>12)repeat++;
                if(Math.Max(Math.Abs(a[i].r-b[i].r),Math.Abs(a[i].g-b[i].g))>12)changed++;
                if(l[i].r>b[i].r+12)mask++;
                if(Warm(a[i]))wo++;if(Warm(b[i]))wf++;
            }
            flame.SetFloat("_Intensity",1.18f);pr.enabled=true;glow.enabled=true;
            var result=new Measurement{unity=Application.unityVersion,renderer=SystemInfo.graphicsDeviceName,scene=scenePath,width=960,height=600,changedPixels=changed,repeatDifference=repeat,totalPixels=a.Length,warmOn=wo,warmOff=wf,sparks=ps.particleCount,maskCoverage=(float)mask/a.Length};
            File.WriteAllText(Path.Combine(output,"environment.json"),JsonUtility.ToJson(result,true));
            Check(repeat<a.Length*.005f && changed>250 && changed<a.Length*.25f && mask>70 && mask<a.Length*.15f && wo>wf && result.sparks>0,"Effect readback "+JsonUtility.ToJson(result));
        }
        static void TransformScatter(GameObject template,Transform root)
        {
            for(int i=0;i<25;i++){
                float x=-17f+i*1.35f;
                var rock=UnityEngine.Object.Instantiate(template,root);rock.name="Schieferrücken "+i;
                rock.transform.localPosition=new Vector3(x,-.22f,7.3f+Mathf.Sin(i*.8f)*1.7f);
                rock.transform.localRotation=Quaternion.Euler(-5,i*53,0);
                rock.transform.localScale=new Vector3(2.15f,1.6f+1.15f*Mathf.Cos(i*.9f),1.85f);
            }
            for(int i=0;i<17;i++){
                var rock=UnityEngine.Object.Instantiate(template,root);rock.name="Schiefer am Weg "+i;
                float side=i%2==0?-1:1;rock.transform.localPosition=new Vector3(side*(1.5f+.28f*Mathf.Sin(i*1.4f)),-.03f,-1.1f+i*.32f);
                rock.transform.localScale=Vector3.one*(.08f+.13f*(1+Mathf.Sin(i*.9f)));rock.transform.localRotation=Quaternion.Euler(0,i*38,0);
            }
        }
    }
}
