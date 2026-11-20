using System;
using UnityEngine;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        private const string FieldPauldronFolder = "Equipment/Feldschulter/";
        private const string FieldPauldronPrefab = FieldPauldronFolder + "Feldschulter_Wearable";
        private const float FieldPauldronReferenceArmMetres = 0.30f;
        private const string FieldPauldronFitResource = "ARE/Visuals/FieldPauldronBodyFit";
        private Transform _fieldPauldronHero, _fieldPauldron;
        private bool _fieldPauldronRequested;

        private void UpdateFieldPauldron(bool equipped)
        {
            if (_fieldPauldronHero != _hero)
            {
                _fieldPauldronHero = _hero;
                _fieldPauldron = null;
                _fieldPauldronRequested = false;
            }
            if (_fieldPauldronRequested == equipped) return;
            _fieldPauldronRequested = equipped;
            if (_fieldPauldron != null)
            {
                _fieldPauldron.gameObject.SetActive(equipped);
                return;
            }
            if (!equipped) return;

            var prefab = Resources.Load<GameObject>(FieldPauldronPrefab);
            if (prefab == null) return;
            var bodyFit = Resources.Load<FieldPauldronBodyFit>(FieldPauldronFitResource);
            Bounds shell = FieldPauldronBounds(prefab);
            foreach (var animator in _heroMeleeAnimations)
            {
                if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.isHuman) continue;
                var upper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                var lower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                var right = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                if (upper == null || lower == null || right == null || hips == null || chest == null) continue;
                foreach (var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var fitted = bodyFit != null && bodyFit.Original == skin.sharedMesh ? bodyFit : null;
                    if (!TryFieldPauldronFit(skin, upper, lower, right, hips, chest, shell, fitted,
                        out var position, out var rotation, out var scale)) continue;
                    var socket = new GameObject("Equipped_FieldPauldron").transform;
                    socket.SetParent(upper, false);
                    socket.localPosition = position;
                    socket.localRotation = rotation;
                    socket.localScale = scale;
                    if (PassiveArtVisuals.TryBindPrefab(socket, prefab))
                    {
                        if (fitted != null) socket.gameObject.AddComponent<FieldPauldronBodySwap>().Bind(skin, fitted);
                        _fieldPauldron = socket;
                        return;
                    }
                    Destroy(socket.gameObject);
                    return;
                }
            }
        }

        // The authored dome and the original cap both stand upright in the body's bind frame.
        // Matching their centres in the upper arm's local space keeps them together for every clip.
        private static bool TryFieldPauldronFit(SkinnedMeshRenderer skin, Transform upper, Transform lower,
            Transform right, Transform hips, Transform chest, Bounds shell, FieldPauldronBodyFit fit,
            out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            scale = Vector3.one;
            if (skin.sharedMesh == null) return false;
            var bones = skin.bones;
            int a = Array.IndexOf(bones, upper), b = Array.IndexOf(bones, lower), r = Array.IndexOf(bones, right);
            int h = Array.IndexOf(bones, hips), c = Array.IndexOf(bones, chest);
            if (a < 0 || b < 0 || r < 0 || h < 0 || c < 0) return false;
            var bind = skin.sharedMesh.bindposes;
            if (bind.Length != bones.Length) return false;
            Vector3 anchor = bind[a].inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 armUp = anchor - bind[b].inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 across = bind[r].inverse.MultiplyPoint3x4(Vector3.zero) - anchor;
            Vector3 torso = bind[c].inverse.MultiplyPoint3x4(Vector3.zero) - bind[h].inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 front = Vector3.Cross(across.normalized, torso.normalized).normalized;
            if (front.sqrMagnitude < 0.0001f || torso.sqrMagnitude < 1e-12f || armUp.sqrMagnitude < 1e-12f) return false;
            var body = Quaternion.LookRotation(front, torso);
            rotation = Quaternion.LookRotation(bind[a].MultiplyVector(front), bind[a].MultiplyVector(body * Vector3.up));
            // Measure scale in the same pose as the socket. A rotated arm under nonuniform character
            // scaling otherwise changes equipment size according to the frame in which it was acquired.
            Matrix4x4 restAxes = skin.transform.localToWorldMatrix * bind[a].inverse * Matrix4x4.Rotate(rotation);
            float x = restAxes.MultiplyVector(Vector3.right).magnitude;
            float y = restAxes.MultiplyVector(Vector3.up).magnitude;
            float z = restAxes.MultiplyVector(Vector3.forward).magnitude;
            if (Mathf.Min(x, Mathf.Min(y, z)) < 1e-6f) return false;
            float size = skin.transform.TransformVector(armUp).magnitude / FieldPauldronReferenceArmMetres;
            if (fit != null && shell.size.x > 0f && shell.size.z > 0f)
            {
                float width = skin.transform.TransformVector(body * Vector3.right * fit.CapWidthDepthInSkin.x).magnitude;
                float depth = skin.transform.TransformVector(body * Vector3.forward * fit.CapWidthDepthInSkin.y).magnitude;
                size = Mathf.Max(width / shell.size.x, depth / shell.size.z) * 1.035f;
            }
            scale = new Vector3(size / x, size / y, size / z);
            var centre = fit != null ? fit.CapCenterInUpperArm :
                rotation * Vector3.Scale(new Vector3(-0.057f, 0.045f, 0.018f), scale);
            position = centre - rotation * Vector3.Scale(shell.center, scale);
            return true;
        }

        private static Bounds FieldPauldronBounds(GameObject prefab)
        {
            var high = prefab.GetComponent<LODGroup>().GetLODs()[0].renderers[0];
            var bounds = high.GetComponent<MeshFilter>().sharedMesh.bounds;
            var map = prefab.transform.worldToLocalMatrix * high.localToWorldMatrix;
            var result = new Bounds(map.MultiplyPoint3x4(bounds.center), Vector3.zero);
            for (int i = 0; i < 8; ++i)
            {
                var corner = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y, (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                result.Encapsulate(map.MultiplyPoint3x4(corner));
            }
            return result;
        }

        private static bool TryGroundFieldPauldron(Transform host, float floor)
        {
            var prefab = Resources.Load<GameObject>(FieldPauldronPrefab);
            if (!PassiveArtVisuals.TryBindGroundedPrefab(host, prefab, floor)) return false;
            var art = host.GetChild(host.childCount - 1);
            art.name = "GroundedFieldPauldron";
            art.localRotation = Quaternion.Euler(12f, 26f, -18f);
            var renderer = art.GetComponentInChildren<MeshRenderer>();
            if (renderer != null) art.position += Vector3.up * (floor + 0.012f - renderer.bounds.min.y);
            return true;
        }

        private void ToggleFieldPauldron()
        {
            bool equip = !_game.Progression.Equipment.FieldPauldronEquipped;
            if (_game.TrySetFieldPauldronEquipped(equip))
            {
                _message = equip ? "Feldschulter angelegt." : "Feldschulter abgelegt.";
                SaveCharacter();
                UpdateEquipmentPresentation();
            }
            BuildMenu();
        }
    }
}
