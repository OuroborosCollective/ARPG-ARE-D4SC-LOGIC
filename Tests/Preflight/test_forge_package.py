import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2] / "Assets/AshenCrypt/MemoryForge"

class ForgePackageTests(unittest.TestCase):
    def test_package_namespaces_avoid_unity_trademark(self):
        for source in ROOT.rglob("*.cs"):
            for name in re.findall(r"^namespace\s+([\w.]+)", source.read_text(), re.M):
                self.assertNotIn("Unity", name.split("."), str(source))

    def test_package_has_no_game_assembly_dependency(self):
        for source in ROOT.rglob("*.asmdef"):
            self.assertNotIn("AreArpg", source.read_text(), str(source))

if __name__ == "__main__":
    unittest.main()
