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
| B1.5-T02 | Complete | Added a generic owner/lease/release contract and narrow deferred-release owner tests. |
| B1.5-T03 | Complete | Logistics now quotes and accepts workplace services; service-owned worker discovery and route cost calculation replace manager carrier enumeration. The service execution still delegates to legacy runner plumbing until T04/T05. |
| B1.5-T04 | Complete | Routine Porter jobs now acquire an Airlock service lease and execute pickup, delivery, and return-to-duty routes through PersonnelRouting. Emergency execution remains on the temporary legacy path until T05. |
| B1.5-T05 | Complete | Emergency Cafeteria pickup now uses the same workplace lease and service route execution; Brain freight/cargo state and its carrier lookup are removed. |

No Unity compile, Test Runner, or Play Mode run has been performed. The user performs the human Unity acceptance run.

## B1.5-T01 — Cargo allocation and route

- `FreightAllocation` now owns order, resource, quantity, source, final destination, and cargo route only.
- `LogisticsRoutePlan` contains stock-to-stock cargo legs and has no worker field. A current local allocation contains one `WalkingCarrier` leg from its source stock to the requesting stock.
- The former worker-current-position → source estimate remains part of candidate service cost. It is stored on the separate `WalkingFreightExecution` record and remains included in candidate ranking; it is no longer a freight cargo leg.
- The source → destination estimate is the loaded distance on the single route leg. Job diagnostics now distinguish loaded cargo distance from worker positioning distance.
- The existing `FreightCandidateRankingTests` still covers quantity-first, distance-second, and stable tie-break ranking. The T00 public boundary checks now match the route/allocation API. Neither suite was run.
- Source inspection confirms no remaining `job.Carrier`, `FreightAllocation.Carrier`, `LogisticsRoutePlan.Carrier`, or `TotalEstimatedDistance` reference in P4b Logistics. These are source checks, not a Unity acceptance claim.

## B1.5-T02 — Generic work-execution ownership

- Added `IWorkExecutionOwner`, `WorkExecutionLease`, `WorkReleaseReason`, and `WorkReleaseDisposition`. The lease records only the worker, workplace, owner, and generic release request; it does not encode cargo or vehicle state.
- `ColonistBrain` can grant a lease only while Working, on an active assignment to that workplace, without a pending stop or another active lease. While a lease is active, `TickWorking` remains Working and does not treat stopped facility choreography as the end of work.
- Shift-end and critical-need stops now request generic owner release. A `Deferred` reply leaves the lease active; the Brain does not stop the service's route or finish the work lifecycle until the owner releases the lease.
- Added two owner-level tests for immediate and deferred lease release. These test the durable generic release contract; they were not run. Brain/scene interactions remain Unity human acceptance.
- **B2 locked rule:** Pilot work must defer release until its assigned Shuttle is physically docked at that Shuttle's home Shuttle Base. This sprint adds only the generic release seam and records the rule; no Shuttle, Pilot executor, or Charlie behavior was added or changed here.

## B1.5-T03 — Workplace freight providers

- Added `WalkingFreightWorkService`, `FreightWorkQuote`, and a provider-owned `WalkingFreightExecution` binding. Services register through `OnEnable`/`OnDisable`; the logistics manager iterates active providers and stock sources, then compares quote quantity, service cost, and a stable provider/source key.
- Routine Porter eligibility, emergency Cafeteria eligibility, worker capacity, minimum remaining emergency staffing, and both PersonnelRouting estimates are evaluated by the workplace service. The quote exposes the provider and cost data; its selected worker is internal to the service.
- P4b fixture authoring configures the Airlock routine service (Porter, capacity 10) and Cafeteria emergency service (capacity 5, minimum remaining staff 0). B1/P4b validation now checks the provider setup.
- The legacy carrier/runner remains only as a temporary execution adapter for T03. T04/T05 move route execution and release decisions into the provider; T06 removes those old colonist components and updates the shared prefab.
- `SupplyChainDebugLog` reports active workplace services and job provider instead of enumerating colonist freight components. Added API-boundary checks for the provider-facing logistics seam. No Unity compile, Test Runner, or Play Mode run has been performed.

## B1.5-T04 — Airlock-owned routine Porter execution

- Routine job acceptance now acquires a `WorkExecutionLease` from the Airlock `WalkingFreightWorkService`. The Brain remains Working while the service owns that lease.
- The service execution drives PersonnelRouting to the source, transfers reserved stock into the worker's generic Inventory, routes to the requesting stock, and then returns an on-duty Porter to the Airlock duty anchor.
- A shift-end or critical-need release before pickup cancels the job, releases its source reservation, and releases the lease. After pickup, the service defers release until delivery; it skips the optional return route when the Brain has requested release.
- Worker identity remains inside the quote/service execution. The provider records which assigned worker it selected so the work log retains attribution without making worker selection a Logistics concern.
- Identity-swap acceptance (Bob assigned Porter, Dana Farmer) remains a human fixture/Play Mode check. No prefab change was made for T04. No Unity compile, Test Runner, or Play Mode run has been performed.

## B1.5-T05 — Cafeteria emergency service and Brain cleanup

- Emergency quote acceptance now acquires a generic lease from the Cafeteria service, stops the active ordinary facility activity, waits for that activity request to exit, and uses the service execution for PersonnelRouting, pickup, delivery, and retry.
- After delivery or a pre-pickup cancellation, the service resumes the assigned facility activity only while that shift is still active and the Brain has not requested release. A release request before pickup cancels and releases the source reservation; after pickup it remains deferred through delivery.
- Removed all work-excursion fields, properties, and methods from `ColonistBrain`, including the mobile-duty `WalkingFreightCarrierComponent.HasCargo` check. Routine and emergency work now share the generic lease behavior.
- Removed the old carrier's Brain excursion methods and made its obsolete runner reject emergency jobs; the service no longer references either legacy colonist freight type. T06 will remove their scripts and prefab composition.
- Added a reflection boundary check for the removed Brain cargo field. Interchangeability, activity resume, and physical cargo custody remain human acceptance checks; no Unity compile, Test Runner, or Play Mode run has been performed.
