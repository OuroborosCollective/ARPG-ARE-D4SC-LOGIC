using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>Only the render subtree of an authored prefab may enter the game.
    /// The box on its parent belongs to the WorldForge authoring view.</summary>
    public static class BorderlandArt
    {
        public const string Folder = "ARE/Borderland/";

        public static GameObject VisualChild(GameObject prefab)
        {
            if (prefab == null) return null;
            var skin = prefab.transform.Find("Meshes");
            return skin == null ? null : skin.gameObject;
        }

        public static GameObject LoadVisual(string key)
        {
            return VisualChild(Resources.Load<GameObject>(Folder + key));
        }
    }

    public sealed partial class ArenaRuntime
    {
        private enum SiteAppearance { Ruined, Contested, Secured }
        private Transform _regionalDetails, _conditionSocket, _conditionArt;
        private SiteAppearance? _shownCondition;
        private string _conditionRegion;
        private bool _conditionReduced;

        private Transform AddRegionalPiece(Transform parent, string key, float x, float z, float yaw = 0, float scale = 1)
        {
            var visual = BorderlandArt.LoadVisual(key);
            if (visual == null) return null;
            var host = new GameObject(key).transform;
            host.SetParent(parent, false);
            host.position = new Vector3(x, BuildGround(x, z), z);
            host.rotation = Quaternion.Euler(0, yaw, 0);
            host.localScale = Vector3.one * scale;
            if (PassiveArtVisuals.TryBindPrefab(host, visual)) return host;
            Destroy(host.gameObject);
            return null;
        }

        // The two rebuild entry points share this hook. Fixed site plans are loaded by BuildPlacedLayer.
        private void BuildRegionalDetails()
        {
            _regionalDetails = _conditionSocket = _conditionArt = null;
            _shownCondition = null;
            if (!_entryProfile) return;
            WorldSurfacePresentation.BindAshPaint();
            _conditionRegion = _game.InJourney ? RegionNetwork.Reise : RegionNetwork.RegionForAct(_game.Progression.EntryAct);
            _regionalDetails = new GameObject("ARE_RegionArt_" + _conditionRegion).transform;
            _chapterScenery.Add(_regionalDetails);
            if (!_game.InJourney && (_game.IsVillage || _game.Progression.EntryAct == 3 ||
                _game.Progression.EntryAct == 4 || _game.InForest || _game.InAschenzoll))
                BorderlandLandscape.Build(_regionalDetails, _conditionRegion);
            float x = 5.2f, z = -15f;
            if (_game.InJourney)
            {
                var map = _game.JourneyGeometry;
                x = map.ReturnX * .001f + 3.2f;
                z = map.ReturnZ * .001f + 1.2f;
                if (map is JourneyRouteLayout route)
                {
                    BorderlandLandscape.BuildJourneyStands(_regionalDetails, route);
                    AddRegionalPiece(_regionalDetails, "CaveMouth", route.CaveX * .001f, route.CaveZ * .001f);
                    AddRegionalPiece(_regionalDetails, "Waystone", route.ForwardX * .001f + 2.7f, route.ForwardZ * .001f);
                    AddRegionalPiece(_regionalDetails, "BloodcapBank", route.CaveX * .001f + 2.55f, route.CaveZ * .001f - .69f, 19, 1.3f);
                }
                else
                {
                    AddDepthDetails(_game.Progression.Journey.Depth, map.ReturnX * .001f, map.ReturnZ * .001f,
                        map.ForwardX * .001f, map.ForwardZ * .001f, _game.Progression.Journey.Depth < _game.Progression.Journey.FloorCount);
                }
            }
            else if (_game.InDungeon)
            {
                int depth = _game.Progression.Dungeon.Depth;
                AddDepthDetails(depth, 0, -24, 0, 24, depth < 3);
                x = 3.15f; z = -22.5f;
            }
            else if (_game.InForest) { x = 3.7f; z = -45.4f; }
            else if (_game.IsVillage) { x = -10.6f; z = -11.9f; }
            else if (_game.Progression.EntryAct == 4) { x = 6.75f; z = 10.5f; }
            _conditionSocket = new GameObject("Condition").transform;
            _conditionSocket.SetParent(_regionalDetails, false);
            _conditionSocket.position = new Vector3(x, BuildGround(x, z), z);
            RefreshRegionalAppearance();
        }

        private void AddDepthDetails(int depth, float entryX, float entryZ, float exitX, float exitZ, bool hasExit)
        {
            AddRegionalPiece(_regionalDetails, "CryptArch", entryX, entryZ + .5f);
            // Twin ribs, mineral splinters and fruiting bodies distinguish the descent.
            string trim = depth == 1 ? "RibPillar" : depth == 2 ? "StoneOutcrop" : "BloodcapBank";
            float scale = depth >= 3 ? 1.7f : .80f;
            AddRegionalPiece(_regionalDetails, trim, entryX - 2.4f, entryZ + 1.65f, 27, scale);
            AddRegionalPiece(_regionalDetails, trim, entryX + 2.4f, entryZ + 1.9f, 149, scale);
            if (hasExit) AddRegionalPiece(_regionalDetails, "Brazier", exitX + 2.15f, exitZ - .85f);
        }

        private static void BindRoleArt(Transform actor, MonsterRole role)
        {
            string key;
            HumanBodyBones bone;
            Vector3 offset;
            switch (role)
            {
                case MonsterRole.Ranged: key = "RangedQuiver"; bone = HumanBodyBones.Chest; offset = new Vector3(.13f, .10f, -.24f); break;
                case MonsterRole.Shield: key = "ShieldPlate"; bone = HumanBodyBones.LeftLowerArm; offset = new Vector3(-.35f, -.10f, .19f); break;
                case MonsterRole.Swarm: key = "SwarmSpines"; bone = HumanBodyBones.Chest; offset = new Vector3(0f, .20f, -.20f); break;
                case MonsterRole.Elite: key = "EliteCrest"; bone = HumanBodyBones.Head; offset = new Vector3(0f, .58f, 0f); break;
                default: return;
            }
            var skin = BorderlandArt.LoadVisual(key);
            if (skin == null) return;
            var host = new GameObject("Role_" + key).transform;
            host.SetParent(actor, false);
            host.localPosition = offset;
            var animator = actor.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman && animator.avatar != null && animator.avatar.isValid)
            {
                var mount = animator.GetBoneTransform(bone);
                if (mount != null)
                {
                    // Keep the authored silhouette upright in the rest pose, then follow the animation.
                    host.position = mount.position + actor.TransformVector(role == MonsterRole.Elite ?
                        new Vector3(0, .075f, 0) : role == MonsterRole.Shield ?
                        new Vector3(-.10f, -.15f, .09f) : new Vector3(.02f, -.04f, -.13f));
                    host.SetParent(mount, true);
                }
            }
            if (!PassiveArtVisuals.TryBindPrefab(host, skin)) Destroy(host.gameObject);
        }

        private void RefreshRegionalAppearance()
        {
            if (_conditionSocket == null) return;
            SiteAppearance state = SiteAppearance.Ruined;
            if (_game.IsVillage || _game.Progression.Regions.IsCleared(_conditionRegion))
                state = SiteAppearance.Secured;
            else
            {
                foreach (var enemy in _game.Monsters)
                    if (!enemy.IsDead) { state = SiteAppearance.Contested; break; }
            }
            bool reduced = _atmosphere != null && _atmosphere.ReducedEffects;
            if (_shownCondition == state && _conditionReduced == reduced) return;
            _shownCondition = state;
            _conditionReduced = reduced;
            if (_conditionArt != null)
            {
                _conditionArt.gameObject.SetActive(false);
                Destroy(_conditionArt.gameObject);
            }
            string key = state == SiteAppearance.Secured ? "Brazier" :
                state == SiteAppearance.Contested ? "Waystone" : "BloodcapBank";
            Vector3 at = _conditionSocket.position;
            _conditionArt = new GameObject("Site dressing").transform;
            _conditionArt.SetParent(_conditionSocket, false);
            AddRegionalPiece(_conditionArt, key, at.x, at.z, 24, .86f);
            if (!reduced)
            {
                string detail = state == SiteAppearance.Contested ? "StoneOutcrop" :
                    state == SiteAppearance.Secured ? "Waystone" : "BloodcapBank";
                AddRegionalPiece(_conditionArt, detail, at.x + .68f, at.z + .47f, 123, .53f);
            }
            _conditionSocket.name = "Condition_" + state;
            AdjustEmbers(_regionalDetails, reduced);
            if (_buildLayer != null) AdjustEmbers(_buildLayer, reduced);
        }

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private static void AdjustEmbers(Transform root, bool reduced)
        {
            var properties = new MaterialPropertyBlock();
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var material = renderer.sharedMaterial;
                if (material == null || !material.IsKeywordEnabled("_EMISSION") || material.name != "BorderlandEmbers") continue;
                renderer.GetPropertyBlock(properties);
                properties.SetColor(EmissionColorId, Color.white * (reduced ? .22f : .92f));
                renderer.SetPropertyBlock(properties);
            }
        }
    }
}
