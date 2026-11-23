using System.Collections.Generic;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// The faces of the story figures come alive (owner remark of 2026-11-23): they blink, keep a mood that fits their
    /// role and move their mouth while their line is heard. Only figures baked with the pack's expression shapes take
    /// part; everybody else is left as it is. Presentation only: the Core knows nothing of faces.
    /// </summary>
    public sealed partial class ArenaRuntime
    {
        private const string BlinkShapeLeft = "Expression_Eyes_UpperClosed_L", BlinkShapeRight = "Expression_Eyes_UpperClosed_R";
        private const string MouthShapeLeft = "Expression_Mouth_Oh_L", MouthShapeRight = "Expression_Mouth_Oh_R";

        private sealed class FaceRig
        {
            public Transform Root;
            public SkinnedMeshRenderer Skin;
            public int[] Blink, Mouth; // each shape with its twins (a beard follows the mouth with shapes of its own)
            public float NextBlink, BlinkUntil, SpeakUntil;
        }

        private readonly List<FaceRig> _faces = new List<FaceRig>();

        /// <summary>The mood a story figure keeps on its face, by the name of its scene object.</summary>
        private static KeyValuePair<string, float>[] FaceMood(string rootName)
        {
            switch (rootName)
            {
                case "ARE_NPC_0": // Ilyan: hungry, hiding his secret
                    return new[] { Pair("Expression_Brows_Sad_L", 45f), Pair("Expression_Brows_Sad_R", 45f), Pair("Expression_Mouth_Sad_L", 15f), Pair("Expression_Mouth_Sad_R", 15f) };
                case "ARE_NPC_1": // Mara: keeps the camp together
                    return new[] { Pair("Expression_Mouth_Happy_L", 18f), Pair("Expression_Mouth_Happy_R", 18f) };
                case "ARE_NPC_2": // Dorn: the guard
                    return new[] { Pair("Expression_Brow_Angry_L", 30f), Pair("Expression_Brow_Angry_R", 30f), Pair("Expression_Brows_Lowered_L", 20f), Pair("Expression_Brows_Lowered_R", 20f) };
                case "ARE_Road_Hunter": // the hunter on the ash road, later the companion: watchful
                case "ARE_Companion_Hunter":
                    return new[] { Pair("Expression_Brows_Lowered_L", 20f), Pair("Expression_Brows_Lowered_R", 20f) };
                case "ARE_Orsel_Healer": // Orsel: gruff, caring in deed
                    return new[] { Pair("Expression_Brows_Lowered_L", 35f), Pair("Expression_Brows_Lowered_R", 35f), Pair("Expression_Brow_Angry_L", 10f), Pair("Expression_Brow_Angry_R", 10f) };
                default:
                    return new KeyValuePair<string, float>[0];
            }
        }

        private static KeyValuePair<string, float> Pair(string shape, float weight) { return new KeyValuePair<string, float>(shape, weight); }

        /// <summary>
        /// The shapes of the merged mesh that answer to a name: the face's own and the twins that other parts bring
        /// under the same name with a number (a beard's mouth shapes are called "…_1").
        /// </summary>
        private static int[] ShapesNamed(Mesh mesh, params string[] names)
        {
            var found = new List<int>();
            for (int shape = 0; shape < mesh.blendShapeCount; shape++)
            {
                string name = mesh.GetBlendShapeName(shape);
                foreach (string wanted in names)
                {
                    if (name == wanted || (name.Length > wanted.Length + 1 && name.StartsWith(wanted + "_", System.StringComparison.Ordinal) &&
                        int.TryParse(name.Substring(wanted.Length + 1), out _))) { found.Add(shape); break; }
                }
            }
            return found.ToArray();
        }

        /// <summary>Find a figure's expression shapes once, when it enters the scene; set its mood.</summary>
        private void RegisterFace(Transform root)
        {
            if (root == null) return;
            for (int i = _faces.Count - 1; i >= 0; i--) if (_faces[i].Root == root || _faces[i].Root == null) _faces.RemoveAt(i);
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh mesh = skin.sharedMesh;
                if (mesh == null) continue;
                if (mesh.GetBlendShapeIndex(BlinkShapeLeft) < 0) continue;
                var face = new FaceRig
                {
                    Root = root, Skin = skin, Blink = ShapesNamed(mesh, BlinkShapeLeft, BlinkShapeRight),
                    Mouth = ShapesNamed(mesh, MouthShapeLeft, MouthShapeRight), NextBlink = Time.time + Random.Range(0.4f, 3.5f)
                };
                foreach (var mood in FaceMood(root.name))
                    foreach (int shape in ShapesNamed(mesh, mood.Key)) skin.SetBlendShapeWeight(shape, mood.Value);
                _faces.Add(face);
            }
        }

        /// <summary>Let a figure's mouth move for a while, as when it speaks a line without a recording.</summary>
        private void FaceSpeak(Transform root, float seconds)
        {
            foreach (var face in _faces) if (face.Root == root) face.SpeakUntil = Time.time + seconds;
        }

        /// <summary>The figure whose recorded line is heard right now, if it has a face that takes part.</summary>
        private Transform SpeakingFigure()
        {
            if (_atmosphere == null || string.IsNullOrEmpty(_atmosphere.Subtitle)) return null;
            int speaker = _atmosphere.SpeakerId;
            // Speaker 3 is the hunter (as for the camera): on the ash road, or at the player's side once he follows.
            if (speaker == 3) return _companionVisual != null && _companionVisual.gameObject.activeInHierarchy ? _companionVisual : _roadHunter;
            return speaker >= 0 && _npcVisuals.TryGetValue(speaker, out var visual) ? visual : null;
        }

        /// <summary>Blink every few seconds, open and close the mouth while speaking; faces off screen rest.</summary>
        private void UpdateFaceLife()
        {
            if (_faces.Count == 0) return;
            float now = Time.time;
            Transform speaking = SpeakingFigure();
            for (int i = _faces.Count - 1; i >= 0; i--)
            {
                FaceRig face = _faces[i];
                if (face.Skin == null || face.Root == null) { _faces.RemoveAt(i); continue; }
                if (!face.Skin.isVisible) continue;
                if (now >= face.NextBlink) { face.BlinkUntil = now + 0.13f; face.NextBlink = now + Random.Range(2.4f, 6f); }
                float blink = now < face.BlinkUntil ? 100f : 0f;
                foreach (int shape in face.Blink) face.Skin.SetBlendShapeWeight(shape, blink);
                bool talking = face.Root == speaking || now < face.SpeakUntil;
                float mouth = talking ? (0.5f + 0.5f * Mathf.Sin(now * 17f) * Mathf.Sin(now * 5.3f + 1f)) * 65f : 0f;
                foreach (int shape in face.Mouth) face.Skin.SetBlendShapeWeight(shape, mouth);
            }
        }
    }
}
