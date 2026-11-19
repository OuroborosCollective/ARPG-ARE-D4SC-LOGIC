using System;
using System.Collections.Generic;
using AshenCrypt.WorldForge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AshenCrypt.WorldForge.Tests
{
    public class AssetIntakeEditModeTests
    {
        [Test]
        public void SceneOwnedActorsAndEffectsNeverBecomeDecoration()
        {
            var actor = new GameObject("MillPatient_A");
            var effect = new GameObject("Dust");
            var sword = new GameObject("Sword");
            try
            {
                actor.AddComponent<Animator>();
                effect.AddComponent<ParticleSystem>();
                Assert.AreEqual(WFAssetIntakeKind.Protected,
                    WFAssetIntakeEvaluator.Assess(actor, "Assets/PrivateArt/OwnerForestReview/Prefabs/Patient.prefab").kind);
                Assert.AreEqual(WFAssetIntakeKind.Protected,
                    WFAssetIntakeEvaluator.Assess(effect, "Assets/PrivateArt/OwnerForestReview/Prefabs/Dust.prefab").kind);
                Assert.AreEqual(WFAssetIntakeKind.Protected,
                    WFAssetIntakeEvaluator.Assess(sword, "Assets/PrivateArt/WoodlandWeapons/Sword.prefab").kind);
                Assert.AreEqual(WFAssetIntakeKind.Protected,
                    WFAssetIntakeEvaluator.Assess(new GameObject("CryptRattler"), "Assets/PrivateArt/VisualPass1/VikingVillage/Thing.prefab").kind);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
                UnityEngine.Object.DestroyImmediate(effect);
                UnityEngine.Object.DestroyImmediate(sword);
            }
        }

        [Test]
        public void UnknownPackageIsNotAdmittedFromKeywordGuess()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                Assert.AreEqual(WFAssetIntakeKind.ReviewRequired,
                    WFAssetIntakeEvaluator.Assess(go, "Assets/PrivateArt/NewUnreviewedPackage/Trees/Tree.prefab").kind);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void RendererGeometryNotGlobalObjectPositionSetsPoiClearance()
        {
            var go = new GameObject("VillageHouse");
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.transform.SetParent(go.transform, false);
            child.transform.localScale = new Vector3(12f, 5f, 8f);
            try
            {
                WFAssetIntakeVerdict v = WFAssetIntakeEvaluator.Assess(go,
                    "Assets/PrivateArt/VisualPass1/VikingVillage/Buildings/House.prefab");
                Assert.AreEqual(WFAssetIntakeKind.Poi, v.kind);
                Assert.GreaterOrEqual(v.clearanceRadius, 1f + Mathf.Sqrt(12f * 12f + 8f * 8f) * 0.5f - .01f);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void ConfirmedSpawnWritesOnlyExistingBiomeAndKeepsGuid()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var biome = ScriptableObject.CreateInstance<BiomeProfile>();
            try
            {
                var v = WFAssetIntakeEvaluator.Assess(go,
                    "Assets/PrivateArt/DesignAssets/StylizedFruitProps/Apple.prefab");
                Assert.AreEqual(WFAssetIntakeKind.Spawn, v.kind);
                WFAssetIntakeApprovals.ConfirmSpawn(biome, go, "aabbccddeeff00112233445566778899", v);
                Assert.AreEqual(1, biome.spawnRules.Count);
                Assert.AreEqual("aabbccddeeff00112233445566778899",
                    biome.spawnRules[0].approvedAssetGuid);
                Assert.AreSame(go, biome.spawnRules[0].prefabs[0]);
                WFAssetIntakeApprovals.ConfirmSpawn(biome, go, "aabbccddeeff00112233445566778899", v);
                Assert.AreEqual(1, biome.spawnRules.Count);
                biome.spawnRules[0].prefabs.Clear(); // emulate missing private asset, GUID must remain
                Assert.AreEqual("aabbccddeeff00112233445566778899",
                    biome.spawnRules[0].approvedAssetGuid);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(biome); }
        }

        [Test]
        public void ProtectedObjectCannotBeForcedThroughApproval()
        {
            var go = new GameObject("EntryHero");
            var biome = ScriptableObject.CreateInstance<BiomeProfile>();
            try
            {
                var verdict = WFAssetIntakeEvaluator.Assess(go,
                    "Assets/PrivateArt/VisualPass1/VikingVillage/Hero.prefab");
                Assert.Throws<InvalidOperationException>(() => WFAssetIntakeApprovals.ConfirmSpawn(
                    biome, go, "aabbccddeeff00112233445566778899", verdict));
                Assert.IsEmpty(biome.spawnRules);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(biome); }
        }

        [Test]
        public void PoiApprovalUpdatesExistingWorldProfileWithoutDuplicateOrAutoSave()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var profile = ScriptableObject.CreateInstance<WorldProfile>();
            try
            {
                var verdict = WFAssetIntakeEvaluator.Assess(go,
                    "Assets/PrivateArt/VisualPass1/VikingVillage/Building.prefab");
                WFAssetIntakeApprovals.ConfirmPoi(profile, go, "00112233445566778899aabbccddeeff", verdict);
                Assert.AreEqual(1, profile.pois.Count);
                Assert.AreEqual("00112233445566778899aabbccddeeff", profile.pois[0].approvedAssetGuid);
                WFAssetIntakeApprovals.ConfirmPoi(profile, go, "00112233445566778899aabbccddeeff", verdict);
                Assert.AreEqual(1, profile.pois.Count);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(profile); }
        }

        [Test]
        public void AllRealPrivateAssetsAreClassifiedWithoutProtectedAsDecor()
        {
            var guids = AssetDatabase.FindAssets("t:GameObject", new[] {"Assets/PrivateArt"});
            Assert.GreaterOrEqual(guids.Length, 791, "Real private asset set required; no fake 791 fixtures");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var total = 0; var protectedCount = 0; var reviewCount = 0; var candidateCount = 0;
            foreach (string guid in guids)
            {
                if (!seen.Add(guid)) continue;
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                WFAssetIntakeVerdict verdict = WFAssetIntakeEvaluator.Assess(prefab, path);
                total++;
                if (verdict.kind == WFAssetIntakeKind.Protected) protectedCount++;
                if (verdict.kind == WFAssetIntakeKind.ReviewRequired) reviewCount++;
                if (verdict.kind == WFAssetIntakeKind.Spawn || verdict.kind == WFAssetIntakeKind.Poi)
                {
                    candidateCount++;
                    Assert.IsFalse(WFAssetIntakeEvaluator.HasGameplayOrEffectComponents(prefab),
                        "An actor or effect entered decoration: " + path);
                    Assert.IsFalse(WFAssetIntakeEvaluator.IsProtectedPackage(path),
                        "A protected package entered decoration: " + path);
                }
            }
            Assert.GreaterOrEqual(total, 791);
            Debug.Log("[WFAssetIntake] real assets=" + total + " protected=" + protectedCount +
                " review=" + reviewCount + " candidates=" + candidateCount +
                " real coverage=" + (100f * (protectedCount + reviewCount + candidateCount) / total) + "%");
        }
    }
}
