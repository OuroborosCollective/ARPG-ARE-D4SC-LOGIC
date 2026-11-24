"""Static Art Forge architecture guards; these are NOT Unity or browser evidence.

Behavioral DAG/byte/revision regressions execute the real C# implementation in
ArtForgeVerification / Unity EditMode. These guards protect ownership and effects.
"""
import json
from pathlib import Path
import re
import unittest
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "Assets/Editor/ArtForge"


def source(relative):
    return (ROOT / relative).read_text(encoding="utf-8")


class ArtForgeArchitectureTests(unittest.TestCase):
    def test_only_existing_bau_dispatcher_owns_the_fadenwerk_extension(self):
        owners = []
        assignment = re.compile(r"\bFadenwerkAuthoringBridge\.Extension\s*=(?!=)")
        for root in (ROOT / "Assets", ROOT / "Packages"):
            for path in root.rglob("*.cs"):
                if "Tests" in path.parts:
                    continue
                owners.extend(str(path.relative_to(ROOT)) for _ in assignment.finditer(path.read_text(encoding="utf-8")))
        self.assertEqual(["Assets/Editor/BauModus/BauModusBridge.cs"], owners)

    def test_art_routes_are_dispatched_before_bau_request_parsing(self):
        bridge = source("Assets/Editor/BauModus/BauModusBridge.cs")
        body = bridge.split("static FadenwerkExtensionReply Handle(", 1)[1]
        route = body.index("ArtForgeCanvasController.Handle(method, path, body, query)")
        self.assertLess(route, body.index('if (method == "GET")'))
        self.assertLess(route, body.index("BauModusStore.Area(q.area)"))

    def test_all_art_mutations_are_refused_before_source_or_asset_reads(self):
        controller = source("Assets/Editor/ArtForge/ArtForgeCanvasController.cs")
        handler = controller.split("public static FadenwerkExtensionReply Handle(", 1)[1].split(
            "static FadenwerkExtensionReply BeginRead(", 1)[0]
        rejection = handler.index('if (method != "GET")')
        self.assertLess(rejection, handler.index("BeginRead("))
        self.assertRegex(handler[:handler.index("BeginRead(")], r'Error\("READ_ONLY"[\s\S]*?,\s*405\)')
        self.assertNotIn("ReadProjectSha(", handler, "The editor dispatcher must not wait for Git.")
        self.assertNotIn("ArtForgeWorldForgeReader.Read(", handler, "The editor dispatcher must not hash files.")
        self.assertIn("Task.Run(async () =>", controller)
        self.assertIn("ReadGate.WaitAsync(cancellation)", controller)

    def test_new_read_surfaces_have_no_game_save_import_or_scene_write_apis(self):
        paths = (ART / "ArtForgeCanvasController.cs", ART / "ArtForgeWorldForgeReader.cs")
        forbidden = re.compile(
            r"\b(?:PlayerPrefs|ArenaSimulation|CharacterProgression|BuildLayerLive|SceneView)\b|"
            r"\b(?:File|Directory)\.(?:Write\w*|Append\w*|Delete|Move|Copy|CreateDirectory)\s*\(|"
            r"\bAssetDatabase\.(?:ImportAsset|CreateAsset|SaveAssets|DeleteAsset|Refresh)\s*\(|"
            r"\bWFBlenderIntake\.(?:Submit|Retry|ImportFbx)\s*\(|"
            r"\bWFAssetIntakeApprovals\.(?:ConfirmSpawn|ConfirmPoi)\s*\(|"
            r"\b(?:HttpListener|TcpListener)\b"
        )
        for path in paths:
            with self.subTest(path=path.name):
                self.assertIsNone(forbidden.search(path.read_text(encoding="utf-8")))

    def test_worldforge_root_is_existing_owner_and_not_a_request_parameter(self):
        controller = source("Assets/Editor/ArtForge/ArtForgeCanvasController.cs")
        self.assertIn("ArtForgeWorldForgeReader.Read(WFBlenderIntake.Root, projectSha, cancellation)", controller)
        self.assertNotRegex(controller, r'Arg\(query,\s*"(?:root|path|host|command)"\)')
        self.assertIn('"QUERY_INVALID"', controller)

    def test_contract_and_adapter_cannot_enter_android_player_assemblies(self):
        for path in ART.rglob("*.asmdef"):
            with self.subTest(path=path.name):
                data = json.loads(path.read_text(encoding="utf-8"))
                self.assertEqual(["Editor"], data["includePlatforms"])
        contract = json.loads((ART / "Contracts/AshenCrypt.ArtForge.Contracts.asmdef").read_text())
        self.assertTrue(contract["noEngineReferences"])
        self.assertEqual([], contract["references"])
        contract_source = (ART / "Contracts/ArtGraphContract.cs").read_text(encoding="utf-8")
        self.assertNotRegex(contract_source, re.compile(r"^using (?:UnityEngine|UnityEditor|AreArpg\.Core)", re.MULTILINE))

    def test_versioned_contract_has_one_relationship_owner_and_behavior_runner(self):
        contract = (ART / "Contracts/ArtGraphContract.cs").read_text(encoding="utf-8")
        self.assertIn('const string Schema = "ashen.art-graph.v1"', contract)
        self.assertRegex(contract, r"public string\[\] parentIds\b")
        self.assertNotRegex(contract, r"public\s+\w+(?:\[\])?\s+edges\b")
        project = ET.parse(ROOT / "Tests/ArtForgeVerification/ArtForgeVerification.csproj").getroot()
        includes = [node.attrib.get("Include", "") for node in project.iter("Compile")]
        self.assertIn("../../Assets/Editor/ArtForge/Contracts/*.cs", includes)
        self.assertIn("../../Assets/Editor/ArtForge/Tests/ArtGraphBehaviorCases.cs", includes)
        self.assertEqual([], list(project.iter("PackageReference")))

    def test_art_browser_boot_does_not_invoke_normal_bau_preview(self):
        page = source("Assets/Editor/BauModus/Web/BauModus.html")
        boot = page.split("async function boot()", 1)[1].split("function showLogin()", 1)[0]
        self.assertRegex(boot, r"if\s*\(ART_MODE\)\s*\{\s*await artBoot\(\);\s*return;")
        self.assertLess(boot.index("artBoot()"), boot.index("selectArea(start)"))
        api = re.split(r"async function api\([^)]*\)", page, maxsplit=1)[1].split("async function apiBlob", 1)[0]
        self.assertIn('!path.startsWith("art/")', api)
        self.assertIn("body !== undefined", api)


if __name__ == "__main__":
    unittest.main()
