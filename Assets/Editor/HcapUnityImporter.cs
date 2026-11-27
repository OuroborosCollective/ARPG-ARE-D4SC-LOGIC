using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace Hcap.Editor
{
    [ScriptedImporter(2, "hcap")]
    public sealed class HcapUnityImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            using var r = new BinaryReader(File.OpenRead(ctx.assetPath));
            if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "HCA1") throw new InvalidDataException("HCAP header");
            int frames = r.ReadInt32();
            float fps = r.ReadSingle(), contact = r.ReadSingle();
            bool loop = r.ReadInt32() == 1;
            int tracks = r.ReadInt32();
            if (frames < 2 || frames > 10000 || !(fps > 0 && fps <= 240) || tracks < 1 || tracks > 2048)
                throw new InvalidDataException("HCAP track dimensions");
            var clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(ctx.assetPath), frameRate = fps, legacy = true };
            var bindings = new List<EditorCurveBinding>();
            var curves = new List<AnimationCurve>();
            for (int t = 0; t < tracks; t++)
            {
                int bytes = r.ReadUInt16(), kind = r.ReadByte();
                string path = Encoding.UTF8.GetString(r.ReadBytes(bytes));
                if (kind > 2 || string.IsNullOrEmpty(path)) throw new InvalidDataException("HCAP binding");
                string property = kind == 0 ? "m_LocalRotation" : kind == 1 ? "m_LocalPosition" : "m_LocalScale";
                int width = kind == 0 ? 4 : 3;
                var values = new float[frames, width];
                for (int f = 0; f < frames; f++)
                    for (int axis = 0; axis < width; axis++)
                    {
                        float value = r.ReadSingle();
                        if (!float.IsFinite(value)) throw new InvalidDataException("Invalid bone transform");
                        values[f, axis] = value;
                    }
                for (int axis = 0; axis < width; axis++)
                {
                    var keys = new Keyframe[frames];
                    for (int f = 0; f < frames; f++)
                    {
                        float value = values[f, axis];
                        float incoming = f == 0 ? 0 : (value-values[f-1, axis])*fps;
                        float outgoing = f+1 == frames ? 0 : (values[f+1, axis]-value)*fps;
                        keys[f] = new Keyframe(f / fps, value, incoming, outgoing);
                    }
                    bindings.Add(EditorCurveBinding.FloatCurve(path,typeof(Transform),property+"."+"xyzw"[axis]));
                    curves.Add(new AnimationCurve(keys));
                }
            }
            if (r.BaseStream.Position != r.BaseStream.Length) throw new InvalidDataException("Trailing HCAP bytes");
            AnimationUtility.SetEditorCurves(clip,bindings.ToArray(),curves.ToArray());
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip,settings);
            clip.wrapMode = WrapMode.ClampForever;
            ctx.AddObjectToAsset("clip",clip);
            ctx.SetMainObject(clip);
        }
    }
}
