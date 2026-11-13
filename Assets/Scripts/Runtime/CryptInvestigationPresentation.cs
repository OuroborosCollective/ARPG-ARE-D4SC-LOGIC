using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        private Transform _investigationGate, _innerGate, _cryptWarning, _cryptDebris;

        /// <summary>Offer at most three contextual actions; Core validates each queued request independently.</summary>
        private HungerAction[] InvestigationActions()
        {
            var p = _game.Progression; var c = p.Investigation;
            if (p.EntryAct == 2 && p.Rattler.BrandExamined && !p.Rattler.Reported) return new[] { HungerAction.ReportRoyalBrand };
            if (p.EntryAct == 7) return p.Rattler.Creature.IsDead && !p.Rattler.BrandExamined ? new[] { HungerAction.InspectRoyalBrand } : new HungerAction[0];
            if (p.EntryAct == 6 && c.TraceFound && !p.Rattler.Active) return new[] { HungerAction.OpenInnerDoor };
            if (p.EntryAct == 2 && c.TraceFound && !c.Reported) return new[] { HungerAction.ReportCrypt };
            if (p.EntryAct == 6 && !c.TraceFound) return new[] { HungerAction.InspectCryptTrace };
            if (p.EntryAct != 5 || c.Approach != SealApproach.Undecided) return new HungerAction[0];
            if ((c.Clues & 4) != 0 && _game.AtInvestigationSeal)
                return c.Clues == 7 ? new[] { HungerAction.OpenDungeonSeal, HungerAction.BreakDungeonSeal } : new[] { HungerAction.BreakDungeonSeal };
            var actions = new List<HungerAction>(3);
            if ((c.Clues & 1) == 0) actions.Add(HungerAction.StoreBandage);
            if ((c.Clues & 2) == 0) actions.Add(HungerAction.StoreTracks);
            if ((c.Clues & 4) == 0) actions.Add(HungerAction.InspectSeal);
            return actions.ToArray();
        }

        /// <summary>Use reviewed passive stone art and the same immutable obstacle footprints as the simulation.</summary>
        private void BuildInvestigationScenery()
        {
            bool crypt = _game.Progression.EntryAct == 6;
            string prefix = crypt ? "HiddenCrypt_" : "OldStore_";
            RenderSettings.ambientLight = crypt ? new Color(.24f, .29f, .32f) : new Color(.34f, .31f, .26f);
            RenderSettings.fogColor = crypt ? new Color(.12f, .17f, .20f) : new Color(.23f, .20f, .17f);
            RenderSettings.fogDensity = .012f;
            ChapterShape(PrimitiveType.Cube, prefix + "Ground", new Vector3(0, -.35f, 0),
                new Vector3(25, .5f, 54), new Color(.24f, .23f, .21f));
            for (int z = -24; z <= 24; z += 6)
                for (int x = -9; x <= 9; x += 6)
                    ChapterShape(PrimitiveType.Cube, prefix + "Floor", new Vector3(x, -.06f, z),
                        new Vector3(6, .12f, 6), new Color(.34f, .34f, .32f), "AshStoneFloor");
            for (int side = -1; side <= 1; side += 2)
                for (int z = -24; z <= 24; z += 6)
                    ChapterShape(PrimitiveType.Cube, prefix + "Wall", new Vector3(side * 12.5f, 1.5f, z),
                        new Vector3(1, 3, 6), new Color(.28f, .29f, .27f), "AshStoneWall");
            for (int i = 0; i < 2; i++)
            {
                var box = ArenaSimulation.InvestigationObstacle(crypt, i);
                ChapterShape(PrimitiveType.Cube, prefix + (crypt ? "Sarcophagus_" : "Crates_") + i,
                    new Vector3(box.X / 1000f, crypt ? .65f : 1.1f, box.Z / 1000f),
                    new Vector3(box.HalfX / 500f, crypt ? 1.3f : 2.2f, box.HalfZ / 500f),
                    crypt ? new Color(.38f, .39f, .37f) : new Color(.38f, .27f, .17f), crypt ? "AshStoneWall" : "AshCrate");
            }
            _returnPassage = ChapterShape(PrimitiveType.Cylinder, "ARE_Return_Passage", new Vector3(0, .035f, -24),
                new Vector3(3.6f, .02f, 3.6f), new Color(.45f, .65f, .72f));
            if (!crypt)
            {
                _worldPassage = ChapterShape(PrimitiveType.Cylinder, "ARE_World_Passage", new Vector3(0, .035f, 24),
                    new Vector3(3.6f, .02f, 3.6f), new Color(.2f, .65f, .72f));
                _investigationGate = BuildInvestigationDoor("OldStore_SealedGate", false);
                ChapterShape(PrimitiveType.Cube, "OldStore_Bandage", new Vector3(-6.5f, .05f, 0),
                    new Vector3(1.1f, .08f, .35f), new Color(.85f, .78f, .61f));
                for (int i = 0; i < 7; i++)
                    ChapterShape(PrimitiveType.Cube, "OldStore_DragTrace", new Vector3(6.5f - i * .65f, .015f, -3 + i * 2),
                        new Vector3(.2f, .02f, 1.3f), new Color(.12f, .08f, .06f));
                ClueMark("OldStore_BandageMark", -6.5f, 0);
                ClueMark("OldStore_TraceMark", 6.5f, -3);
                ClueMark("OldStore_SealMark", 0, 18);
            }
            else
            {
                _worldPassage = ChapterShape(PrimitiveType.Cylinder, "ARE_World_Passage", new Vector3(0, .035f, 24),
                    new Vector3(3.6f, .02f, 3.6f), new Color(.2f, .65f, .72f));
                _innerGate = BuildInvestigationDoor("HiddenCrypt_InnerDoor", true);
                ChapterShape(PrimitiveType.Cube, "HiddenCrypt_FreshBandage", new Vector3(0, .05f, 15),
                    new Vector3(1.3f, .08f, .4f), new Color(.9f, .8f, .65f));
                ClueMark("HiddenCrypt_TraceMark", 0, 15);
                _cryptWarning = ChapterShape(PrimitiveType.Cylinder, "HiddenCrypt_FallWarning", new Vector3(0, .04f, ArenaSimulation.CryptTrapZmm / 1000f),
                    new Vector3(ArenaSimulation.CryptTrapRadiusMm / 500f, .02f, ArenaSimulation.CryptTrapRadiusMm / 500f), new Color(.95f, .20f, .09f));
                _cryptDebris = ChapterShape(PrimitiveType.Cube, "HiddenCrypt_SpentFall", new Vector3(0, .035f, ArenaSimulation.CryptTrapZmm / 1000f),
                    new Vector3(3.7f, .06f, 3.4f), new Color(.22f, .22f, .23f));
            }
            UpdateInvestigationPresentation();
        }

        /// <summary>Build a recognizable four-metre doorway within permanent flanking walls.</summary>
        private Transform BuildInvestigationDoor(string label, bool crypt)
        {
            for (int side = -1; side <= 1; side += 2)
                for (int section = 0; section < 2; section++)
                    ChapterShape(PrimitiveType.Cube, label + "_Flank", new Vector3(side * (4.5f + section * 5f), 1.7f, 21),
                        new Vector3(5, 3.4f, 1), new Color(.3f, .31f, .32f), "AshStoneWall");
            var door = ChapterShape(PrimitiveType.Cube, label, new Vector3(0, 1.6f, 21),
                new Vector3(4, 3.2f, 1), crypt ? new Color(.18f, .24f, .27f) : new Color(.34f, .23f, .13f));
            for (int side = -1; side <= 1; side += 2)
            {
                var edge = ChapterShape(PrimitiveType.Cube, label + "_Binding", new Vector3(side * 1.75f, 1.6f, 20.45f),
                    new Vector3(.16f, 3, .12f), new Color(.58f, .47f, .25f));
                edge.SetParent(door, true);
            }
            var seal = ChapterShape(PrimitiveType.Cylinder, label + "_Seal", new Vector3(0, 1.7f, 20.43f),
                new Vector3(.8f, .06f, .8f), crypt ? new Color(.21f, .57f, .65f) : new Color(.75f, .44f, .16f));
            seal.rotation = Quaternion.Euler(90, 0, 0); seal.SetParent(door, true);
            return door;
        }

        /// <summary>Flat clue markers are cosmetic and create no collision or quest state.</summary>
        private void ClueMark(string label, float x, float z)
        {
            ChapterShape(PrimitiveType.Cylinder, label, new Vector3(x, .025f, z),
                new Vector3(2.5f, .01f, 2.5f), new Color(.63f, .48f, .19f));
        }

        /// <summary>Show the canonical opening and one-second hazard warning; Unity never applies damage.</summary>
        private void UpdateInvestigationPresentation()
        {
            var c = _game.Progression.Investigation;
            if (_investigationGate != null) _investigationGate.gameObject.SetActive(c.Approach == SealApproach.Undecided);
            if (_innerGate != null) _innerGate.gameObject.SetActive(!_game.Progression.Rattler.Active);
            if (_cryptWarning != null) _cryptWarning.gameObject.SetActive(c.TrapTicks > 0);
            if (_cryptDebris != null) _cryptDebris.gameObject.SetActive(c.TrapTicks == -1);
        }
    }
}
