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
        private const string TestParent = "Assets/PrivateArt/VisualPass1/VikingVillage";
        private const string TestRoot = TestParent + "/ZZ_WF_AssetIntake_EditMode_Test";

        private static GameObject CreateSourcePrefab(string name, out string guid)
        {
            Assert.IsTrue(AssetDatabase.IsValidFolder(TestParent),
                "Real VikingVillage private asset package required for isolated native test");
            Assert.IsFalse(AssetDatabase.IsValidFolder(TestRoot),
                "Pre-existing temporary directory must not be overwritten");
            AssetDatabase.CreateFolder(TestParent, "ZZ_WF_AssetIntake_EditMode_Test");
            GameObject go = null;
            bool ok = false;
            try
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                string path = TestRoot + "/" + name + ".prefab";
                Assert.IsNotNull(PrefabUtility.SaveAsPrefabAsset(go, path));
                guid = AssetDatabase.AssetPathToGUID(path);
                Assert.AreEqual(32, guid.Length);
                GameObject loaded = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(loaded);
                ok = true;
                return loaded;
            }
            finally
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
                if (!ok) AssetDatabase.DeleteAsset(TestRoot);
            }
        }
        [Test]
        public void SceneOwnedActorsAndEffectsNeverBecomeDecoration()
        {
            var actor = new GameObject("MillPatient_A");
            var effect = new GameObject("Dust");
            var sword = new GameObject("Sword");
            var rattler = new GameObject("CryptRattler");
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
                    WFAssetIntakeEvaluator.Assess(rattler, "Assets/PrivateArt/VisualPass1/VikingVillage/Thing.prefab").kind);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
                UnityEngine.Object.DestroyImmediate(effect);
                UnityEngine.Object.DestroyImmediate(sword);
                UnityEngine.Object.DestroyImmediate(rattler);
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
            string guid;
            var prefab = CreateSourcePrefab("ApprovedApple", out guid);
            var biome = ScriptableObject.CreateInstance<BiomeProfile>();
            try
            {
                var v = WFAssetIntakeEvaluator.Assess(prefab, AssetDatabase.GetAssetPath(prefab));
                Assert.AreEqual(WFAssetIntakeKind.Spawn, v.kind);
                WFAssetIntakeApprovals.ConfirmSpawn(biome, prefab, guid, v);
                Assert.AreEqual(1, biome.spawnRules.Count);
                Assert.AreEqual(guid, biome.spawnRules[0].approvedAssetGuid);
                Assert.AreSame(prefab, biome.spawnRules[0].prefabs[0]);
                WFAssetIntakeApprovals.ConfirmSpawn(biome, prefab, guid, v);
                Assert.AreEqual(1, biome.spawnRules.Count);
                Assert.Throws<InvalidOperationException>(() => WFAssetIntakeApprovals.ConfirmSpawn(
                    biome, prefab, "aabbccddeeff00112233445566778899", v),
                    "Spoofed caller-supplied GUID must never be accepted");
                biome.spawnRules[0].prefabs.Clear(); // missing private art must retain approval
                Assert.AreEqual(guid, biome.spawnRules[0].approvedAssetGuid);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(biome);
                AssetDatabase.DeleteAsset(TestRoot);
            }
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
            string guid;
            var prefab = CreateSourcePrefab("ApprovedBuilding", out guid);
            var profile = ScriptableObject.CreateInstance<WorldProfile>();
            try
            {
                var verdict = WFAssetIntakeEvaluator.Assess(prefab, AssetDatabase.GetAssetPath(prefab));
                Assert.AreEqual(WFAssetIntakeKind.Spawn, verdict.kind);
                WFAssetIntakeApprovals.ConfirmPoi(profile, prefab, guid, verdict);
                Assert.AreEqual(1, profile.pois.Count);
                Assert.AreEqual(guid, profile.pois[0].approvedAssetGuid);
                Assert.IsTrue(profile.pois[0].intakePlacementPending,
                    "Approval is not placement: a new POI must stay pending");
                Assert.AreEqual(0f, profile.pois[0].flattenRadius,
                    "Unplaced asset must not flatten terrain");
                Assert.AreEqual(0f, profile.pois[0].clearSpawnRadius,
                    "Unplaced asset must not clear any spawn area");
                WFAssetIntakeApprovals.ConfirmPoi(profile, prefab, guid, verdict);
                Assert.AreEqual(1, profile.pois.Count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                AssetDatabase.DeleteAsset(TestRoot);
            }
        }

        [Test]
        public void InvalidApprovedPoiGuidFailsBeforeAnyTerrainOrSceneMutation()
        {
            string guid;
            var prefab = CreateSourcePrefab("PreflightBuilding", out guid);
            var profile = ScriptableObject.CreateInstance<WorldProfile>();
            try
            {
                profile.rootObjectName = "ZZ_WF_Intake_Preflight_MustNotSpawn";
                Assert.IsNull(GameObject.Find(profile.rootObjectName));
                string wrongGuid = guid == new string('0', 32)
                    ? new string('1', 32) : new string('0', 32);
                profile.pois.Add(new WFPoiDefinition
                {
                    id = "preflight.invalid-guid",
                    decorativePrefab = prefab,
                    approvedAssetGuid = wrongGuid,
                    intakePlacementPending = false,
                    flattenRadius = 4f
                });
                var failure = Assert.Throws<InvalidOperationException>(() =>
                    WFTerrainBuilder.GenerateAll(profile));
                StringAssert.Contains("POI_ASSET_GUID_DRIFT", failure.Message);
                Assert.IsNull(GameObject.Find(profile.rootObjectName),
                    "Invalid GUID preflight must reject before generating a world root");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                AssetDatabase.DeleteAsset(TestRoot);
            }
        }

        [Test]
        public void UnplacedPoiWithLegacyFlattenRadiusCannotChangeHeightGrid()
        {
            var profile = ScriptableObject.CreateInstance<WorldProfile>();
            try
            {
                profile.pois.Add(new WFPoiDefinition
                {
                    intakePlacementPending = true, position = new Vector2(10f, 10f),
                    flattenRadius = 20f, flattenFalloff = 2f, clearSpawnRadius = 20f
                });
                var ctx = new WFHeightContext(profile);
                for (int i = 0; i < ctx.Heights.Length; i++)
                    ctx.Heights[i] = i % 17;
                float[] before = (float[])ctx.Heights.Clone();
                new WFFlattenPoiLayer().Apply(ctx);
                CollectionAssert.AreEqual(before, ctx.Heights,
                    "Pending POIs, including old saved approvals with nonzero radius, must be inert");
            }
            finally { UnityEngine.Object.DestroyImmediate(profile); }
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
