"""Real pure-Python Director route contracts: no mocked production Unity runtime."""
import json
import tempfile
import unittest
from pathlib import Path

from neural_cortex import load_catalog, route
from route_work import plan


class NeuralCortexTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.project = self.root / "AshenCrypt"
        (self.project / "Assets").mkdir(parents=True)
        (self.project / "ProjectSettings").mkdir()
        (self.project / "ProjectSettings/ProjectVersion.txt").write_text(
            "m_EditorVersion: 6000.6.8f1\n")
        self.catalog = Path(__file__).with_name("neural_catalog.json")

    def test_shader_only_wakes_shader_department(self):
        result = route("Shader Graph fuer metallene Ruestung", self.catalog, self.project)
        self.assertEqual(["shaders"], [x["id"] for x in result["selected"]])
        self.assertEqual([], result["deferred"])
        self.assertEqual("NONE", result["execution"])
        self.assertEqual("Unity Game Director", result["authority"])
        self.assertFalse(result["broadcast"])
        self.assertTrue(result["advisory_only"])

    def test_multiple_matching_departments_are_bounded(self):
        result = route("Android Shader Cache und Performance verbessern",
                       self.catalog, self.project, max_departments=2)
        ids = [x["id"] for x in result["selected"]]
        self.assertEqual(2, len(ids))
        self.assertIn("performance_cache", ids)
        self.assertIn("shaders", [*ids, *result["deferred"]])
        self.assertIn("android_release", [*ids, *result["deferred"]])
        self.assertNotIn("quest_design", ids)

    def test_quest_and_dungeon_are_distinct_departments(self):
        result = route("Quest-Verknuepfung im Dungeon pruefen", self.catalog, self.project)
        ids = [x["id"] for x in result["selected"]]
        self.assertIn("quest_design", ids)
        self.assertIn("dungeons", ids)
        self.assertNotIn("shaders", ids)

    def test_unknown_intent_never_broadcasts(self):
        result = route("Mach mal ditte da", self.catalog, self.project)
        self.assertEqual([], result["selected"])
        self.assertEqual("NO_DOMAIN_MATCH", result["status"])
        self.assertFalse(result["broadcast"])

    def test_exact_unity_six_project_required(self):
        (self.project / "ProjectSettings/ProjectVersion.txt").write_text(
            "m_EditorVersion: 2022.3.62f1\n")
        with self.assertRaisesRegex(ValueError, "UNITY_VERSION_MISMATCH"):
            route("Shader", self.catalog, self.project)
        with self.assertRaisesRegex(ValueError, "UNITY_PROJECT_NOT_VERIFIED"):
            route("Shader", self.catalog, self.root / "missing")

    def test_unknown_manifest_keys_cannot_smuggle_commands(self):
        data = json.loads(self.catalog.read_text())
        data["departments"][0]["command"] = "do-not-execute"
        path = self.root / "invalid.json"
        path.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError, "INVALID_CATALOG"):
            load_catalog(path)

    def test_duplicate_department_fails_closed(self):
        data = json.loads(self.catalog.read_text())
        data["departments"].append(data["departments"][0].copy())
        path = self.root / "duplicate.json"
        path.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError, "DUPLICATE_DEPARTMENT"):
            load_catalog(path)

    def test_hash_hint_never_claims_runtime_or_authority(self):
        result = route("Unity Shader Graph", self.catalog, self.project,
                       observed_skill_states={"shader-graph-create-custom-node": "INSTALLED"})
        self.assertEqual("6000.6.8f1", result["unity"])
        self.assertEqual("NOT_EXECUTED", result["runtime_evidence"])
        hints = [s for s in result["selected"][0]["skills"]
                 if s["name"] == "shader-graph-create-custom-node"]
        self.assertEqual("INSTALLED_FILE_HASH_ONLY", hints[0]["status"])
        self.assertFalse(result["selected"][0]["authorized"])

    def test_cortex_is_reached_through_existing_director_router(self):
        result = plan("Shader Graph", project=self.project)
        self.assertIn("neural_cortex", result)
        self.assertEqual(["shaders"],
                         [item["id"] for item in result["neural_cortex"]["selected"]])
        self.assertEqual("ugd.route.v1", result["schema"])
        self.assertTrue(result["advisory_only"])

    def test_german_audio_keyword_routes_to_audio(self):
        result = route("Geräusch für Fackeln", self.catalog, self.project)
        self.assertIn("audio", [x["id"] for x in result["selected"]])

    def test_legacy_unity_project_retains_existing_director_plan(self):
        (self.project / "ProjectSettings/ProjectVersion.txt").write_text(
            "m_EditorVersion: 2022.3.62f1\\n")
        result = plan("Wald ansehen", project=self.project)
        self.assertEqual("ugd.route.v1", result["schema"])
        self.assertIn("phases", result)
        self.assertEqual("REFUSED", result["neural_cortex"]["status"])
        self.assertEqual([], result["neural_cortex"]["selected"])
        self.assertEqual("NONE", result["neural_cortex"]["execution"])

    def test_deterministic_routing_and_catalog_fingerprint(self):
        a = route("Fackel Partikel und Rauch", self.catalog, self.project)
        b = route("Fackel Partikel und Rauch", self.catalog, self.project)
        self.assertEqual(a, b)
        self.assertRegex(a["catalog_sha256"], r"^[0-9a-f]{64}$")
        self.assertIn("vfx", [x["id"] for x in a["selected"]])

    def test_never_autoroutes_effects(self):
        result = route("Quest speichern und freigeben", self.catalog, self.project)
        for department in result["selected"]:
            self.assertEqual("PROPOSAL_ONLY", department["effect"])
            self.assertFalse(department["authorized"])
        self.assertEqual("NONE", result["execution"])


if __name__ == "__main__":
    unittest.main()
