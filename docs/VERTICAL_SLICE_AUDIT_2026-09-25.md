# Vertical slice audit — 25 September 2026

> Owner correction after this audit: assigned final-leg cargo must be completed in full by its worker across as many trips as needed. Playtesting found that early release leaves reserved cargo at docks. The audit's between-load release and small-emergency-buffer recommendations are superseded. A demand must also support multiple shipments and heterogeneous providers. The current execution authority for implementing these corrections is [the logistics completion stack](../tickets/LOGISTICS_COMPLETION_STACK/00_START_HERE.md), especially its locked rules. This audit remains the historical diagnosis, not an implementation instruction to restore the superseded policies.

## Judgment

The core decomposition is worth keeping. The slice has real inventory custody, meaningful worker ownership, physical route completion, and reusable production and interaction systems. It is substantially more than a visual mock-up.

The logistics loop is not yet dependable across ordinary gameplay variations. The highest risks are progress and recovery: stock can remain correctly accounted for while a worker, shipment, or shuttle is stuck indefinitely. A successful demonstration of the authored route does not establish that emergency labor, larger orders, full storage, shift changes, and dock contention work.

I recommend a focused correction pass before treating the first UI as a playable logistics game. Preserve the current owners; repair the contracts between them. Do not introduce a universal scheduler, command bus, generalized supply-chain solver, or second simulation in tests.

## Scope and evidence

- Audited the working tree based on HEAD `ccc329f4`, including the substantial pre-existing uncommitted inventory/shared-capacity/multi-trip freight changes. Findings describe that working tree, not just HEAD.
- Traced the canonical workforce/brain → walking work service → freight allocation → inventory custody → shuttle request/trip/voyage → final delivery path, plus food reservation/consumption, production capacity, personnel routing, and legacy boundaries.
- Read the architecture constitution, ownership/workforce documents, exploration testing policy, corrective reports, relevant tests, and scene authoring code. Several descriptive documents are older than the implementation; code takes precedence in this audit.
- Successfully built `ColonyPrototype.Runtime.csproj`, `ColonyPrototype.Tests.csproj`, `ColonyPrototype.Editor.csproj`, and `ColonyPrototype.PlayModeTests.csproj` with `dotnet build --no-restore --verbosity quiet`. Each reported zero warnings and errors.
- These are generated-project compilation checks, not Unity Test Runner executions. The new untracked `InventoryComponentEditor.cs` is not listed in the generated editor project, so that build does not validate that file. No Play Mode session or physical acceptance run was performed. Unity editors were already running; this audit did not alter their sessions.
- `git diff --check` passed. Production code and pre-existing changes were left untouched. This report is the audit artifact.

The failures below are source-traced conditions and consequences, not claims of observed Play Mode reproductions. Physical outcomes requiring authored navigation/animation are explicitly identified.

## The real ownership model

| Concern | Current owner | Assessment |
|---|---|---|
| Stock, capacity, stock reservations | `InventoryComponent` | Good foundation; public mutable entries and compatibility APIs weaken its boundary. |
| Replenishment settings | `LogisticsStockComponent` | Simple and useful, but per-resource targets need rules for shared capacity. |
| Order, allocation, leg progression, cargo transfers | `FreightLogisticsManager` | Correct ownership choice; concrete executor branches and recovery gaps now carry too much policy. |
| Worker selection and freight labor permissions | `WalkingFreightWorkService` | Correct place for porter, producer assistance, and emergency employee eligibility. |
| A worker's concrete freight execution | `WalkingFreightExecution` + `WorkExecutionLease` | Good separation from employment; release currently lasts too long. |
| Regular employment | `WorkforceManager` | Separate from the brain and physical execution, as intended. |
| Personal decisions | `ColonistBrain` | Appropriately asks the work owner to release; the work owner must honor that request. |
| Physical personnel route | `PersonnelRouteRunner` | Correlated completion is good; shuttle lifecycle and frame-driven completion deserve attention. |
| Shuttle transportation obligation and batching | `ShuttleManager` | Persistent requests are useful; scheduling currently commits vehicles too early. |
| Boarding, loading, trip execution | `ShuttleServiceComponent` | Unload acknowledgment and retry/cancellation handling are incomplete. |
| Flight and dock claims | `ShuttleVoyageComponent` + docking port | Real physical authority; transport must distinguish temporary berth contention from fatal failure. |
| Production | `ResourceConverterComponent` through facility performance | Good composition; shared-capacity conversion math needs correction. |
| Meals | `FoodManager` / food service / brain / activity runner | Real inventory integration now exists, contrary to older workforce/ownership prose. |

The central rule to add is **conservation plus eventual resolution**. Keeping cargo reserved forever is not sufficient correctness. Every live obligation needs a path to completion, safe cancellation, or a visible recoverable hold.

## Priority findings

### 1. P1 — Shuttle completion does not acknowledge successful unloading

Evidence: `Vehicles/ShuttleServiceComponent.cs:371`, particularly 392–408; `Logistics/P4b/ShuttleFreightExecution.cs:70`; `FreightLogisticsManager.cs:643` and 671.

`CompleteTripAtDestination` invokes a void freight-arrival callback, then completes the request and trip unconditionally. Freight delivery can reject the transfer because the destination lacks capacity or the cargo reservation is no longer usable. The callback discards the manager's result.

Concrete trigger: after dispatch, fill the destination staging inventory through another legitimate inventory writer. On arrival the freight job becomes `Blocked`, cargo stays aboard, but transport becomes `Completed` and the vehicle becomes available again. There is no shuttle-freight tick/retry path that subsequently unloads that blocked job. Merely changing the callback to retry is insufficient: the freight state machine also needs a legal retry transition.

This can strand reserved cargo aboard a shuttle that goes on to perform other work. It is an obligation/custody mismatch, not necessarily immediate resource destruction.

Smallest correction: unloading returns an explicit result. Complete only acknowledged transfers; retain the trip and custody while unloading is blocked. Retry from a defined state. Expose why the vehicle is waiting.

### 2. P1 — Normal shuttle waiting and cancellation can permanently wedge a trip

Evidence: `ShuttleServiceComponent.cs:156`, 181–203, 304–360; `ShuttleTransportContracts.cs:409–469` and 515–531; `Flight/ShuttleVoyageComponent.cs:205–258`.

There are several related handshake defects:

- A destination dock temporarily refusing reservation makes `PrepareAndDepart` set the trip to `Blocked`. `SimulationTickTransport` then returns immediately for blocked trips. A later free berth does not resume it.
- A request cancelled before boarding remains in `currentTrip.Requests`. Loading treats every terminal request as `allReady = false`, so the remaining passengers/cargo can wait forever.
- Loading calls `TryLoad` again for freight already loaded if another payload prevented departure on a previous tick. The freight manager rejects the second pickup because the job has already picked up. Mixed/batched loads need idempotent readiness handling.
- Vehicle selection ignores pilot availability. A lexically preferred unstaffed shuttle can take a request and hold it in `WaitingForPilot` even when another shuttle could serve it.
- A failed initial reposition can return false after assigning `currentTrip`. The manager clears the requests' trip associations, but the service does not roll back its own trip ownership.
- If acceptance first waits for a pilot while the shuttle is already at the requested destination, a later tick can call destination completion without first visiting the origin. Dock location is used to infer trip progress without requiring pickup to have happened. This also needs an explicit progression guard.

Smallest correction: distinguish waiting for a pilot/berth/payload from unrecoverable failure; remove cancelled unboarded requests from the active manifest; treat already transferred payloads as loaded; make acceptance rollback symmetric. Prefer presently serviceable vehicles while keeping unmet requests persistent.

Do not solve berth contention with a large traffic-control system yet. A retryable wait and clear occupancy rules are enough for the current scope. Two shuttles trying to exchange two occupied single berths still require an explicit gameplay/authoring rule or spare berth; retry alone cannot create space.

### 3. P1 — Emergency employee labor cannot initiate ordinary local rescue hauling

Evidence: `FreightLogisticsManager.cs:1135–1195`, 1078–1092, and 1551–1564.

Initial candidate discovery considers routine porters and producer outbound assistance. It never considers a consumer employee. Consumer emergency pickup is considered only for an awaiting final walking leg, and explicitly rejects `CurrentLegIndex <= 0`.

Concrete trigger: cafeteria below its emergency threshold; food available in a reachable depot; no porter available; an eligible cafeteria employee and emergency permission exist. No initial allocation is created for that employee. Producer assistance may mask this gap when the source is a staffed producer; it does not solve depot pickup.

Smallest correction: apply the emergency eligibility policy to initial local allocations as well as final staged deliveries. Keep the same service, lease, and inventory transfer path. The emergency condition should describe urgency and permissible labor, not where a route happens to be in its leg list.

Preserve minimum staff remaining, critical-hunger exclusion, and genuine active-work eligibility. Decide explicitly whether one emergency trip restores enough stock to resume service, instead of automatically borrowing an employee until an entire large target is filled.

### 4. P1 — Walking work can hold a colonist through many trips and indefinitely blocked work

Evidence: `FreightLogisticsManager.cs:365–379` and 800–819; `WalkingFreightExecution.cs:121–170`, 284–310, 352–359; `People/ColonistBrain.cs:651–663`; `Workforce/WorkExecutionLease.cs:56–84`.

Every live walking execution returns `Deferred` for release, including before pickup. After a partial delivery, remaining stock sends the same worker straight back for another trip. Pending release is consulted when the entire execution completes, not at each empty-handed boundary. The lease caches a repeated release result, so progress depends on the owner actively honoring the pending request.

Concrete trigger: a 100-unit shipment, 5-unit carry capacity, then shift end or critical hunger after the first delivery. The worker is still committed to the remaining trips. If the job cannot finish, the biological or schedule interruption has no effective escape.

Smallest correction: release immediately before pickup when safe; after pickup, finish one safe deposit and yield before taking another load. Reassign the remaining shipment without losing its staged/origin reservations. The obligation can outlive a worker's execution.

This also addresses labor fairness: one large consumer order currently reserves a large quantity and monopolizes one porter rather than naturally sharing work across available porters or reconsidering urgency between loads.

### 5. P1 — Blocked walking pickup has no valid resume transition

Evidence: `FreightLogisticsManager.cs:309–314`, 963–999; `WalkingFreightExecution.cs:146–152` and 237–272.

After the first leg or partial progress, a failure before the next pickup blocks the job to preserve custody. The executor's retry calls `StartPickupRoute`, but `PickupRouteStarted` accepts only `Assigned`, not `Blocked`. The new physical route is stopped when its report is rejected; the job stays blocked.

Concrete trigger: fail the final walking leg's pickup route temporarily, then restore reachability. Unlike a loaded delivery retry, the pickup retry cannot transition back into execution.

Smallest correction: define and validate the blocked-to-pickup retry transition, with its existing correlation checks and intact source custody. Couple it with the worker-release rule above so persistent failures do not hold people forever.

### 6. P1 — Bigger stock/order sizes can make shuttle freight disappear from consideration

Evidence: `FreightLogisticsManager.cs:1211–1276`, especially 1220 and 1250–1253.

The multimodal builder computes a shipment quantity from demand, source, and staging space, then rejects the route if that quantity exceeds the largest shuttle's free cargo capacity. It does not reduce the allocation to a feasible shuttle load.

Concrete trigger: available source stock and destination demand are 100, both depots have room, and the shuttle carries 50. The route is rejected entirely. The same network may work with only 40 available at source. More stock should not make delivery impossible.

The builder also always constructs walking → shuttle → walking and excludes an origin depot that is already the source. Stock physically at a departure depot cannot simply start with the shuttle leg; it needs another depot/extra walking arrangement to fit the current builder.

Smallest correction: size each allocation to feasible transport capacity, preserving the uncovered remainder. Support starting/ending at a transfer stock point without forcing a redundant walking leg. This requires allowing the first provider to be a shuttle where appropriate; `AcceptCandidate` currently assumes walking.

### 7. P1 before multi-resource content — Shared storage is not yet a coherent replenishment/production policy

Evidence: `LogisticsStockComponent.cs:18–29`; `FreightLogisticsManager.cs:245–281`, 1139–1144, 1509–1541; `Production/ResourceConverterComponent.cs:167–181`, 231–257.

The shared physical capacity itself is sensible. Three surrounding rules need attention:

1. **Every `targetFull` resource targets the inventory's entire capacity.** Two input resources can each request 100 in a 100-unit store. Freight projection prevents straightforward freight overbooking, but allocation order can fill the entire store with one material, making the other input impossible to import. There is no balanced replenishment rule.
2. **Conversion ignores the space its inputs will free.** A full 10-unit inventory containing 10 input units rejects a recipe consuming 10 and producing 5, because output capacity is checked before consumption. Continuous conversion has the same problem through `FreeCapacity / totalOutputs`. This can deadlock a processor stocked to full with precisely the material it needs.
3. **Projected inbound space is local to freight.** Production and other inventory additions do not participate in that projection. They can consume expected unloading room. Either capacity claims must be honored by those writers, or unloading/backpressure must recover correctly. Currently the latter is broken for shuttles.

Smallest correction: reserve `targetFull` for single-resource stores or provide explicit per-resource target budgets; make recipe feasibility consider the complete input/output mutation; choose one simple policy for inbound space and competing production. Do not add weighted resource volumes or elaborate warehouse optimization without a gameplay need.

Also normalize feasible discrete shipment quantities after applying shared free-space limits. For example, mixed continuous stock can leave 5.5 free units, while `TryReserveOwned` correctly rejects a requested 5.5 units of a discrete resource. The planner should choose 5, not repeatedly propose an illegal shipment.

### 8. P2 — Dedicated porter capacity reads the wrong setting

Evidence: `WalkingFreightWorkService.cs:394–407`; authoring configures routine capacity at `Assets/Editor/Logistics/P4bModularSceneAuthoring.cs:171`.

`GetTripCapacity` groups `RoutinePorter` with `ProducerOutboundAssist` and returns `producerAssistCapacityPerWorker` for both. The separate routine capacity field is configured and exposed but not used for execution. With defaults, a configured 10-unit porter carries 5.

Correction: use the matching setting for each purpose. This is a small, definite defect and makes the larger multi-trip/release problem worse.

### 9. P2 — Producer assistance skips the physical exit wait

Evidence: `WalkingFreightWorkService.cs:231–242`; `WalkingFreightExecution.cs:137–143`; `Packages/com.asteroidcolony.interactions/Runtime/ColonistActivityRunner.cs:350–379` and 1032 onward.

The service gracefully stops facility activity for any facility worker it borrows. Execution waits for that activity to release only when `IsEmergency` is true. Producer outbound assistance is not classified as emergency, so it can start routing while the worker is still in exit animation/placement.

The missing gate is confirmed in source; the visible consequence depends on authored exit choreography. The motor's activity motion temporarily owns root placement and navigation settings, so this is a real ownership overlap to check in Play Mode.

Correction: wait for physical release whenever an execution suspended facility activity. Use physical ownership as the condition, not freight purpose.

### 10. P2 before UI — Policy changes, removal, and stopped services lack a complete obligation lifecycle

Evidence: `FreightLogisticsManager.cs:231–242`, 245–264, 1387–1404; `WalkingFreightWorkService.cs:59–63`; `ShuttleServiceComponent.cs:129–134` and 614–628.

Orders capture an immutable requested quantity. Republishing only refreshes their timestamp. Unpublished orders with any committed quantity never expire. Disabling walking service stops execution ticks without a custody handoff or a surfaced pause state. Disabling a shuttle marks its trip blocked, retains its associations, and does not provide an ordinary resume path.

A pause can intentionally preserve ownership. The problem is that pause, cancel, configuration change, destruction, and retry do not have separate, complete meanings. A player UI will expose these ambiguities quickly through job reassignment, stock-policy edits, and service toggles.

Correction: define narrow commands and outcomes. Before pickup, cancellation should release the claim and provider. After pickup, preserve cargo and deposit or hand it off safely. A paused provider should be visibly paused and resumable. Do not let UI edits bypass these owners or silently erase loaded obligations.

## Improvements that protect the architecture

### Make the existing execution seam real

`IFreightLegExecution` is a useful seam, but `FreightLogisticsManager` now branches on `ShuttleFreightExecution` and casts to `WalkingFreightExecution` for transfer behavior, release behavior, capacity, and state transitions. Its public field type is generic while its implementation understands the concrete providers.

After correcting behavior, extract only the facts the manager actually needs: readiness at source, carry capacity, transfer result, safe release boundary, and current custody. Keep provider-specific movement inside the provider. Do not hide the same concrete assumptions behind a new abstract factory hierarchy.

Likewise, `FreightLegProgress` repeats quantities and references already represented by reservation tokens (`RemainingAtOrigin`, `InCarrierQuantity`, accumulated staged tokens). This is understandable for a multi-trip shipment, but every failure/reconciliation path now has to keep both descriptions consistent. Prefer deriving physical quantities from current owned claims; retain separate historical delivered totals only where necessary. This is an invariant concern, not an arbitrary file-length objection.

### Tighten inventory access before the UI grows around it

`Entries` is a read-only list of mutable `InventoryEntry` instances, and `GetEntry` returns those instances with public `onHand`/`reserved` fields. Current runtime consumers largely respect the API, but the type does not enforce the advertised sole-owner boundary.

Both `Capacity` and `FreeCapacity` can expand capacity to fit existing stock. One-time migration is reasonable; routine getter-based repair hides an invariant violation and lets reads mutate gameplay capacity. Keep migration explicit, expose read-only entry snapshots/accessors, and report invalid runtime state rather than resizing storage as a side effect of a query.

Food currently uses the aggregate legacy `Reserve`/`WithdrawReserved`/`ReleaseReservation` APIs, while freight uses owned tokens. Aggregate reservations work in the normal meal path, but cannot identify which meal lost its claim if stock is reduced beneath reservations. Move meals to owned claims when touching that lifecycle. This is a boundary improvement, not a claim that ordinary dining currently double-consumes meals.

### Bound active history and keep diagnostics truthful

Freight `orders`/`jobs` and shuttle `requests`/`trips` retain terminal records indefinitely. Their dispatch, capacity, and scheduling routines repeatedly traverse those lists. At one logical step per simulated second, accumulated completed work will eventually become part of every scheduling cost.

Keep live obligations in active collections and retain only a bounded recent history for inspection; structured logs already provide the longer history. Profile candidate/path estimation after that. The current route search nests orders, sources, endpoints, services, and worker path queries, so cache/invalidation policy may become useful with larger colonies, but do not invent it prematurely.

There are smaller observability remnants: shuttle transfers emit `logistics.inventory_transfer` twice in the non-walking branch; `production.food_output` is used for every recipe output; several shuttle diagnostic keys say passenger for freight as well. Those should be corrected before using logs as economic counters.

### Preserve the legacy boundary and update the current map

The legacy transport folder and fixture guard are helpful. Extraction still uses `ShipComponent`, `ShipMovementComponent`, and legacy `ResourceStockPolicyComponent`; it is not automatically part of modern shuttle logistics. When mining returns to the playable loop, hand its output into modern stock/custody rather than quietly activating both dispatch generations.

Do not delete all old code just because it looks old. Identify remaining scene/content consumers first. But mark compatibility APIs, inert stores, and old documents honestly so a future contributor does not extend the wrong path.

`STATE_OF_THE_PROJECT.md` still describes September 20, and workforce/ownership prose says food integration is deferred despite the implemented meal reservation/commit path. Refresh one current architecture map and the legacy boundary document; avoid another stack of mutually inconsistent reports.

## What is especially good

1. **Physical custody is concrete.** Owned stock transfers update both inventories before publishing change events. Staged cargo receives an owned reservation before being exposed. This is the right basis for conservation and preventing double allocation.
2. **Employment is not execution.** A regular assignment, genuine active staffing, and an execution lease are separate concepts. A worker sent hauling naturally ceases to provide physical facility staffing. Keep that model.
3. **The brain does not know freight choreography.** It requests work release through a generic lease. Fix the owner's release behavior rather than teaching the brain special cases for porters and shuttles.
4. **Activity entry depends on whole-route completion with correlation.** Intermediate motor arrivals and stale callbacks cannot simply complete the intended route. This is an important subtle invariant worth retaining.
5. **Production consumes facility performance.** Recipes do not count workers or direct them. This supports new production facilities through composition without bespoke controllers.
6. **Orders, allocations, and physical legs are distinct.** That separation can support smaller shipments, multiple providers, and interrupted workers without throwing away the architecture.
7. **The testing policy is well judged.** Quantity rules, ownership, and state transitions deserve focused tests; authored walking and animation deserve actual Unity observation. Existing source-shape tests alone are not evidence of functional multimodal logistics.

## Recommended correction sequence

| Order | Deliverable | Acceptance |
|---|---|---|
| 1 | Reliable shuttle load/unload acknowledgment, transient waits, cancellation and rollback | Full destination, cancelled passenger, delayed berth, and mixed payloads recover without orphaning cargo/people. |
| 2 | Walking retry and safe worker-release boundaries; correct porter capacity and facility exit gate | Shift end/critical hunger yields at a safe boundary; a restored route resumes; porter tuning is honored. |
| 3 | Emergency pickup as an ordinary eligible labor source; feasible shuttle-sized allocations and endpoint routes | An unstaffed logistics network can be rescued locally; excess demand splits; already-staged depot stock ships. |
| 4 | Shared-capacity stock targets and conversion feasibility; policy lifecycle | Two required inputs can coexist; a full store can consume inputs into smaller outputs; policy edits resolve outstanding obligations explicitly. |
| 5 | Minimal truthful UI and bounded live inspection | The player can see stock, reservations, incoming/staged cargo, blockers, responsible provider, and the next available action. |

The UI can be sketched concurrently, but avoid making it depend on today's accidental meanings of `Completed`, `Blocked`, `targetFull`, or worker release.

## Lightweight verification

No new tests were added in this audit. For corrections, choose a small set of durable assertions:

- A rejected unload cannot complete transport, and a successful retry transfers exactly once.
- Cancelling an unboarded member cannot trap the remaining manifest; retrying a partially loaded manifest cannot load twice.
- A legal blocked pickup retry advances, while stale execution/route reports remain rejected.
- Releasing a worker preserves all stock and claims and does not start another load after a safe handoff.
- Shared-capacity recipe feasibility uses the complete transformation; discrete allocation sizing remains integral.

These can be focused owner-level regressions or pure rules. Do not build a fake NavMesh/shuttle colony to automate the whole experience. Replace source/property-name checks with behavioral assertions where they no longer protect the intended boundary.

Use this compact Unity acceptance matrix for the physical evidence:

| Scenario | Observable |
|---|---|
| Local porter, two consumers | Both receive food; no duplicate reservations; large shipments do not trap the worker past safe release. |
| No porter, low-stock consumer employee, depot food | Employee exits work, collects food, deposits it, and resumes eligible duty. |
| Producer assistance with a nonzero exit animation | No routing starts before physical exit/releases complete. |
| Shift end / critical hunger before and after pickup | Empty-handed worker yields promptly; loaded worker deposits safely and then yields. |
| Temporarily unavailable final pickup path | Restore the path; the same obligation resumes or is safely reassigned. |
| Demand exceeds shuttle capacity | Several feasible loads deliver the total; increasing stock does not stop service. |
| Food starts in the departure depot | Shipment can begin with a shuttle leg. |
| Destination staging fills before arrival | Cargo remains aboard visibly; unloading retries when space appears; completion is truthful. |
| Two shuttles / busy destination / one unstaffed shuttle | Serviceable work proceeds; berth/pilot waits do not become silent permanent blocks. |
| Cancel one queued/assigned passenger in a batch | Remaining requests finish; no absent passenger is boarded. |
| Mixed resources and full processor input storage | Inputs can coexist and conversion starts when the resulting stock fits. |
| Pause/resume, then fast speed and several game days | No hidden progress while paused, no stranded leases/claims, and active/history counts remain explainable. |

For each run, inspect custody and the reason for a wait, not just the visible movement. Personnel shuttle completion is currently polled in `Update`, whereas much of the rest advances on simulation ticks; pause and high-speed observations should explicitly cover that boundary.

The next slice should feel like managing scarce transport and labor, not diagnosing which callback stopped arriving. The existing architecture can get there with these targeted corrections.
