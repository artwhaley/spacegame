# P05 Sprint B1.5 Corrective Report

Starting HEAD: `7111e29b750d2403579101f66c7a33372271b3d9` (`main`)

## B1.5-T00 — Baseline and invariants

No gameplay behavior changed in T00.

### Baseline findings

- **Named-character coupling:** Runtime freight dispatch contains no Alice/Dana display-name check. The fixture authoring does contain one unwanted dependency: `P4bModularSceneAuthoring` reads Alice's configured shift to seed Dana's Porter assignment. Fixture code names are acceptable; borrowing one character's schedule for another is not. B1 authoring also assigns Charlie by name for its Pilot fixture; actual Pilot execution remains outside this corrective sprint and is deferred to B2 per the project scope.
- **Colonist freight-component coupling:** `WalkingFreightCarrierComponent` is required on colonists, registers a global `Active` list, owns capacity/job state and references the Brain, Inventory, PersonnelRouteRunner, ActivityRunner, and `WalkingFreightRunner`. The shared colonist prefab contains both freight components. `FreightLogisticsManager` and `SupplyChainDebugLog` enumerate that component list.
- **Brain freight coupling:** `ColonistBrain` has `workExcursionActive`, `workExcursionHasCargo`, and `workExcursionWorkplace`; exposes excursion readiness/abort and begin/cargo/complete methods; and checks `WalkingFreightCarrierComponent.HasCargo` in `TickWorking` for MobileDuty.
- **Single-carrier allocation/route coupling:** `FreightAllocation` stores one `Carrier`, and `LogisticsRoutePlan` stores that same carrier. `FreightDeliveryJob` combines allocation data with the current walking worker/job lifecycle.
- **Worker positioning encoded as cargo movement:** candidate creation estimates worker-current-position → source and source → destination. `AcceptCandidate` encodes both estimates as `WalkingCarrier` legs, so the first leg moves an empty worker and is incorrectly represented as freight movement.
- **Existing routing ownership:** `WalkingFreightRunner` already sends pickup/dropoff movement through `PersonnelRouteRunner`; it still owns execution phases and reads freight state from the colonist carrier/Brain.

### Regression guard rationale

Added compact API-boundary checks for allocation/route worker ownership, Brain freight-excursion API, and the legacy colonist freight component types. These are stable architectural constraints explicitly required for B2; they do not inspect scene YAML, private fields, or physical navigation. They are expected to fail against the pre-B1.5 baseline and are not run in this execution. Assignment interchangeability, NavMesh travel, emergency activity choreography, and inventory custody remain human Play Mode acceptance, following `TESTING_IN_EXPLORATION_MODE.md`.

### Ticket status

| Ticket | Status | Evidence |
|---|---|---|
| B1.5-T00 | Complete | Baseline documented; architecture boundary tests added before the refactor. |
| B1.5-T01 | Complete | Allocation and cargo-route data no longer contain a worker; the current walking binding and empty positioning estimate live in a separate execution record. |

No Unity compile, Test Runner, or Play Mode run has been performed. The user performs the human Unity acceptance run.

## B1.5-T01 — Cargo allocation and route

- `FreightAllocation` now owns order, resource, quantity, source, final destination, and cargo route only.
- `LogisticsRoutePlan` contains stock-to-stock cargo legs and has no worker field. A current local allocation contains one `WalkingCarrier` leg from its source stock to the requesting stock.
- The former worker-current-position → source estimate remains part of candidate service cost. It is stored on the separate `WalkingFreightExecution` record and remains included in candidate ranking; it is no longer a freight cargo leg.
- The source → destination estimate is the loaded distance on the single route leg. Job diagnostics now distinguish loaded cargo distance from worker positioning distance.
- The existing `FreightCandidateRankingTests` still covers quantity-first, distance-second, and stable tie-break ranking. The T00 public boundary checks now match the route/allocation API. Neither suite was run.
- Source inspection confirms no remaining `job.Carrier`, `FreightAllocation.Carrier`, `LogisticsRoutePlan.Carrier`, or `TotalEstimatedDistance` reference in P4b Logistics. These are source checks, not a Unity acceptance claim.
