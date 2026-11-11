"""URP render-pipeline contract (static gate; not a substitute for Editor rendering).

The private Fantasy Kingdom art base ships URP materials. The game project must
therefore pin URP and assign a project-owned, version-controlled pipeline asset,
so a fresh clone renders through the same pipeline without any private asset.
"""
import json
from pathlib import Path
import re
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[2]
URP = 'com.unity.render-pipelines.universal'
URP_VERSION = '17.6.0'
PIPELINE = 'Assets/Settings/Rendering/AreUrpPipeline.asset'
RENDERER = 'Assets/Settings/Rendering/AreUrpRenderer.asset'
GLOBAL_SETTINGS = 'Assets/Settings/Rendering/AreUrpGlobalSettings.asset'


def guid_of(relative):
    meta = (ROOT / (relative + '.meta')).read_text(encoding='utf-8')
    match = re.search(r'^guid: ([0-9a-f]{32})$', meta, re.MULTILINE)
    assert match, 'meta without guid: ' + relative
    return match.group(1)


class UrpRenderPipelineContract(unittest.TestCase):
    def test_manifest_and_lock_pin_urp(self):
        manifest = json.loads((ROOT / 'Packages/manifest.json').read_text())['dependencies']
        locked = json.loads((ROOT / 'Packages/packages-lock.json').read_text())['dependencies']
        self.assertEqual(URP_VERSION, manifest.get(URP))
        self.assertEqual(URP_VERSION, locked.get(URP, {}).get('version'))

    def test_project_owned_pipeline_assets_exist_with_meta(self):
        for relative in (PIPELINE, RENDERER, GLOBAL_SETTINGS):
            with self.subTest(asset=relative):
                self.assertTrue((ROOT / relative).is_file(), relative)
                self.assertTrue((ROOT / (relative + '.meta')).is_file(), relative + '.meta')

    def test_graphics_settings_assign_the_project_owned_pipeline(self):
        graphics = (ROOT / 'ProjectSettings/GraphicsSettings.asset').read_text(encoding='utf-8')
        match = re.search(r'm_CustomRenderPipeline: \{fileID: 11400000, guid: ([0-9a-f]{32}), type: 2\}', graphics)
        self.assertIsNotNone(match, 'Built-in pipeline still active (m_CustomRenderPipeline unset)')
        self.assertEqual(guid_of(PIPELINE), match.group(1))

    def test_pipeline_asset_uses_the_project_owned_renderer(self):
        pipeline = (ROOT / PIPELINE).read_text(encoding='utf-8')
        self.assertIn('guid: ' + guid_of(RENDERER), pipeline)

    def test_no_quality_level_overrides_the_pipeline_with_a_foreign_asset(self):
        quality = (ROOT / 'ProjectSettings/QualitySettings.asset').read_text(encoding='utf-8')
        own = guid_of(PIPELINE)
        overrides = re.findall(r'customRenderPipeline: \{fileID: (\d+)(?:, guid: ([0-9a-f]{32}))?', quality)
        self.assertTrue(overrides, 'no quality levels found')
        for file_id, guid in overrides:
            with self.subTest(file_id=file_id, guid=guid):
                self.assertTrue(file_id == '0' or guid == own)

    def test_linear_lighting_contract_matches_the_urp_art_base(self):
        # Gamma colour space blew the Kingdom scene out to white (light intensity 35 is authored for linear).
        player = (ROOT / 'ProjectSettings/ProjectSettings.asset').read_text(encoding='utf-8')
        graphics = (ROOT / 'ProjectSettings/GraphicsSettings.asset').read_text(encoding='utf-8')
        self.assertRegex(player, r'(?m)^  m_ActiveColorSpace: 1$')
        self.assertRegex(graphics, r'(?m)^  m_LightsUseLinearIntensity: 1$')
        self.assertRegex(graphics, r'(?m)^  m_LightsUseColorTemperature: 1$')

    def test_pipeline_provides_the_textures_the_water_shaders_sample(self):
        pipeline = (ROOT / PIPELINE).read_text(encoding='utf-8')
        self.assertRegex(pipeline, r'(?m)^  m_RequireDepthTexture: 1$')
        self.assertRegex(pipeline, r'(?m)^  m_RequireOpaqueTexture: 1$')
        self.assertRegex(pipeline, r'(?m)^  m_SoftShadowsSupported: 1$')

    def test_render_settings_are_version_controlled_and_private_art_is_not(self):
        tracked = subprocess.run(['git', 'ls-files'], cwd=ROOT, check=True, capture_output=True,
                                 text=True).stdout.splitlines()
        for relative in ('ProjectSettings/GraphicsSettings.asset', 'ProjectSettings/QualitySettings.asset',
                         'ProjectSettings/ProjectSettings.asset',
                         PIPELINE, PIPELINE + '.meta', RENDERER, RENDERER + '.meta',
                         GLOBAL_SETTINGS, GLOBAL_SETTINGS + '.meta'):
            with self.subTest(tracked=relative):
                self.assertIn(relative, tracked)
        # The project-owned README anchors the folder/GUID in clean clones; no vendor content is allowed.
        anchors = {'Assets/PrivateArt/README.md', 'Assets/PrivateArt/README.md.meta'}
        self.assertEqual([], [p for p in tracked if p.startswith('Assets/PrivateArt/') and p not in anchors])
        ignored = subprocess.run(['git', 'check-ignore', '--no-index',
                                  'Assets/PrivateArt/FantasyKingdom3D/private.prefab'], cwd=ROOT,
                                 capture_output=True, text=True)
        self.assertEqual(0, ignored.returncode, 'Private vendor files must be ignored even before import')

    def test_setup_tool_is_project_owned_editor_code(self):
        tool = (ROOT / 'Assets/Editor/AreRenderPipelineSetup.cs').read_text(encoding='utf-8')
        for needle in ('UniversalRenderPipelineAsset', 'GraphicsSettings.defaultRenderPipeline', PIPELINE,
                       'ColorSpace.Linear', 'lightsUseLinearIntensity', 'supportsCameraDepthTexture',
                       'supportsCameraOpaqueTexture'):
            with self.subTest(needle=needle):
                self.assertIn(needle, tool)


if __name__ == '__main__':
    unittest.main()
