using System;
using System.Collections.Generic;
using System.Linq;
using AreArpg.UnityClient;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AreArpg.Editor
{
    public static partial class FieldPauldronPrivateFitBaker
    {
        private const string PrivateRoot = "Assets/PrivateArt/BozoCharacters/";
        private const string ClothesRoot = "Assets/BoZo_StylizedModularCharacters/Prefabs/Fantasy/Resources/";

        private sealed class WardrobeSource
        {
            public string id, top;
            public string[] uniform, hair;
        }

        // The module names are the equipped slots of BozoCharacters/figures.json. Only proved
        // source triangles may be removed. Body/head/legs, weights, material, UVs and original
        // asset references survive a round trip between old and new equipment.
        public static void BakeRegionalFits()
        {
            if (!AssetDatabase.IsValidFolder(ClothesRoot.TrimEnd('/'))) return;
            // The wardrobes of 2026-11-24 (Dark-Souls looks): the hero's coat with pauldrons and hooded cape; Mara's blouse
            // with nothing over it, so only the covered front and back of her top give way to the work outfit.
            RegionalVariant(new WardrobeSource { id = "hero", top="Top/Top_VanguardCoat", uniform = new[] {
                "Shoulders/Shoulders_SoldiersPauldrons", "Neck/Neck_HoodedCape" },
                hair = new[] {"HairFront/HairFront_FreeTussle","HairBack/HairBack_ShortChoppy"} }, "EntryHero");
            RegionalVariant(new WardrobeSource { id = "mara", top="Top/Top_WitchBlouse", uniform = Array.Empty<string>(),
                hair = Array.Empty<string>() }, "EntryNpc1");
            AssetDatabase.SaveAssets();
        }

        private static void RegionalVariant(WardrobeSource recipe, string role)
        {
            string prefix = PrivateRoot+recipe.id+"/"+recipe.id;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ARE/Visuals/"+role+".prefab");
            if (!prefab) throw new InvalidOperationException("Privater Spielkörper fehlt: "+role);
            var inspection=Object.Instantiate(prefab);
            inspection.hideFlags=HideFlags.HideAndDontSave;
            try { RegionalVariant(recipe,prefix,prefab,inspection,role); }
            finally { Object.DestroyImmediate(inspection); }
        }

        private static void RegionalVariant(WardrobeSource recipe,string prefix,GameObject prefab,GameObject inspection,string role)
        {
            var avatar = inspection.GetComponentsInChildren<Animator>(true).Single(a=>a.avatar && a.isHuman);
            avatar.cullingMode=AnimatorCullingMode.AlwaysAnimate;avatar.Rebind();avatar.Update(0f);
            var skin = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s=>s.sharedMesh &&
                s.sharedMesh.name == recipe.id+"_GameMesh");
            var plain = AssetDatabase.LoadAssetAtPath<Mesh>(prefix+"_Mesh.asset");
            if (!plain) throw new InvalidOperationException("Private Modulgeometrie fehlt: "+prefix);
            var input = ReadableCopy(plain); var shaped = ReadableCopy(skin.sharedMesh);
            try
            {
                var indices = input.triangles;
                if (!indices.SequenceEqual(shaped.triangles) || input.vertexCount != shaped.vertexCount)
                    throw new InvalidOperationException("Original und Spielfassung besitzen verschiedene Modulindizes.");
                var points = input.vertices;
                var targets = Components(points,indices);
                var uniform = new HashSet<int>(); var hair = new HashSet<int>();
                foreach (string module in recipe.uniform.Concat(recipe.hair))
                {
                    var candidates=recipe.uniform.Contains(module)?uniform:hair;
                    MatchWardrobePart(module, targets, candidates, indices, points, skin.bones, input.boneWeights);
                }
                var untrimmed = new HashSet<int>(uniform); untrimmed.UnionWith(hair);
                if ((recipe.uniform.Length>0 && uniform.Count==0) || (recipe.hair.Length>0 && hair.Count==0) || untrimmed.Count!=uniform.Count+hair.Count)
                    throw new InvalidOperationException("Arbeitskleidung oder Kopfgeometrie nicht getrennt");
                var originalTop=new HashSet<int>();
                MatchTopSurfaces(recipe.top,originalTop,indices,points,skin.bones,input.boneWeights,input.bindposes);
                if (originalTop.Overlaps(untrimmed)) throw new InvalidOperationException("Oberkleidung überlagert fremde Modulindizes");
                TrimCoveredClothing(avatar,skin,shaped,indices,originalTop,uniform,hair);
                var any = new HashSet<int>(uniform); any.UnionWith(hair);
                var profilePath = "Assets/Resources/ARE/Visuals/RegionalFit_"+role+".asset";
                var fit = AssetDatabase.LoadAssetAtPath<RegionalBodyFit>(profilePath);
                if (!fit) {fit=ScriptableObject.CreateInstance<RegionalBodyFit>(); AssetDatabase.CreateAsset(fit,profilePath);}
                fit.Original=skin.sharedMesh;
                fit.Uniform=Filtered(shaped,indices,uniform,prefix+"_RegionalBody.asset");
                // The mill worker wears an open brow band. Her braid is part of the approved silhouette.
                // Only a character actually wearing a closed helmet needs a hair-free skin variant.
                fit.HoodOnly=hair.Count>0 ? Filtered(shaped,indices,hair,prefix+"_RegionalHood.asset") : null;
                fit.HoodedUniform=hair.Count>0 ? Filtered(shaped,indices,any,prefix+"_RegionalBodyHood.asset") : null;
                if (recipe.id=="hero")
                {
                    var shoulder = AssetDatabase.LoadAssetAtPath<FieldPauldronBodyFit>(FitPath);
                    if (shoulder && shoulder.Original==fit.Original && shoulder.WithoutLeftCap)
                    {
                        var left = ReadableCopy(shoulder.WithoutLeftCap);
                        try
                        {
                            var leftIndices=left.triangles; var combined=new HashSet<int>(hair); int kept=0;
                            for (int i=0;i<indices.Length;i+=3)
                            {
                                if (kept+2<leftIndices.Length && indices[i]==leftIndices[kept] &&
                                    indices[i+1]==leftIndices[kept+1] && indices[i+2]==leftIndices[kept+2]) kept+=3;
                                else combined.Add(i);
                            }
                            if(kept!=leftIndices.Length) throw new InvalidOperationException("Feldschulter verliert ihre private Referenz.");
                            fit.OriginalShoulder=shoulder.WithoutLeftCap;
                            fit.ShoulderWithoutHair=Filtered(shaped,indices,combined,prefix+"_RegionalHoodFeldschulter.asset");
                        }
                        finally { Object.DestroyImmediate(left); }
                    }
                }
                MeasureRegionalFit(avatar, skin, shaped, indices, untrimmed, fit);
                CalibrateHands(prefab,fit,false,recipe.id=="hero");
                CalibrateHands(prefab,fit,true,recipe.id=="hero");
                EditorUtility.SetDirty(fit);
                Debug.Log("REGIONAL-FIT "+role+" uniform="+uniform.Count+" hair="+hair.Count+" baseTris="+indices.Length/3+
                    " faceLow="+fit.FaceLow.ToString("F4")+" faceHigh="+fit.FaceHigh.ToString("F4")+
                    " torsoWidthDepth="+fit.TorsoWidth.ToString("F4")+" "+fit.TorsoDepth.ToString("F4"));
            }
            finally { Object.DestroyImmediate(input); Object.DestroyImmediate(shaped); }
        }

        private static void CalibrateHands(GameObject prefab,RegionalBodyFit fit,bool worker,bool hero)
        {
            var model=Object.Instantiate(prefab);
            model.hideFlags=HideFlags.HideAndDontSave;
            try
            {
                var actor=model.GetComponentsInChildren<Animator>(true).First(a=>a.isHuman);
                if (worker && hero)
                {
                    var donor=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ARE/Visuals/EntryNpc2.prefab");
                    var idle=donor.GetComponentsInChildren<Animator>(true).First(a=>a.isHuman).runtimeAnimatorController;
                    if (!PassiveArtVisuals.HasPlayableClip(idle)) throw new InvalidOperationException("Schlackengarde benötigt den vorhandenen Standclip");
                    actor.runtimeAnimatorController=idle;
                }
                actor.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                actor.Rebind();actor.Update(0f);actor.Update(.08f);
                if (actor.GetCurrentAnimatorClipInfo(0).Length==0) throw new InvalidOperationException("Handanker ohne echten Idle-Clip.");
                var up=model.transform.up;
                var across=actor.GetBoneTransform(HumanBodyBones.RightUpperArm).position-actor.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
                var front=Vector3.Cross(across.normalized,up).normalized;
                var outward=Quaternion.LookRotation(front,up);
                var left=Quaternion.Inverse(actor.GetBoneTransform(HumanBodyBones.LeftHand).rotation)*outward;
                var right=Quaternion.Inverse(actor.GetBoneTransform(HumanBodyBones.RightHand).rotation)*outward;
                if(worker){fit.StaffLeft=left;fit.StaffRight=right;}else {fit.HeroLeft=left;fit.HeroRight=right;}
            }
            finally{Object.DestroyImmediate(model);}
        }

        private static Mesh Filtered(Mesh body,int[] original,HashSet<int> gaps,string path)
        {
            var keep=new List<int>(original.Length-gaps.Count*3);
            for(int i=0;i<original.Length;i+=3)
                if(!gaps.Contains(i)){keep.Add(original[i]);keep.Add(original[i+1]);keep.Add(original[i+2]);}
            var copy=Object.Instantiate(body);
            try
            {
                copy.SetTriangles(keep,0,false);
                copy.name=System.IO.Path.GetFileNameWithoutExtension(path);
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(!saved) { saved=Object.Instantiate(copy);saved.name=copy.name;AssetDatabase.CreateAsset(saved,path); }
                else EditorUtility.CopySerialized(copy,saved);
                SetReadable(saved,false); EditorUtility.SetDirty(saved); return saved;
            }
            finally { Object.DestroyImmediate(copy); }
        }

        private static void MatchWardrobePart(string module, Island[] dest, HashSet<int> identified,
            int[] originalIndices, Vector3[] bodyPoints, Transform[] bodyBones, BoneWeight[] bodyWeights)
        {
            var piece=AssetDatabase.LoadAssetAtPath<GameObject>(ClothesRoot+module+".prefab");
            if(!piece) throw new InvalidOperationException(module+" fehlt in der privaten Figurenquelle.");
            int start=identified.Count;
            int expected=0;
            foreach(var authored in piece.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s=>s.sharedMesh))
            {
                var source=ReadableCopy(authored.sharedMesh);
                try
                {
                    var sourcePoints=source.vertices;var sourceIndices=source.triangles;var sourceWeights=source.boneWeights;
                    expected+=sourceIndices.Length/3;
                    var parts=Components(sourcePoints,sourceIndices);
                    var possibilities=new List<Island[]>();
                    foreach(var part in parts)
                    {
                        var sourceBones=NameWeights(part,authored.bones,sourceWeights);
                        var ranked=dest.Where(d=>d.Faces.Count==part.Faces.Count && !identified.Contains(d.Faces[0]) &&
                            d.Topology.SequenceEqual(part.Topology) && NameWeights(d,bodyBones,bodyWeights).SetEquals(sourceBones))
                            .Select(d=>new {island=d, distance=Distance(part.Geometry,d.Geometry)})
                            .Where(m=>m.distance<.011).OrderBy(m=>m.distance).ToArray();
                        if(ranked.Length==0)
                            throw new InvalidOperationException("Fehlende Modulinsel "+module+" faces="+part.Faces.Count);
                        possibilities.Add(ranked.Where(m=>m.distance-ranked[0].distance<.000004).Select(m=>m.island).ToArray());
                    }
                    // Repeated buckles can be congruent. Their union is proved only when the
                    // same number of distinct target islands admits a full bijection.
                    var required=possibilities.SelectMany(row=>row).Distinct().ToArray();
                    if(required.Length!=parts.Length)
                        throw new InvalidOperationException("Uneindeutige Modulmenge "+module+
                            ": source="+parts.Length+" target="+required.Length);
                    var assigned=new Dictionary<Island,int>();
                    bool Match(int row,HashSet<Island> visited)
                    {
                        foreach(var island in possibilities[row])
                            if(visited.Add(island) && (!assigned.TryGetValue(island,out var other) || Match(other,visited)))
                            { assigned[island]=row; return true; }
                        return false;
                    }
                    for(int row=0;row<possibilities.Count;row++)
                        if(!Match(row,new HashSet<Island>()))throw new InvalidOperationException("Keine bijektive Modulzuordnung "+module);
                    foreach(var island in required)
                        foreach(int face in island.Faces)
                            if(!identified.Add(face)) throw new InvalidOperationException("Doppelte Modulfläche");

                }
                finally { Object.DestroyImmediate(source); }
            }
            if(identified.Count-start!=expected || expected==0) throw new InvalidOperationException("Unvollständiges Modul "+module);
            Debug.Log("REGIONAL-SOURCE "+module+" triangles="+expected);
        }

        // Combined characters weld some corset seams to neighbouring islands. Match each
        // oriented source triangle against all three actual corners and their bone identities.
        private static void MatchTopSurfaces(string module, HashSet<int> result, int[] faces, Vector3[] points,
            Transform[] bones,BoneWeight[] weights,Matrix4x4[] rest)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(ClothesRoot+module+".prefab");
            if(!source)throw new InvalidOperationException("Fehlende Oberkleidung: "+module);
            var byName=bones.Select((b,i)=>new{b,i}).Where(row=>row.b).ToDictionary(row=>row.b.name,row=>row.i);
            var cells=new Dictionary<Vector3Int,List<int>>();var adjacency=new List<int>[points.Length];
            Vector3Int Cell(Vector3 p)=>new Vector3Int(Mathf.FloorToInt(p.x*10000f),Mathf.FloorToInt(p.y*10000f),Mathf.FloorToInt(p.z*10000f));
            for(int i=0;i<points.Length;i++)
            {
                var key=Cell(points[i]);
                if(!cells.TryGetValue(key,out var list)){list=new List<int>();cells.Add(key,list);}list.Add(i);
                adjacency[i]=new List<int>();
            }
            for(int f=0;f<faces.Length;f+=3)
                for(int j=0;j<3;j++)adjacency[faces[f+j]].Add(f);
            int authored=0,absent=0;var ownership=new List<int[]>();
            foreach(var renderer in source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.sharedMesh))
            {
                var mesh=ReadableCopy(renderer.sharedMesh);
                try
                {
                    var coords=mesh.vertices;var indices=mesh.triangles;var sw=mesh.boneWeights;var sr=mesh.bindposes;
                    var targets=new HashSet<int>[coords.Length];
                    for(int i=0;i<coords.Length;i++)
                    {
                        var valid=new HashSet<int>();targets[i]=valid;
                        int src=Dominant(sw[i]);
                        if(!byName.TryGetValue(renderer.bones[src].name,out var dest))continue;
                        Vector3 p=rest[dest].inverse.MultiplyPoint3x4(sr[src].MultiplyPoint3x4(coords[i])); var key=Cell(p);
                        for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)
                            if(cells.TryGetValue(key+new Vector3Int(x,y,z),out var nearby))
                                foreach(int v in nearby)
                                    if(Dominant(weights[v])==dest && (points[v]-p).sqrMagnitude<=.00010f*.00010f)valid.Add(v);
                    }
                    for(int f=0;f<indices.Length;f+=3)
                    {
                        authored++;
                        var a=targets[indices[f]];var b=targets[indices[f+1]];var c=targets[indices[f+2]];
                        var matches=new HashSet<int>();
                        foreach(int v in a)foreach(int face in adjacency[v])for(int k=0;k<3;k++)
                            if(faces[face+k]==v && b.Contains(faces[face+(k+1)%3]) && c.Contains(faces[face+(k+2)%3]))
                                matches.Add(face);
                        if(matches.Count==0) {absent++;continue;}
                        ownership.Add(matches.OrderBy(i=>i).ToArray());
                    }
                }
                finally{Object.DestroyImmediate(mesh);}
            }
            var offsets=ownership.Select((row,i)=>new{row,i}).Where(r=>r.row.Length==1).GroupBy(r=>r.row[0]-r.i*3)
                .OrderByDescending(g=>g.Count()).Take(10).Select(g=>g.Key+"x"+g.Count()).ToArray();
            var starts=ownership[0].Where(o=>ownership.Select((row,i)=>Array.IndexOf(row,o+i*3)>=0).All(v=>v)).ToArray();
            Debug.Log("REGIONAL-ORDER "+module+" first="+string.Join(",",ownership[0])+" last="+string.Join(",",ownership[ownership.Count-1])+
                " concat="+string.Join(",",starts)+" offsets="+string.Join(",",offsets));
            if(starts.Length==1 && absent==0)
            {
                for(int i=0;i<ownership.Count;i++)result.Add(starts[0]+i*3);
                Debug.Log("REGIONAL-SOURCE "+module+" indexed="+result.Count+"/"+authored+" contiguous="+starts[0]);
                return;
            }
            var groups=ownership.GroupBy(row=>row[0]).ToArray();
            int repeated=0;
            foreach(var group in groups)
            {
                var candidates=group.First();
                if(group.Count()!=candidates.Length || group.Any(r=>!r.SequenceEqual(candidates)))
                    throw new InvalidOperationException("Mehrdeutige Dreiecksgruppe "+module+" source="+group.Count()+
                        " targets="+candidates.Length+" candidates="+string.Join(",",candidates)+" authored="+authored+
                        " union="+ownership.SelectMany(row=>row).Distinct().Count());
                if(candidates.Length>1)repeated+=candidates.Length;
                foreach(int face in candidates)
                    if(!result.Add(face))throw new InvalidOperationException("Doppelte Zuordnung von Oberkleidung");
            }
            Debug.Log("REGIONAL-SOURCE "+module+" indexed="+result.Count+"/"+authored+" absent="+absent+" coincident="+repeated);
            if(absent!=0 || result.Count!=authored)
                throw new InvalidOperationException("Oberkleidungsflächen "+module+" exact="+result.Count+"/"+authored+" absent="+absent);
        }

        private static HashSet<string> NameWeights(Island part,Transform[] bones,BoneWeight[] weights)
        {
            var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(var v in part.Corners) {int b=Dominant(weights[v]);if(b>=0 && b<bones.Length && bones[b])names.Add(bones[b].name);}
            return names;
        }

        private static void TrimCoveredClothing(Animator avatar, SkinnedMeshRenderer skin, Mesh mesh, int[] faces,
            HashSet<int> originalTop, HashSet<int> uniform, HashSet<int> hood)
        {
            var bones=skin.bones; var points=mesh.vertices; var weights=mesh.boneWeights; var rest=mesh.bindposes;
            Vector3 Joint(Transform bone) => rest[Array.IndexOf(bones,bone)].inverse.MultiplyPoint3x4(Vector3.zero);
            var head=avatar.GetBoneTransform(HumanBodyBones.Head);
            var headPivot=Joint(head);
            var hips=Joint(avatar.GetBoneTransform(HumanBodyBones.Hips));
            var left=Joint(avatar.GetBoneTransform(HumanBodyBones.LeftUpperArm));
            var right=Joint(avatar.GetBoneTransform(HumanBodyBones.RightUpperArm));
            var shoulder=(left+right)*.5f;
            var side=(right-left).normalized;var up=(shoulder-hips).normalized;
            float arm=Vector3.Distance(left,Joint(avatar.GetBoneTransform(HumanBodyBones.LeftLowerArm)));
            float height=Vector3.Distance(shoulder,hips);
            int covered=0;
            foreach(int i in originalTop)
            {
                var center=(points[faces[i]]+points[faces[i+1]]+points[faces[i+2]])/3f-hips;
                float y=Vector3.Dot(center,up);
                if(y>height-.27f*arm/.30f && y<height+.018f*arm/.30f &&
                    Mathf.Abs(Vector3.Dot(center,side))<(right-left).magnitude*.52f)
                { uniform.Add(i); covered++; }
            }
            if(covered<60)throw new InvalidOperationException("Kein geschlossenes Hemdvorder-/Rückenteil identifiziert");
            if(hood.Count==0) {Debug.Log("REGIONAL-COVER "+avatar.avatar.name+" shirt="+covered+" skull=0");return;}
            var brows=new[]{"brow_l","brow_r"}.Select(n=>bones.Single(b=>b && b.name==n)).ToArray();
            float crownStart=Vector3.Dot((Joint(brows[0])+Joint(brows[1]))*.5f-headPivot,up)+.013f*arm/.30f;
            bool IsSkull(int v)
            {
                var b=bones[Dominant(weights[v])];
                if(!b || (b!=head && !b.IsChildOf(head)))return false;
                return Vector3.Dot(points[v]-headPivot,up)>crownStart;
            }
            int coveredHead=0;
            for(int i=0;i<faces.Length;i+=3)
                if(!hood.Contains(i) && IsSkull(faces[i]) && IsSkull(faces[i+1]) && IsSkull(faces[i+2]))
                { hood.Add(i);coveredHead++; }
            if(coveredHead<30)throw new InvalidOperationException("Teil des Schädeldachs über den nativen Brauen nicht identifiziert");
            Debug.Log("REGIONAL-COVER "+avatar.avatar.name+" shirt="+covered+" skull="+coveredHead+" crownStart="+crownStart.ToString("F4"));
        }

        private static void MeasureRegionalFit(Animator avatar,SkinnedMeshRenderer skin,Mesh mesh,int[] faces,HashSet<int> excluded,RegionalBodyFit fit)
        {
            var bones=skin.bones;var bind=mesh.bindposes;var points=mesh.vertices;var weights=mesh.boneWeights;
            int Index(HumanBodyBones b){var joint=avatar.GetBoneTransform(b);return joint ? Array.IndexOf(bones,joint):-1;}
            Vector3 Joint(HumanBodyBones b)=>bind[Index(b)].inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 shoulder=(Joint(HumanBodyBones.LeftUpperArm)+Joint(HumanBodyBones.RightUpperArm))*.5f;
            Vector3 hips=Joint(HumanBodyBones.Hips),head=Joint(HumanBodyBones.Head);
            Vector3 across=Joint(HumanBodyBones.RightUpperArm)-Joint(HumanBodyBones.LeftUpperArm);
            Vector3 up=(shoulder-hips).normalized;
            var inverse=Quaternion.Inverse(Quaternion.LookRotation(Vector3.Cross(across,up).normalized,up));
            var usable=new HashSet<int>();
            for(int i=0;i<faces.Length;i+=3) if(!excluded.Contains(i)) for(int j=0;j<3;j++) usable.Add(faces[i+j]);
            var face=new List<Vector3>();var torso=new List<Vector3>();var waist=new List<Vector3>();
            var headBone=avatar.GetBoneTransform(HumanBodyBones.Head);
            float height=Vector3.Dot(shoulder-hips,up);
            foreach(int i in usable)
            {
                var bone=bones[Dominant(weights[i])];if(!bone)continue;
                if(bone==headBone || bone.IsChildOf(headBone))face.Add(inverse*(points[i]-head));
                var delta=inverse*(points[i]-hips);
                if(delta.y>height*.035f && delta.y<height*.23f && Mathf.Abs(delta.x)<across.magnitude*.76f)
                    waist.Add(delta);
                if(delta.y>height*.5f && delta.y<height*.92f && Mathf.Abs(delta.x)<across.magnitude*.79f &&
                   (bone.name.StartsWith("spine",StringComparison.OrdinalIgnoreCase)||bone.name.StartsWith("clav",StringComparison.OrdinalIgnoreCase)))
                    torso.Add(delta);
            }
            if(face.Count<80 || torso.Count<20) throw new InvalidOperationException("Körpermaße nicht aus dem Kaufmesh lesbar: head="+face.Count+" torso="+torso.Count);
            float Q(IEnumerable<float> samples,float quantile){var a=samples.OrderBy(k=>k).ToArray();return a[Mathf.Clamp(Mathf.RoundToInt((a.Length-1)*quantile),0,a.Length-1)];}
            Vector3 Range(List<Vector3> a,float q)=>new Vector3(Q(a.Select(v=>v.x),q),Q(a.Select(v=>v.y),q),Q(a.Select(v=>v.z),q));
            int eyeL=Index(HumanBodyBones.LeftEye), eyeR=Index(HumanBodyBones.RightEye);
            if(eyeL<0 || eyeR<0)
            {
                eyeL=Array.FindIndex(bones,b=>b && string.Equals(b.name,"eyeRoot_l",StringComparison.OrdinalIgnoreCase));
                eyeR=Array.FindIndex(bones,b=>b && string.Equals(b.name,"eyeRoot_r",StringComparison.OrdinalIgnoreCase));
            }
            fit.EyeHeight = (eyeL>=0 && eyeR>=0) ? (inverse*((bind[eyeL].inverse.MultiplyPoint3x4(Vector3.zero)+
                bind[eyeR].inverse.MultiplyPoint3x4(Vector3.zero))*.5f-head)).y : height*.141f;
            fit.FaceLow=Range(face,.022f);fit.FaceHigh=Range(face,.978f);
            var brow = new List<Vector3>();
            for(int i=0;i<points.Length;i++)
            {
                var b=bones[Dominant(weights[i])];
                if(!b || (b!=headBone && !b.IsChildOf(headBone)))continue;
                var d=inverse*(points[i]-head);
                if(d.y>fit.EyeHeight+.025f && d.y<fit.EyeHeight+.095f) brow.Add(d);
            }
            if(brow.Count<30) throw new InvalidOperationException("Stirnumfang nicht aus den Kopf-/Haardaten lesbar");
            fit.BrowLow=Range(brow,.022f);fit.BrowHigh=Range(brow,.978f);
            var lo=Range(torso,.022f);var hi=Range(torso,.978f);
            fit.TorsoWidth=hi.x-lo.x;fit.TorsoDepth=hi.z-lo.z;fit.TorsoCenterZ=(lo.z+hi.z)*.5f;
            if(waist.Count<12)throw new InvalidOperationException("Gürtelquerschnitt fehlt");
            lo=Range(waist,.025f); hi=Range(waist,.975f);
            fit.WaistWidth=hi.x-lo.x;fit.WaistDepth=hi.z-lo.z;fit.WaistCenterZ=(lo.z+hi.z)*.5f;
            float unit=Vector3.Distance(Joint(HumanBodyBones.LeftUpperArm),Joint(HumanBodyBones.LeftLowerArm))/.30f;
            var relative=points.Select(v=>inverse*(v-shoulder)).ToArray();
            CalibrateTorsoClearance(relative,faces,excluded,unit,fit);
        }

        // Sagittal sections of the two exported shells in metres, measured from the final FBX.
        // Determine the smallest depth and centre that enclose every corresponding section
        // of the actual purchased body; symmetric expansion alone leaves the chest protruding.
        private static void CalibrateTorsoClearance(Vector3[] points,int[] faces,HashSet<int> excluded,float unit,RegionalBodyFit fit)
        {
            float[] heights={-.16f,-.12f,-.06f,0,.035f,.060f,.090f,.120f};
            float[] front=new float[heights.Length], back=new float[heights.Length];
            for(int j=0;j<heights.Length;j++)
            {
                float y=(heights[j]-.108f)*unit;var slice=new HashSet<int>();
                for(int i=0;i<faces.Length;i+=3)
                {
                    if(excluded.Contains(i))continue;
                    for(int e=0;e<3;e++)
                    {
                        var a=points[faces[i+e]];var b=points[faces[i+(e+1)%3]];
                        if((a.y<y)==(b.y<y))continue;
                        var crossing=Vector3.LerpUnclamped(a,b,(y-a.y)/(b.y-a.y));
                        if(Mathf.Abs(crossing.x)<=.080f) slice.Add(Mathf.RoundToInt(crossing.z*1000000f));
                    }
                }
                var z=slice.OrderBy(v=>v).ToArray();
                if(z.Length<4)throw new InvalidOperationException("Zu wenige Brustquerschnitte bei "+heights[j]);
                back[j]=z[Mathf.RoundToInt((z.Length-1)*.025f)]*.000001f-.008f;
                front[j]=z[Mathf.RoundToInt((z.Length-1)*.975f)]*.000001f+.008f;
            }
            Vector2 Fit(float[] shellBack,float[] shellFront)
            {
                float scale=unit;
                for(int a=0;a<front.Length;a++)
                    for(int b=0;b<back.Length;b++)
                        scale=Mathf.Max(scale,(front[a]-back[b])/(shellFront[a]-shellBack[b]));
                float min=float.NegativeInfinity,max=float.PositiveInfinity;
                for(int j=0;j<front.Length;j++)
                { min=Mathf.Max(min,front[j]-shellFront[j]*scale);max=Mathf.Min(max,back[j]-shellBack[j]*scale); }
                return new Vector2((min+max)*.5f,scale);
            }
            fit.GuardTorso=Fit(new[]{-.109f,-.125f,-.134f,-.143f,-.145f,-.152f,-.155f,-.111f},
                new[]{.109f,.125f,.134f,.143f,.148f,.155f,.155f,.111f});
            fit.BellTorso=Fit(new[]{-.111f,-.115f,-.116f,-.124f,-.129f,-.128f,-.127f,-.120f},
                new[]{.111f,.142f,.143f,.150f,.150f,.140f,.140f,.122f});
            Debug.Log("REGIONAL-TORSO "+fit.Original.name+" centerZ/depthScale: guard="+fit.GuardTorso.ToString("F4")+" mill="+fit.BellTorso.ToString("F4"));
        }
    }
}
