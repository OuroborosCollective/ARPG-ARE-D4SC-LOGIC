# Integration and API

## Ownership

Your host owns actor identity, health, hunger, resources, positions, perception, navigation, quests, combat and the simulation tick. `MemoryArchive` belongs to that host and is embedded in its existing save. `NpcMind` stores individual knowledge, relationships, its immutable controller binding and decision continuity. Share one registry per host session. Do not create a second autonomous `Update()` loop for every NPC.

The sample's `DemoWorld` is a small independent host illustrating this boundary. It is not a game engine or an Ashen Crypt save adapter.

## Spawn, observe and choose

```csharp
using AshenCrypt.MemoryForge;

var registry = ForgeDefaults.CreateRegistry(); // Or a ForgeRegistryAsset.Build().
var archive = new MemoryArchive();
var npc = archive.Spawn("village.watch.01", "guard", registry);

// Construct this mask from actual world rules BEFORE utility scoring.
var observation = new ObservationFrame(
    ActionSet.Of(ForgeAction.Wait, ForgeAction.Flee),
    new[] { new Observation(Feature.Danger, 8000) });
ulong hostTick = 1;
var decision = UtilityEvaluator.Decide(
    npc.Controller, observation, npc.DecisionState, hostTick);
// Map decision.Action to an existing, validated host command.
// An attack is impossible here because the legal mask excludes it.
```

Supply nondecreasing ticks from your canonical clock. The supplied defaults were integrated with 100-ms ticks; interval values are tick counts, not seconds. `eventChanged: true` requests an early reevaluation; hard permissions, minimum duration and hysteresis still apply. An empty legal mask returns `None`; the host must safely do nothing. An unknown archetype binds the admitted fallback controller.

Stable identifiers use lowercase ASCII letters, digits, `.`, `_`, `-` or `:`, with 1–128 characters. Generate IDs in your host and reuse them on load. Do not derive persistent IDs from `GetHashCode()` or Unity instance IDs.

## Record a real event

```csharp
// Call only AFTER the host successfully transfers the actual ration.
var helped = archive.Append(MemoryKind.Help, "player", npc.Id,
    hostTick, 5000, false, "provision",
    new RelationshipChange(trust: 1200, gratitude: 2000));
archive.Learn(npc.Id, helped.Id, KnowledgeOrigin.Self, npc.Id, hostTick);
```

Retain the returned event ID in the canonical event/quest receipt. Replaying the same ID cannot apply the same social change twice. Appending a NEW ID represents a different event and can legitimately apply another change; this API cannot infer that two host commands were accidental duplicates.

`Learn(...Witnessed...)` requires the host to have checked real distance, visibility or other authorized perception. The library cannot inspect your scene. `Self` also verifies that the NPC is a participant. Keep a successful event in the archive without teaching it to uninformed NPCs.

For a real conversation, `archive.Report(sourceNpc, recipientNpc, eventId, tick)` verifies that the source has direct knowledge. Reported knowledge remains unconfirmed. The current contract permits one-hop reports from direct sources, not arbitrary rumor chains. Suspicions and refutations are distinct origins. Refuting an unconfirmed memory removes its social contribution without erasing the event history; a direct fact cannot be refuted through this shortcut.

`MemoryKind.SemanticClaim` can represent a host-defined claim about a route, merchant or location. Its `Context` is a stable host-owned semantic key, not a natural-language proposition parser. Keep evidence and source validation in your host. No NPC learns global facts automatically.

## Relationships and recall

Eight dimensions are supported: trust, fear, gratitude, affection, respect, mistrust, hostility and familiarity. Signed dimensions are clamped to −10,000…10,000; the others to 0…10,000. Raw contributions are retained so rumor retraction cannot erase another event's contribution.

`Known` exposes current individual knowledge, including origins. `Relevant(tick, context)` returns relevant essential memories plus up to 64 recently acquired ordinary memories in stable relevance/ID order. Ordinary relevance fades over 36,000 ticks; essential events do not expire. Relevance uses importance, personal impact, credibility, social closeness, context and tick retention. Call `KnownMemory.Relevance` with a host-derived repetition count when needed. Forgetting does not delete history or silently reset a relationship.

## Projection

Attach `ForgeNpcView` to an existing NPC and call `Bind(npc)` after spawning or loading. Feed positions and animations from your existing actor controller. The view never owns a save, decides actions or awards XP. Your host must provide reachable destinations, animation triggers, navigation and action effects.

Companion XP, equipment, death and recruitment remain host contracts. Ashen Crypt supplies that integration in its game repository; the commercial toolkit supplies reusable behavior and memory contracts and the independent village sample, not the ARPG's inventory/combat code or licensed character art.
