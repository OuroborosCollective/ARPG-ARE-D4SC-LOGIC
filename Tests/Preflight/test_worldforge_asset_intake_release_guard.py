"""Static release guards for WorldForge asset-intake effects.

These tests inspect repository production source, NOT the Unity Editor or 791 private assets.
Native EditMode tests and signed/physical Android evidence remain separate gates.
"""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
WF = ROOT / "Assets" / "AshenCrypt" / "WorldForge"
TERRAIN = (WF / "Editor" / "WFTerrainBuilder.cs").read_text(encoding="utf-8")
HEIGHT = (WF / "Runtime" / "WFHeightLayers.cs").read_text(encoding="utf-8")
INTAKE = (WF / "Editor" / "WFAssetIntakeEvaluator.cs").read_text(encoding="utf-8")
WINDOW = (WF / "Editor" / "WFAssetIntakeWindow.cs").read_text(encoding="utf-8")
NATIVE = (WF / "Tests" / "Editor" / "AssetIntakeEditModeTests.cs").read_text(encoding="utf-8")

class WorldForgeIntakeSafetySourceContract(unittest.TestCase):
    def test_pending_approval_cannot_change_terrain_or_spawn_clearance(self):
        self.assertIn("poi.intakePlacementPending", HEIGHT)
        poi = INTAKE.split("public static void ConfirmPoi(", 1)[1]
        self.assertIn("flattenRadius = 0f", poi)
        self.assertIn("clearSpawnRadius = 0f", poi)
        self.assertIn("if (def.intakePlacementPending)", TERRAIN)

    def test_poi_source_preflight_runs_before_terrain_mutations(self):
        validate = TERRAIN.split("private static void Validate(WorldProfile p)", 1)[1]
        validate = validate.split("private static void PrepareStampTextures", 1)[0]
        self.assertIn("ValidateApprovedPoiVisuals(p);", validate)
        self.assertIn("POI_ASSET_GUID_DRIFT", TERRAIN)
        self.assertIn("POI_ASSET_IS_NO_LONGER_SAFE", TERRAIN)

    def test_disabling_pois_still_removes_old_scene_root(self):
        create = TERRAIN.split("private static void CreatePois(", 1)[1]
        create = create.split("private static void TryBakeNavMesh", 1)[0]
        destroy = create.index("Undo.DestroyObjectImmediate(old.gameObject)")
        disabled = create.index("!p.createPoiObjects")
        self.assertLess(destroy, disabled)

    def test_approval_rechecks_actual_runtime_bound_names(self):
        validate = INTAKE.split("private static WFAssetIntakeVerdict Validate(", 1)[1]
        validate = validate.split("public static void ConfirmSpawn(", 1)[0]
        self.assertIn("CollectRuntimeBoundNames()", validate)

    def test_changing_package_folder_invalidates_stale_scan(self):
        choice = WINDOW.split('if (AssetDatabase.IsValidFolder(folder)', 1)[1]
        choice = choice.split('EditorGUILayout.LabelField("Quelle"', 1)[0]
        self.assertIn("entries.Clear()", choice)
        self.assertIn('selectedGuid = "";', choice)

    def test_failed_editor_prefab_setup_removes_temporary_private_art_folder(self):
        helper = NATIVE.split("private static GameObject CreateSourcePrefab(", 1)[1]
        helper = helper.split("[Test]", 1)[0]
        self.assertIn("if (!ok) AssetDatabase.DeleteAsset(TestRoot)", helper)

if __name__ == "__main__":
    unittest.main()
