# Changelog

## 1.1.0 – 2026-11-19

- New: project extensions via `FadenwerkAuthoringBridge`. A project can serve its own browser page at `/?<key>`
  (`ExtensionPageKey`, `ExtensionPage`) and its own API calls below `/api/x/…` (`Extension`, returns a
  `FadenwerkExtensionReply`). They use the same server, access token and proxy as the Fadenwerk interface and run on
  the Unity main thread, also in Play Mode. Throw `InvalidOperationException` for a user-facing refusal (HTTP 422).
- New: `FadenwerkAuthoringBridge.ServerRunning` and `StartServer()`.
- Tests: `FadenwerkExtensionTests` (dispatch, refusal, missing handler, exact page key).
- Fix: the sample authoring scene (*Fadenwerk → Beispiel*) also works when Fadenwerk is installed from the Asset
  Store under `Assets/Fadenwerk`; the template is resolved by GUID instead of a fixed `Packages/…` path
  (`FadenwerkSample.AuthoringTemplatePath()`). The matching package test no longer assumes the UPM location.
- Unchanged: data format, runtime components, existing browser interface and API.

## 1.0.0 – 2026-11-15

- First release: quests, dialogues and reward kits; Unity window, browser interface (also for Unity on a VPS) and
  offline runtime.
