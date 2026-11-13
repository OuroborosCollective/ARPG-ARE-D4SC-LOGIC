# Controllers and local neural policies

## Deterministic utility

Observations are integers in 0…10,000. Each utility term is `round(weight * observation / 10000)` with exact halves rounded away from zero. Multiplication uses a checked 64-bit intermediate and scores use checked integer sums. Weights are bounded to ±40,000; base priorities to ±10,000. Utility totals can exceed the normalized observation range.

The host's legal-action mask is intersected with the archetype whitelist before scoring. A high score cannot authorize an action. Unique rule order is part of the hashed artifact and breaks ties. Cooldowns, minimum duration and hysteresis prevent needless switching; forbidden current actions are not retained.

Available inputs are hunger, fatigue, injury, danger, food, trust, fear, gratitude, hostility, quest relevance, travel cost, resource cost, ally distance and target injury. Missing observations are zero. Supply only facts your host actually owns. Defaults are starting policies with concrete regression receipts, not a promise of appropriate balance for every game.

## Approval and versioning

A `ControllerArtifact` includes the archetype manifest, ordered utility rules, timing, runtime/memory versions and optional neural network. Its SHA-256 hash covers all of these. `ControllerAdmission.Run` executes real cases and records the resulting trace hash. Registering requires the expected artifact hash and a nonempty passing receipt. A hash provides integrity and version identity, not a publisher signature or an anti-cheat trust boundary.

In the Editor, review and seal a candidate after running cases. The Inspector offers a version fork; it cannot silently approve edited bytes under an old seal. Keep every historical artifact needed by your saves in the registry. New-spawn selection is explicit. Existing minds retain their binding.

Use `MemoryArchive.TryMigrate` only as an explicit host command after testing the target. It requires the same archetype capabilities and an admitted exact target, rejects backwards ticks, records source/target/regression hashes and persists migration history. Loading never chooses the newest version to conceal a missing historical artifact.

The supplied templates cover fallback, villager, merchant, guard, healer, traveler, companion, monster and animal. They expose only the generic supported action vocabulary. For example, the healer template does not grant a treatment effect in a host that has no validated healing action. A custom manifest may enable `Treatment`/`Treat` once your host provides that action. Changing an existing archetype's capability contract is not a weight-only migration; define and migrate the host contract explicitly.

## Integer neural controllers

`QuantizedNetwork` implements a local 14-input, 1–16-hidden-unit, 10-action feed-forward network. Hidden activations are clipped to 0…10,000; outputs to ±10,000. Integer weights, biases, matrix layout and arithmetic are included in the immutable artifact. A network contributes a bounded delta to each legal utility score. It cannot add an action, capability, observation, gameplay effect or simulation clock.

The Inspector exposes candidate matrices and regression cases. Research, fit or repair weights offline, create a NEW artifact version, run held-out cases and hard-permission tests, then review and admit it. There is no model download, cloud inference, runtime training, backpropagation or automatic weight mutation in this package.

The project's guard experiment rejected a candidate that fit 12 training cases but failed broader evaluation. A corrected neural candidate and a simple utility correction both passed 1,722 finite regression cases; the utility path was faster in the recorded managed benchmark. The game therefore retains utility defaults. These finite tests are not a mathematical proof of every possible input or evidence that neural policies outperform utility rules.

## Explain a decision

Use `Decision.Priorities` for legal flags and scores, `Contributions` for actual values × weights, and `CanonicalTrace` for a stable textual trace. Diagnostic text is generated on demand to avoid formatting allocations in ordinary decision calls. The NPC Inspector is read-only; changing Inspector selection does not alter the simulation.
