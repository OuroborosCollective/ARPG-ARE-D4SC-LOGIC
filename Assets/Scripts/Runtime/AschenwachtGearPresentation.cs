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
        /// <summary>
        /// The hero's two hair bones. Under a worn helm his hair is hidden like a model taken off (owner 2026-11-26): the two
        /// bones shrink to nearly nothing, so all hair hanging on them collapses into its roots inside the head.
        /// </summary>
        private static readonly string[] HairRoots = { "FrontHair", "BackHair" };
        private const float HiddenHairSize = .001f;

        private static bool IsBakedArmor(RegionalRelic id)
        {
            return id == RegionalRelic.A_Visierhelm || id == RegionalRelic.A_Kuerass || id == RegionalRelic.A_Schulterplatten || id == RegionalRelic.A_Gliedpanzer ||
                NachtvesperSet.IsPiece(id) || ((RotfaehrteSet.IsPiece(id) || RusskantorSet.IsPiece(id)) && RegionalRelics.HandDefinition(id) == null);
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
            var head = RegionalRelics.Slot(id) != RegionalRelicSlot.Head ? HeadWear.None :
                id == RegionalRelic.F_Gugel || id == RegionalRelic.K_Kapuze ? HeadWear.Hood : HeadWear.Helm;
            return MountBakedPrefab(RegionalSourcePrefab(id), id.ToString(), head);
        }

        /// <summary>The hunter's back quiver, worn while the slate longbow is in hand (HunterCantorBuilder.Quiver).</summary>
        private const string HunterQuiver = "Werkarsenal/F_Koecher";

        /// <summary>What a piece does to the hair: nothing, hide it under a closed helm, or keep the fringe in an open hood.</summary>
        private enum HeadWear { None, Helm, Hood }

        private bool MountBakedPrefab(GameObject prefab, string label, HeadWear head)
        {
            Animator avatar = null;
            if (_hero) foreach (var candidate in _hero.GetComponentsInChildren<Animator>(true))
                if (candidate.isHuman && candidate.avatar && candidate.avatar.isValid) { avatar = candidate; break; }
            if (!prefab || !avatar) return false;
            var holder = new GameObject("Equipped_" + label).transform;
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
            if (head != HeadWear.None) HideHair(avatar.GetBoneTransform(HumanBodyBones.Head), head == HeadWear.Hood);
            return true;
        }

        /// <summary>
        /// A worn helm hides the hero's hair: his stylized hair stands far out from his head, and a helm large enough to
        /// cover it would sit on him like a bucket. An open hood hides the hair at the back and keeps the front hair, drawn
        /// in to <see cref="HoodFringeSize"/>, framing the face. Taking the piece off brings the hair back.
        /// </summary>
        private const float HoodFringeSize = .75f;
        private void HideHair(Transform head, bool hood)
        {
            if (!head) return;
            if (!_hairUnderHelm || _hairUnderHelm.transform != head)
                _hairUnderHelm = head.TryGetComponent(out HairUnderHelm held) ? held : head.gameObject.AddComponent<HairUnderHelm>();
            foreach (var bone in head.GetComponentsInChildren<Transform>(true))
                if (Array.IndexOf(HairRoots, bone.name) >= 0) _hairUnderHelm.Tuck(bone, hood && bone.name == "FrontHair" ? HoodFringeSize : HiddenHairSize);
        }
    }
}
