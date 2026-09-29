# Logistics topology and sequential execution

Status: code implementation complete; Unity Play Mode acceptance remains pending for scene navigation, end-to-end freight, and visible walking animation.

## Required behavior

- Geography is determined before workers are enumerated. A loaded walking leg between two fixed anchors is checked once for the shared navigation profile, not once per colonist.
- Connected walking modules form runtime regions, with diagnostic labels such as region 0 and region 1. No gameplay rule depends on scene names or those numeric labels.
- Module construction and connection/disconnection publish topology changes. Ordinary demand processing queries the resulting registry instead of rediscovering the station.
- Docks are independent destinations. ANY shuttle can travel to ANY dock. There are no paired docks, service corridors, home-base restrictions on destinations, or per-dock service assignments.
- For cross-region freight, choose the nearest reachable freight dock in the source region and the nearest reachable freight dock in the destination region, using walking distance and deterministic tie breaking. The shuttle scheduler sends a vehicle to collect the cargo.
- A full dock is a capacity wait, and a busy or unstaffed shuttle is a dispatch wait. Neither changes physical connectivity or erases a valid route.
- Only the current leg is dispatched. Its confirmed completion unlocks the next leg. Completion means all cargo assigned to that leg has physically reached its handoff inventory.
- An accepted final walking leg finishes all assigned cargo across repeated loads. Intermediate staging never counts as final demand satisfaction.
- The parent demand can have multiple bounded children fulfilled by different providers. Arithmetic example: 40 units at 10 units per load requires four loads. Child allocation and repeated trips must not duplicate quantity or claims.

## Evidence in current code

- `ModuleConnectionPoint` still owns link creation and disposal. `ModuleNavigationTopology` now derives connected regions from registered module surfaces and includes an edge only while its link has a complete shared walking path; explicit NavMesh rebuild notifications recheck link readiness.
- `PedestrianRouteProvider` now answers fixed-anchor walking queries without a colonist. The freight manager checks direct geography once per source/request before requesting labor quotes, then uses source- and destination-local reachable dock selection with stable tie breaking.
- `WalkingFreightWorkService` now uses the personnel multimodal planner for worker positioning while cargo geography remains actor-independent. This permits emergency staff to ride a shuttle to a remote pickup.
- `ShuttleManager.CanService` requires two distinct registered endpoints and imposes no dock pairing. It quotes against configured cargo capacity without considering temporary occupancy or pilot state, keeps accepted work queued, and now allows shuttle freight to finish an allocation in repeated holds, including when the fleet is temporarily absent.
- The live log showed `Animator is not playing an AnimatorController` while route runners stopped during scene teardown. `ColonistMotor` now guards parameter writes during animator shutdown. That does not establish or fix the reported visible walking-animation regression; scene observation is still required.

## Execution record — 2026-09-27

Implemented in the working tree: T01 region registration/readiness and lifecycle invalidation; T02 shared fixed-anchor route query and bounded cache; T03 geography-first direct/multimodal selection and nearest local docks; T04 multimodal worker positioning; T05 queued shuttle dispatch, any-dock service, and repeated capacity-limited shuttle loads. T06's teardown-time Animator warning is guarded, but visual walking behavior remains unverified and must not be called fixed. T07 runtime, editor, and test assemblies compile successfully; `git diff --check` passes. Unity EditMode tests were not executed, and focused Play Mode acceptance remains pending because the available UI-control runtime failed to initialize.

## Ownership and implementation order

Connection lifecycle -> walking topology -> actor-independent path query -> freight itinerary -> current-leg dispatch -> physical execution report -> next-leg dispatch.

Keep `FreightLogisticsManager` as the sole owner of demands, jobs and custody. Keep `ShuttleManager` as the scheduler and `ShuttleVoyageComponent` as the movement owner. Use the existing route/leg contracts where possible. The topology registry is a derived index, not another transport or inventory authority.

Execute T01 through T06 in order, then T07. Read `TESTING_IN_EXPLORATION_MODE.md` before changing tests. No scene-name branches, fixture-specific runtime fixes, fake colonies, generic transport-framework rewrite, or arbitrary expansion into new vehicle types.

## T01 — Publish connections and maintain walking regions

**Change:** Add a small navigation topology owner fed by module and connection lifecycle. Expose module membership, region lookup, connection readiness, topology revision, and endpoints in a region.

**Implementation:**

1. Register modules independently of whether their connection points currently have partners; isolated modules must exist in the registry. Prefer the existing owner-surface identity, with a thin lifecycle component only if necessary.
2. Publish added/removed/ready/unavailable connections from the existing discovery/disposal lifecycle. Retain one owner of actual NavMesh links.
3. Batch startup changes. Recompute connected components after changes using a simple graph traversal; do not build an elaborate incremental graph algorithm. Do not traverse or rebuild per logistics request.
4. Cover construction, removal, disable/enable, relocation and NavMesh rebuild. Existing `DiscoverAndConnect` preserves connections, so relocation must explicitly invalidate old edges before rediscovery. Define the notification entry point for each supported mutation.
5. Register freight anchors and shuttle transfer anchors against their owning module. Resolve ownership at registration, allow an explicit reference when hierarchy is ambiguous, and report missing membership. Do not infer membership from a display name or nearest station through space.
6. Region handles belong to a topology revision; persisted jobs reference actual stocks/endpoints, not transient region numbers. Reset registration correctly with domain reload disabled.
7. Treat a connected module group as a coarse walking region. It must not fabricate a path across disconnected NavMesh islands inside a module. Exact local queries remain authoritative for anchor-to-anchor walking.

### Verification

**Automated verification:** Small pure graph tests for merging two components, splitting after bridge removal, and retaining an isolated module. These protect stable topology rules. Add a lifecycle regression only where needed to establish actual registration/disposal behavior.

**Human Unity acceptance:** Open the commute scene and inspect anonymous regions and dock membership. Confirm the farm-side modules and main-side modules are separate. Connect/disconnect modules and confirm membership updates once without stale edges; repeat play with domain reload disabled.

**Optional diagnostics:** A read-only topology snapshot with revision, region/module counts and dock membership. No per-frame dump.

## T02 — Query walking geography without a colonist

**Depends on:** T01.

**Change:** Refactor `PedestrianRouteProvider` to expose anchor/position + navigation-profile queries. No colonist, workforce manager or work service is required to answer whether a walking leg exists.

**Implementation:**

1. Make the current colonist agent type and walkable-area mask an explicit shared profile. Use the appropriate NavMesh query filter. Future different agent types may have different results; do not key shared geography by actor identity.
2. Reject known different walking regions before invoking NavMesh or enumerating workers. Missing registration/startup readiness returns an explicit unresolved result, not a permanent negative result.
3. Within a region, query the actual anchor-to-anchor path. Cache success and failure for fixed anchors by direction, profile and navigation/topology revision. Do not assume all points under one surface are internally connected.
4. Invalidate on anchor movement, NavMesh rebuild, connection readiness/change and removal. Bound the cache and remove dead anchors. Dynamic worker positions do not create an unbounded position cache.
5. Retain thin personnel wrappers where useful: they extract position/profile and delegate to the common query. Remove actor-specific logging for shared fixed freight geometry.

### Verification

**Automated verification:** Narrow routing regression: repeated requests for the same fixed loaded leg and revision share one query; cross-region requests invoke no worker lookup and no exact walking search. Changed revision can produce a new answer. Use a small query collaborator, not a fake scene.

**Human Unity acceptance:** Cross-station freight reports a geography decision once. A local disconnected anchor reports a local path problem without trying every worker.

**Optional diagnostics:** Counts of region rejections, exact fixed-leg queries and cache hits, available in debug inspection.

## T03 — Build freight itineraries from geography

**Depends on:** T02.

**Change:** Replace worker-driven route discovery in `FindBestCandidate`, `FindBestMultimodalCandidate`, `FindShuttleLeadingCandidate`, and `TryEstimateWalkingLeg` with a small geography-first planner.

**Implementation:**

1. For a direct local transfer, establish the loaded walking path before requesting any worker quotes.
2. For a cross-region transfer, query freight docks indexed in the source region, then freight docks indexed in the destination region. Choose nearest reachable anchors by local walking distance. Never search distant station docks as candidate first walking legs.
3. Represent source -> origin dock -> destination dock -> final consumer as intended handoffs. Omit zero-length legs when cargo already occupies the relevant dock inventory.
4. A shuttle connection between registered docks requires no route-pair configuration. Remove fleet availability, current pilot state and current free cargo space from physical itinerary discovery. Report lack of a vehicle as waiting for a vehicle at dispatch.
5. Keep geometry and dispatch quantity separate. Inventory space constrains admission/transfer, not whether a path exists. Use existing capacity claims and waits to prevent overfilling or competing overcommitments.
6. Retain the final destination throughout all intermediate legs, including emergency-labor authorization. Do not require a downstream worker to be available before finding the itinerary.
7. Establish the intended handoff chain up front so invalid endpoint membership is visible before pickup. Create/activate each executable leg only when its predecessor completes. Revalidate unexecuted handoffs when topology changes; do not eagerly lease downstream workers or shuttles.
8. Remove obsolete nested global dock/worker discovery paths after migration; keep one geography owner.

### Verification

**Automated verification:** A small planner test with generic regions and anchors: a cross-region route selects the local docks and a shuttle step without workforce enumeration. An unavailable fleet leaves the same itinerary. This protects the architecture behind the observed regression.

**Human Unity acceptance:** Set farm stock to 55 and create consumer demand. Observe the intended three legs before provider selection. Names appear only as diagnostic labels. With a shuttle occupied elsewhere, the same itinerary remains visible.

**Optional diagnostics:** Route chosen/rejected, topology revision, source/destination region, intended handoffs and reason. Emit on changes, not every tick or once per porter.

## T04 — Dispatch workers for a known walking leg

**Depends on:** T03.

**Change:** Make walking work services select labor for an already established loaded leg. Worker availability and worker positioning must not determine station geography.

**Implementation:**

1. Pass the shared loaded-leg estimate into provider quoting. Do not rerun that geometry for each assignment.
2. Filter workers by existing employment, purpose, lease, hunger and staffing rules. Consider the eligible workplace services; do not add a second canonical workforce registry.
3. Evaluate remaining worker-specific positioning from the worker's current location to the pickup. Permit the existing passenger route planner to use walk -> shuttle -> walk for emergency pickup and remote repositioning. Positioning does not reserve or credit cargo movement as though pickup has happened.
4. Reuse T01/T02 in personnel route planning so remote positioning also filters docks by region rather than testing every dock for every colonist.
5. Separate `no_loaded_walk_path`, `no_available_worker` and `worker_positioning_unavailable`. A rejected worker is not evidence against an already established loaded-leg route.
6. Preserve authorization by final consumer across intermediate docks. Once a final-leg worker accepts quantity Q, complete Q over repeated loads before honoring an ordinary pending release.

### Verification

**Automated verification:** A narrow regression at the quote/positioning seam proving that multimodal personnel positioning can qualify a worker for a local loaded leg. Retain the existing final-consumer authorization and assigned-quantity invariants; do not recreate the Unity scene in tests.

**Human Unity acceptance:** Verify dedicated porter delivery. Then remove available porter labor, enable emergency pickup, and lower cafeteria stock to 4. Observe the employee travel to pickup by shuttle when required and complete assigned work.

**Optional diagnostics:** Worker rejection only after a valid loaded leg exists, with positioning and availability reasons separated.

## T05 — Advance custody one completed leg at a time

**Depends on:** T04.

**Change:** Audit and adapt existing job progression rather than introduce a second job coordinator.

**Implementation:**

1. Dispatch the first walking leg. Its completion requires all assigned units staged under the job's owned claim at the origin dock. Partial arrival records progress without advancing the whole leg.
2. On that completion, create the shuttle work request. Accept it into the queue even when every shuttle is currently occupied or awaiting a pilot. The scheduler chooses any shuttle and repositions it to the origin dock.
3. Keep current free capacity, pilot readiness and berth contention in scheduling/loading. Static carrying capacity may size loads; temporary occupancy must not delete queued work or its itinerary.
4. Preserve bounded demand children and support repeated capacity-limited transport as needed. If a staged obligation exceeds a dispatched vehicle's capacity, split its remaining transport work without duplicating source claims, final commitments or completion credit. Do not leave it permanently unserviceable merely because only a smaller vehicle is available.
5. Advance to destination walking only when the shuttle has physically transferred the assigned quantity into the destination dock's claimed inventory. Delivery at a dock does not fulfill consumer demand.
6. Dispatch the final walking leg; finish the assigned quantity over as many loads as required. No newly unowned reserve cargo is left behind.
7. On topology changes, replan only remaining work from authoritative current custody. Never reset cargo to its original source or lose an accepted obligation. Missing/removed handoffs produce an explicit recovery state.
8. Preserve report correlation and idempotence. Busy provider, full destination and release requests are waits or existing custody transitions, not reasons to rediscover station geography every tick.

### Verification

**Automated verification:** Protect the existing quantity/custody owner with narrow checks for duplicate completion and partial handoff: intermediate completion does not credit final delivery, and downstream work is not released before the predecessor quantity completes. Protect partial-load accounting if its owner changes.

**Human Unity acceptance:** Trace first walk -> queued shuttle -> physical shuttle unloading -> final walking completion. Repeat with a busy shuttle, a temporarily unavailable pilot, small carrying capacities, and destination capacity pressure. Confirm all assigned cargo is accounted for and work resumes without a new demand.

**Optional diagnostics:** One transition record with demand/job/leg, quantity remaining, custody inventory, provider, wait reason and next action. No log should call expected cross-region walking rejection a colonist execution failure.

## T06 — Diagnose and correct walking animation regression

**Depends on:** T05 for the complete logistics acceptance run; investigation may precede it.

**Change:** Reproduce the reported animation failure and fix its demonstrated owner. Do not assume boarding restoration is the cause.

**Implementation:** Inspect walking before first boarding, after disembarkation, and after facility activity interruption for freight. Trace `ColonistMotor`, `ColonistAnimationDriver`, activity exit, route cancellation and shuttle presentation restoration. Capture actual velocity, navigation ownership, animator locomotion parameter/state and presentation speed at the failing transition. Correct the ownership/restore defect; remove temporary noisy probes.

### Verification

**Automated verification:** No new permanent animation choreography test is justified. Only add a small owner-level regression if investigation identifies a durable nonvisual invariant.

**Human Unity acceptance:** At 1x and accelerated simulation speed, observe visible walking during ordinary work travel, travel to a dock, post-shuttle walking, emergency activity exit and loaded delivery. Record what was actually observed; compilation is not visual verification.

**Optional diagnostics:** Temporary state capture scoped to the reproduced transition. Document and remove it after diagnosis unless a concise observable has ongoing value.

## T07 — Integration, cleanup and handoff

**Depends on:** T01-T06.

**Change:** Finish the migration and document current ownership. Do not call the routing or animation bugs fixed solely because the project compiles.

### Verification

**Automated verification:** Compile affected runtime/editor/test assemblies. Run directly relevant topology, route-sharing and custody regressions. Delete or revise assertions that require worker-driven geography or current shuttle availability for route existence. No test quota and no scene YAML assertions.

**Human Unity acceptance:**

1. Reproduce the original farm-stock-55 and cafeteria-stock-4 scenarios. Trace demand through final delivery with dedicated porters and emergency labor.
2. Confirm a shuttle can collect at a dock other than its home and deliver to another registered dock without configuring a route pairing. Home remains a pilot-duty location, not a service boundary.
3. With temporary provider shortages, confirm the itinerary remains valid and the active obligation waits visibly, then resumes.
4. Confirm repeated small loads satisfy a larger demand without orphaned staging cargo or duplicate delivery credit.
5. Add/remove a walking connection; verify fresh geography and correct handling of existing custody. No stale region decision survives invalidation.
6. Inspect query counters under repeated demand evaluation: fixed loaded-route checks are independent of porter population, and unchanged topology is not rebuilt. Worker-positioning work is measured separately.
7. Complete T06's walking-animation observations.

**Optional diagnostics:** Keep concise topology, chosen-itinerary, current-leg and wait-state inspection. Remove obsolete actor-per-route warning spam and abandoned experimental probes.

**Closeout:** Update architecture ownership and record verified observations, commands/results, and any Unity acceptance still unperformed. Leave scene-specific examples in acceptance documentation only. If live Unity cannot be operated, state that exact limitation and keep physical acceptance explicitly pending.
