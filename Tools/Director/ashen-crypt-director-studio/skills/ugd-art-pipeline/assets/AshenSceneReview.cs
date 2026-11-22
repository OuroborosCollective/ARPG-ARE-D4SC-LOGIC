using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AshenCrypt.ArtReview
{
    // Read-only, batch-mode observation of the explicit scene in an isolated project.
    public static class AshenSceneReview
    {
        [Serializable] class Finding {public string path, observation, technique;}
        [Serializable] class Report
        {
            public string scene, unity;
            public int renderers, particles, lodGroups;
            public List<Finding> findings = new List<Finding>();
            public List<string> shaders = new List<string>();
        }
        static string Hierarchy(Transform t)
        {
            return t.parent ? Hierarchy(t.parent)+"/"+t.name : t.name;
        }
        public static void Run()
        {
            string path=Environment.GetEnvironmentVariable("ASHEN_REVIEW_SCENE");
            string outFile=Environment.GetEnvironmentVariable("ASHEN_SCENE_RECEIPT");
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/",StringComparison.Ordinal) ||
                path.Contains("..") || !path.EndsWith(".unity",StringComparison.Ordinal) || !File.Exists(path) || !Application.isBatchMode)
                throw new ArgumentException("Use an existing project scene in isolated batch mode.");
            if (string.IsNullOrEmpty(outFile)) throw new ArgumentException("Missing output receipt");
            var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            var report=new Report{scene=scene.path,unity=Application.unityVersion};
            var seen=new HashSet<string>();
            var roots=scene.GetRootGameObjects();
            foreach(var root in roots)
            {
                report.lodGroups+=root.GetComponentsInChildren<LODGroup>(false).Length;
                foreach(var p in root.GetComponentsInChildren<ParticleSystem>(false)){
                    report.particles++;
                    if(p.GetComponent<ParticleSystemRenderer>()?.sharedMaterial==null)
                        report.findings.Add(new Finding{path=Hierarchy(p.transform),observation="Particle renderer lacks material",technique="fx"});
                }
                foreach(var r in root.GetComponentsInChildren<Renderer>(false)){
                    report.renderers++;
                    foreach(var m in r.sharedMaterials){
                        if(!m||!m.shader||ShaderUtil.ShaderHasError(m.shader)||m.shader.name=="Hidden/InternalErrorShader")
                            report.findings.Add(new Finding{path=Hierarchy(r.transform),observation="Missing or erroneous material shader",technique="shader"});
                        else seen.Add(m.shader.name);
                    }
                    if(r is SkinnedMeshRenderer sr && sr.sharedMesh && sr.sharedMesh.bindposes.Length>0 && (sr.bones.Length==0||sr.bones.Any(b=>!b)))
                        report.findings.Add(new Finding{path=Hierarchy(r.transform),observation="Missing referenced skin bone",technique="equipment"});
                }
            }
            report.shaders=seen.OrderBy(x=>x,StringComparer.Ordinal).ToList();
            report.findings=report.findings.OrderBy(f=>f.technique=="fx"||f.technique=="shader"?0:1)
                .ThenBy(f=>f.path,StringComparer.Ordinal).ToList();
            File.WriteAllText(outFile,JsonUtility.ToJson(report,true));
        }
    }
}
