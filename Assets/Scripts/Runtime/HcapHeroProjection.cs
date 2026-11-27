using System;
using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    // Presentation only. Sample after humanoid IK so an imported clip can drive this exact rig.
    [DefaultExecutionOrder(1200)]
    public sealed class HcapHeroProjection : MonoBehaviour
    {
        [Serializable] public sealed class Marker { public string name; public float contact; }
        [Serializable] public sealed class Index { public Marker[] clips; }
        private const string Folder = "ARE/Hcap/";
        private static Dictionary<string,float> markers;
        private static bool checkedIndex;
        private readonly Dictionary<string,AnimationClip> loaded = new Dictionary<string,AnimationClip>();
        private Transform[] bones;
        private Quaternion[] before;
        private Vector3[] positions;
        private AnimationClip motion;
        private float sampleTime, weight;
        private ulong sequence;
        private int stage = -1;
        public string SampledClip { get; private set; }
        public int SampleCount { get; private set; }

        internal static HcapHeroProjection Bind(Animator rig)
        {
            if (!checkedIndex)
            {
                checkedIndex = true;
                var index = Resources.Load<TextAsset>(Folder+"index");
                if (index != null)
                {
                    markers = new Dictionary<string,float>();
                    foreach (var row in JsonUtility.FromJson<Index>(index.text).clips)
                        markers.Add(row.name,row.contact);
                }
            }
            if (markers == null || rig == null || rig.GetComponent<EntryWeaponGrip>() == null) return null;
            if (rig.TryGetComponent<HcapHeroProjection>(out var bound)) return bound;
            var added = rig.gameObject.AddComponent<HcapHeroProjection>();
            added.bones = rig.GetComponentsInChildren<Transform>(true);
            added.before = new Quaternion[added.bones.Length];
            added.positions = new Vector3[added.bones.Length];
            return added;
        }

        internal void Accept(ulong actionSequence,int acceptedStage)
        {
            sequence = actionSequence;
            stage = acceptedStage;
        }

        internal void Project(ArenaSimulation world, float fraction, bool paused)
        {
            if (paused) return;
            motion = null;
            if (!world.CombatV2 || world.GameOver) return;
            var action = world.ActionKind;
            var weapon = world.Progression.Equipment.Weapon?.Definition;
            var timing = world.CurrentActionTiming;
            if (timing.Total <= 0) return;
            float tick = Mathf.Clamp(world.ActionTick + fraction,0,timing.Total);
            string key;
            bool attack = action == CombatActionKind.Strike || action == CombatActionKind.HeavyStrike;
            if (attack)
            {
                string family = "straight_sword";
                if (weapon != null)
                {
                    switch (weapon.Family)
                    {
                        case WeaponFamily.Sword: family = weapon.TwoHanded ? "greatsword" : "straight_sword"; break;
                        case WeaponFamily.Dagger: family = "dagger"; break;
                        case WeaponFamily.Axe: family = weapon.TwoHanded ? "greataxe" : "axe"; break;
                        case WeaponFamily.Mace: family = weapon.TwoHanded ? "great_hammer" : "hammer"; break;
                        case WeaponFamily.Spear: family = "spear"; break;
                        case WeaponFamily.Bow: family = "bow"; break;
                        case WeaponFamily.Staff: family = "staff"; break;
                        default: return;
                    }
                }
                // The equipped shield has a fitted off-hand IK path and its own accepted chain.
                if (world.Progression.Equipment.Shield != null) return;
                int stroke = sequence == world.ActionSequence && stage >= 0 ? stage : (action == CombatActionKind.HeavyStrike ? 3 : 0);
                key = family + AttackSuffixes[Mathf.Clamp(stroke,0,3)];
            }
            else if (action == CombatActionKind.Dodge) key = "dodge_forward";
            else return;
            if (!markers.TryGetValue(key,out float contact)) return;
            if (!loaded.TryGetValue(key,out motion))
            {
                motion = Resources.Load<AnimationClip>(Folder+key);
                loaded.Add(key,motion);
            }
            if (motion == null) return;
            float impact = timing.Startup+1f;
            float phase = attack ? (tick <= impact ? contact * Mathf.InverseLerp(0,impact,tick) :
                Mathf.Lerp(contact,1,Mathf.InverseLerp(impact,timing.Total,tick))) : tick / timing.Total;
            sampleTime = phase * motion.length;
            // Return the weapon to the established finish and held chain pose in late recovery.
            weight = Mathf.SmoothStep(0,1,Mathf.Clamp01(tick*2)) *
                     Mathf.SmoothStep(0,1,Mathf.Clamp01((timing.Total-tick)/(attack ? Mathf.Max(1,timing.Recovery*.7f) : 1f)));
        }
        private static readonly string[] AttackSuffixes = {"__attack_01","__attack_02","__attack_03","__attack_04"};

        private void LateUpdate()
        {
            if (motion == null || weight <= 0f || bones == null) { SampledClip = null; return; }
            for (int i=0;i<bones.Length;i++)
            {
                var bone=bones[i];
                if (bone == null) continue;
                before[i]=bone.localRotation; positions[i]=bone.localPosition;
            }
            motion.SampleAnimation(gameObject,sampleTime);
            if (weight < 1f)
                for (int i=0;i<bones.Length;i++)
                {
                    var bone=bones[i]; if (bone == null) continue;
                    bone.localRotation=Quaternion.Slerp(before[i],bone.localRotation,weight);
                    bone.localPosition=Vector3.Lerp(positions[i],bone.localPosition,weight);
                }
            SampledClip=motion.name; SampleCount++;
        }
    }
}
