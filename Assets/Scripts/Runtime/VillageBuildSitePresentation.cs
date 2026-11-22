using System;
using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// The lots of Aschengrenze in the running game. The Core says which house stands, who asks for which lot and
    /// what has been delivered; here a house that does not stand yet is taken out of the picture, its lot is staked
    /// out with the piles of what was brought, and the person asking stands in front of it. Talking to that person
    /// and handing materials over goes through the Core, which alone decides what is taken.
    /// </summary>
    public sealed partial class ArenaRuntime
    {
        public const string BuildSitePrefix = "ARE_BuildSite_", SiteAskerPrefix = "ARE_SiteAsker_";
        /// <summary>
        /// The lot (Tools/Art/blender/village_build_site.py): one model for all three houses, whose footprints are
        /// alike. It carries a pile for every material any house asks for; a lot shows the piles of its own needs.
        /// </summary>
        public static readonly VillageWorkplace VillageSiteModel = new VillageWorkplace(VillageSpot.Home, "BuildSite", 0f, 0f, 6.4f, 6.4f, 1.5f, false,
            (int)VillageGood.Wood, 12, (int)VillageGood.Metal, 6, (int)VillageGood.Herb, 4);
        /// <summary>The figures of the people asking, in the order of <see cref="VillagePlace"/>: Orsel, the smith, the farmer.</summary>
        public static readonly string[] SiteAskerKeys = { "Orsel0", "Smith0", "Villager0" };
        /// <summary>Scenery whose middle lies this far around a house's footprint belongs to that house.</summary>
        private const float LotMargin = 1.2f;

        private sealed class SiteView
        {
            public VillagePlace Place;
            public Transform Host, Asker;
            public readonly Transform[][] Piles = new Transform[VillageBuildRules.NeedCount][];
            /// <summary>Pile parts of materials this house does not ask for: never shown at this lot.</summary>
            public readonly List<Transform> Unused = new List<Transform>();
        }

        private readonly List<SiteView> _siteViews = new List<SiteView>();
        private bool _sitePanelOpen;
        private int _pendingSiteGift = -1;
        private VillagePlace _siteThanked;

        private static EntryWorldLayout.Footprint HouseFootprint(VillagePlace place)
        {
            for (int i = 0; i < EntryWorldLayout.BuildingCount; i++)
                if (EntryWorldLayout.Building(i).Id == VillagePlaceLayout.House(place)) return EntryWorldLayout.Building(i);
            throw new InvalidOperationException("No building for " + place);
        }

        // ---------------------------------------------------------------- the lots

        /// <summary>Put up the lots of every house that does not stand yet. Called with the village's work scenery, out in the village.</summary>
        private void BuildVillageSites()
        {
            _siteViews.Clear();
            _sitePanelOpen = false; _pendingSiteGift = -1;
            bool hidden = false;
            for (int i = 1; i <= VillagePlaceLayout.PlaceCount; i++)
            {
                var place = (VillagePlace)i;
                if (_game.Progression.Houses.Built(place)) continue;
                hidden |= HideHouse(place);
                _siteViews.Add(BuildSite(place));
            }
            if (!hidden) return;
            // Whatever the hidden houses did for the picture is undone: no step up to a porch that is not there,
            // and the camera does not pull in for walls that are not there.
            foreach (Transform part in _chapterScenery)
                if (part != null && part.name == AschengrenzeArt.BackdropLabel) CollectWalkSurfaces(part);
            RefreshCameraOccluders();
        }

        /// <summary>Take a house out of the picture: everything of the village's scenery that stands on its ground.</summary>
        private bool HideHouse(VillagePlace place)
        {
            EntryWorldLayout.Footprint f = HouseFootprint(place);
            float x = f.X / 1000f, z = f.Z / 1000f, hw = f.HalfWidth / 1000f + LotMargin, hd = f.HalfDepth / 1000f + LotMargin;
            bool any = false;
            foreach (Transform part in _chapterScenery)
            {
                if (part == null) continue;
                foreach (Renderer renderer in part.GetComponentsInChildren<Renderer>(true))
                {
                    Bounds b = renderer.bounds;
                    // The ground, the backdrop and other wide pieces belong to the village, not to one house.
                    if (!renderer.enabled || b.size.x > 16f || b.size.z > 16f) continue;
                    if (Mathf.Abs(b.center.x - x) > hw || Mathf.Abs(b.center.z - z) > hd) continue;
                    renderer.enabled = false; any = true;
                }
                foreach (Light light in part.GetComponentsInChildren<Light>(true))
                {
                    Vector3 at = light.transform.position;
                    if (light.type != LightType.Directional && Mathf.Abs(at.x - x) <= hw && Mathf.Abs(at.z - z) <= hd) light.enabled = false;
                }
            }
            return any;
        }

        private SiteView BuildSite(VillagePlace place)
        {
            EntryWorldLayout.Footprint f = HouseFootprint(place);
            var view = new SiteView { Place = place };
            view.Host = new GameObject(BuildSitePrefix + place).transform;
            view.Host.SetParent(_villageWork, false);
            view.Host.position = OnGround(new Vector2(f.X, f.Z) / 1000f);
            // The model's front is the side the door will be on; like the rooms it is put up with half a turn.
            view.Host.rotation = Quaternion.LookRotation(new Vector3(-VillagePlaceLayout.DoorOutX(place), 0f, -VillagePlaceLayout.DoorOutZ(place)));
            GameObject prefab = Resources.Load<GameObject>("ARE/Visuals/" + VillageSiteModel.Key);
            bool art = prefab != null && PassiveArtVisuals.TryBindPrefab(view.Host, prefab);
            if (!art) BuildSiteBlockout(view.Host, f);
            // Every pile part is found by name; a lot shows its own needs and hides the piles of the other houses.
            var parts = view.Host.GetComponentsInChildren<Transform>(true);
            for (int g = 0; g < VillageSiteModel.Goods.Length; g++)
            {
                string prefix = StockPrefix + VillageSiteModel.Goods[g] + "_";
                var units = new List<Transform>();
                foreach (Transform part in parts) if (part.name.StartsWith(prefix, StringComparison.Ordinal)) units.Add(part);
                units.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                foreach (Transform unit in units) unit.gameObject.SetActive(false);
                int need = VillageBuildRules.NeedFor(place, VillageSiteModel.Goods[g]);
                if (need < 0) { view.Unused.AddRange(units); continue; }
                // The whole pile is kept: pieces beyond what this house needs can never be delivered and so stay hidden.
                view.Piles[need] = units.ToArray();
            }
            for (int need = 0; need < VillageBuildRules.NeedCount; need++) if (view.Piles[need] == null) view.Piles[need] = new Transform[0];
            UpdateSite(view);
            return view;
        }

        /// <summary>Without the private model: trampled ground, four stakes with a rope, and one small block per piece of material.</summary>
        private void BuildSiteBlockout(Transform host, EntryWorldLayout.Footprint f)
        {
            float hw = f.HalfWidth / 1000f - 0.2f, hd = f.HalfDepth / 1000f - 0.2f;
            var soil = new Color(0.27f, 0.23f, 0.19f); var wood = new Color(0.40f, 0.29f, 0.19f); var rope = new Color(0.62f, 0.55f, 0.42f);
            Func<string, Vector3, Vector3, Color, Transform> block = (name, at, size, colour) =>
            {
                Transform t = Shape(PrimitiveType.Cube, name, Vector3.zero, size, colour);
                t.SetParent(host, false); t.localPosition = at; t.localRotation = Quaternion.identity;
                return t;
            };
            block("Platzhalter_" + VillageSiteModel.Key, new Vector3(0f, 0.03f, 0f), new Vector3(hw * 2f, 0.06f, hd * 2f), soil);
            foreach (int sx in new[] { -1, 1 })
                foreach (int sz in new[] { -1, 1 })
                    block("Bauplatz_Pflock", new Vector3(sx * hw, 0.55f, sz * hd), new Vector3(0.12f, 1.1f, 0.12f), wood);
            // The host stands with half a turn: its -Z is the side the door will be on. That side stays open.
            foreach (int sx in new[] { -1, 1 }) block("Bauplatz_Seil", new Vector3(sx * hw, 0.8f, 0f), new Vector3(0.04f, 0.04f, hd * 2f), rope);
            block("Bauplatz_Seil", new Vector3(0f, 0.8f, hd), new Vector3(hw * 2f, 0.04f, 0.04f), rope);
            Color[] colours = { new Color(0.48f, 0.34f, 0.20f), new Color(0.42f, 0.44f, 0.47f), new Color(0.36f, 0.50f, 0.28f) };
            for (int g = 0; g < VillageSiteModel.Goods.Length; g++)
                for (int i = 0; i < VillageSiteModel.Units[g]; i++)
                    block(StockPrefix + VillageSiteModel.Goods[g] + "_" + i.ToString("00"),
                        new Vector3(hw - 0.6f - g * 1.9f - (i % 3) * 0.34f, 0.2f + (i / 6) * 0.3f, -hd + 0.7f + ((i / 3) % 2) * 0.34f), new Vector3(0.3f, 0.26f, 0.3f), colours[g]);
        }

        /// <summary>The piles show exactly what the Core has as delivered; the asker stands there while the Core has somebody asking.</summary>
        private void UpdateSite(SiteView view)
        {
            VillageBuildingState houses = _game.Progression.Houses;
            for (int need = 0; need < VillageBuildRules.NeedCount; need++)
            {
                int delivered = houses.Delivered(view.Place, need);
                Transform[] pile = view.Piles[need];
                for (int i = 0; i < pile.Length; i++)
                    if (pile[i] != null && pile[i].gameObject.activeSelf != i < delivered) pile[i].gameObject.SetActive(i < delivered);
            }
            foreach (Transform unit in view.Unused) if (unit != null && unit.gameObject.activeSelf) unit.gameObject.SetActive(false);
            bool asking = _game.SiteOffered(view.Place);
            if (asking && view.Asker == null) view.Asker = BuildSiteAsker(view.Place);
            if (view.Asker == null) return;
            if (view.Asker.gameObject.activeSelf != asking) view.Asker.gameObject.SetActive(asking);
            if (!asking) return;
            // The asker looks at whoever comes close, and otherwise away from the lot towards the village.
            Vector3 at = view.Asker.position;
            var toPlayer = new Vector3(_game.PlayerXmm / 1000f - at.x, 0f, _game.PlayerZmm / 1000f - at.z);
            Vector3 look = toPlayer.sqrMagnitude < 36f && toPlayer.sqrMagnitude > 0.04f ? toPlayer :
                new Vector3(VillagePlaceLayout.DoorOutX(view.Place), 0f, VillagePlaceLayout.DoorOutZ(view.Place));
            view.Asker.rotation = Quaternion.RotateTowards(view.Asker.rotation, Quaternion.LookRotation(look), 240f * Time.deltaTime + 1f);
        }

        private Transform BuildSiteAsker(VillagePlace place)
        {
            var at = new Vector2(VillageBuildRules.AskerXmm(place), VillageBuildRules.AskerZmm(place)) / 1000f;
            Color colour = place == VillagePlace.SickRoom ? new Color(0.62f, 0.62f, 0.52f) : place == VillagePlace.Smithy ? new Color(0.34f, 0.30f, 0.28f) : new Color(0.55f, 0.44f, 0.28f);
            Transform asker = Shape(PrimitiveType.Capsule, SiteAskerPrefix + place, OnGround(at, 1f), new Vector3(0.8f, 1f, 0.8f), colour);
            asker.SetParent(_villageWork, true);
            PassiveArtVisuals.TryBindGrounded(asker, SiteAskerKeys[(int)place - 1], 0f);
            var shared = _entryProfile ? Resources.Load<RuntimeAnimatorController>("ARE/Visuals/EntryMelee") : null;
            foreach (Animator animator in asker.GetComponentsInChildren<Animator>(true))
            {
                if (shared != null && PassiveArtVisuals.NeedsSharedLocomotion(animator)) animator.runtimeAnimatorController = shared;
                animator.applyRootMotion = false;
            }
            RegisterActorMotion(asker);
            AnimateActor(asker, 0, 0);
            asker.rotation = Quaternion.LookRotation(new Vector3(VillagePlaceLayout.DoorOutX(place), 0f, VillagePlaceLayout.DoorOutZ(place)));
            return asker;
        }

        private void UpdateVillageSites()
        {
            foreach (SiteView view in _siteViews) UpdateSite(view);
        }

        private void ClearVillageSites()
        {
            foreach (SiteView view in _siteViews) if (view.Asker != null) _actorAnimations.Remove(view.Asker);
            _siteViews.Clear();
            _sitePanelOpen = false; _pendingSiteGift = -1;
        }

        // ---------------------------------------------------------------- talking at the lot

        /// <summary>What is offered above the controls while the player stands with the person at a lot. Null away from the lots.</summary>
        public string VillageSiteHint
        {
            get
            {
                if (_game == null) return null;
                VillagePlace place = _game.SiteInReach;
                return place == VillagePlace.None ? null :
                    "BAUPLATZ: " + VillagePlaceLayout.Name(place).ToUpperInvariant() + " · " + VillageBuildRules.Asker(place).ToUpperInvariant() + " ANSPRECHEN";
            }
        }

        public static Rect VillageSiteToggleRect(ArenaHudLayout layout) { return layout.ContextActionRect; }

        /// <summary>One layout for drawing and for touch: the panel, its close button and one hand-over button per need.</summary>
        public static Rect VillageSitePanel(ArenaHudLayout layout)
        {
            float height = Mathf.Min(330f, layout.Height - 58f);
            return new Rect((layout.Width - 600f) * .5f, (layout.Height - height) * .5f, 600f, height);
        }

        public static Rect VillageSiteCloseRect(ArenaHudLayout layout)
        { Rect panel = VillageSitePanel(layout); return new Rect(panel.xMax - 132f, panel.y + 8f, 120f, 30f); }

        public static Rect VillageSiteGiftRect(ArenaHudLayout layout, int need)
        {
            Rect panel = VillageSitePanel(layout);
            return new Rect(panel.x + 14f + need * 288f, panel.yMax - 50f, 284f, 40f);
        }

        public static Rect VillageSiteTextRect(ArenaHudLayout layout)
        {
            Rect panel = VillageSitePanel(layout);
            return Rect.MinMaxRect(panel.x + 16f, panel.y + 46f, panel.xMax - 16f, VillageSiteGiftRect(layout, 0).yMin - 6f);
        }

        /// <summary>What the panel says: the asker's words, then for every need what lies at the lot and what the pack holds.</summary>
        private string VillageSiteText(VillagePlace place)
        {
            CharacterProgression p = _game.Progression;
            var text = new System.Text.StringBuilder();
            text.AppendLine(_siteThanked == place && p.Houses.Stage(place) == BuildStage.Raising ? VillageBuildRules.Thanks(place) : _game.SiteWords(place));
            text.AppendLine();
            for (int need = 0; need < VillageBuildRules.NeedCount; need++)
            {
                VillageBuildRules.Need n = VillageBuildRules.NeedAt(place, need);
                text.AppendLine(VillageBuildRules.GoodName(n.Good) + ": " + p.Houses.Delivered(place, need) + " von " + n.Count + " gebracht · du hast " +
                    VillageBuildRules.Carried(p, n.Good) + " bei dir");
            }
            return text.ToString();
        }

        /// <summary>The label of a hand-over button, or null when there is nothing to hand over for that need.</summary>
        private string VillageSiteGiftLabel(VillagePlace place, int need)
        {
            CharacterProgression p = _game.Progression;
            if (p.Houses.Stage(place) != BuildStage.Site) return null;
            VillageBuildRules.Need n = VillageBuildRules.NeedAt(place, need);
            int count = Mathf.Min(p.Houses.Missing(place, need), VillageBuildRules.Carried(p, n.Good));
            return count <= 0 ? null : "ABGEBEN: " + count + " × " + VillageBuildRules.GoodName(n.Good);
        }

        private bool VillageSiteTouch(ArenaHudLayout layout, TouchPhase phase, Vector2 point)
        {
            VillagePlace place = _game.SiteInReach;
            if (!_sitePanelOpen)
            {
                if (phase != TouchPhase.Began || !VillageSiteToggleRect(layout).Contains(point)) return false;
                _sitePanelOpen = true;
                return true;
            }
            if (phase != TouchPhase.Began) return true;
            if (VillageSiteCloseRect(layout).Contains(point)) { _sitePanelOpen = false; return true; }
            for (int need = 0; need < VillageBuildRules.NeedCount; need++)
                if (VillageSiteGiftRect(layout, need).Contains(point) && VillageSiteGiftLabel(place, need) != null)
                { _pendingSiteGift = (int)VillageBuildRules.NeedAt(place, need).Good; break; }
            return true;
        }

        /// <summary>The panel belongs to standing with the asker; the Core checks the distance again when something is handed over.</summary>
        private bool PollVillageSiteInput(ArenaHudLayout layout)
        {
            if (_game.SiteInReach == VillagePlace.None || _hungerJournalOpen) { _sitePanelOpen = false; return false; }
            bool consumed = false;
            if (Input.GetKeyDown(KeyCode.N) || (_sitePanelOpen && Input.GetKeyDown(KeyCode.Escape))) { _sitePanelOpen = !_sitePanelOpen; consumed = true; }
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (!layout.IsWithinSafeArea(touch.position)) continue;
                consumed |= VillageSiteTouch(layout, touch.phase, layout.ToGuiTouch(touch.position));
            }
            if (!consumed && !_sitePanelOpen) return false;
            _game.SetMoveInput(0, 0);
            ReleasePresentationTouches();
            return true;
        }

        /// <summary>Hand over on the game tick; the Core decides, and the save follows an accepted hand-over.</summary>
        private void ProcessVillageSiteGift()
        {
            if (_pendingSiteGift < 0) return;
            var good = (VillageGood)_pendingSiteGift;
            _pendingSiteGift = -1;
            VillagePlace place = _game.SiteInReach;
            SocialResult result = _game.GiveToSite(good);
            if (!string.IsNullOrEmpty(result.Text)) _message = result.Text;
            if (!result.Accepted) return;
            if (place != VillagePlace.None && _game.Progression.Houses.Stage(place) == BuildStage.Raising) _siteThanked = place;
            SaveCharacter();
        }

        /// <summary>Draw the button while the player stands with the asker, and the panel when it is open.</summary>
        private bool DrawVillageSite(ArenaHudLayout layout)
        {
            VillagePlace place = _game.SiteInReach;
            if (place == VillagePlace.None || _hungerJournalOpen || _villageBoardOpen) return false;
            bool touch = Application.isMobilePlatform;
            if (!_sitePanelOpen)
            {
                if (touch) GUI.Box(VillageSiteToggleRect(layout), VillageSiteHint);
                else if (GUI.Button(VillageSiteToggleRect(layout), "N: " + VillageSiteHint)) _sitePanelOpen = true;
                return false;
            }
            Rect panel = VillageSitePanel(layout);
            GUI.Box(panel, "");
            GUI.Label(new Rect(panel.x + 16f, panel.y + 10f, panel.width - 160f, 26f), "BAUPLATZ: " + VillagePlaceLayout.Name(place).ToUpperInvariant(), _labelStyle);
            if (touch) GUI.Box(VillageSiteCloseRect(layout), "SCHLIESSEN");
            else if (GUI.Button(VillageSiteCloseRect(layout), "SCHLIESSEN (N)")) _sitePanelOpen = false;
            GUI.Label(VillageSiteTextRect(layout), VillageSiteText(place), _smallStyle);
            for (int need = 0; need < VillageBuildRules.NeedCount; need++)
            {
                string label = VillageSiteGiftLabel(place, need);
                if (label == null) continue;
                if (touch) GUI.Box(VillageSiteGiftRect(layout, need), label);
                else if (GUI.Button(VillageSiteGiftRect(layout, need), label)) _pendingSiteGift = (int)VillageBuildRules.NeedAt(place, need).Good;
            }
            return true;
        }
    }
}
