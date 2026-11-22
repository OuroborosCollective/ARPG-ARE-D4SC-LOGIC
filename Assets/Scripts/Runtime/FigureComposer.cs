using System.Collections.Generic;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// Figures built in two parts (a body renderer and a head renderer on one humanoid skeleton) can take the head of
    /// another such figure. The head keeps its own mesh and colours and is skinned to the bones of the body, so it sits
    /// on that body's neck and follows its animation. Bones a head brings along that the body lacks (hair, hood, plume)
    /// are copied under the same parent bone. Presentation only: nothing here is read by the Core.
    /// </summary>
    public static class FigureComposer
    {
        public const string BodyPart = "Body", HeadPart = "Head";
        const string ArtPrefix = "BozoFigure_";

        /// <summary>The renderer of one part of a figure, or null for a figure in one piece.</summary>
        public static SkinnedMeshRenderer Part(Transform figure, string part)
        {
            if (figure == null) return null;
            foreach (SkinnedMeshRenderer skin in figure.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (skin.name == part) return skin;
            return null;
        }

        /// <summary>A figure built in two parts: only these take or give a head.</summary>
        public static bool IsComposite(GameObject figure)
        {
            return figure != null && Part(figure.transform, BodyPart) != null && Part(figure.transform, HeadPart) != null;
        }

        /// <summary>
        /// Put the head of <paramref name="headFigure"/> (a prefab or an instance, left unchanged) on the body of the
        /// figure instance <paramref name="body"/>. False, with the body's own head kept, when either is not built in
        /// two parts or a bone of the head cannot be placed.
        /// </summary>
        public static bool TryPutHead(Transform body, GameObject headFigure)
        {
            SkinnedMeshRenderer target = Part(body, HeadPart), torso = Part(body, BodyPart);
            SkinnedMeshRenderer donor = headFigure != null ? Part(headFigure.transform, HeadPart) : null;
            if (target == null || torso == null || donor == null || donor.sharedMesh == null) return false;
            Transform bodyArt = ArtRoot(torso.transform, body), donorArt = ArtRoot(donor.transform, headFigure.transform);
            // Only the skeleton counts: the pack keeps empty helpers with bone names beside it (MagicaClothSupport/FrontHair),
            // and a hair bound to such a helper would hang in the air.
            var byName = new Dictionary<string, Transform>();
            Transform rootBone = torso.rootBone != null ? torso.rootBone : bodyArt;
            foreach (Transform bone in rootBone.GetComponentsInChildren<Transform>(true))
                if (!byName.ContainsKey(bone.name)) byName.Add(bone.name, bone);
            foreach (Transform bone in torso.bones)
                if (bone != null && !byName.ContainsKey(bone.name)) byName.Add(bone.name, bone);
            for (Transform up = rootBone.parent; up != null && up != bodyArt; up = up.parent)
                if (!byName.ContainsKey(up.name)) byName.Add(up.name, up);
            Transform[] source = donor.bones;
            var mapped = new Transform[source.Length];
            var made = new List<Transform>();
            for (int i = 0; i < source.Length; i++)
            {
                mapped[i] = Resolve(source[i], donorArt, bodyArt, byName, made);
                if (mapped[i] != null) continue;
                // Nothing half done: bones copied for this head go again, the body keeps its own head.
                for (int m = made.Count - 1; m >= 0; m--)
                {
                    if (made[m] == null) continue;
                    if (Application.isPlaying) Object.Destroy(made[m].gameObject); else Object.DestroyImmediate(made[m].gameObject);
                }
                return false;
            }
            target.sharedMesh = donor.sharedMesh;
            target.sharedMaterials = donor.sharedMaterials;
            target.bones = mapped;
            target.rootBone = torso.rootBone;
            target.localBounds = torso.localBounds;
            return true;
        }

        static Transform Resolve(Transform source, Transform donorArt, Transform bodyArt, Dictionary<string, Transform> byName, List<Transform> made)
        {
            if (source == null) return null;
            if (source == donorArt) return bodyArt;
            if (byName.TryGetValue(source.name, out Transform have)) return have;
            Transform parent = Resolve(source.parent, donorArt, bodyArt, byName, made);
            if (parent == null) return null;
            var copy = new GameObject(source.name).transform;
            copy.SetParent(parent, false);
            copy.localPosition = source.localPosition; copy.localRotation = source.localRotation; copy.localScale = source.localScale;
            byName.Add(source.name, copy);
            made.Add(copy);
            return copy;
        }

        /// <summary>The scaled figure inside a visual (the pack's character root), else the visual itself.</summary>
        static Transform ArtRoot(Transform from, Transform visual)
        {
            for (Transform up = from; up != null && up != visual; up = up.parent)
                if (up.name.StartsWith(ArtPrefix, System.StringComparison.Ordinal)) return up;
            return visual;
        }
    }
}
