# P0-A Working Design Reference

> **STATUS: REFERENCE / WORKING.** The staffing facade/refactor intent is useful. The
> walk-specific details must be re-earned by the current visible commute experiment.

The names and phase details below describe the historical proposal. Tickets must not
treat them as fixed until the active window and current code justify them.

---

## Part 1 — Epic G: `StaffingManager` split

### Principle

`StaffingManager` remains the **facade and the only `MonoBehaviour`**. It keeps its
static `Instance`, its `ISimulationTickable` registration, its tick priority `100`,
and every existing public member with identical signatures. Behavior is unchanged.

The extracted classes are **plain C# classes** (no `MonoBehaviour`, no static
`Instance`), constructed once in `StaffingManager.Awake`, and handed the shared
`List<ColonistAgent> knownColonists` by reference. They never discover colonists
themselves.

### Files

| New file (`Assets/Scripts/ColonyPrototype/People/`) | Moves from `StaffingManager` |
|---|---|
| `Staffing/ScheduleReportFormatter.cs` | `BuildDailyScheduleTable`, `CompareColonistsForSchedule`, `FormatWorkWindow`, `FormatOffDutyWindow`, `ValidDailyShift`, `FormatDailyBoundary`, `FormatLastDuty`, and the label helpers `AnchorLabel`, `ColonistLabel`, `ShipLabel`, `RoleLabel`. All become `internal static`. |
| `Staffing/EmploymentRegistry.cs` | `Assign`, `Unassign`, `ValidateAssignment`, `GetEligibleColonists`, `GetAssignmentCandidates`, `GetAssignedWorkers`, `CollectAssignedWorkers`, `CountAssigned`, `ReleasePreviousEmployment`, `ValidateAuthoredEmployment`, `Describe`. |
| `Staffing/CommuteBatcher.cs` | `CollectiveCommute`, `QueueCommute`, `FlushCommutes`, `CommuteChunkSize`, `HasActivePassengerContract`, `FindPassengerContract`, `HasPassengerInTransit`. |
| `Staffing/PilotDutyReconciler.cs` | `TickPilot`, `ReconcilePilot`, `FindReturningShip`, `ShipForEmployment`, `IsShipEmployment`, `HasActiveShipOperation`, `TickResponsibleShipDuties`. |
| `Staffing/ColonistReconciler.cs` | `TickColonist`, `ReconcileColonist`, `RouteHomeOrRest`, `EndDutyIfActive`, `SetActivity`, `EnsureStatus`, `HomeRestfulness`. Delegates to `PilotDutyReconciler` for ship employment and to `CommuteBatcher` for movement requests. |

`StaffingManager.cs` keeps: lifecycle (`Awake/OnEnable/OnDisable/OnDestroy`),
`SimulationTick` (orchestration order only), registration
(`RegisterColonist/UnregisterColonist/ReconcilePopulation/DiscoverShips/DiscoverColonists`),
`DumpDailySchedule`, `IsSafelyAtHome`, `UpdateCounts`, `SetDiagnostic`, `Log`,
`CurrentGameHour`, and one-line public forwarders for the employment API.

Target sizes: facade ≤ 250 lines; each extracted class ≤ 300.

### Ordering inside `SimulationTick` (unchanged, now explicit)

```text
1. pilotDutyReconciler.TickResponsibleShipDuties(delta, hour)   // ship work first (T07 rule)
2. for each colonist: colonistReconciler.Reconcile(colonist, hour)
3. for each colonist: colonistReconciler.Tick(colonist, delta, hour)
4. commuteBatcher.Flush()
5. UpdateCounts(); daily dump at day boundary
```

### Forbidden

- Changing any duty-phase write. `StaffingManager` (via `ColonistReconciler`) remains
  the **sole writer** of `ColonistDutyState`.
- Changing `AssignmentResult` values or `Assign` validation order.
- Introducing events, interfaces, or abstractions "for later." This is a move, not a
  redesign.

---

## Part 2 — Epic H: Walkable and shipped base

### Vocabulary

```text
LocationAnchor       logical place a colonist can be (exists)
TransitLink          an authored, two-way, walkable connection between two anchors
TransitGraph         static registry of enabled links; answers shortest walk path
RoutePlan            the resolver's answer: AlreadyThere | Walk | Ship | Unreachable
PedestrianTransit    a colonist's in-progress walk along a RoutePlan (sim authority)
Passenger contract   a colonist's in-progress ship trip (exists, unchanged)
```

### Strategic rule the code must express

> Two anchors joined by an enabled walk path never use a shuttle. Two anchors with no
> walk path always use a shuttle. If neither exists, the colonist is **Blocked** with a
> visible reason. The resolver never invents a third option.

Corridors are therefore an investment that removes recurring shuttle load; distant
modules are cheaper to place but tax pilots and vehicles forever. Nothing in Runtime
"balances" this — content and player layout do.

### New files (`Assets/Scripts/ColonyPrototype/World/Transit/`)

#### `TransitLinkComponent.cs` — `MonoBehaviour`

```csharp
public class TransitLinkComponent : MonoBehaviour
{
    public LocationAnchor endpointA;
    public LocationAnchor endpointB;
    [Min(0.01f)] public float traversalHours = 0.25f;   // authored; content decides walking cost
    public string displayName;

    public bool IsOpen => isActiveAndEnabled && endpointA != null && endpointB != null && endpointA != endpointB;
    public LocationAnchor Other(LocationAnchor from);    // null if 'from' is not an endpoint

    // OnEnable → TransitGraph.Register(this); OnDisable → TransitGraph.Unregister(this)
    // OnValidate → reject A == B, clamp traversalHours
}
```

Disabling the component = corridor closed (depressurized, under repair). It is not
"removed"; it is a **blocker** for routes that needed it (rule 9).

#### `TransitGraph.cs` — `static class`

```csharp
public static class TransitGraph
{
    public static IReadOnlyList<TransitLinkComponent> Links { get; }
    public static void Register(TransitLinkComponent link);
    public static void Unregister(TransitLinkComponent link);

    /// Dijkstra over open links by traversalHours. Returns false if no path.
    /// 'path' includes origin as [0] and destination as last. No allocation on the
    /// hot path beyond the caller-provided buffer.
    public static bool TryFindWalkPath(LocationAnchor origin, LocationAnchor destination,
        List<LocationAnchor> path, out float totalHours);
}
```

Only enabled links are registered (follow `ShipComponent.Ships` pattern exactly).

#### `RoutePlan.cs` — value types

```csharp
public enum RouteMode { AlreadyThere, Walk, Ship, Unreachable }

public enum RouteBlockReason
{
    None,
    NullEndpoint,
    NoWalkPathAndNoPersonnelVehicle,   // the strategic failure state
    LogisticsUnavailable                // ContractManager/LogisticsManager absent
}

[System.Serializable]
public struct RoutePlan
{
    public RouteMode mode;
    public RouteBlockReason blockReason;
    public float estimatedHours;        // Walk: sum of link hours. Ship: 0 (unknown; contracts own ETA)
    // Walk path is returned separately into a caller buffer to avoid per-plan allocation.
}
```

#### `RouteResolver.cs` — `static class`

```csharp
public static class RouteResolver
{
    /// Pure function of graph + fleet state. Never mutates anything.
    public static RoutePlan Resolve(LocationAnchor origin, LocationAnchor destination,
        List<LocationAnchor> walkPathBuffer);
}
```

Decision procedure, in this order, no exceptions:

```text
1. origin == null || destination == null           → Unreachable / NullEndpoint
2. origin == destination                            → AlreadyThere
3. TransitGraph.TryFindWalkPath(...) succeeds       → Walk (estimatedHours = totalHours)
4. LogisticsManager.Instance == null
   || ContractManager.Instance == null              → Unreachable / LogisticsUnavailable
5. any vehicle in LogisticsManager.Instance.TransportVehicles
   with personnelEnabled                            → Ship
6. otherwise                                        → Unreachable / NoWalkPathAndNoPersonnelVehicle
```

Step 5 deliberately checks *existence* of a personnel-capable vehicle, not current
availability. A busy shuttle is a queue, not a block; the contract system already
models waiting. Availability explanations stay in `TryGetAvailability` (T08).

#### `PedestrianTransitComponent.cs` — `MonoBehaviour, ISimulationTickable, ISimulationTickPriority`

Lives on the colonist prefab (`Assets/Person.prefab`) next to `ColonistAgent`.
Tick priority **400** (physical transport, same band as `TransportExecutorComponent`).

```csharp
public enum PedestrianTransitState { Idle, Walking, Blocked }

public class PedestrianTransitComponent : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
{
    [SerializeField] private PedestrianTransitState state;
    [SerializeField] private List<LocationAnchor> path;      // [0] = origin of current leg ... last = destination
    [SerializeField] private int legIndex;                   // index of the leg's origin in path
    [SerializeField] private float legHoursRemaining;
    [SerializeField] private string blockReason;

    public PedestrianTransitState State { get; }
    public LocationAnchor FinalDestination { get; }          // null when Idle
    public LocationAnchor LegOrigin { get; }                 // for presentation
    public LocationAnchor LegDestination { get; }
    public float LegProgress01 { get; }                      // for presentation
    public string BlockReason { get; }
    public bool IsWalking => state == PedestrianTransitState.Walking;

    /// Validated command. Fails (returns false, no mutation) if already walking to a
    /// different destination, if path.Count < 2, or if path[0] != colonist.currentLocation.
    public bool BeginWalk(IReadOnlyList<LocationAnchor> walkPath);

    /// Explicit cancellation: colonist stays at currentLocation (last committed anchor).
    public void Cancel();
}
```

Tick semantics — **arrival means arrived** (T03 rule):

```text
on BeginWalk:        colonist.BeginTransit(path[0], path[1], "walking"); legHoursRemaining = link.traversalHours
each tick:           legHoursRemaining -= delta
                     if the link for the current leg is no longer open → state = Blocked, blockReason = "corridor closed: <name>"
                       (colonist remains in transit; currentLocation is still the last committed anchor)
on leg complete:     colonist.CompleteTransit()                     // currentLocation = waypoint, truthful
                     if more legs: colonist.BeginTransit(next), legHoursRemaining = next link hours
                     else: state = Idle, path cleared
```

Each waypoint commit is what makes re-routing trivial: on Blocked, the next
`ColonistReconciler` pass sees a real `currentLocation` and re-resolves from there.
There is no "position between anchors" in the simulation; that is presentation's job
(`LegProgress01`).

The link for a leg is found via `TransitGraph.Links` lookup by endpoints each tick
(cheap; links are few). Do not cache link references across ticks — a destroyed link
must block, not NRE.

### Changes to existing files

#### `ColonistAgent.cs`
- Add `ColonistActivity.Walking` (append to the enum; do not reorder).
- Add `public PedestrianTransitComponent Pedestrian => GetComponent<PedestrianTransitComponent>();`
- No other change. `BeginTransit/CompleteTransit` are already correct.

#### `CommuteBatcher.cs` (created in T03, extended in T07)
`QueueCommute(source, destination, priority, colonist)` becomes:

```text
plan = RouteResolver.Resolve(source, destination, buffer)
AlreadyThere  → return
Walk          → colonist.Pedestrian.BeginWalk(buffer); requester sets activity Walking
Ship          → existing grouping into CollectiveCommute (unchanged)
Unreachable   → report to caller via out RouteBlockReason; caller writes Blocked duty state
```

Signature: `public RouteMode QueueCommute(LocationAnchor source, LocationAnchor destination, int priority, ColonistAgent colonist, out RouteBlockReason blockReason)`.

#### `ColonistReconciler.cs` (created in T04, extended in T07)
- A colonist whose `Pedestrian.IsWalking` is true is **left alone** exactly like a
  passenger in transit: duty state `ScheduledShift` or `ReturningHome` by destination,
  activity `Walking`, return.
- A colonist whose `Pedestrian.State == Blocked` gets `ColonistDutyState.Blocked` with
  `DutyBlocker = pedestrian.BlockReason`, then `Pedestrian.Cancel()` so the next pass
  re-resolves from the committed waypoint.
- When `QueueCommute` returns `Unreachable`, set `ColonistDutyState.Blocked` with a
  `DutyBlocker` string built from `RouteBlockReason`, activity `Idle`. Do not loop
  requesting.
- `IsSafelyAtHome` additionally requires `!Pedestrian.IsWalking`.

#### `PassengerCarrierComponent.cs`
- Boarding must refuse a colonist with `Pedestrian.IsWalking` (return false). A body
  cannot be in a corridor and a seat.

#### `ReadinessHistory` events (add, do not change existing)
`transit.walk.begin`, `transit.walk.leg`, `transit.walk.blocked`, `transit.walk.cancel`,
`route.unreachable`.

### Scene content (T07)
In `Assets/SpaceSim.unity`, under a new empty `Transit Links` root:
- `Corridor CommandPod-Farm`: `TransitLinkComponent` A = CommandPod anchor, B = Farm anchor,
  `traversalHours = 0.25`.
- Water Processor gets **no** link — it must remain shuttle-served. This is the packet's
  demonstration of the strategic split: farm workers walk, water workers fly.

### Forbidden in Epic H
- Any pathfinding beyond Dijkstra over authored links. No NavMesh, no grid.
- Presentation (bodies moving, animation). `LegProgress01` is exposed *for* it; nothing
  here consumes it.
- A `TransitLinkDefinition` ScriptableObject. Links are scene components until
  construction (P0-C) creates them; their cost lives in the corridor's
  `BuildingDefinition` later, not here.
- One-way links, capacity limits, pressurization state, travel speed by class/fatigue.
- Ship-mode changes. Passenger contracts, batching and dispatch are untouched.

---

## Tick-order picture after this packet

```text
100  StaffingManager
       ├ PilotDutyReconciler.TickResponsibleShipDuties
       ├ ColonistReconciler.Reconcile  (may call CommuteBatcher.QueueCommute → RouteResolver)
       ├ ColonistReconciler.Tick
       └ CommuteBatcher.Flush          (ship groups → ContractManager)
200  converters / consumers
300  policies / LogisticsManager arbitration
400  TransportExecutorComponent, ExtractionMissionController, PedestrianTransitComponent
```
