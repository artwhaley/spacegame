# P05 Sprint B1.6 Corrective Report

## Execution Baseline

- Repository: `https://github.com/artwhaley/spacegame.git`
- Starting `HEAD`: `5d680ea58b4c2865373d96ac422015cf088d4454`
- B1.5 is present at the starting revision, including `Assets/Tests/EditMode/B1_5ArchitectureBoundaryTests.cs`.
- The working tree contained unrelated deletions under `unrealstars/StarSphere/` and untracked UI specification files before B1.6 work began. They are preserved and excluded from B1.6 commits.
- Play Mode and scene acceptance remain for the user to run in Unity.

## B1.6 Starting Couplings

### Activity completion coupling

`IActivityApproachRouter` only starts or stops a route and has no correlation identity or completion/failure event. `ColonistActivityRunner` subscribes to `ColonistMotor.Arrived` and `HandleArrived` starts activity entry from that motor-level event. `PersonnelRouteRunner` also consumes `motor.Arrived` to advance its legs and emits `RouteCompleted(PersonnelRoutePlan)` only after the full plan finishes, but the activity runner does not use that whole-route event.

### Freight executor coupling

`FreightDeliveryJob` stores a concrete `WalkingFreightExecution`. `FreightLogisticsManager` reads walking-specific service, distance, and cargo inventory data and exposes raw job-state mutators plus pickup, delivery, block, and resume operations. `WalkingFreightExecution` calls those operations directly. The freight route model already stores ordered loaded-cargo legs, but jobs have no current-leg index or generic active execution.

### Inventory authority coupling

`InventoryEntry.reserved` is serialized and is updated alongside nonserialized legacy-reservation and owned-token ledgers. Initialization and validation clamp the serialized aggregate but do not rebuild it from surviving runtime reservation owners, so an orphan reserved amount can remain after those owners are lost.

### Legacy boundary

Legacy transport types remain in the shared runtime assembly and existing source tree. The B1 and P4b modern fixture validators do not currently reject active legacy transport authorities. `GLOSSARY.md` does not yet mark `TransportContract` as a legacy term.

## B1.6-T00 — Freeze Current Behavior and Record Couplings

Source audit completed at the starting revision. Existing EditMode coverage includes inventory, freight candidate ranking, personnel route planning, and the B1.5 architecture boundary. This report records the baseline before production changes.

## B1.6-T01 — Activity Approach Completion

`IActivityApproachRouter` now starts a route with a correlation ID, reports whole-route completion/failure with that ID, and stops only the matching route. `PersonnelRouteRunner` creates unique runtime route IDs and translates its complete-plan and failure outcomes through the package-owned seam. `ColonistActivityRunner` now starts entry only after receiving completion for its expected route ID; it ignores stale completion/failure and no longer subscribes to motor arrival or movement-failure events for approach lifecycle.

Added `ActivityApproachCorrelationTests` to cover intermediate motor arrivals, stale route outcomes, matching whole-route completion, and matching failure. The test uses a fake route and deliberately has no baked NavMesh; physical entry remains a Unity Play Mode check.

## B1.6-T02 — Inventory Reservation Authority

`InventoryEntry.reserved` and its cached availability are now nonserialized. `InventoryComponent` derives the aggregate from the nonserialized legacy reservation ledger and owned reservation tokens on enable and before reservation-sensitive reads or writes. After stock is removed beneath reservations, the legacy aggregate is reconciled first and owned tokens are trimmed newest-first, preserving deterministic ownership for earlier allocations. Inactive tokens are removed from the live ledger.

Added focused inventory tests for mixed legacy/owned double-allocation prevention, aggregate derivation from owners, stale reservation removal on reinitialization, and deterministic trimming after stock removal. These protect the inventory ownership invariant and the prior stale-reservation failure mode; they do not test scene composition or physical behavior.

Verification note: the focused EditMode tests were not executed in this turn. `dotnet test ColonyPrototype.Tests.csproj --no-restore --filter FullyQualifiedName~InventoryComponentTests` could not start because the sandbox denied access to `C:\Users\artwh\AppData\Local\Microsoft SDKs`. Unity Editor processes are already running, so I did not launch a second editor in batch mode. Manual Play Mode acceptance remains with the user.

## B1.6-T03 — Generic Freight Leg Execution

`FreightDeliveryJob` now stores an `IFreightLegExecution`, `CurrentLegIndex`, and derived `CurrentLeg`; it no longer exposes a walking-typed execution field. The job's cargo inventory, emergency classification, provider context, and completion callback now come through that seam. The current walking execution implements the contract, while candidate discovery and assignment remain owned by `WalkingFreightWorkService`.

Split `FreightWorkQuote`, `WalkingFreightWorkService`, and `WalkingFreightExecution` into separate source files, preserving the existing Unity script metadata for the `WalkingFreightWorkService` component. The supply debug snapshot reads the generic provider context. Added an architecture assertion that jobs depend on the generic execution contract and identify their current route leg.

## B1.6-T04 — Logistics-Owned State and Cargo Custody

`FreightDeliveryJob` now exposes read-only state/custody data and accepts mutations only through manager-held authority. Public raw manager mutators were replaced with one internal semantic execution report entrypoint. It validates that the reported execution is active for this job's current leg and permits only legal report/state combinations. The walking execution reports route start, source arrival, loaded arrival, failure, and release requests; it no longer changes freight state or calls raw pickup/delivery/block/resume commands.

Logistics now owns pickup, delivery, staging, order accounting, cancellation, retry, and completion. Each pickup atomically transfers its source reservation into an executor-owned cargo reservation. A completed intermediate leg transfers cargo into the next origin inventory under a new reservation, advances the leg, and marks the job `Assigned` with `IsAwaitingLegAssignment` true. No shuttle execution or next-leg provider assignment is implemented here. Failure control uses `FreightFailureReason` enums; strings remain diagnostic fields only. Route plans now require adjacent legs to meet at the same stock point.

Added architecture checks for private job state mutation and removal of public raw lifecycle mutators, plus an inventory invariant test for atomic owned transfer into destination-reserved staged cargo. Physical navigation and staged-leg scene acceptance remain for Unity Play Mode.

Verification note: `git diff --check` passed. The new EditMode cases and Unity compile were not executed; the test-runner limitation recorded under T02 still applies. No Play Mode run was attempted.

## B1.6-T05 — Correlation Identity and Deterministic Infrastructure

Walking freight now reports whole-personnel-route outcomes through the route runner's correlation events. It records both the plan `StableId` and runtime route token and ignores callbacks whose token does not match. Each leg execution has its own runtime `ExecutionId`; Logistics validates allocation ID, current leg index, execution ID, and active route identity before applying a report. `FreightDeliveryJob` no longer exposes an alias job ID equal to the allocation ID, and freight logs use allocation, leg, execution, and route identifiers without duplicating `jobId`.

`PersonnelRouteIdentity`, `LogisticsStockComponent`, and `WalkingFreightWorkService` now share `SceneStableIdentity`, which preserves the existing scene/hierarchy key shape and formats sibling indices invariantly. Named logistics priorities preserve 300/310/320 order. Demand and allocation IDs use generic prefixes with invariant padded counters.

Added EditMode coverage for shared key formatting, priority ordering, report identity shape, route-token correlation, and generic ID naming. `git diff --check` passes. The existing focused test attempt still cannot reach compilation because the sandbox blocks the .NET SDK resolver's per-user SDK lookup; no Unity Play Mode run was attempted.

## B1.6-T06 — Visible Legacy Transport Boundary

Moved the ten pre-P4b transport scripts and their `.meta` sidecars under `Assets/Scripts/ColonyPrototype/LegacyTransport/`. Their GUIDs are unchanged, and their assembly and namespace remain unchanged. `StaffingManager` and `ShipCrewDutyComponent` retain their existing passenger-transport compatibility references because they also own staffing and pilot-duty behavior; those files were not moved or rewritten.

Added `docs/LEGACY_TRANSPORT_BOUNDARY.md`, marked `TransportContract` as a legacy glossary term, and defined the modern B1.6 freight/work terms. A shared editor guard now rejects active `LogisticsManager`, `ContractManager`, and `TransportExecutorComponent` authorities in B1 and P4b fixtures; a future B2 fixture validator is directed to call the same guard. Added source-boundary and fixture-guard tests. No assembly or namespace surgery was introduced.

Verification note: the old/new `.meta` GUIDs match for all ten moved scripts, and `git diff --check` passes. Automated tests and Unity compilation remain unexecuted because of the SDK/editor limitation above. The guard does not rewrite or disable legacy scenes; it reports active legacy authorities when validating modern fixtures.

## B1.6-T07 — B1.6 Regression and B2 Readiness Gate

Added a structural freight test for `WalkingCarrier → ShuttleFreight → WalkingCarrier`: the route and allocation hold all three legs, `CurrentLegIndex` advances, and the job can await a new generic executor at the non-walking leg. The enum entry is only a route-shape marker; no Shuttle executor or dispatch was added. Added a manager-report test proving stale route-token and execution-ID callbacks are ignored while a matching callback cancels the pre-pickup allocation and releases its reservation. Existing activity-correlation and inventory tests cover their respective gates. Determinism tests cover stable key formatting, quantity-first ranking remains covered by the existing test, priority numbers/order, and generic ID formatting.

The automated gate is authored but has not yet been executed. A later elevated `dotnet test` invocation exited 0 but produced no test-run output; inspection confirmed Unity's generated `ColonyPrototype.Tests.csproj` predates and omits the new B1.6 source and test files. That command is not evidence that the new code compiled or the tests ran. I did not edit the generated project or launch a second Unity editor. Therefore this report does not claim B1.6 acceptance has passed.

### Unity acceptance left for the project owner

1. Bob walks to the Farm and performs normal work choreography.
2. Routine Porter freight runs Airlock → Farm pickup → Cafeteria delivery → Airlock return, with exact Food conservation.
3. Emergency Cafeteria freight is delivered by an eligible current worker and normal work resumes where appropriate.
4. Before-pickup release removes reservation and releases the worker; after-pickup release stays deferred until safe delivery.
5. Food, Sleep, Work, and OffDuty activity entry still begins at the requested destination.
6. The authored B1 allocation remains one loaded WalkingCarrier leg Farm → Cafeteria; empty worker positioning is not a freight leg.

### Deferred debt (recorded, not expanded in B1.6)

Brain lifecycle extraction; WorkforceManager/Brain dependency direction; walking worker-selection scale; orders × services × supplies × assignments complexity; push/pull stock discovery; generic registry abstraction; save/load for in-flight reservations or work; full legacy namespace/assembly isolation and deletion; provider ETA; Shuttle scheduling; proactive depot provisioning/output pressure.

### Locked pilot release rule

A Pilot's duty release stays deferred until that Shuttle is physically docked at its own home Shuttle Base. A safe remote dock does not satisfy the release condition.

## Ticket Progress

Implemented: T00, T01, T02, T03, T04, T05, T06, T07.
Acceptance evidence pending: focused automated gate execution and the Unity Play Mode checks listed above.
