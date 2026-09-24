# P05 Sprint B1 Implementation Report

Execution resume HEAD: `cb9ad11f7d74520237133d597448c5eb815fe310` (`main`)

B1 work began at `b13b43e5d24a7d46a5e1d9cd5106520ad3f18109`; T00 and T01 were already committed when this execution resumed. The checkout contained partial, uncommitted T02 work at resume.

Packet baseline: `2d8523c3f40edaeaa57d5d57296f951d851ce209`

The checkout was clean when B1 began. The actual starting HEAD is newer than the packet-authoring baseline and includes Sprint A/A2 shuttle-flight work.

## Current Personnel Routing Ownership

The current code has policy selection largely separated from physical execution, but physical route selection is not centralized.

| Caller / path | Current responsibility | Classification | Current routing coupling |
|---|---|---|---|
| `ColonistBrain` Work/Food/Sleep/OffDuty seeking | Chooses whether and what activity to seek, consumes offers, and applies biological priority | POLICY | Calls `ColonistActivityRunner.RequestActivity`; it does not calculate a path itself. |
| `ColonistTargetResolver` | Resolves the assigned Work or personal Sleep facility/activity | POLICY / destination selection | No path calculation. |
| `ColonistActivityRunner` facility approach | Reserves the facility, then navigates the actor to the binding approach anchor and plays local activity choreography | ROUTING + EXECUTION | Calls `ColonistMotor.MoveTo` directly for both ordinary activities and sequence steps. This is currently the de facto personnel approach router. |
| `ColonistMotor` | Samples the NavMesh, calculates/sets an agent path, performs physical walking, and reports arrival/failure | EXECUTION | Owns `NavMeshPath`, `NavMesh.SamplePosition`, `NavMeshAgent.CalculatePath`, and agent motion. This low-level execution knowledge should remain here. |
| `ColonistBrain.TryStartWork` MobileDuty | Chooses the scheduled workplace and marks the colonist Working | POLICY + premature execution | Directly obtains `ColonistMotor` and calls `MoveTo(Workplace.DutyAnchor)`, bypassing ActivityRunner and any central route plan. |
| `WalkingFreightCarrierComponent.Complete` | Returns an idle non-emergency Porter to its duty anchor after a job | EXECUTION convenience | Calls `ColonistMotor.MoveTo` directly, bypassing central personnel routing. |
| `WalkingFreightRunner` | Moves Dana/Alice to freight pickup and dropoff anchors | EXECUTION with duplicated ROUTING | Calls `ColonistMotor.MoveTo` directly for both legs. |
| `ColonistActor.MoveTo` | Legacy logical-location transit bridge | EXECUTION bridge | Calls `ColonistMotor.MoveTo`; this path is not the Bob-era Brain/ActivityRunner path targeted by B1. |
| `ModuleConnectionPoint` | Validates authored module walk-anchor connectivity | AUTHORING / physical topology validation | Uses `NavMesh.CalculatePath` and `SamplePosition`; this is module authoring validation, not colonist policy or a person-route authority. |

### Personnel answers before B1

- **Why does Bob go to Farm?** `ColonistBrain` observes a current scheduled Work obligation; `ColonistTargetResolver` resolves Bob's canonical `WorkforceManager` assignment to the Farm workplace activity. That is policy and employment, not routing.
- **How is Bob's route selected?** Today `ColonistActivityRunner` asks `ColonistMotor` to move directly to the Farm activity binding's approach anchor. The motor samples and calculates the NavMesh path. There is no domain-level `PersonnelRoutePlan`.
- **What moves Bob?** `ColonistMotor` physically moves the `NavMeshAgent`; `ColonistActivityRunner` begins the facility-owned activity only after motor arrival.
- **Food, Sleep, and OffDuty?** The same activity-approach path is used after Brain/manager offer policy selects a target. Local facility reservation and choreography remain in the activity/facility layer.
- **MobileDuty?** `ColonistBrain` directly invokes the motor and immediately reports Working, so even physical arrival is not currently a prerequisite for the Working state.
- **Porter and emergency Alice work excursions?** Logistics chooses freight work; `WalkingFreightRunner` independently commands the motor. Alice's Brain only knows an authorized Work excursion and cargo completion, which is the correct policy boundary to preserve.

## Current Freight Routing Ownership

| Responsibility | Current owner / location | Finding |
|---|---|---|
| Demand publication | `LogisticsStockComponent.SimulationTick` → `FreightLogisticsManager.Publish` | Consumer stock policy opens a stable `FreightOrder` when on-hand is at/below reorder threshold. |
| Source selection | `FreightLogisticsManager.FindBestCandidate` | Iterates all registered `LogisticsStockComponent` supplies, excluding the requester and Consumer-only stocks. |
| Carrier selection | `FreightLogisticsManager.FindBestCandidate` | Iterates registered `WalkingFreightCarrierComponent` instances and filters by current duty/work excursion eligibility. |
| Useful quantity | Candidate construction | Correctly caps by uncovered demand, carrier remaining capacity, source available inventory, and destination free capacity after other commitments. |
| Route viability | `FreightLogisticsManager.TryGetFullTripDistance` | Incorrectly coupled to `UnityEngine.AI`; it directly calculates `carrier current → source` and `source → destination` NavMesh paths. |
| Distance | `FreightLogisticsManager.TryGetPathDistance` | Sums NavMesh corners for each leg. This is a second pedestrian estimator outside Personnel Routing. |
| Candidate ranking | `Candidate.Compare` | Confirmed ratio-first: descending `quantity / distance`, then distance, then stable key. This does not implement quantity-first lexicographic ranking. |
| Reservation | `FreightLogisticsManager.AcceptCandidate` | Correctly reserves source stock only after a candidate and carrier have been accepted, immediately before job creation/assignment. |
| Freight allocation/job identity | `FreightOrder.Id` and `FreightDeliveryJob.Id` | Current demand and job IDs are preserved in logs, but there is no separate route/allocation model. |
| Pickup | `FreightLogisticsManager.TryPickup`, called by `WalkingFreightRunner` on arrival | Atomically transfers reserved source inventory into carrier cargo. |
| Delivery | `FreightLogisticsManager.TryDeliver`, called by `WalkingFreightRunner` on arrival | Atomically transfers available carrier cargo into destination inventory. |
| Pre-pickup failure | `CancelBeforePickup` | Releases the owned reservation, decrements `Committed`, cancels the job, and clears carrier state. |
| Post-pickup failure | `BlockJob` | Leaves cargo physically on the carrier and schedules retry; this invariant must be preserved. |
| Emergency Alice excursion | `FreightLogisticsManager.DispatchEmergencyExcursions` → carrier/Brain authorization → freight runner | Logistics owns why/what/from/how-much/where; Brain sees only an authorized Work excursion; the runner currently owns independent pickup/dropoff navigation. |

### Logistics answers before B1

- **Why does Cafeteria request Food?** `LogisticsStockComponent` owns the Consumer policy; the stock component publishes to `FreightLogisticsManager` below the reorder threshold.
- **Who chooses Farm as source?** `FreightLogisticsManager.FindBestCandidate` chooses among registered supplies using the current ratio-first candidate score.
- **Who decides how Dana reaches Farm?** The manager uses its own direct NavMesh calculation for viability/distance, while `WalkingFreightRunner` independently calls the motor. These are duplicate routing seams.
- **What transfers the Food?** `FreightLogisticsManager.TryPickup` transfers reserved Food from Farm inventory to Dana's `InventoryComponent`; `TryDeliver` transfers it from Dana to Cafeteria. Physical custody is conserved by `InventoryComponent.TransferOwnedTo` / `TransferAvailableTo`.

## Known Legacy Transport Dependencies

The older passenger/freight contract architecture remains in the repository but is not used by the B1 P4b fixture:

- `LogisticsManager` arbitrates legacy transport vehicles, passenger contracts, and legacy freight demands.
- `ContractManager` owns `TransportContract` lifecycle.
- `TransportExecutorComponent` executes ship pickup/loading/delivery through `ShipMovementComponent`.
- `ShipCrewDutyComponent` and `StaffingComponent` implement older ship staffing/duty behavior.
- Sprint A/A2 flight code (`ShuttleVoyageComponent`, route planner, docking components) is independent 3D shuttle flight work and is not a B1 passenger/freight authority.

B1 must use `FreightLogisticsManager` and canonical `WorkforceManager`; it must not route the modular fixture through this legacy stack or add a class named `LogisticsManager`.

## Migration Risks

1. **ActivityRunner is a package boundary.** Its direct motor calls currently serve ordinary Work, Food, Sleep, OffDuty, and sequences. B1 needs a narrow route-planning integration without moving reservation or choreography policy into Personnel Routing.
2. **Motor arrival is global.** `ColonistMotor.Arrived` / `MoveFailed` are shared by ActivityRunner, freight, MobileDuty, and legacy transit. The new `PersonnelRouteRunner` must not double-subscribe or let unrelated listeners complete the wrong work.
3. **Activity interruptions are policy-owned.** Critical Hunger, Sleep, Work end, and work-excursion authorization can stop an activity while travel is in progress. Routing failure must be observable without teaching Brain NavMesh details.
4. **Freight custody is critical.** Pre-pickup failure may release a reservation; post-pickup failure must preserve cargo and block rather than cancel or duplicate inventory.
5. **Scene lifecycle order is not guaranteed.** Managers and per-colonist route runners need explicit runtime resolution while still detecting duplicate authorities in validation.
6. **Current tests are sparse for P4b.** The project testing policy favors stable owner-level tests; physical NavMesh traversal and scene wiring remain human acceptance concerns.
7. **Human acceptance boundary.** NavMesh movement, facility choreography, scene wiring, and freight custody remain Play Mode checks. This resumed execution follows the project's exploration testing guidance and the user's prior instruction to leave gameplay testing to their Play Mode run.

## Ticket Results

### B1-T00

Commit: `6f55f8e9` (`B1-T00 document current routing ownership`)

Files: `docs/B1_IMPLEMENTATION_REPORT.md`

Tests: No new test (characterization only). Historical targeted build results from the prior agent are not re-run in this execution. No Unity or gameplay tests were run.

Findings:

- Confirmed `FreightLogisticsManager` directly imports `UnityEngine.AI` and calculates both trip legs.
- Confirmed `WalkingFreightRunner` directly commands `ColonistMotor.MoveTo` for pickup and dropoff.
- Confirmed candidate ranking is quantity/distance-first rather than quantity-first.
- Confirmed Work/Food/Sleep/OffDuty facility approach is centralized only inside `ColonistActivityRunner`, while MobileDuty and freight bypass it.
- No production behavior changed.

### B1-T01

Commit: `cb9ad11f` (`B1-T01 add central personnel route planning`)

Files:

- `Assets/Scripts/ColonyPrototype/People/Routing/PersonnelRoutePlan.cs`
- `Assets/Scripts/ColonyPrototype/People/Routing/PedestrianRouteProvider.cs`
- `Assets/Scripts/ColonyPrototype/People/Routing/PersonnelRoutingManager.cs`
- `Assets/Tests/EditMode/PersonnelRoutingManagerTests.cs`
- Unity metadata for the new folder and files

Tests:

- Added 7 focused EditMode owner tests covering one-Walk-leg success, NoRoute, useful distance, hypothetical start, policy independence, deterministic repeated plans, and no B1 Shuttle leg.
- The prior agent reported targeted project compilation; it was not repeated after the partial T02 edits.
- Unity Test Runner was not started. No test is claimed as executed in this resumed work.

Design decisions:

- A route plan contains only person, final destination, ordered physical legs, estimated distance, and a stable diagnostic identity. It contains no Brain/activity purpose or need policy.
- `PersonnelRoutingManager` is the only B1 planner. It delegates all reachability/distance to `IPersonnelRouteProvider` and creates exactly one `Walk` leg when a provider returns a complete route.
- `PedestrianRouteProvider` is the only new personnel NavMesh estimator. It samples the person's area mask, calculates a complete path, and returns a transient estimate; it does not retain `NavMeshPath` in gameplay route state.
- Hypothetical-start estimates are exposed for Logistics' future `carrier → source` and `source → destination` questions without coupling Logistics to Unity NavMesh.
- The B1 enum contains only `Walk`; another transport kind can be introduced when B2 implements it. No Shuttle request or behavior exists in this ticket.

### B1-T02

Commit: pending

Files:

- `Assets/Scripts/ColonyPrototype/People/Routing/PersonnelRouteRunner.cs`
- `Assets/Scripts/ColonyPrototype/People/ColonistBrain.cs`
- `Packages/com.asteroidcolony.interactions/Runtime/IActivityApproachRouter.cs`
- `Packages/com.asteroidcolony.interactions/Runtime/ColonistActivityRunner.cs`

Changes:

- Added a per-colonist route executor with route status, current leg, estimated distance, failure reason, and completion/failure events. It executes Walk through `ColonistMotor` and does not choose activities or policy.
- Connected ActivityRunner's ordinary activity and sequence approach movement through the package-neutral approach-router interface. Missing central routing now reports a route failure instead of falling back to a direct motor route.
- MobileDuty work now routes to its duty anchor and enters `Working` only after the route runner reports arrival. Critical need or shift-end cancellation stops an in-flight route.
- Food, Sleep, OffDuty, activity approaches, and work excursions already use ActivityRunner, so they now use the central seam when the scene supplies a route runner.

Verification: Source review and `git diff --check` only. No compile, automated test, or Unity run was performed. Manual NavMesh and activity parity remain pending in Unity.

### B1-T03

Commit: pending

Files:

- `Assets/Scripts/ColonyPrototype/Logistics/P4b/LogisticsRoutePlan.cs`
- `Assets/Scripts/ColonyPrototype/Logistics/P4b/FreightLogisticsManager.cs`
- `Assets/Tests/EditMode/FreightCandidateRankingTests.cs`

Changes:

- Added Logistics-owned `FreightAllocation`, `LogisticsRoutePlan`, and `WalkingCarrier` route legs. An accepted job retains the demand identity, selected source, carrier, useful quantity, and two estimated local walking legs.
- Removed NavMesh types and path calculation from `FreightLogisticsManager`; it now requests the carrier-to-source and hypothetical source-to-destination estimates from `PersonnelRoutingManager`.
- Candidate ordering now prefers larger useful allocations, then shorter combined route distance, then a stable scene/hierarchy identity. The existing useful quantity cap and reserve-on-accept lifecycle remain in place.
- Added one pure owner test for quantity-first, distance-second, stable-key ranking. The test is not run in this execution.

Verification: Source review, direct dependency search, and `git diff --check` only. No compile, automated test, or Unity run was performed. Physical freight traversal and inventory custody remain pending in Play Mode.
