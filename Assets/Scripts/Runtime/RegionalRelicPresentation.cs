using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        private const string Workshop = "Equipment/AshWorks/";
        private const string Arsenal = "ARE/Visuals/Werkarsenal/";
        private const string LampPrefab = "AshenRelics/Grubenlicht_World";
        private readonly Dictionary<RegionalRelic, Transform> _regionalSources = new Dictionary<RegionalRelic, Transform>();
        private Transform _regionalWorker;
        private ArenaSimulation _regionalSourceWorld;
        private ulong _regionalSourceTick = ulong.MaxValue;
        private WeaponEquipment _regionalWornOwner;
        private long _regionalWearMask = -1;
        private RegionalWearableRig _regionalWornRig;

        private void ClearRegionalWorld()
        {
            if (_regionalWorker) _regionalWorker.gameObject.SetActive(false);
            _regionalWorker = null;
            foreach (var root in _regionalSources.Values)
                if (root) { root.gameObject.SetActive(false); Destroy(root.gameObject); }
            _regionalSources.Clear();
            _regionalSourceWorld = null;
            _regionalSourceTick = ulong.MaxValue;
        }

        private void BuildRegionalWorker()
        {
            if (!_entryProfile || _game.InDungeon || _game.InWoodlandRegion || _game.InJourney) return;
            int act = _game.Progression.EntryAct;
            if (act != 3 && act != 4) return;
            bool guard = act == 3;
            float x = guard ? 4.15f : -7.46f, z = guard ? -1.02f : 2.17f;
            var actor = new GameObject(guard ? "ARE_Schlackengarde" : "ARE_Glockentraegerin").transform;
            actor.position = new Vector3(x, GroundHeight(x,z), z);
            actor.rotation = Quaternion.Euler(0f, guard ? -100f : 144f, 0f);
            string character = guard ? "EntryHero" : "EntryNpc1";
            if (PassiveArtVisuals.TryBindGrounded(actor, character, actor.position.y))
            {
                // The worker borrows a story figure's body, not its staff, pouches or jewellery.
                PassiveArtVisuals.DropCarried(actor);
                if (guard)
                {
                    var donor=Resources.Load<GameObject>("ARE/Visuals/EntryNpc2");
                    var original=donor ? donor.GetComponentInChildren<Animator>(true) : null;
                    var worker=actor.GetComponentInChildren<Animator>(true);
                    if (worker && original && PassiveArtVisuals.HasPlayableClip(original.runtimeAnimatorController))
                    {
                        worker.runtimeAnimatorController=original.runtimeAnimatorController;
                        worker.Rebind();worker.Update(0f);
                    }
                }
                var rig = RegionalWearableRig.Find(actor);
                var id = guard ? RegionalRelic.Schlackengarde : RegionalRelic.Glockentraegerin;
                if (rig != null && rig.Dress(id, true, false))
                {
                    foreach (var animator in actor.GetComponentsInChildren<Animator>())
                    { animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms; animator.Update(0f); }
                    _regionalWorker = actor;
                    _chapterScenery.Add(actor);
                    return;
                }
            }
            Debug.LogWarning("Ashen Crypt: Arbeitsmontur oder Humanoid fehlt für " + actor.name);
            actor.gameObject.SetActive(false);
            Destroy(actor.gameObject);
        }

        private static GameObject RegionalSourcePrefab(RegionalRelic id)
        {
            string path;
            switch (id)
            {
                case RegionalRelic.Schlackengarde: path = Workshop + "Schlackengarde_Fund"; break;
                case RegionalRelic.Glockentraegerin: path = Workshop + "Glockentraegerin_Fund"; break;
                case RegionalRelic.Brandreif: path = Workshop + "Brandreif_Fund"; break;
                case RegionalRelic.Grubenlicht: path = LampPrefab; break;
                case RegionalRelic.Rattlerkluft: path = Arsenal + "R_Wangenmaske"; break;
                // Orsel's staff has no model of its own yet; it borrows the one its hand definition names.
                case RegionalRelic.M_Heilerstab: path = "ARE/Visuals/" + RegionalRelics.HandDefinition(id).VisualKey; break;
                default: path = Arsenal + id; break;
            }
            return Resources.Load<GameObject>(path);
        }

        private void UpdateRegionalSources()
        {
            if (_game == _regionalSourceWorld && _regionalSourceTick == _game.CurrentTick) return;
            _regionalSourceWorld = _game;
            _regionalSourceTick = _game.CurrentTick;
            foreach (var id in RegionalRelics.All)
            {
                bool visible = _game.TryGetRegionalRelicSource(id, out long xmm, out long zmm);
                if (_regionalSources.TryGetValue(id, out var previous))
                {
                    if (visible) continue;
                    if (previous) { previous.gameObject.SetActive(false); Destroy(previous.gameObject); }
                    _regionalSources.Remove(id);
                }
                if (!visible) continue;
                var root = new GameObject("Fund_" + id).transform;
                var prefab = RegionalSourcePrefab(id);
                float x = xmm * .001f, z = zmm * .001f;
                float floor = GroundHeight(x,z) + WalkSurfaceHeight(x,z);
                root.position = new Vector3(x, floor, z);
                if (!PassiveArtVisuals.TryBindPrefab(root, prefab))
                {
                    Debug.LogWarning("Ashen Crypt: Fundmodell fehlt für " + id);
                    Destroy(root.gameObject);
                    _regionalSources.Add(id, null);
                    continue;
                }
                var art = root.GetChild(0);
                bool tool = RegionalRelics.Slot(id) == RegionalRelicSlot.Weapon || RegionalRelics.Slot(id) == RegionalRelicSlot.Shield;
                art.localRotation = tool ? Quaternion.Euler(66f, 33f, -12f) : Quaternion.Euler(7f, 21f, -8f);
                var renderers = art.GetComponentsInChildren<Renderer>();
                float bottom = float.MaxValue;
                foreach (var renderer in renderers) bottom = Mathf.Min(bottom, renderer.bounds.min.y);
                if (bottom < float.MaxValue) art.position += Vector3.up * (floor + .012f - bottom);
                _regionalSources.Add(id, root);
            }
        }

        private bool RefreshRegionalWear(WeaponEquipment gear)
        {
            bool changed = _regionalWornOwner != gear || gear.RegionalWearMask != _regionalWearMask;
            if (!changed) return false;
            if (_regionalWornRig != null) _regionalWornRig.Clear();
            _regionalWornRig = null;
            ClearBakedArmor();
            _regionalWornOwner = gear;
            _regionalWearMask = gear.RegionalWearMask;
            return true;
        }

        private void MountRegionalWear(WeaponEquipment gear)
        {
            if (_regionalWearMask == 0) return;
            var outfit = gear.WornRegionalRelic(RegionalRelicSlot.Body);
            var head = gear.WornRegionalRelic(RegionalRelicSlot.Head);
            var belt = gear.WornRegionalRelic(RegionalRelicSlot.Belt);
            var utility = gear.WornRegionalRelic(RegionalRelicSlot.Utility);
            var shoulders = gear.WornRegionalRelic(RegionalRelicSlot.Shoulders);
            var limbs = gear.WornRegionalRelic(RegionalRelicSlot.Limbs);
            // Aschenwacht's armour and the Nachtvesper are skinned to the hero's own bones (AschenwachtGearBuilder), not hung on sockets.
            foreach (var piece in new[] { outfit, head, shoulders, limbs, gear.WornRegionalRelic(RegionalRelicSlot.Arms), gear.WornRegionalRelic(RegionalRelicSlot.Hands),
                gear.WornRegionalRelic(RegionalRelicSlot.Waist), gear.WornRegionalRelic(RegionalRelicSlot.Legs), gear.WornRegionalRelic(RegionalRelicSlot.Feet) })
                if (IsBakedArmor(piece) && !MountBakedArmor(piece)) Debug.LogWarning("Ashen Crypt: Rüstung konnte nicht angelegt werden: " + piece);
            if (gear.WornRegionalRelic(RegionalRelicSlot.Weapon) == RegionalRelic.F_Langbogen &&
                !MountBakedPrefab(Resources.Load<GameObject>("ARE/Visuals/" + HunterQuiver), "F_Koecher", false))
                Debug.LogWarning("Ashen Crypt: Köcher konnte nicht angelegt werden");
            if (IsBakedArmor(outfit)) outfit = RegionalRelic.None;
            if (IsBakedArmor(head)) head = RegionalRelic.None;
            if (outfit == RegionalRelic.None && head == RegionalRelic.None && belt == RegionalRelic.None && utility == RegionalRelic.None) return;
            _regionalWornRig = RegionalWearableRig.Find(_hero);
            if (_regionalWornRig == null) return;
            if (outfit != RegionalRelic.None && !_regionalWornRig.Dress(outfit, false, head != RegionalRelic.None))
                Debug.LogWarning("Ashen Crypt: unvollständige Montur für " + outfit);
            if (head == RegionalRelic.Brandreif && !_regionalWornRig.Headpiece())
                Debug.LogWarning("Ashen Crypt: Brandreif konnte nicht gebunden werden");
            if (belt == RegionalRelic.Grubenlicht)
                _regionalWornRig.Equipment(RegionalSourcePrefab(belt), "Equipped_Grubenlicht",
                    HumanBodyBones.Hips, new Vector3(.205f, -.025f, -.085f));
            if (utility != RegionalRelic.None)
                _regionalWornRig.Equipment(RegionalSourcePrefab(utility), "Equipped_" + utility, HumanBodyBones.LeftHand, Vector3.zero);
        }

        private SocialResult TakeRegionalRelic()
        {
            _game.TryGetClaimableRegionalRelic(out var id);
            var result = _game.ActOnHunger(HungerAction.ClaimRegionalRelic);
            if (result.Accepted)
            {
                var owned = OwnedRegionalRelics();
                int index = owned.IndexOf(id);
                _selectedItem = _game.Progression.Items.Count + (_game.Progression.Equipment.FieldPauldronOwned ? 1 : 0) + index;
                _inventoryPage = _selectedItem / InventoryPageSize;
                _regionalSourceTick = ulong.MaxValue;
                UpdateRegionalSources();
            }
            return result;
        }

        private List<RegionalRelic> OwnedRegionalRelics()
        {
            var owned = new List<RegionalRelic>();
            foreach (var id in RegionalRelics.All)
                if (_game.Progression.Equipment.OwnsRegionalRelic(id)) owned.Add(id);
            return owned;
        }

        private void ToggleRegionalRelic(RegionalRelic id)
        {
            bool equip = !_game.Progression.Equipment.IsRegionalRelicEquipped(id);
            if (_game.TrySetRegionalRelicEquipped(id, equip))
            {
                _message = RegionalRelics.Name(id) + (equip ? " angelegt." : " abgelegt.");
                SaveCharacter();
                UpdateEquipmentPresentation();
            }
            else _message = "Dafür brauchst du eine freie Hand. Lege zuerst die Zweihandwaffe, das Werkzeug oder den Schild ab.";
            BuildMenu();
        }

        private void RegionalInventoryCell(Transform cell, RegionalRelic id, float size)
        {
            string iconPath;
            switch (id)
            {
                case RegionalRelic.Grubenlicht: iconPath = "AshenRelics/Grubenlicht_Icon"; break;
                case RegionalRelic.Schlackengarde: case RegionalRelic.Glockentraegerin:
                case RegionalRelic.Brandreif: case RegionalRelic.G_Ziegelschild:
                case RegionalRelic.G_Schuerhaken: case RegionalRelic.M_Handglocke:
                    iconPath = Workshop + id + "_Icon"; break;
                case RegionalRelic.Rattlerkluft: iconPath = "Equipment/Werkarsenal/Icons/R_Gratharnisch"; break;
                default: iconPath = "Equipment/Werkarsenal/Icons/" + id; break;
            }
            var icon = Resources.Load<Texture2D>(iconPath);
            if (icon)
            {
                var image = AshenUiTheme.Node("RegionalIcon", cell).gameObject.AddComponent<RawImage>();
                image.texture = icon;
                image.raycastTarget = false;
                AshenUiTheme.Stretch(image.rectTransform, 3f);
            }
            else
            {
                var label = AshenUiTheme.Label("Name", cell, Mathf.Clamp(Mathf.RoundToInt(size * .23f), 8, 13),
                    AshenUiTheme.Parchment, TextAnchor.MiddleCenter, FontStyle.Bold);
                label.resizeTextForBestFit = true; label.resizeTextMinSize = 7; label.resizeTextMaxSize = 13;
                label.text = RegionalRelics.Name(id);
                AshenUiTheme.Stretch(label.rectTransform, 2f);
            }
        }

        /// <summary>Where a piece of armour is worn, and for the Nachtvesper how far the set is complete and what it gives.</summary>
        private string ArmourDescription(RegionalRelic id)
        {
            string place;
            switch (RegionalRelics.Slot(id))
            {
                case RegionalRelicSlot.Head: place = "Helm"; break;
                case RegionalRelicSlot.Body: place = "Brustpanzer"; break;
                case RegionalRelicSlot.Shoulders: place = "Schulterschutz"; break;
                case RegionalRelicSlot.Limbs: place = "Arm- und Beinschutz"; break;
                case RegionalRelicSlot.Arms: place = "Armschutz"; break;
                case RegionalRelicSlot.Hands: place = "Handschutz"; break;
                case RegionalRelicSlot.Waist: place = "Gürtung"; break;
                case RegionalRelicSlot.Legs: place = "Beinschutz"; break;
                case RegionalRelicSlot.Feet: place = "Fußschutz"; break;
                default: place = "Rüstung"; break;
            }
            var gear = _game.Progression.Equipment;
            int Worn(RegionalRelic[] set) { int n = 0; foreach (var piece in set) if (gear.IsRegionalRelicEquipped(piece)) n++; return n; }
            if (RotfaehrteSet.IsPiece(id)) return place + " · Leder · Set Rotfährte (" + Worn(RotfaehrteSet.Pieces) + "/" + RotfaehrteSet.Pieces.Length + " angelegt)";
            if (RusskantorSet.IsPiece(id)) return place + " · Leinen · Set Rußkantor (" + Worn(RusskantorSet.Pieces) + "/" + RusskantorSet.Pieces.Length + " angelegt)";
            if (!NachtvesperSet.IsPiece(id)) return place + " · Platte";
            int worn = _game.Progression.NachtvesperPiecesWorn;
            return place + " · Platte · Set Nachtvesper (" + worn + "/" + NachtvesperSet.Pieces.Length + " angelegt)" +
                "\nJedes angelegte Teil: +1 % Lebenspunkte\nAlle " + NachtvesperSet.Pieces.Length + " Teile: +25 % Ausdauer";
        }

        private void RegionalInventoryDetail(RectTransform panel, Text info, Vector2 size, float width, RegionalRelic id)
        {
            bool equipped = _game.Progression.Equipment.IsRegionalRelicEquipped(id);
            var definition = RegionalRelics.HandDefinition(id);
            string description;
            if (definition != null)
            {
                description = definition.Family == WeaponFamily.Shield ?
                    "Schild · " + (definition.BasePower / 100f).ToString("0.#") + " % Schadensminderung" :
                    definition.FamilyName + " · " + (definition.TwoHanded ? "Zweihand" : "Einhand") +
                    "\nGrundschaden " + definition.BasePower + " · Reichweite " + (definition.RangeMm / 1000f).ToString("0.0") + " m";
            }
            else if (IsBakedArmor(id)) description = ArmourDescription(id);
            else
            {
                var slot = RegionalRelics.Slot(id);
                description = slot == RegionalRelicSlot.Body ? "Montur" : slot == RegionalRelicSlot.Head ? "Kopfschmuck" :
                    slot == RegionalRelicSlot.Belt ? "Gürtellicht" : "Werkzeug für die Nebenhand";
            }
            info.text = "<size=18><b>" + RegionalRelics.Name(id) + "</b></size>\n" + description + "\n\n" +
                RegionalRelics.Location(id) + "\n\n" + (equipped ? "Angelegt" : "In der Tasche");
            AshenUiTheme.Place(info.rectTransform, new Rect(16,36,width-32,size.y-100));
            AshenUiTheme.MenuButton("EquipRegionalRelic", panel, new Rect(16,size.y-48,width-32,34),
                equipped ? "ABLEGEN" : "ANLEGEN", () => ToggleRegionalRelic(id), fontSize: 13);
        }
    }
}
