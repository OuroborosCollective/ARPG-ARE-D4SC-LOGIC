using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AshenCrypt.WorldForge.EditorTools;
using UnityEditor;
using UnityEngine;

namespace AshenCrypt.ArtReview
{
    // Install this optional acceptance fixture in an isolated project's Assets/Editor.
    public static class AshenArtIntakeProbe
    {
        const string Review = "Assets/PrivateArt/ArtTechniqueReview";

        [Serializable] class Model
        {
            public string name, job, guid;
            public int triangles, vertices, bones, clips;
            public int[] lodTriangles;
            public float poseDeltaMetres, lowestY, minGroundAcrossClip, maxGroundAcrossClip;
        }
        [Serializable] class Receipt
        {
            public string unity, device, pipeline, shader;
            public int livingParticles;
            public List<Model> models = new List<Model>();
        }

        static void Require(bool value, string detail)
        {
            if (!value) throw new InvalidOperationException(detail);
        }

        static Texture2D Map(string name, bool srgb, bool normal = false)
        {
            string path = Review + "/" + name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer != null, path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.SaveAndReimport();
            var map = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Require(map != null && map.width >= 512, path);
            return map;
        }

        static Material MakeMaterial()
        {
            string path = Review + "/AshenAtlas.mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Require(shader != null && !ShaderUtil.ShaderHasError(shader), "URP Lit compilation");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.SetTexture("_BaseMap", Map("albedo", true));
            mat.SetTexture("_BumpMap", Map("normal", false, true));
            mat.SetTexture("_MetallicGlossMap", Map("metalSmooth", false));
            mat.SetTexture("_EmissionMap", Map("emission", true));
            mat.SetColor("_EmissionColor", Color.white * 1.5f);
            mat.SetFloat("_Smoothness", 1f);
            mat.EnableKeyword("_NORMALMAP"); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); mat.EnableKeyword("_EMISSION");
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat); AssetDatabase.SaveAssetIfDirty(mat);
            return mat;
        }

        static Model Import(string name, string id, Material material, Transform staging)
        {
            Require(WFBlenderIntake.State(id) == WFBlenderJobState.Ready, "World Forge " + id);
            var asset = WFBlenderIntake.ImportFbx(id);
            string path = AssetDatabase.GetAssetPath(asset);
            string guid = AssetDatabase.AssetPathToGUID(path);
            Require(WFBlenderIntake.ImportFbx(id) && guid == AssetDatabase.AssetPathToGUID(path), "Reimport GUID " + name);
            asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = UnityEngine.Object.Instantiate(asset, staging);
            go.name = name;
            var r = new Model { name = name, job = id, guid = guid };
            var items = go.GetComponentsInChildren<Renderer>();
            foreach (var rr in items) rr.sharedMaterials = rr.sharedMaterials.Select(x => material).ToArray();
            var lods = new List<LOD>();
            var levelCounts = new List<int>();
            foreach (var level in new[] { 0, 1, 2 })
            {
                var group = items.Where(x => x.name.EndsWith("_LOD" + level, StringComparison.Ordinal)).ToArray();
                Require(group.Length == 1, "Expected one renderer for "+name+" LOD"+level+" found "+string.Join(",",items.Select(a=>a.name)));
                Mesh mesh = group[0] is SkinnedMeshRenderer sr ? sr.sharedMesh : group[0].GetComponent<MeshFilter>().sharedMesh;
                levelCounts.Add(Enumerable.Range(0,mesh.subMeshCount).Sum(i => (int)mesh.GetIndexCount(i)/3));
                lods.Add(new LOD(new[] { .55f,.24f,.045f }[level], group));
            }
            r.lodTriangles = levelCounts.ToArray();
            Require(levelCounts[0] > levelCounts[1] && levelCounts[1] > levelCounts[2], "LOD order "+name);
            var nativeLod = go.GetComponentInChildren<LODGroup>() ?? go.AddComponent<LODGroup>();
            nativeLod.SetLODs(lods.ToArray()); nativeLod.RecalculateBounds();
            var skinned = go.GetComponentsInChildren<SkinnedMeshRenderer>();
            var meshes = go.GetComponentsInChildren<MeshFilter>().Select(x => x.sharedMesh)
                .Concat(skinned.Select(x => x.sharedMesh)).ToArray();
            Require(meshes.Length > 0 && meshes.All(x => x && x.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0)), "Missing mesh or UV0 " + name);
            r.vertices = meshes.Sum(x => x.vertexCount);
            r.triangles = meshes.Sum(x => Enumerable.Range(0, x.subMeshCount).Sum(i => (int)x.GetIndexCount(i) / 3));
            r.lowestY = items.Min(x => x.bounds.min.y);
            if (name != "Aschenhorcher") Require(Math.Abs(r.lowestY) < .02f, "Ground " + name + " " + r.lowestY);
            if (name == "Aschenhorcher")
            {
                Require(skinned.Length > 0, "Missing skin");
                var skin = skinned.OrderByDescending(x => x.sharedMesh.vertexCount).First();
                r.bones = skin.bones.Length;
                var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .Where(x => !x.name.StartsWith("__preview__") && x.length > 0).ToArray();
                r.clips = clips.Length;
                Require(r.bones >= 16 && clips.Length > 0, "Missing creature rig or clips");
                var m = new Mesh();
                clips[0].SampleAnimation(go, 0); skin.BakeMesh(m); var start = m.vertices;
                clips[0].SampleAnimation(go, clips[0].length * .25f); skin.BakeMesh(m); var mid = m.vertices;
                r.poseDeltaMetres = start.Zip(mid, (a, b) => Vector3.Distance(a,b)).Max();
                r.minGroundAcrossClip=float.PositiveInfinity; r.maxGroundAcrossClip=float.NegativeInfinity;
                for (int i=0;i<=24;i++)
                {
                    clips[0].SampleAnimation(go, clips[0].length*i/24f); skin.BakeMesh(m);
                    float floor=m.vertices.Min(v=>skin.transform.TransformPoint(v).y);
                    r.minGroundAcrossClip=Math.Min(r.minGroundAcrossClip,floor);
                    r.maxGroundAcrossClip=Math.Max(r.maxGroundAcrossClip,floor);
                }
                Require(Math.Max(Math.Abs(r.minGroundAcrossClip),Math.Abs(r.maxGroundAcrossClip))<.008f, "Animated feet: "+r.minGroundAcrossClip+" / "+r.maxGroundAcrossClip);
                UnityEngine.Object.DestroyImmediate(m);
                Require(r.poseDeltaMetres > .001f, "Clip has no visible deformation");
            }
            return r;
        }

        public static void Run()
        {
            string jobsFile = Environment.GetEnvironmentVariable("ASHEN_ART_JOBS");
            string receipt = Environment.GetEnvironmentVariable("ASHEN_ART_RECEIPT");
            Require(!string.IsNullOrEmpty(receipt) && File.Exists(jobsFile), "Missing jobs or evidence destination");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var mat = MakeMaterial();
            var fxShader = AssetDatabase.LoadAssetAtPath<Shader>(Review + "/Embers.shadergraph");
            Require(fxShader != null && !ShaderUtil.ShaderHasError(fxShader), "Upstream Embers Shader Graph compilation");
            var root = new GameObject("Ossuar – Werkprobe");
            var result = new Receipt { unity = Application.unityVersion, device = SystemInfo.graphicsDeviceName,
                pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().Name, shader = fxShader.name };
            foreach (string line in File.ReadAllLines(jobsFile))
            {
                var parts = line.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
                result.models.Add(Import(parts[0], parts[1], mat, root.transform));
            }
            Require(result.models.Count == 4 && result.models.Select(x=>x.name).Distinct().Count() == 4, "Require four specimens");
            root.transform.Find("Ossuarbogen").localPosition = new Vector3(0,0,3f);
            root.transform.Find("Gluthelmlinge").localPosition = new Vector3(-1.65f,0,-.6f);
            root.transform.Find("Schieferbruch").localPosition = new Vector3(1.6f,0,1.3f);
            var sparks = new GameObject("Glutsporen"); sparks.transform.SetParent(root.transform.Find("Gluthelmlinge"),false);
            var ps = sparks.AddComponent<ParticleSystem>(); ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.useAutoRandomSeed = false; ps.randomSeed = 7619;
            var main = ps.main; main.startColor = new Color(1f,.27f,.06f); main.startSpeed = .14f;
            main.startLifetime = 2.7f; main.startSize = .025f; main.maxParticles = 16;
            var emission = ps.emission; emission.rateOverTime = 3f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .28f;
            var psRenderer = sparks.GetComponent<ParticleSystemRenderer>();
            var fxMat = AssetDatabase.LoadAssetAtPath<Material>(Review + "/Spore.mat");
            if (!fxMat) { var sf=Shader.Find("Universal Render Pipeline/Particles/Unlit"); Require(sf && !ShaderUtil.ShaderHasError(sf),"Particle shader"); fxMat=new Material(sf); AssetDatabase.CreateAsset(fxMat,Review+"/Spore.mat"); }
            psRenderer.sharedMaterial=fxMat;
            ps.Simulate(1.5f,true,true,true); result.livingParticles=ps.particleCount;
            Require(result.livingParticles > 0 && result.livingParticles <= main.maxParticles,"Particle simulation");
            string prefab = Review + "/Ossuar-Werkprobe.prefab";
            PrefabUtility.SaveAsPrefabAsset(root,prefab,out bool saved);
            Require(saved && AssetDatabase.LoadAssetAtPath<GameObject>(prefab).GetComponentsInChildren<Renderer>().Length >= 5,"Prefab reload");
            UnityEngine.Object.DestroyImmediate(root);
            File.WriteAllText(receipt, JsonUtility.ToJson(result,true));
        }
    }
}
