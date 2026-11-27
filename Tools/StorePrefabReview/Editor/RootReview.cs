using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ouroboros.StorePrefabReview
{
    public static class RootReview
    {
        const string Marker = "StorePrefabReview.copy";
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        static readonly Regex Header = new Regex(@"(?m)^--- !u!(\d+) &(-?\d+)( stripped)?\r?$");
        static readonly Regex ZeroParent = new Regex(@"(?m)^    m_TransformParent: \{fileID: 0\}\r?$");
        static readonly Regex ZeroFather = new Regex(@"(?m)^  m_Father: \{fileID: 0\}\r?$");
        static readonly Type[] StaticComponents = { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer),
            typeof(BoxCollider), typeof(SphereCollider), typeof(CapsuleCollider), typeof(MeshCollider) };

        sealed class Change
        {
            public string Path, Fingerprint;
            public byte[] Before, After, Meta;
        }

        public static bool IsIdentity(Transform t)
        {
            Quaternion q = t.localRotation;
            return t.localPosition.Equals(Vector3.zero) && t.localScale.Equals(Vector3.one)
                && q.x == 0f && q.y == 0f && q.z == 0f && Mathf.Abs(q.w) == 1f;
        }

        static string Project => Directory.GetParent(Application.dataPath).FullName;
        static void LocalPath(string path)
        {
            var raw = path.Split('/');
            if (raw.Length < 2 || raw[0] != "Assets" || raw.Any(p => p.Length == 0 || p == "." || p == ".." ||
                p.IndexOfAny(new[] { '\\', ':' }) >= 0))
                throw new InvalidOperationException("Select one product folder below Assets.");
            string full = Project;
            foreach (string part in raw)
            {
                full = System.IO.Path.Combine(full,part);
                if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Linked files and folders require manual review: " + path);
            }
        }
        static string[] Prefabs(string folder)
        {
            LocalPath(folder);
            if (!AssetDatabase.IsValidFolder(folder)) throw new InvalidOperationException("Unknown product folder: "+folder);
            // Traverse explicitly so a symlink is rejected before descent.
            var files = new List<string>();
            var dirs = new Stack<string>(); dirs.Push(folder);
            while (dirs.Count != 0)
            {
                string dir = dirs.Pop();
                foreach (string child in Directory.GetDirectories(dir))
                {
                    var name = child.Replace('\\','/'); LocalPath(name); dirs.Push(name);
                }
                foreach (string file in Directory.GetFiles(dir))
                {
                    if (!file.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase)) continue;
                    string path = file.Replace('\\','/'); LocalPath(path); LocalPath(path+".meta"); files.Add(path);
                }
            }
            if (files.Count == 0) throw new InvalidOperationException("No .prefab files in "+folder);
            return files.OrderBy(p=>p,StringComparer.Ordinal).ToArray();
        }
        static GameObject Read(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path)
            ?? throw new InvalidOperationException("Could not load prefab: "+path);

        [Serializable] public sealed class Finding
        {
            public string path, guid, type;
            public Vector3 position, scale;
            public Quaternion rotation;
            public bool identity;
        }
        [Serializable] public sealed class Report { public string unity, folder; public Finding[] prefabs; }
        public static Report Inspect(string folder)
        {
            return new Report { unity=Application.unityVersion, folder=folder, prefabs=Prefabs(folder).Select(path=>{
                var go=Read(path); var t=go.transform;
                return new Finding { path=path, guid=AssetDatabase.AssetPathToGUID(path),
                    type=PrefabUtility.GetPrefabAssetType(go).ToString(), position=t.localPosition,
                    rotation=t.localRotation, scale=t.localScale, identity=IsIdentity(t) };
            }).ToArray() };
        }
        static void RequireCopy(string folder)
        {
            string marker=System.IO.Path.Combine(Project,Marker);
            if (!File.Exists(marker) || (File.GetAttributes(marker)&FileAttributes.ReparsePoint)!=0 ||
                !File.ReadAllLines(marker).SequenceEqual(new[]{Project,folder}))
                throw new InvalidOperationException("Repair requires a disposable copy bound by "+Marker+" to this project and product.");
        }

        static string Key(Object obj)
        {
            if (!obj) return "-";
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj,out string guid,out long id) || id==0)
                throw new InvalidOperationException("No stable local identifier for "+obj.name);
            return guid+"@"+id.ToString(CultureInfo.InvariantCulture);
        }
        static string Fingerprint(Transform payload)
        {
            var sb=new StringBuilder();
            Action<string> add=s=>{sb.Append(s.Length).Append(':').Append(s);};
            foreach(var t in payload.GetComponentsInChildren<Transform>(true))
            {
                add(t.name); add(Key(t)); add(Key(t.gameObject)); add(t.childCount.ToString()); add(t.gameObject.activeSelf.ToString());
                for(int i=0;i<16;i++) add(t.localToWorldMatrix[i].ToString("R",CultureInfo.InvariantCulture));
                foreach(var c in t.GetComponents<Component>())
                {
                    if (!c || !StaticComponents.Contains(c.GetType()))
                        throw new InvalidOperationException("Manual review for animated, scripted, physics-driven or unsupported component at "+t.name);
                    add(c.GetType().FullName); add(Key(c));
                    if (c is Transform) continue;
                    if (c is MeshFilter mf && !mf.sharedMesh) throw new InvalidOperationException("Missing mesh at "+t.name);
                    // Includes serialized collider dimensions and material/mesh references. Use stable
                    // identifiers for references; instance IDs are local to the current Editor session.
                    var so=new SerializedObject(c); var it=so.GetIterator();
                    while(it.Next(true))
                    {
                        if (it.propertyPath=="m_PrefabInstance" || it.propertyPath=="m_PrefabAsset" ||
                            it.propertyPath=="m_CorrespondingSourceObject") continue;
                        if (it.propertyType==SerializedPropertyType.ObjectReference)
                        {
#if UNITY_6000_6_OR_NEWER
                            bool hasId = it.objectReferenceEntityIdValue != default;
#else
                            bool hasId = it.objectReferenceInstanceIDValue != 0;
#endif
                            if (!it.objectReferenceValue && hasId)
                                throw new InvalidOperationException("Unresolved reference at "+t.name+"."+it.propertyPath);
                            add(it.propertyPath); add(Key(it.objectReferenceValue));
                        }
                    }
                    if(c is Collider collider) {
                        add(collider.enabled.ToString()); add(collider.isTrigger.ToString());
                        if(c is BoxCollider box) {add(box.center.ToString("R")); add(box.size.ToString("R"));}
                        if(c is SphereCollider sphere) {add(sphere.center.ToString("R")); add(sphere.radius.ToString("R"));}
                        if(c is CapsuleCollider cap) {add(cap.center.ToString("R"));add(cap.radius.ToString("R"));add(cap.height.ToString("R"));add(cap.direction.ToString());}
                    }
                }
            }
            using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Utf8.GetBytes(sb.ToString())));
        }

        // The old serialized subtree is kept verbatim apart from its one parent reference.
        // This also preserves mirrored and non-uniform scales without decomposing a world matrix.
        static byte[] Wrap(byte[] bytes, GameObject go)
        {
            string text=Utf8.GetString(bytes);
            if (!text.StartsWith("%YAML 1.1",StringComparison.Ordinal) || go.name.Any(char.IsControl))
                throw new InvalidOperationException("Manual review for binary or unfamiliar prefab format: "+go.name);
            string nl=text.Contains("\r\n")?"\r\n":"\n"; text=text.Replace("\r\n","\n");
            var docs=Header.Matches(text).Cast<Match>().ToArray();
            var used=new HashSet<long>(docs.Select(m=>long.Parse(m.Groups[2].Value,CultureInfo.InvariantCulture)));
            foreach(var t in go.GetComponentsInChildren<Transform>(true))
            {
                foreach(var o in t.GetComponents<Component>().Cast<Object>().Append(t.gameObject))
                    if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o,out string _,out long id))used.Add(id);
            }
            long next=43100000;
            long NewId() {while(!used.Add(++next)){} return next;}
            long outer=NewId(), transform=NewId(), child;
            string stripped="";
            if (PrefabUtility.GetPrefabAssetType(go)==PrefabAssetType.Variant)
            {
                var src=PrefabUtility.GetCorrespondingObjectFromSource(go.transform);
                if (docs.Length!=1 || docs[0].Groups[1].Value!="1001" || ZeroParent.Matches(text).Count!=1 || !src ||
                    !(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(src)) is ModelImporter) ||
                    !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(src,out string srcGuid,out long srcId) ||
                    !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(go.transform,out string _,out child) ||
                    child==0 || docs.Any(m=>m.Groups[2].Value==child.ToString()) ||
                    !text.Contains("m_SourcePrefab: {fileID: 100100000, guid: "+srcGuid+", type: 3}"))
                    throw new InvalidOperationException("Manual review for complex or non-model variant: "+go.name);
                text=ZeroParent.Replace(text,"    m_TransformParent: {fileID: "+transform+"}",1);
                stripped=$"--- !u!4 &{child} stripped\nTransform:\n  m_CorrespondingSourceObject: {{fileID: {srcId}, guid: {srcGuid}, type: 3}}\n  m_PrefabInstance: {{fileID: {docs[0].Groups[2].Value}}}\n  m_PrefabAsset: {{fileID: 0}}\n";
            }
            else
            {
                if(PrefabUtility.GetPrefabAssetType(go)!=PrefabAssetType.Regular || ZeroFather.Matches(text).Count!=1 ||
                    !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(go.transform,out string _,out child) ||
                    !docs.Any(m=>m.Groups[1].Value=="4" && m.Groups[2].Value==child.ToString()) ||
                    !Regex.IsMatch(text,@"(?ms)^--- !u!4 &"+child+@"\n(?:(?!^---).)*?^  m_Father: \{fileID: 0\}$"))
                    throw new InvalidOperationException("Ambiguous root. Manual review required: "+go.name);
                text=ZeroFather.Replace(text,"  m_Father: {fileID: "+transform+"}",1);
            }
            string fields="  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n";
            string header=$"--- !u!1 &{outer}\nGameObject:\n"+fields+"  serializedVersion: 6\n  m_Component:\n"+
                $"  - component: {{fileID: {transform}}}\n  m_Layer: {go.layer}\n  m_Name: '{go.name.Replace("'","''")}'\n"+
                "  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n"+
                $"--- !u!4 &{transform}\nTransform:\n"+fields+$"  m_GameObject: {{fileID: {outer}}}\n  serializedVersion: 2\n"+
                "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n"+
                $"  m_ConstrainProportionsScale: 0\n  m_Children:\n  - {{fileID: {child}}}\n  m_Father: {{fileID: 0}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n";
            return Utf8.GetBytes((text.TrimEnd('\n')+"\n"+stripped+header).Replace("\n",nl));
        }

        static void Swap(string path, byte[] bytes)
        {
            // Keep both files on the same volume. Hidden scratch is ignored by Unity.
            string temp=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path),".root-review-"+Guid.NewGuid().ToString("N"));
            try {File.WriteAllBytes(temp,bytes); File.Replace(temp,path,null);}
            finally {if(File.Exists(temp))File.Delete(temp);}
        }
        static void Import(string path) => AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
        public static int Repair(string folder)
        {
            RequireCopy(folder);
            var changed=Inspect(folder).prefabs.Where(x=>!x.identity).ToArray();
            if(changed.Length==0)return 0;
            var affected=new HashSet<string>(changed.Select(x=>x.path),StringComparer.Ordinal);
            foreach(string path in AssetDatabase.GetAllAssetPaths())
            {
                if(!(path.StartsWith("Assets/",StringComparison.Ordinal)||path.StartsWith("Packages/",StringComparison.Ordinal)) || AssetDatabase.IsValidFolder(path))continue;
                foreach(string dep in AssetDatabase.GetDependencies(path,false))
                    if(dep!=path && affected.Contains(dep))
                        throw new InvalidOperationException("Manual instance/reference migration required: "+path+" -> "+dep);
            }
            var plan=new List<Change>();
            foreach(var row in changed)
            {
                var go=Read(row.path);
                if (!go.activeSelf || go.tag!="Untagged" || go.GetComponentsInChildren<Transform>(true).Any(t=>
                    (t.hideFlags & HideFlags.NotEditable)!=0 && PrefabUtility.GetPrefabAssetType(go)!=PrefabAssetType.Variant))
                    throw new InvalidOperationException("Root state requires manual review: "+row.path);
                var bytes=File.ReadAllBytes(row.path); var digest=Fingerprint(go.transform);
                plan.Add(new Change{Path=row.path,Before=bytes,After=Wrap(bytes,go),Meta=File.ReadAllBytes(row.path+".meta"),Fingerprint=digest});
            }
            var applied=new List<Change>();
            try
            {
                foreach(var change in plan)
                {
                    if(!File.ReadAllBytes(change.Path).SequenceEqual(change.Before) || !File.ReadAllBytes(change.Path+".meta").SequenceEqual(change.Meta))
                        throw new InvalidOperationException("Source changed after preflight: "+change.Path);
                    Swap(change.Path,change.After); applied.Add(change); Import(change.Path);
                    var root=Read(change.Path).transform;
                    if(!IsIdentity(root)||root.childCount!=1||Fingerprint(root.GetChild(0))!=change.Fingerprint ||
                        !File.ReadAllBytes(change.Path+".meta").SequenceEqual(change.Meta))
                        throw new InvalidOperationException("Identity, local IDs, geometry, components or meta changed: "+change.Path);
                }
                return plan.Count;
            }
            catch
            {
                foreach(var c in applied.AsEnumerable().Reverse()) {Swap(c.Path,c.Before);Import(c.Path);}
                throw;
            }
        }
        static string SelectedFolder() => AssetDatabase.GetAssetPath(Selection.activeObject);
        [MenuItem("Assets/Store Prefabs/Inspect selected product folder")]
        static void InspectSelected() => Debug.Log(JsonUtility.ToJson(Inspect(SelectedFolder()),true));
        [MenuItem("Assets/Store Prefabs/Repair marked package copy")]
        static void RepairSelected() => Debug.Log("Repaired prefab roots: "+Repair(SelectedFolder()));
        public static void AuditBatch() => Debug.Log(JsonUtility.ToJson(Inspect(Environment.GetEnvironmentVariable("PRODUCT_ROOT")),true));
        public static void RepairBatch() => Debug.Log("Repaired prefab roots: "+Repair(Environment.GetEnvironmentVariable("PRODUCT_ROOT")));
    }
}
