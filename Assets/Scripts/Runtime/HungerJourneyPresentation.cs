using AreArpg.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using Input = AreArpg.UnityClient.PlayerDevice;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        private bool StoryJournalAvailable => _game.Progression.EntryAct >= 2 || _game.InCourt || _game.InKilnYard;
        private bool _hungerJournalOpen;
        private bool _chroniclePageOpen;
        private int _chroniclePage;
        private CharacterProgression _chronicleCacheOwner;
        private int _chronicleCacheEvents = -1;
        private int _chronicleCacheAcquisitions = -1;
        private string _chronicleCacheToken;
        private IReadOnlyList<ChronicleEntry> _chronicleCache = Array.Empty<ChronicleEntry>();
        private int _pendingHungerAction = -1;
        private Transform _returnPassage;

        /// <summary>Keep the journal control separate from movement, combat, pickup and camera targets.</summary>
        /// <summary>The journal button sits at the right end of the quest tracker.</summary>
        public static Rect HungerToggleRect(ArenaHudLayout layout)
        { Rect q = layout.QuestTrackerRect; return new Rect(q.xMax - 98f, q.y + 6f, 92f, q.height - 12f); }

        /// <summary>One modal layout is shared by desktop drawing and physical Android touch routing.</summary>
        private static Rect HungerPanel(ArenaHudLayout layout)
        { return new Rect((layout.Width - 560f) * .5f, 50f, 560f, 350f); }

        /// <summary>Bounded, touch-safe view toggle; the original quest panel remains intact.</summary>
        public static Rect ChronicleTabRect(ArenaHudLayout layout)
        { var p = HungerPanel(layout); return new Rect(p.x + p.width - 122f, p.y + 6f, 106f, 36f); }
        public static Rect ChroniclePreviousRect(ArenaHudLayout layout)
        { var p = HungerPanel(layout); return new Rect(p.x + 18f, p.yMax - 48f, 102f, 36f); }
        public static Rect ChronicleNextRect(ArenaHudLayout layout)
        { var p = HungerPanel(layout); return new Rect(p.xMax - 120f, p.yMax - 48f, 102f, 36f); }

        /// <summary>Lay out a bounded context action beneath the full objective and feedback text.</summary>
        private static Rect HungerActionRect(ArenaHudLayout layout, int index)
        { var panel = HungerPanel(layout); return new Rect(panel.x + 15f, panel.y + 220f + index * 44f, 530f, 40f); }

        /// <summary>Expose actions relevant to the current chapter; the Core independently enforces distance and state.</summary>
        private HungerAction[] HungerActions()
        {
            var story = StoryActions();
            if (!_game.CanClaimRegionalRelic) return story;
            var actions = new HungerAction[story.Length + 1];
            System.Array.Copy(story, actions, story.Length);
            actions[story.Length] = HungerAction.ClaimRegionalRelic;
            return actions;
        }

        private HungerAction[] StoryActions()
        {
            var p = _game.Progression;
            if (_game.InNordwechselRift) return Array.Empty<HungerAction>();
            if (_game.InCourt) return _game.NearCourtTrace.HasValue ? new[] { HungerAction.ReadCourtTrace } : new HungerAction[0];
            if (_game.InKilnYard)
                return _game.NearKilnGate ? new[] { HungerAction.OpenKilnGate } :
                    _game.NearKilnVase.HasValue ? new[] { HungerAction.BreakKilnVase } : new HungerAction[0];
            if (_game.InJourney) return JourneyActions();
            if (_game.InDungeon) return DungeonActions();
            var freight=FreightActions(); if(freight!=null)return freight;
            var woodland=WoodlandActions(); if(woodland!=null)return woodland;
            if (p.Investigation.Active) return InvestigationActions();
            if (p.EntryAct == 2 && p.HungerStage == 0) return new[] { HungerAction.HearIlyan };
            if (p.EntryAct == 3 && p.HungerStage == 1) return new[] { HungerAction.InspectTrail };
            if (p.EntryAct == 4 && p.HungerStage == 2) return new[] { HungerAction.InspectShelter };
            if (p.EntryAct == 2 && p.HungerStage == 3 && p.SupplyChoice == HungerChoice.Undecided)
                return new[] { HungerAction.Reveal, HungerAction.Protect, HungerAction.Replace };
            if (p.EntryAct == 4 && p.HungerStage == 3 && p.SupplyChoice != HungerChoice.Undecided)
                return new[] { HungerAction.InspectBeds };
            return new HungerAction[0];
        }

        /// <summary>Show the consequence and real replacement cost before the player commits the branch.</summary>
        private string HungerActionLabel(HungerAction action)
        {
            var authored=AuthoredHunger;
            if(authored!=null && ((int)action>=0 && (int)action<=2 || action==HungerAction.InspectBeds)) return authored.labels[(int)action];
            switch (action)
            {
                case HungerAction.ClaimRegionalRelic:
                    return _game.TryGetClaimableRegionalRelic(out var relic) ? RegionalRelics.Name(relic) + " mitnehmen" : "Fundstück mitnehmen";
                case HungerAction.HearIlyan: return "BEI ILYAN: Nach seinem Geheimnis fragen";
                case HungerAction.InspectTrail: return "AM WEGZEICHEN: Verband und Spuren untersuchen";
                case HungerAction.InspectShelter: return "IM LAGER: Verbandszeug und Essensreste untersuchen";
                case HungerAction.Reveal: return "BEI MARA: Hilfe offenlegen - Mara erfaehrt das Geheimnis";
                case HungerAction.Protect: return "BEI ILYAN: Schweigen zusagen - Fehlbestand bleibt";
                case HungerAction.Replace: return "BEI MARA: Geheimnis bewahren, Lager auffuellen (" +
                    (SocialLedger.CampCapacity - _game.Progression.Social.CampProvisions) + " Rationen)";
                case HungerAction.InspectBeds: return "AN DEN BETTEN: Das Verschwinden untersuchen";
                case HungerAction.StoreBandage: return "AM WESTLICHEN VERBAND: Stoff und Knoten untersuchen";
                case HungerAction.StoreTracks: return "AN DER SCHLEIFSPUR: Fugen und Staub untersuchen";
                case HungerAction.InspectSeal: return "AM SIEGEL: Mechanismus und Gefahr untersuchen";
                case HungerAction.ReadCourtTrace: return "SPUR: " + (_game.NearCourtTrace.HasValue ? _game.NearCourtTrace.Value.Title : "Untersuchen");
                case HungerAction.OpenKilnGate: return KilnYard.OnLatchSide(_game.PlayerXmm, _game.PlayerZmm) ? "AM GITTER: Riegel zurückziehen" : "AM GITTER: Untersuchen";
                case HungerAction.BreakKilnVase: return "VASE: Zerschlagen";
                case HungerAction.OpenDungeonSeal: return "SIEGEL ENTLASTEN: Drei Kryptaebenen erkunden";
                case HungerAction.BeginDungeonRun: return "AM OFFENEN SIEGEL: Neuen Dungeon-Run beginnen";
                case HungerAction.BreakDungeonSeal: return "SIEGEL BRECHEN: Krypta betreten - Steinschlaggefahr!";
                case HungerAction.OpenWestChest: case HungerAction.OpenEastChest: return "AN DER TRUHE: Oeffnen und Beute freilegen";
                case HungerAction.OpenSeal: return "SIEGEL ENTLASTEN: Zugang kontrolliert oeffnen";
                case HungerAction.BreakSeal: return "SIEGEL BRECHEN: Gefahr durch lose Deckensteine!";
                case HungerAction.InspectCryptTrace: return "AN DER INNEREN TUER: Frische Spuren untersuchen";
                case HungerAction.OpenInnerDoor: return "AM VERBAND: Inneren Riegel loesen und weitergehen";
                case HungerAction.InspectRoyalBrand: return "AM HALS: Koenigliches Brandmal untersuchen";
                case HungerAction.ReportRoyalBrand: return "BEI ILYAN: Das Brandmal von Immergruen melden";
                case HungerAction.ReportCrypt: return "BEI ILYAN: Persoenlich von der Krypta berichten";
                case HungerAction.AcceptTransportLead: return "BEI ILYAN: Transportspur nach Aschenzoll aufnehmen";
                case HungerAction.ForestChest0: case HungerAction.ForestChest1: case HungerAction.ForestChest2: case HungerAction.ForestChest3: return "AN DER WALDTRUHE: Öffnen und Beute freilegen";
                case HungerAction.InspectCubs: return "AM NEST: Bärenjunge und Spuren untersuchen";
                case HungerAction.SpareCubs: return "VERSCHONEN: Junge am Leben lassen, Mara um Schutz bitten";
                case HungerAction.SkinCubs: return "TÖTEN UND HÄUTEN: 3 Felle; keine geretteten Jungen";
                case HungerAction.ReportCubs: return "BEI MARA: Standort der lebenden Jungen melden";
                case HungerAction.OrganizeCubProtection: return "BEI MARA: Futter und Schutz für die Jungen vereinbaren";
                case HungerAction.CraftPeltArmor: return "BEI MARA: 3 Felle verarbeiten – Rüstung, 10 % Schutz";
                case HungerAction.InspectFreightNotice: return "AM AUSHANG: Handels- und Aufenthaltsregeln lesen";
                case HungerAction.InspectFreightCage: return "AM KÄFIG: Leere Rückfracht und Sendungsnummer prüfen";
                case HungerAction.ReadFreightManifest: return "AM PULT: Den Frachtbeleg lesen";
                case HungerAction.SecureFreight: return "FESTSETZEN: Original bleibt hier; Weiterfahrt entfällt";
                case HungerAction.ReleaseFreight: return "FREIGEBEN: Original fährt mit; oben Spur prüfen";
                case HungerAction.TakeFreightOriginal: return "AM PULT: Originalbeleg tatsächlich mitnehmen";
                case HungerAction.InspectFreightArrival: return "OBERE RAMPE: Sendung und Abladezeichen prüfen";
                case HungerAction.ReportFreightEvidence: return "BEI ILYAN: Persönlich die Transportspur berichten";
                default: return "Unbekannte Handlung";
            }
        }

        /// <summary>Capture the whole open modal so story taps cannot also trigger attacks, movement or camera pinches.</summary>
        private bool PollHungerInput(ArenaHudLayout layout)
        {
            if (!StoryJournalAvailable) return false;
            bool consumed = false;
            if (Input.GetKeyDown(KeyCode.J) || (_hungerJournalOpen && Input.GetKeyDown(KeyCode.Escape)))
            { _hungerJournalOpen = !_hungerJournalOpen; consumed = true; }
            var actions = HungerActions();
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began || !layout.IsWithinSafeArea(touch.position)) continue;
                Vector2 point = layout.ToGuiTouch(touch.position);
                if (HungerToggleRect(layout).Contains(point))
                { _hungerJournalOpen = !_hungerJournalOpen; consumed = true; }
                else if (_hungerJournalOpen)
                {
                    if (ChronicleTabRect(layout).Contains(point)) _chroniclePageOpen = !_chroniclePageOpen;
                    else if (_chroniclePageOpen)
                    {
                        if (ChroniclePreviousRect(layout).Contains(point)) _chroniclePage++;
                        if (ChronicleNextRect(layout).Contains(point)) _chroniclePage = Math.Max(0, _chroniclePage - 1);
                    }
                    else
                        for (int action = 0; action < actions.Length; action++)
                            if (HungerActionRect(layout, action).Contains(point)) _pendingHungerAction = (int)actions[action];
                    consumed = true;
                }
            }
            if (!consumed && !_hungerJournalOpen) return false;
            _game.SetMoveInput(0, 0);
            ReleasePresentationTouches();
            return true;
        }

        /// <summary>Render the current Core objective and decision result; no UI-owned quest state is saved.</summary>
        private bool DrawHungerJournal(ArenaHudLayout layout)
        {
            if (!StoryJournalAvailable) return false;
            string toggle = _hungerJournalOpen ? "SCHLIESSEN" : (Application.isMobilePlatform ? "SPUREN" : "SPUREN (J)");
            if (Application.isMobilePlatform) GUI.Box(HungerToggleRect(layout), toggle);
            else if (GUI.Button(HungerToggleRect(layout), toggle)) _hungerJournalOpen = !_hungerJournalOpen;
            if (!_hungerJournalOpen) return false;
            Rect panel = HungerPanel(layout);
            if (_chroniclePageOpen)
            {
                DrawChroniclePage(layout, panel);
                return true;
            }
            string title = _game.InNordwechselRift ? "GLUTKLAMM · TIEFE " + _game.CryptDepth :
                _game.InJourney ? _game.ChapterName : _game.InDungeon ? "KRYPTA - RUN " + _game.Progression.Dungeon.Run + " / EBENE " + _game.Progression.Dungeon.Depth :
                _game.InAschenzoll ? "ASCHENZOLL — DIE TRANSPORTSPUR" : (AuthoredHunger!=null?AuthoredHunger.title:"HUNGER HINTER DEN MAUERN");
            GUI.Box(panel, title);
            if (Application.isMobilePlatform) GUI.Box(ChronicleTabRect(layout), "CHRONIK");
            else if (GUI.Button(ChronicleTabRect(layout), "CHRONIK")) _chroniclePageOpen = true;
            GUI.Label(new Rect(panel.x + 15, panel.y + 30, 530, 70), PresentedHungerObjective(), _smallStyle);
            GUI.Label(new Rect(panel.x + 15, panel.y + 105, 530, 30), "Proviant: " + _game.Progression.Social.Provisions +
                "  |  Entscheidung: " + HungerChoiceLabel(_game.Progression.SupplyChoice), _smallStyle);
            GUI.Label(new Rect(panel.x + 15, panel.y + 140, 530, 75), _message, _smallStyle);
            var actions = HungerActions();
            for (int i = 0; i < actions.Length; i++)
            {
                if (Application.isMobilePlatform) GUI.Box(HungerActionRect(layout, i), HungerActionLabel(actions[i]));
                else if (GUI.Button(HungerActionRect(layout, i), HungerActionLabel(actions[i]))) _pendingHungerAction = (int)actions[i];
            }
            return true;
        }

        private IReadOnlyList<ChronicleEntry> CachedPlayerChronicle()
        {
            var owner = _game.Progression;
            var archive = owner.Social.Forge?.Archive;
            int events = archive?.Events.Count ?? 0, acquisitions = archive?.Acquisitions.Count ?? 0;
            owner.TryGetSection("chronicle.v1", out string refs);
            if (_chronicleCacheOwner != owner || _chronicleCacheEvents != events ||
                _chronicleCacheAcquisitions != acquisitions ||
                !string.Equals(_chronicleCacheToken, refs, StringComparison.Ordinal))
            {
                _chronicleCache = LivingChronicle.VisibleToPlayer(owner);
                _chronicleCacheOwner = owner;
                _chronicleCacheEvents = events;
                _chronicleCacheAcquisitions = acquisitions;
                _chronicleCacheToken = refs;
            }
            return _chronicleCache;
        }

        private string ChronicleStatement(ChronicleEntry entry)
        {
            // The authored Fadenwerk text is a separately checksummed, terminal
            // dialogue; a missing or invalid published key must fail closed.
            var text = AuthoredHunger?.ChronicleTextFor(entry.TextKey);
            if (string.IsNullOrEmpty(text)) return "Chroniktext nicht freigegeben.";
            if (entry.Origin == ChronicleOrigin.Performed) return "Selbst erlebt: " + text;
            string origin = entry.KnowledgeOrigin == AshenCrypt.MemoryForge.KnowledgeOrigin.Reported ?
                "Nur berichtet von " : "Von einem Augenzeugen gehoert: ";
            return origin + entry.SourceNpcId + ": " + text;
        }

        private void DrawChroniclePage(ArenaHudLayout layout, Rect panel)
        {
            GUI.Box(panel, "ASCHENCHRONIK - NUR BELEGTE ERLEBNISSE");
            if (Application.isMobilePlatform) GUI.Box(ChronicleTabRect(layout), "SPUREN");
            else if (GUI.Button(ChronicleTabRect(layout), "SPUREN")) _chroniclePageOpen = false;
            var entries = CachedPlayerChronicle();
            int pages = Math.Max(1, (entries.Count + 3) / 4);
            _chroniclePage = Math.Min(_chroniclePage, pages - 1);
            if (entries.Count == 0)
                GUI.Label(new Rect(panel.x + 16f, panel.y + 65f, 524f, 75f),
                    "Noch keine selbst erlebte Tat und keine persoenlich gehoerte Aussage.", _smallStyle);
            for (int i = 0; i < 4; i++)
            {
                int index = entries.Count - 1 - _chroniclePage * 4 - i;
                if (index < 0) break;
                var entry = entries[index];
                GUI.Label(new Rect(panel.x + 16f, panel.y + 53f + i * 61f, 526f, 60f),
                    "Tick " + entry.Tick + " / #" + entry.EventId + ": " +
                    ChronicleStatement(entry), _smallStyle);
            }
            if (Application.isMobilePlatform)
            {
                GUI.Box(ChroniclePreviousRect(layout), "AELTER");
                GUI.Box(ChronicleNextRect(layout), "NEUER");
            }
            else
            {
                if (GUI.Button(ChroniclePreviousRect(layout), "AELTER"))
                    _chroniclePage = Math.Min(pages - 1, _chroniclePage + 1);
                if (GUI.Button(ChronicleNextRect(layout), "NEUER"))
                    _chroniclePage = Math.Max(0, _chroniclePage - 1);
            }
            GUI.Label(new Rect(panel.center.x - 65f, panel.yMax - 45f, 130f, 38f),
                "Seite " + (_chroniclePage + 1) + "/" + pages, _smallStyle);
        }

        /// <summary>Translate only the persisted branch, without inventing offscreen social knowledge.</summary>
        private static string HungerChoiceLabel(HungerChoice choice)
        { return choice == HungerChoice.Reveal ? "Offengelegt" : choice == HungerChoice.Protect ? "Ilyan geschuetzt" :
            choice == HungerChoice.Replace ? "Ersatzversorgung" : "Noch offen"; }

        /// <summary>Fit reviewed passive art into the existing projection volume without moving its Core-owned host.</summary>
        private static void FitHungerArt(Transform host, string key)
        {
            if (key == "MillHouse" || key == "MillWheel")
            {
                if (!PassiveArtVisuals.TryBindGroundedPrefab(host, BorderlandArt.LoadVisual(key), 0f)) return;
            }
            else if (!PassiveArtVisuals.TryBindGrounded(host, key, 0f)) return;
            Transform art = host.GetChild(host.childCount - 1);
            Renderer[] renderers = art.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Destroy(art.gameObject);
                host.GetComponent<Renderer>().enabled = true;
                return;
            }
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Vector3 targetSize = host.lossyScale;
            if (bounds.size.x <= 0f || bounds.size.y <= 0f || bounds.size.z <= 0f)
            {
                Destroy(art.gameObject);
                host.GetComponent<Renderer>().enabled = true;
                return;
            }
            art.localScale = Vector3.Scale(art.localScale, new Vector3(targetSize.x / bounds.size.x,
                targetSize.y / bounds.size.y, targetSize.z / bounds.size.z));
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            art.position += host.position - bounds.center;
        }

        private void ProjectRoadStone(Transform piece, Material slate)
        {
            if (slate == null) return;
            foreach (var renderer in piece.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled) continue;
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = slate;
                renderer.sharedMaterials = slots;
            }
        }

        /// <summary>Project two real returnable regions using existing passive materials and one Core mill footprint.</summary>
        private void BuildHungerScenery()
        {
            bool mill = _game.Progression.EntryAct == 4;
            RenderSettings.ambientLight = new Color(.38f, .39f, .4f);
            RenderSettings.fogColor = new Color(.24f, .26f, .27f);
            RenderSettings.fogDensity = .009f;
            ChapterShape(PrimitiveType.Cube, mill ? "OldMill_Ground" : "AshRoad_Ground", new Vector3(0, -.4f, 0),
                new Vector3(57, .6f, 57), new Color(.42f, .42f, .43f)); // ash snow, also without the private ground art
            var slate = Resources.Load<Material>("ARE/Borderland/RoughSlate");
            for (int z = -24; z <= 24; z += 4)
                ProjectRoadStone(ChapterShape(PrimitiveType.Cube, "AshRoad_Paving", new Vector3(0, -.08f, z),
                    new Vector3(5, .12f, 4), new Color(.4f, .38f, .34f), "AshStoneFloor"), slate);
            for (int side = -1; side <= 1; side += 2)
                for (int z = -24; z <= 24; z += 8)
                    ProjectRoadStone(ChapterShape(PrimitiveType.Cube, "AshRoad_Boundary", new Vector3(side * 28.5f, 1.2f, z),
                        new Vector3(1, 2.4f, 8), new Color(.26f, .28f, .27f), "AshStoneWall"), slate);
            _worldPassage = ChapterShape(PrimitiveType.Cylinder, "ARE_World_Passage",
                new Vector3(ArenaSimulation.PassageXmm * .001f, .03f, ArenaSimulation.PassageZmm * .001f),
                new Vector3(3.6f, .02f, 3.6f), mill ? new Color(.25f, .68f, .72f) : new Color(.82f, .56f, .28f));
            _returnPassage = ChapterShape(PrimitiveType.Cylinder, "ARE_Return_Passage", new Vector3(0, .03f, -24),
                new Vector3(3.6f, .02f, 3.6f), new Color(.58f, .62f, .69f));
            var roadside = RefugeScenery.Create(mill);
            _chapterScenery.Add(roadside);
            _refugeFlora = roadside.GetComponent<RefugeScenery>();
            if (!mill)
            {
                float bandageX = ArenaSimulation.AshRoadBandageXmm * .001f;
                float bandageZ = ArenaSimulation.AshRoadBandageZmm * .001f;
                ChapterShape(PrimitiveType.Cube, "AshRoad_Trail_Marker", new Vector3(bandageX + .05f, .8f, bandageZ),
                    new Vector3(.18f, 1.6f, .18f), new Color(.4f, .3f, .2f));
                ChapterShape(PrimitiveType.Cube, "AshRoad_Bandage", new Vector3(bandageX, AshRoadBandageHeight, bandageZ),
                    new Vector3(.75f, .25f, .08f), new Color(.8f, .75f, .64f));
                return;
            }
            ChapterShape(PrimitiveType.Cube, "OldMill_House", new Vector3(10.5f, 3f, 5),
                new Vector3(7, 6, 11), new Color(.42f, .37f, .29f), "MillHouse");
            // Wheel lies inside the same solid footprint, so art never invents a collision boundary.
            ChapterShape(PrimitiveType.Cube, "OldMill_Wheel", new Vector3(7.53f, 1.90f, 7),
                new Vector3(1.02f, 3.80f, 3.80f), new Color(.22f, .17f, .13f), "MillWheel");
            ChapterShape(PrimitiveType.Cube, "OldMill_Medical_Supplies", new Vector3(-4, .3f, -2),
                new Vector3(1.2f, .6f, .8f), new Color(.54f, .43f, .3f), "AshCrate");
            for (int i = 0; i < 3; i++)
            {
                ChapterShape(PrimitiveType.Cube, "OldMill_Bed_" + i, new Vector3(-4 - i * 2.5f, .15f, 5),
                    new Vector3(1.5f, .3f, 2.4f), new Color(.55f, .52f, .44f));
                if (i >= 3 - _game.HungerShelterPatients)
                {
                    Transform patient = ChapterShape(PrimitiveType.Capsule, "OldMill_Patient_" + i,
                        new Vector3(-4 - i * 2.5f, .32f, 5), Vector3.one, new Color(.46f, .4f, .35f));
                    if (!PassiveArtVisuals.TryBindGrounded(patient, "MillPatient" + i, .32f))
                    {
                        patient.position += Vector3.up * .26f;
                        patient.localScale = new Vector3(.55f, .85f, .55f);
                        patient.rotation = Quaternion.Euler(90, 0, 0);
                    }
                }
            }
            ChapterShape(PrimitiveType.Cube, "OldMill_Torn_Bandage", new Vector3(-4, .32f, 5),
                new Vector3(.7f, .02f, .2f), new Color(.82f, .78f, .65f));
        }
    }
}
