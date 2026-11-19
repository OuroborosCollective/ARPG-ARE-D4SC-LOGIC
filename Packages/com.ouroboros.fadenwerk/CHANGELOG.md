# Changelog

## 1.1.0 – 2026-11-19

- New: project extensions via `FadenwerkAuthoringBridge`. A project can serve its own browser page at `/?<key>`
  (`ExtensionPageKey`, `ExtensionPage`) and its own API calls below `/api/x/…` (`Extension`, returns a
  `FadenwerkExtensionReply`). They use the same server, access token and proxy as the Fadenwerk interface and run on
  the Unity main thread, also in Play Mode. Throw `InvalidOperationException` for a user-facing refusal (HTTP 422).
- New: `FadenwerkAuthoringBridge.ServerRunning` and `StartServer()`.
- Tests: `FadenwerkExtensionTests` (dispatch, refusal, missing handler, exact page key).
- Unchanged: data format, runtime components, existing browser interface and API.

## 1.0.0 – 2026-11-15

- First release: quests, dialogues and reward kits; Unity window, browser interface (also for Unity on a VPS) and
  offline runtime.
