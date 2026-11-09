"""Unity asset GUID integrity checked without an Editor or synthetic runtime."""
from pathlib import Path
import re
import unittest

ASSETS = Path(__file__).resolve().parents[2] / "Assets"
GUID = re.compile(r"(?m)^guid: ([0-9a-f]{32})$")


class TestUnityAssetMetadata(unittest.TestCase):
    def test_every_asset_and_folder_has_a_unique_guid(self):
        self.assertTrue(ASSETS.is_dir(), "Assets directory missing")
        seen = {}
        for asset in sorted(ASSETS.rglob("*")):
            if asset.name.endswith(".meta"):
                self.assertTrue(Path(str(asset)[:-5]).exists(), f"orphan metadata: {asset}")
                continue
            meta = Path(str(asset) + ".meta")
            rel = asset.relative_to(ASSETS.parent).as_posix()
            self.assertTrue(meta.is_file(), f"missing Unity .meta: {rel}")
            content = meta.read_text(encoding="utf-8")
            matches = GUID.findall(content)
            self.assertEqual(len(matches), 1, f"expected exactly one valid 128-bit GUID in {rel}")
            guid = matches[0]
            self.assertNotIn(guid, seen, f"duplicate GUID {guid}: {rel} / {seen.get(guid)}")
            seen[guid] = rel
            self.assertIn("fileFormatVersion: 2", content, f"unsupported metadata format: {rel}")
            if asset.is_dir():
                self.assertIn("folderAsset: yes", content, f"folder missing marker: {rel}")
            elif asset.suffix == ".cs":
                self.assertIn("MonoImporter:", content, f"script importer missing: {rel}")
            elif asset.suffix == ".asmdef":
                self.assertIn("AssemblyDefinitionImporter:", content, f"assembly importer missing: {rel}")

    def test_asset_count_is_not_silently_zero(self):
        self.assertGreaterEqual(sum(1 for p in ASSETS.rglob("*") if not p.name.endswith(".meta")), 14)


if __name__ == "__main__":
    unittest.main()
