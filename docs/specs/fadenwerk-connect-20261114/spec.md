# Fadenwerk: owner browser and first canonical story binding

Owner request: inspect/adapt Fadenwerk and make its HTML interface usable from a tablet through arelogic.space. Existing assets suffice; no additional prop imports.

## Acceptance

- Protected HTTPS at `/fadenwerk/`, native browser text input, relative API/image URLs; localhost-only Unity listener. Existing owner access, no dependency on Aurion gameplay or databases.
- Test authentication boundary, same-origin JSON writes, body limits, unavailable editor and actual browser write/readback. No credentials in Git or evidence.
- Authoring changes must not silently overwrite a newer editor/browser revision.
- First playable binding: existing **Hunger hinter den Mauern**, Ilyan/Mara/mill. Fadenwerk supplies validated presentation text; existing Core actions remain responsible for proximity, costs, choice, memory, progression and AC saves. No FadenwerkManager or parallel reward/save state in production.
- An explicit publish step validates the fixed quest/action structure and writes a build-included text asset. Unbound/custom quests remain authoring drafts, never silently become gameplay. New reward rules and story branches require their own reviewed integration.
- Actual Core and native Unity tests cover rejected remote/duplicate actions and save/load. Browser publication and game readback must be revision-bound; editor tests are not Android evidence.

## Delivery order

1. Browser gateway and lost-update protection with negative tests.
2. Canonical hunger binding, authoring setup, publish/readback controls.
3. Native Unity and browser checks; append one Memory entry and preserve source/evidence hashes.

No changes to existing release scope, story choices, procedural layout, private asset originals, Fable HUD layout or camera.
