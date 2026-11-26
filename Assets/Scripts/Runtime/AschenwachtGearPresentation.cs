using System;
using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        private readonly List<GameObject> _bakedArmor = new List<GameObject>();
        private HairUnderHelm _hairUnderHelm;
        /// <summary>The hero's two hair bones, and how far his hair is drawn in under a helm (it then lies inside the helm's shell).</summary>
        private static readonly string[] HairRoots = { "FrontHair", "BackHair" };
        private const float HairUnderHelmFactor = .7f;

        private static bool IsBakedArmor(RegionalRelic id)
        {
            return id == RegionalRelic.A_Visierhelm || id == RegionalRelic.A_Kuerass || id == RegionalRelic.A_Schulterplatten || id == RegionalRelic.A_Gliedpanzer ||
                NachtvesperSet.IsPiece(id);
        }

        private void ClearBakedArmor()
        {
            foreach (var piece in _bakedArmor) if (piece) { piece.SetActive(false); Destroy(piece); }
            _bakedArmor.Clear();
            if (_hairUnderHelm) _hairUnderHelm.Release();
        }

        /// <summary>
        /// Put on one piece of the Glockenwacht's armour (owner decision 2026-11-26). Its private prefab holds a mesh already
        /// fitted to the hero's bind pose and weighted to his humanoid bones; its bone list is a row of transforms named
        /// <c>Bone_</c> plus the humanoid bone. Here the renderer is pointed at the hero's own bones, so the armour moves with
        /// him like his clothes.
        /// </summary>
        private bool MountBakedArmor(RegionalRelic id)
        {
            var prefab = RegionalSourcePrefab(id);
            Animator avatar = null;
            if (_hero) foreach (var candidate in _hero.GetComponentsInChildren<Animator>(true))
                if (candidate.isHuman && candidate.avatar && candidate.avatar.isValid) { avatar = candidate; break; }
            if (!prefab || !avatar) return false;
            var holder = new GameObject("Equipped_" + id).transform;
            holder.SetParent(avatar.transform, false);
            if (!PassiveArtVisuals.TryBindPrefab(holder, prefab)) { Destroy(holder.gameObject); return false; }
            foreach (var skin in holder.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bones = skin.bones;
                var mapped = new Transform[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                {
                    string name = bones[i] ? bones[i].name : "";
                    if (!name.StartsWith("Bone_", StringComparison.Ordinal) ||
                        !Enum.TryParse(name.Substring(5), out HumanBodyBones human) || !(mapped[i] = avatar.GetBoneTransform(human)))
                    { Destroy(holder.gameObject); return false; }
                }
                skin.bones = mapped;
                skin.rootBone = avatar.GetBoneTransform(HumanBodyBones.Hips);
                skin.updateWhenOffscreen = true;
            }
            _bakedArmor.Add(holder.gameObject);
            if (RegionalRelics.Slot(id) == RegionalRelicSlot.Head) TuckHair(avatar.GetBoneTransform(HumanBodyBones.Head));
            return true;
        }

        /// <summary>
        /// Under a helm the hero's hair is drawn in toward its roots: his stylized hair stands far out from his head, and a
        /// helm large enough to cover it would sit on him like a bucket. Taking the helm off gives the hair back its size.
        /// </summary>
        private void TuckHair(Transform head)
        {
            if (!head) return;
            if (!_hairUnderHelm || _hairUnderHelm.transform != head)
                _hairUnderHelm = head.TryGetComponent(out HairUnderHelm held) ? held : head.gameObject.AddComponent<HairUnderHelm>();
            foreach (var bone in head.GetComponentsInChildren<Transform>(true))
                if (Array.IndexOf(HairRoots, bone.name) >= 0) _hairUnderHelm.Tuck(bone, HairUnderHelmFactor);
        }
    }
}
