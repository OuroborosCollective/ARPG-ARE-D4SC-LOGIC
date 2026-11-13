# Quick start

## Import and play

1. Import the `.unitypackage` into a Unity 6 project. Keep the entire `Assets/AshenCrypt/MemoryForge` folder, including `.meta` files.
2. Open **Tools → Memory Forge → Open remembering-village example**. This asks Unity to preserve any unsaved scene before opening the example.
3. Press Play. The example works with mouse or touch buttons and the built-in render pipeline. Its material selection also recognizes URP when installed; verify your own pipeline and lighting.
4. Choose **Approach Nara**, wait until the player reaches her, then **Give food**. A ration is consumed, Nara gains a personal memory, trust and gratitude change, and her next greeting mentions the help. Decision changes respect the controller's minimum duration and decision interval.
5. **Leave**, return and **Greet**. Choose **Save**, **Reset scenario**, then **Load**. The loaded state contains the exact historical controller binding, resources, personal memories and decision continuity.
6. Reset. Approach Nara and take the purse while the watch is occluded. Inspect the watch's personal memory: it does not know about the theft. Reset again, approach Nara, move the watch into view, then take the purse. Its witnessed memory now causes pursuit and real damage in the sample host.

The sample stores only `AshenCrypt.MemoryForge.Sample.v1` in PlayerPrefs. Reset changes the live scenario and preserves that saved snapshot. To remove the sample save, delete only that key in your own cleanup code.

## Inspect a live NPC

Select `Nara` or `Watch` in the hierarchy during Play. `ForgeNpcView` displays the mind supplied by the sample host. The Inspector shows its exact controller hash, legal actions, weighted scores, origins of memories and relationships. It does not create or mutate gameplay state.

## Configure a controller

The included `Samples/RememberingVillage/Controllers/DefaultRegistry.asset` contains nine role templates and a safe fallback. If these assets have not yet been generated, use **Tools → Memory Forge → Create example controller assets**. Existing assets are preserved.

Select a controller asset. Run its regression cases, review the results, then seal the candidate. Approved fields cannot be silently resealed after a change. Use **Fork next version** for an explicit new candidate; keep the old asset for existing saves. Registry entries explicitly select which version new NPCs receive.

The default example uses the code-defined, reviewed registry so it remains reproducible. To use your authored registry in your game, call `ForgeRegistryAsset.Build()` once and pass the resulting registry to your own host. Changing an asset does not silently change the running example or existing NPCs.

## Run the tests

Install Unity Test Framework through Package Manager if it is not already present. Open **Window → General → Test Runner** and run the Memory Forge EditMode and PlayMode tests. Tests use the same production evaluator and persistence code. They do not substitute mock gameplay for the sample.
