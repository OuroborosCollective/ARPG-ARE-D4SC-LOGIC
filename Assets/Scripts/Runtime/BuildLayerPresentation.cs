using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// Bau-Modus projection: passive scenery from Resources/ARE/BuildLayouts/&lt;area&gt;.json on top of the
    /// chapter scenery. Never feeds Core rules; collision and walkability stay with ArenaSimulation.
    /// </summary>
    public sealed partial class ArenaRuntime
    {
        private Transform _buildLayer, _buildGhost;
        private string _buildArea;

        /// <summary>
        /// Fixed game areas that own a build plan, in the same order as ArenaSimulation.ChapterName.
        /// Procedural journeys and dungeon levels have none.
        /// </summary>
        public static string BuildAreaKey(ArenaSimulation game, bool entryProfile)
        {
            if (game == null || !entryProfile || game.InJourney) return null;
            // The rooms of the village houses are no building sites (yet): inside one nothing of the village plan is placed.
            if (game.InsidePlace != VillagePlace.None) return null;
            if (game.InWoodlandRegion) return game.InForest ? "wald" : game.Progression.EntryAct == 9 ? "baerenhoehle" : "aschenzoll";
            if (game.InDungeon) return null;
            switch (game.Progression.EntryAct)
            {
                case 7: return "rattlernest";
                case 6: return "verborgene-krypta";
                case 5: return "speicher";
                case 4: return "muehle";
                case 3: return "aschestrasse";
                case 2: return "aschengrenze";
                case 1: return "krypta";
            }
            return null;
        }

        private bool _buildLiveHooked;

        /// <summary>Listen for live plans for the whole life of the runtime, also while it is disabled (paused, tests).</summary>
        private void HookBuildLive()
        {
            if (_buildLiveHooked) return;
            BuildLayerLive.LayoutChanged += OnLiveBuildLayout;
            BuildLayerLive.GhostChanged += OnLiveBuildGhost;
            _buildLiveHooked = true;
        }

        /// <summary>Called from OnDestroy: stop listening and report that no game shows a plan any more.</summary>
        private void UnhookBuildLive()
        {
            if (!_buildLiveHooked) return;
            BuildLayerLive.LayoutChanged -= OnLiveBuildLayout;
            BuildLayerLive.GhostChanged -= OnLiveBuildGhost;
            _buildLiveHooked = false;
            BuildLayerLive.ReportActive(null, 0, 0);
            BuildLayerLive.ReportGhost(false);
        }

        private float BuildGround(float x, float z) { return GroundHeight(x, z) + WalkSurfaceHeight(x, z); }

        /// <summary>Rebuild the placed pieces of the current area (after every scenery rebuild).</summary>
        private void BuildPlacedLayer(BuildLayoutData live = null)
        {
            HookBuildLive();
            if (_buildLayer != null)
            {
                _chapterScenery.Remove(_buildLayer);
                Destroy(_buildLayer.gameObject);
            }
            _buildLayer = null;
            ClearBuildGhost();
            _buildArea = BuildAreaKey(_game, _entryProfile);
            BuildLayoutData data = _buildArea == null ? null : live ?? BuildLayer.Load(_buildArea);
            if (data != null && data.pieces.Count > 0)
            {
                _buildLayer = BuildLayer.Build(data, BuildGround);
                // Chapter scenery: cleared on travel and considered by the camera occluder fade.
                _chapterScenery.Add(_buildLayer);
            }
            BuildLayerLive.ReportActive(_buildArea, data != null ? data.revision : 0, BuildLayer.CountPieces(_buildLayer));
        }

        private void OnLiveBuildLayout(string area, BuildLayoutData data)
        {
            if (_game == null || data == null || area != BuildAreaKey(_game, _entryProfile)) return;
            BuildPlacedLayer(data.Clone());
            RefreshCameraOccluders();
        }

        private void OnLiveBuildGhost(string area, BuildPiece ghost)
        {
            ClearBuildGhost();
            if (_game == null || ghost == null || area != _buildArea) return;
            _buildGhost = BuildLayer.CreateGhost(ghost, BuildGround);
            BuildLayerLive.ReportGhost(_buildGhost != null);
        }

        private void ClearBuildGhost()
        {
            if (_buildGhost != null) Destroy(_buildGhost.gameObject);
            _buildGhost = null;
            BuildLayerLive.ReportGhost(false);
        }
    }
}
