# C03 — Give recoverable shuttle handoffs a legal retry path

Priority: P1. Corrects T05/T06. Status: implemented in source; Unity acceptance pending.

## Reproduction A: passenger arrival stops the entire trip

Arrive with a passenger and freight. Temporarily make the passenger's destination NavMesh sample/warp unavailable, then restore it.

`CompleteTripAtDestination` disembarks passengers before processing any payload acknowledgments. Failure sets the trip to Blocked and returns (service lines 491–505). `SimulationTickTransport` returns immediately for Blocked trips (247–249); only UnloadingOrDisembarking retries (251–255). Restoring the physical condition never reaches the retry. Freight remains unresolved behind the passenger, and there is no normal recovery command for this transition.

## Reproduction B: staged freight source retry is rejected

Stage a child at an intermediate dock, assign its shuttle, and temporarily reduce shuttle free cargo room before loading. Restore room after the failed pickup.

`PickupAtCurrentLeg` finds the load no longer fits (manager lines 750–758). Because the shipment already passed a leg, `CancelBeforePickup` retains custody and calls `BlockJob` (1146–1149). The shuttle keeps trying `TryLoad`, but `ReportExecution(ProviderAtSource)` permits non-personnel pickup only from Assigned (491–499). Blocked is never accepted, regardless of its retry tick. The cargo is ready and fits again, yet cannot load.

Primary sources: [ShuttleServiceComponent.cs](../../Assets/Scripts/ColonyPrototype/Vehicles/ShuttleServiceComponent.cs), [ShuttleFreightExecution.cs](../../Assets/Scripts/ColonyPrototype/Logistics/P4b/ShuttleFreightExecution.cs), [FreightLogisticsManager.cs](../../Assets/Scripts/ColonyPrototype/Logistics/P4b/FreightLogisticsManager.cs).

## Correction

- Classify a temporary disembark failure as an unloading wait with a visible reason and bounded retry. Preserve actor/ship responsibility until disembark actually succeeds.
- Process independent payload acknowledgments without losing progress behind one failed passenger. Successfully accepted freight/passengers remain accepted exactly once; the ship cannot take another trip while unresolved manifest work remains.
- Allow the current correlated non-personnel execution to retry a retryable, unpicked staged source handshake after its retry tick. Retain all identity/leg/custody checks and reject stale or already-loaded reports.
- Permanent invalid payload/owner failures remain explicit recovery holds. Do not use disable/re-enable as the recovery protocol, auto-complete missing payloads, or release reserved dock cargo.

## Acceptance

Use a narrow freight-owner regression for staged pickup: fail on temporary carrier capacity, restore it, advance past retry time, and accept one load. Assert no transfer before readiness, one transfer afterward, unchanged parent commitment, and rejection of stale/duplicate reports.

Use an owner-level arrival-result case only if it can stay independent of NavMesh. In Unity, temporarily block one passenger's disembark and then restore it. Confirm visible waiting, successful unrelated payload acceptance, eventual passenger completion once, and trip/pilot retention until all obligations resolve. Repeat the existing full-destination freight wait to ensure it still retries. No fake NavMesh or scene-text tests.
