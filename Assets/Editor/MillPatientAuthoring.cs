using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditorTools
{
    /// <summary>Bake private peasant rigs into passive, covered resting figures; source prefabs stay untouched.</summary>
    public static class MillPatientAuthoring
    {
        private const string Root = "Assets/Resources/ARE/Visuals/";
        private const string Output = Root + "MillPatients/";

        [MenuItem("ARE ARPG/Art/Build Private Mill Patients")]
        public static void Build()
        {
            Directory.CreateDirectory(Output); AssetDatabase.Refresh();
            for (int i = 0; i < 3; i++) BuildPatient(i);
            AssetDatabase.SaveAssets();
            Debug.Log("ARE_MILL_PATIENTS: three passive resting figures baked from existing private peasant models.");
        }

        private static void BuildPatient(int id)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Npc" + (id == 1 ? 1 : 0) + ".prefab");
            if (source == null) throw new InvalidOperationException("Import the private peasant visuals first.");
            foreach (var component in source.GetComponentsInChildren<Component>(true))
                if (!(component is Transform || component is Renderer || component is MeshFilter || component is Animator || component is LODGroup))
                    throw new InvalidOperationException("Patient source contains a non-visual component.");
            var actor = UnityEngine.Object.Instantiate(source);
            var result = new GameObject("MillPatient" + id);
            var body = new GameObject("RestingBody"); body.transform.SetParent(result.transform, false);
            try
            {
                var animator = actor.GetComponentInChildren<Animator>();
                if (animator == null || !animator.isHuman || !animator.avatar.isValid)
                    throw new InvalidOperationException("Patient source needs a valid humanoid rig.");
                animator.enabled = false;
                // Lower the arms alongside the body before laying the figure on its back.
                Aim(animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, new Vector3(-.08f, -1, .05f));
                Aim(animator, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, new Vector3(.1f, -1, .12f));
                Aim(animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, new Vector3(.08f, -1, .05f));
                Aim(animator, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, new Vector3(-.1f, -1, .12f));
                Aim(animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, new Vector3(-.04f, -1, 0));
                Aim(animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, new Vector3(.04f, -1, 0));
                Aim(animator, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, Vector3.down);
                Aim(animator, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, Vector3.down);
                actor.transform.rotation = Quaternion.Euler(-90, 0, 0);
                var materials = new Dictionary<Material, Material>();
                int meshIndex = 0;
                foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!skin.enabled) continue;
                    var mesh = new Mesh { name = "Patient" + id + "_" + meshIndex };
                    skin.BakeMesh(mesh);
                    // Bake to world space while the output root is identity, then fit the complete body uniformly.
                    Vector3[] vertices = mesh.vertices; Vector3[] normals = mesh.normals;
                    Matrix4x4 matrix = skin.transform.localToWorldMatrix;
                    Matrix4x4 normalMatrix = matrix.inverse.transpose;
                    for (int v = 0; v < vertices.Length; v++) vertices[v] = matrix.MultiplyPoint3x4(vertices[v]);
                    for (int v = 0; v < normals.Length; v++) normals[v] = normalMatrix.MultiplyVector(normals[v]).normalized;
                    mesh.vertices = vertices; mesh.normals = normals; mesh.RecalculateBounds();
                    var part = new GameObject(skin.name); part.transform.SetParent(body.transform, false);
                    part.AddComponent<MeshFilter>().sharedMesh = SaveAsset(mesh, Output + mesh.name + ".asset");
                    var slots = skin.sharedMaterials;
                    for (int m = 0; m < slots.Length; m++)
                    {
                        Material original = slots[m];
                        if (original == null) throw new InvalidOperationException("Patient material missing.");
                        if (!materials.TryGetValue(original, out var copy))
                        {
                            copy = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                            Texture texture = original.HasProperty("_BaseMap") ? original.GetTexture("_BaseMap") : original.mainTexture;
                            copy.SetTexture("_BaseMap", texture);
                            copy.SetColor("_BaseColor", original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : original.color);
                            copy.SetFloat("_Smoothness", .12f);
                            copy = SaveAsset(copy, Output + "Patient" + id + "_Material" + materials.Count + ".mat");
                            materials.Add(original, copy);
                        }
                        slots[m] = copy;
                    }
                    part.AddComponent<MeshRenderer>().sharedMaterials = slots; meshIndex++;
                }
                if (meshIndex == 0) throw new InvalidOperationException("Patient source has no visible skin.");
                Bounds bounds = BoundsOf(body);
                body.transform.localScale *= 1.92f / bounds.size.z;
                bounds = BoundsOf(body);
                body.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                var cloth = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                cloth.SetColor("_BaseColor", id == 0 ? new Color(.38f, .31f, .23f) :
                    id == 1 ? new Color(.27f, .34f, .32f) : new Color(.35f, .32f, .27f));
                cloth.SetFloat("_Smoothness", .05f);
                cloth = SaveAsset(cloth, Output + "Blanket" + id + ".mat");
                // A low curved cover leaves the head and boots visible; no anatomy or injury is invented.
                AddCover(result.transform, id, cloth);
                Bounds finished = BoundsOf(result);
                if (finished.size.x > 1.45f || finished.size.y > .8f)
                    throw new InvalidOperationException("Patient pose does not fit the existing bed: " + finished);
                if (PrefabUtility.SaveAsPrefabAsset(result, Root + result.name + ".prefab") == null)
                    throw new InvalidOperationException("Could not save resting patient.");
                Debug.Log("ARE_PATIENT " + id + " bounds=" + finished + " meshes=" + meshIndex);
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); UnityEngine.Object.DestroyImmediate(result); }
        }

        private static void Aim(Animator animator, HumanBodyBones from, HumanBodyBones to, Vector3 direction)
        {
            Transform bone = animator.GetBoneTransform(from), end = animator.GetBoneTransform(to);
            if (bone == null || end == null) throw new InvalidOperationException("Incomplete patient skeleton.");
            bone.rotation = Quaternion.FromToRotation(end.position - bone.position, direction) * bone.rotation;
        }

        private static void AddCover(Transform parent, int id, Material material)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int row = 0; row < 5; row++)
                for (int x = 0; x < 7; x++)
                {
                    float across = (x - 3) / 3f;
                    vertices.Add(new Vector3(across * .46f, .12f + .30f * (1 - across * across) + (row % 2) * .018f,
                        -.47f + row * .305f));
                    if (row == 4 || x == 6) continue;
                    int n = row * 7 + x;
                    triangles.AddRange(new[] { n, n + 7, n + 1, n + 1, n + 7, n + 8 });
                }
            var mesh = new Mesh { name = "PatientCover" + id, vertices = vertices.ToArray(), triangles = triangles.ToArray() };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var cover = new GameObject("Blanket"); cover.transform.SetParent(parent, false);
            cover.AddComponent<MeshFilter>().sharedMesh = SaveAsset(mesh, Output + mesh.name + ".asset");
            cover.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("Patient mesh is empty.");
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static T SaveAsset<T>(T value, string path) where T : UnityEngine.Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, existing); EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(value); return existing;
        }
    }
}
