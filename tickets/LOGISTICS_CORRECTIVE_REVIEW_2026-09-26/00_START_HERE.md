# Logistics corrective review — September 26, 2026

Status: C01–C08 source corrections implemented; generated assemblies compile; Unity acceptance pending.

The owner boundaries and the accepted-worker completion rule are worth keeping. At the review baseline, source-confirmed paths could strand usable transport capacity, resurrect cancelled requests, or stop retrying recoverable work. This packet records those findings and their corrections; source completion does not establish Unity behavior or readiness for player UI.

This review inspected the current dirty working tree on `main`, based on HEAD `ccc329f4`, including the implementation of [the original stack](../LOGISTICS_COMPLETION_STACK/00_START_HERE.md). It did not modify gameplay code, scenes, or tests. Source line references in these tickets refer to that reviewed tree, not the older committed revision.

## Corrective sequence

P1 means a normal dispatch or recoverable transport scenario can stop making progress or report false completion. P2 means a narrower command/lifecycle/accounting path or inspection contract is incorrect and should be corrected before UI depends on it.

| Order | Ticket | Priority | Concrete problem |
|---|---|---|---|
| 1 | [C01 — Serviceable shipment sizing](C01_SERVICEABLE_SHIPMENT_SIZING.md) | P1 | An unstaffed capacity-20 shuttle causes 20-unit children that a ready capacity-10 shuttle cannot carry; a minimum of 20 can also veto every feasible 10-unit load. |
| 2 | [C02 — Terminal shuttle requests](C02_TERMINAL_SHUTTLE_REQUESTS.md) | P1 | A cancelled manifest entry is changed back to InTransit and can complete without boarding. |
| 3 | [C03 — Recoverable shuttle handoffs](C03_RECOVERABLE_SHUTTLE_HANDOFFS.md) | P1 | Temporary passenger disembark failure freezes the whole trip; a staged freight pickup blocked once cannot retry the source handshake. |
| 4 | [C04 — Account for actual transfers](C04_ACTUAL_TRANSFER_ACCOUNTING.md) | P2 | A shortened cargo claim can deposit stock, then return before crediting the physical delivery. |
| 5 | [C05 — Guard intermediate stock bindings](C05_LIVE_ROUTE_BINDINGS.md) | P2 | The live-work guard checks only shipment endpoints, allowing a dock used by an active route to change inventory under its reservation. |
| 6 | [C06 — Walking provider lifetime](C06_WALKING_PROVIDER_LIFETIME.md) | P2 | Destroying the work service while its worker survives leaves an execution considered available that will never tick again. |
| 7 | [C07 — Replenishment and terminal history](C07_REPLENISHMENT_HISTORY.md) | P2 | A target-satisfied historical order can reopen above the reorder threshold; pruning history changes dispatch behavior. |
| 8 | [C08 — Truthful custody inspection](C08_CUSTODY_INSPECTION.md) | P2 | Loaded shuttle and staged cargo quantities can read zero, final deliveries can read as staged, and shuttle deposits emit duplicate transfer events. |

Work sequentially; C08 should follow the custody corrections. Each ticket includes a trigger, the actual call path, a bounded correction, and acceptance. These are corrections to the existing owners, not a replacement logistics architecture.

## Scenario trace at review baseline

“Consistent” below means the inspected logic supports the scenario under the stated conditions. It is not a Unity playtest result.

| Scenario | Trace and result |
|---|---|
| Dedicated porter, accepted final leg 20, capacity 5 | Pickup creates leg progress and transfers one load under an owned claim. Final deposit credits 5; remaining origin stock sends the same execution back for another load. Execution finishes only after the child's remaining final obligation reaches zero. Consistent. |
| Employee accepts the same 20; shift ends or hunger becomes critical after the first load | Walking execution defers release; the manager does not shrink the obligation. The same remaining 15 stays owned. Consistent with the owner's explicit playtest decision; keep this rule. |
| No eligible porter; emergency consumer needs local stock | Candidate discovery includes consumer emergency pickup, and work-service eligibility/leases own worker admission. The accepted leg then follows the same completion flow. Physical workplace exit, walking and return still need Unity acceptance. |
| One capacity-10 shuttle, demand 40, sufficient stock/room, minimum at most 10 | Children can be bounded to 10 and serviced on successive voyages. The quote/accept boundary can queue commitments before a specific shuttle is ready; the readiness and minimum counterexamples in C01 mean this is not a general proof of feasible dispatch. |
| Unstaffed capacity-20 shuttle plus ready capacity-10 shuttle | Planner uses maximum nominal free cargo capacity; scheduler subsequently requires readiness and fit. The ready shuttle cannot take the 20-unit child. C01. |
| Demand 40, minimum shipment 20, only capacity-10 providers | Demand publication succeeds, but candidate filtering rejects each feasible load while the uncovered amount exceeds the minimum. No first load happens. C01. |
| Cargo begins/ends in a transfer depot | Route construction can omit unnecessary walking legs and accept the actual first provider. Consistent; physical routes remain unobserved. |
| Mixed capacities 20/10/5/5 | Parent accounting supports these commitments and out-of-order final deliveries. Existing tests exercise accounting/sizing helpers, not actual heterogeneous provider discovery/dispatch. Physical freighters and drones remain unimplemented by design. |
| Destination staging fills during flight, then frees room | Cargo transfer fails before mutation; the payload returns RetryableWait, and the trip retries while unloading. Consistent for an intact claim and a freight-only unload. Passenger disembark failure can stop this path: C03. |
| Staged freight cannot fit the shuttle at loading, then room is restored | The manager blocks the retained child, but the next ProviderAtSource report requires Assigned. The advertised retry cannot pass the state gate. C03. |
| Cancel one unboarded passenger while another remains in the trip | Loading skips the terminal entry, but the departure loop revives it. Arrival can falsely complete it. C02. |
| Temporary NavMesh failure for one arriving passenger | Disembark failure sets trip Blocked; the tick loop never retries Blocked trips. Freight acknowledgments behind it are not reached. C03. |
| A valid inventory removal shortens a carried reservation before final deposit | Inventory can transfer the surviving amount. Freight's partial-result branch returns before accounting for it. C04. This is a supported inventory mutation boundary, not a claim that current normal meal/recipe code steals freight. |
| Rebind a live intermediate depot's stock to another inventory | Guard omits intermediate legs, so the command succeeds; the token still belongs to the old inventory. C05. |
| Disable/re-enable walking service; destroy only the service | Disable stops service ticks and defers route callbacks. Destruction is not recognized by ExecutorAvailable when the worker survives; there is no future resume. C06. Physical pause behavior still needs observation. |
| Target becomes satisfied without freight; later one unit is consumed | A closed policy-retired order is reused without applying the reorder threshold. C07. |
| Shared storage and production | Policy-set validation is atomic; continuous production uses net capacity growth; an active batch consumes inputs once and waits for output room. Consistent through the validated commands with unchanged recipe/bindings. |
| Inspect loaded, staged, and partially delivered jobs | Current derived fields and duplicate events do not reliably describe custody. C08. |

## Keep these parts

- Inventory owns physical quantities and claims. Intermediate transfer-and-reserve is one atomic inventory operation, and meal claims remain separate owners.
- Parent demand, child shipment, route leg, carrier load, and accepted worker obligation are separate concepts. This is the right shape for future carriers.
- Correlated allocation/leg/execution/route reports reject stale execution facts. Keep those checks while repairing legal retry states.
- Movement stays with providers and voyage/docking; the freight manager owns accounting and custody handoffs. Fix the handshake boundaries without moving physical behavior into the manager.
- Final-leg workers finish all accepted cargo. No ticket permits ending after one load, shrinking to a safety buffer, or relabeling the remainder to release a worker.

## Watch as content and UI arrive

- Use a common definition of provider feasibility for planning and execution. Avoid adding a separate “probably available” rule at each layer.
- A future vehicle needs usable candidate discovery and acceptance, not just an implementation of the execution interface. The present shared lifecycle seam and numeric mixed-capacity example do not prove that adding a new vehicle requires no planner changes. Extend that seam when introducing the first new provider; do not build a universal provider framework now.
- UI commands should use validated owner APIs. For example, `ResourceConverterComponent.activeRecipe` remains a writable authoring field even though `TrySelectRecipe` rejects switching an active batch. Direct field edits are outside the production trace above; enforce recipe/batch identity when adding live recipe editing.
- Production can legitimately consume room after freight planning. Preserve waits with custody; do not promise a capacity-reservation system the current implementation does not have.
- Exceptional cargo loss and permanent destruction need truthful retained obligations. These tickets do not add salvage, teleport cargo, or infer that missing units were delivered.

## Corrective implementation status — 2026-09-26

All eight corrective tickets have source changes and focused owner-level regression cases. The corrections preserve the original owner boundaries: inventory owns physical claims, the manager owns parent/child accounting and transfer reconciliation, execution providers own movement, and shuttle services own voyage/actor behavior.

| Ticket | Source disposition | Compiled regression coverage |
|---|---|---|
| C01 | Child sizing now uses future-serviceable capacity: a healthy piloted shuttle may receive queued work while busy, while unstaffed/blocked providers are excluded; physical trip assignment still requires immediate readiness. Feasible loads below the minimum batch preference remain candidates. | Regression covers a busy piloted capacity-10 shuttle being quoted while an unstaffed capacity-20 shuttle is excluded. Actual queued-voyage execution remains Unity acceptance. |
| C02 | Shuttle request transitions and completion are immutable after a terminal state; completion is exactly once. | Cancelled request cannot revive or emit completion; duplicate completion emits once. |
| C03 | A blocked staged pickup can retry after its retry tick; temporary passenger disembark failure remains an unloading wait while independent payloads are acknowledged. | Staged pickup succeeds once capacity returns. Passenger/NavMesh behavior remains Unity acceptance. |
| C04 | Every positive final transfer is credited before a partial result is classified; missing accepted cargo becomes an explicit manual-recovery hold. | Single-load and across-load shortfall regressions verify physical amount, parent commitment, custody, and idempotent acknowledgment. |
| C05 | Live-job checks include every route origin/destination and reservation owner; stock and endpoint rebinding are rejected atomically. | Intermediate stock/anchor and endpoint rebind rejection; unrelated idle stock remains rebindable. |
| C06 | A disabled provider remains a recoverable provider wait; a destroyed service is no longer mistaken for a live executor. | Source guard compiled. Disable/destroy lifecycle observation remains Unity acceptance. |
| C07 | Closed policy-retired history is excluded from open-order reuse while accepted commitments remain discoverable. | Target satisfaction, below-threshold consumption, and reorder-threshold reopening regression. |
| C08 | Read-only custody snapshot separates source claims, carried cargo, staging, delivered quantity, and outstanding obligation; transfer logging is emitted once. | Split custody, direct shuttle cargo, final walking delivery, and worker remainder regressions. Event count through a real shuttle voyage remains Unity acceptance. |

Generated Runtime, Editor, EditMode Tests, and PlayMode Tests projects compiled successfully. The regression test source compiled, but no Unity Test Runner execution/count is available. A first Runtime build run concurrently with other generated-project builds failed because a generated editorconfig file was transiently absent; the standalone Runtime rebuild passed. `git diff --check` reported no whitespace errors; Git emitted expected line-ending normalization warnings.

## Evidence and completion gate

The original review was documentation-only. The corrective pass added the regression source listed above and compiled it with the generated test project. No Unity Test Runner session or physical scene acceptance was run. The earlier `dotnet test` process exit had no discovered/executed test count and is not evidence of a test pass.

Before implementing a regression, read [TESTING_IN_EXPLORATION_MODE.md](../../TESTING_IN_EXPLORATION_MODE.md). Use compact owner-level cases for the actual defects; do not create fake colonies, scene-text assertions, or a suite for every branch. Navigation, passenger positioning, docking, worker choreography, and simulation-speed behavior use explicit Unity acceptance.

Revisit the original [acceptance matrix](../LOGISTICS_COMPLETION_STACK/03_ACCEPTANCE_MATRIX.md), especially A02–A15 and A18–A20. Preserve its NOT RUN status until observations exist. A compile pass and a plausible source trace are not substitutes for those observations.
