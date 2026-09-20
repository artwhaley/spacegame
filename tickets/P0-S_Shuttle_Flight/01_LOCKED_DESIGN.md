# P0-S Locked Design

---

## 0. The clock (prerequisite content change)

Shuttle flight is authored in **game-seconds** (1 game-hour = 3600 game-seconds).
For flight to be watchable, `SimulationManager.gameHoursPerRealSecond` must drop from
`1` to a value in `[1/60, 1/30]` (one game-hour per 30–60 real seconds; a day is 12–24
real minutes at 1×, comparable to Banished/RimWorld). `tickIntervalSeconds` stays
`0.1`. Nothing else in the sim cares — every rate is already per game-hour.

At 1/60 and 1×: one tick = 6 game-seconds. At 10×: 60 game-seconds per tick. The
integrator substeps accordingly (§2).

---

## 1. Docking ports and dock control (`World/Docking/`)

### `DockingPortComponent : MonoBehaviour`

Child of a station GameObject. One per physical berth.

```csharp
public enum DockingPortState { Free, Reserved, Occupied, Closed }

public class DockingPortComponent : MonoBehaviour
{
    public string displayName;
    public DockingControlComponent control;        // parent dock master; auto-found in Awake
    public Transform berth;                        // final captured pose (position + forward = port axis out of the station)
    public Transform approach;                     // waypoint on the port axis, ~3–5 ship lengths out
    [Min(0.1f)] public float captureDistance = 0.3f;
    [Min(0.1f)] public float captureSpeed = 0.5f;  // game-m per game-s
    [Range(1f, 30f)] public float captureAngleDegrees = 5f;

    [SerializeField] private DockingPortState state;
    [SerializeField] private ShipComponent occupant;   // Occupied or Reserved holder

    public DockingPortState State { get; }
    public ShipComponent Occupant { get; }
    public bool IsOpen => isActiveAndEnabled && state != DockingPortState.Closed;
    // Disabling the component = Closed. Occupant (if any) stays docked; no new grants.
}
```

### `DockingControlComponent : MonoBehaviour` (the dock master)

Sibling of a station's `LocationAnchor`. Exactly one per dockable anchor. Registers in
a static `DockingControl.ForAnchor(LocationAnchor)` registry (pattern: `ShipComponent.Ships`).

```csharp
public enum BerthRequestOutcome { Granted, Queued, Denied }
public enum BerthDenyReason { None, NoControlAtDestination, AllPortsClosed, ShipNull, AlreadyHoldsBerth }

public struct BerthRequestResult
{
    public BerthRequestOutcome outcome;
    public DockingPortComponent port;    // Granted only
    public int queuePosition;            // Queued only, 0-based
    public BerthDenyReason denyReason;
}

public class DockingControlComponent : MonoBehaviour
{
    public LocationAnchor station;
    public List<DockingPortComponent> ports;           // authored; validated non-empty at startup
    public List<Transform> holdingSlots;               // station-keeping poses for queued ships; ≥1

    [SerializeField] private List<BerthRequest> queue;  // [Serializable] { ShipComponent ship; int priority; float requestedHour; }

    public IReadOnlyList<DockingPortComponent> Ports { get; }
    public IReadOnlyList<BerthRequest> Queue { get; }

    /// Idempotent. Re-calling for a queued ship returns its current position.
    public BerthRequestResult RequestBerth(ShipComponent ship, int priority);
    /// Explicit cancel; safe if not queued.
    public void CancelRequest(ShipComponent ship);
    /// Marks the ship's reserved port Occupied. False if the ship holds no reservation here.
    public bool TryOccupy(ShipComponent ship, DockingPortComponent port);
    /// Frees the port; then grants the head of the queue. Called on undock complete.
    public void ReleaseBerth(ShipComponent ship);
    /// Holding pose assigned to a queued ship (slot index = queue position mod slots).
    public Transform HoldingSlotFor(ShipComponent ship);
    /// Validated: which port the ship currently holds (Reserved or Occupied), or null.
    public DockingPortComponent PortHeldBy(ShipComponent ship);
}
```

Grant policy (fixed): on `RequestBerth` or `ReleaseBerth`, walk the queue sorted by
**priority desc, then requestedHour asc, then ship registration order**; grant the
first free open port to the head; a granted request leaves the queue and its port
becomes `Reserved`. A reservation is not time-limited in this packet.

A ship that is *already docked* at this control's port (startup, `initialDock`) is
adopted as `Occupied` in `Start()` by matching `ShipComponent.CurrentDock == station`
and nearest port to the ship pose; log if no port is near.

### Startup validation
Every `LocationAnchor` referenced by any `ShipComponent.initialDock`, `crewChangeBase`,
`ExtractionMissionController.unloadLocation`, or stock-policy-bearing inventory must
have a `DockingControlComponent` with ≥1 port. Missing → `SimulationLog` error and
`ReadinessHistory("dock.validation")`. Runtime does **not** auto-create ports (rule 9:
no silent fallback). The scene ticket adds them.

---

## 2. Flight model and guidance (`Vehicles/Flight/`)

### `ShipFlightProfile : ScriptableObject` (Content)

```csharp
[CreateAssetMenu(menuName = "Asteroid Colony/Ship Flight Profile")]
public class ShipFlightProfile : ScriptableObject
{
    [Min(1f)]    public float massKg = 20000f;
    [Min(0.1f)]  public float mainThrustN = 60000f;      // along +Z body
    [Min(0.1f)]  public float rcsThrustN = 8000f;        // any body axis, used for translation ≤ approach
    [Min(0.1f)]  public float rcsTorqueNm = 40000f;      // any body axis
    [Min(0.1f)]  public float inertiaKgM2 = 150000f;     // scalar, symmetric
    [Min(0.1f)]  public float cruiseSpeedMax = 40f;      // game-m/s
    [Min(0.1f)]  public float approachSpeedMax = 3f;     // game-m/s inside approach corridor
    [Min(0.01f)] public float undockPushDistance = 15f;  // game-m along port axis before cruise
    [Min(0.001f)] public float substepSeconds = 0.5f;    // integrator substep, game-s
    [Min(1)]     public int maxSubstepsPerTick = 200;
}
```

### `ShipFlightState` (struct, `[Serializable]`)

```csharp
public struct ShipFlightState
{
    public Vector3 position;          // world, game-m
    public Vector3 velocity;          // game-m/s
    public Quaternion rotation;       // body→world
    public Vector3 angularVelocity;   // rad/s, world
    // Last commanded actuation, for presentation and diagnostics:
    public Vector3 commandedForceBody;    // N, body frame (z = main engine, others = RCS)
    public Vector3 commandedTorqueBody;   // N·m, body frame
}
```

### `FlightIntegrator` (static)

`Step(ref ShipFlightState s, in ShipFlightProfile p, Vector3 forceBody, Vector3 torqueBody, float dtSeconds)`:
semi-implicit Euler; clamp force/torque components to profile limits; integrate
velocity then position, angular velocity then rotation (normalize). No drag, no
gravity. Pure; unit-tested for conservation when force = 0.

### `FlightGuidance` (static)

Two pure functions, both return body-frame commands:

```csharp
// Cruise: go to targetPosition and arrive with ~zero velocity. Uses "burn-flip-burn":
//   desired accel = clamp(k_p * toTarget - k_d * velocity) with braking-distance check
//   v_allowed(d) = sqrt(2 * a_max * d) so we never overshoot; capped at cruiseSpeedMax.
//   Rotation: point +Z body along desired acceleration direction when |desired| > rcs
//   capability; otherwise translate with RCS and keep current heading.
public static void Cruise(in ShipFlightState s, in ShipFlightProfile p, Vector3 targetPosition,
    out Vector3 forceBody, out Vector3 torqueBody);

// Approach / hold: reach a full pose (position + rotation) using RCS only, speed ≤ approachSpeedMax,
//   PD on position and on rotation error (quaternion → axis-angle).
public static void PoseHold(in ShipFlightState s, in ShipFlightProfile p, Vector3 targetPosition,
    Quaternion targetRotation, float speedCap, out Vector3 forceBody, out Vector3 torqueBody);

public static bool WithinCapture(in ShipFlightState s, Transform berth, DockingPortComponent port);
```

Gains are derived from the profile (`a_max = thrust/mass`, `alpha_max = torque/inertia`)
so content authors tune physical numbers, not PD constants.

---

## 3. The voyage authority (`Vehicles/ShipVoyageComponent.cs`)

`MonoBehaviour, ISimulationTickable, ISimulationTickPriority` at **400**. Replaces
`ShipMovementComponent` (deleted in S04). Required by `ShipComponent` via
`[RequireComponent]`.

```csharp
public enum VoyagePhase
{
    Docked, Undocking, Cruise, RequestingBerth, Holding, Approach, FinalDocking,
    Loitering,     // at a non-berth destination (deposit work point)
    Blocked
}

public enum VoyageDestinationKind { Berth, Loiter }

public enum VoyageRequestResult { Accepted, AlreadyUnderway, NotDocked, MovementOwnedByOther, MissingProfile, NullDestination }

public class ShipVoyageComponent : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
{
    public ShipFlightProfile profile;
    public Transform dockingProbe;   // ship-side port; pose that must coincide with berth. Defaults to transform.

    [SerializeField] private ShipFlightState flight;
    [SerializeField] private VoyagePhase phase;
    [SerializeField] private VoyageDestinationKind destinationKind;
    [SerializeField] private LocationAnchor destinationStation;
    [SerializeField] private Vector3 loiterPoint;
    [SerializeField] private ShipMovementOwner owner;
    [SerializeField] private DockingPortComponent reservedPort;
    [SerializeField] private int priority;
    [SerializeField] private string blockReason;
    [SerializeField] private float phaseEnteredHour;

    public ShipFlightState Flight { get; }
    public VoyagePhase Phase { get; }
    public LocationAnchor DestinationStation { get; }
    public DockingPortComponent ReservedPort { get; }
    public string BlockReason { get; }
    public int QueuePosition { get; }               // -1 unless Holding
    public bool IsDocked => phase == VoyagePhase.Docked;
    public bool IsLoitering => phase == VoyagePhase.Loitering;

    /// Validated command. Only from Docked (or Loitering). Claims ShipComponent movement lease for 'owner'.
    public VoyageRequestResult RequestVoyageToBerth(LocationAnchor station, ShipMovementOwner owner, int priority);
    public VoyageRequestResult RequestVoyageToLoiter(Vector3 worldPoint, ShipMovementOwner owner);

    /// Explicit cancel from Cruise/RequestingBerth/Holding: cancels queue entry, returns ship to
    /// nearest of {origin station, destination station} as a new Berth voyage under the same owner.
    /// Not allowed during Undocking/Approach/FinalDocking (finish the maneuver first).
    public bool TryAbortToNearestBerth();

    /// Presentation helpers (read-only): body-frame commanded actuation is in Flight.
}
```

### Phase machine (one writer; `ShipComponent.MovementPhase` is derived from this)

```text
Docked
  RequestVoyageToBerth/Loiter accepted
    → ShipComponent.TryClaimMovement(owner) must succeed, else MovementOwnedByOther
    → DockingControl(currentDock).ReleaseBerth is *deferred* until Undocking completes
    → phase = Undocking

Undocking      PoseHold toward (berth.position + berth.forward * undockPushDistance), RCS only
  reached      → DockingControl.ReleaseBerth(ship); ShipComponent.MarkDeparted(); phase = Cruise

Cruise         Guidance.Cruise toward:
                 Berth  : destination control's nearest holdingSlot (or first port approach if none) 
                 Loiter : loiterPoint
  within 2×undockPushDistance of target →
                 Berth  : phase = RequestingBerth
                 Loiter : phase = Loitering (owner polls IsLoitering)

RequestingBerth control = DockingControl.ForAnchor(destinationStation)
  null           → Blocked("no docking control at <station>")
  Granted(port)  → reservedPort = port; phase = Approach
  Queued(n)      → phase = Holding
  Denied(reason) → Blocked(reason)

Holding        PoseHold at control.HoldingSlotFor(ship), approachSpeedMax; re-poll RequestBerth each tick
  Granted        → reservedPort = port; phase = Approach

Approach       PoseHold to port.approach pose (rotation = look along -port.forward so probe faces berth)
  within capture tolerance of approach pose → phase = FinalDocking

FinalDocking   PoseHold to berth pose, speed ≤ port.captureSpeed
  Guidance.WithinCapture → control.TryOccupy(ship, port);
                            ShipComponent.TryCompleteArrival(owner, destinationStation) — must return true
                            snap flight to exact berth pose, zero velocities
                            ShipComponent.ReleaseMovement(owner)   ← the voyage releases the lease, not the caller
                            phase = Docked; ReadinessHistory("ship.docked")

Blocked        holds pose (PoseHold at current position), no queue entry, movement lease retained so nobody
               else can claim a stuck ship. Cleared only by TryAbortToNearestBerth or by the blocker
               disappearing (RequestingBerth re-polls when control becomes available).
```

Every phase change: `ReadinessHistory.Record("ship.phase", ship, from, to)`. Each tick,
after integration, `transform.SetPositionAndRotation(flight.position, flight.rotation)`
on the ship root — the one place the ship transform is written.

Missing/disabled `ShipVoyageComponent` or null `profile` → the requesting caller gets
`MissingProfile` and sets its own Blocked diagnostic (existing fail-closed rule).

### Integration
`dtGame = deltaGameHours * 3600`; `n = ceil(dtGame / substepSeconds)` capped at
`maxSubstepsPerTick` (then substep grows to fit). Guidance is evaluated once per
**substep**, not once per tick, so 10× speed doesn't degrade into overshoot.

---

## 4. Caller migration (S04)

| Caller | Before | After |
|---|---|---|
| `TransportExecutorComponent` | `MoveToward` loop + `BeginUndocking/MarkDeparted/BeginDocking/TryCompleteArrival/ReleaseMovement` | On entering a Traveling state: `voyage.RequestVoyageToBerth(target, Transport, contract.priority)`; each tick: if `voyage.IsDocked && ship.CurrentDock == target` → advance; if `voyage.Phase == Blocked` → `SetBlocked(voyage.BlockReason)`. No lease calls. |
| `ExtractionMissionController` | same, with `BeginDocking(null)` at the deposit | `RequestVoyageToLoiter(deposit.WorldPosition, Extraction)` → wait `IsLoitering` → extract → `RequestVoyageToBerth(unloadLocation, Extraction, priority)`. |
| `ShipCrewDutyComponent` | same for crew return | `RequestVoyageToBerth(crewChangeBase, CrewReturn, 10)`; release pilot on `IsDocked`. |

`ShipComponent` changes: `BeginUndocking`, `BeginDocking`, `MarkDeparted` become
`internal` and are called only by `ShipVoyageComponent`; `MovementPhase` maps from
`VoyagePhase` (Undocking→Undocking; Cruise/RequestingBerth/Holding/Loitering→InFlight;
Approach/FinalDocking→Docking). `ShipMovementComponent` is deleted; its two test
usages move to a `ShipFlightProfile` fixture with very high thrust ("instant" for tests).

### Watchable loading/unloading (S04, small)
`TransportExecutorComponent` gains `loadGameHoursPerUnit` and
`loadGameHoursPerPassenger` (default `0.002` h/unit ≈ 7 game-s, `0.01` h/passenger);
Loading/Unloading states now take time and expose `TransferProgress01`. Inventory
mutations still happen atomically at the *end* of the phase, so conservation rules are
unchanged. Cargo racks (`InventoryRackView`) already project the result.

---

## 5. Presentation (`Assets/Scripts/ColonyPrototype.Presentation/Ships/`, new asmdef)

Reads only. Never touches the ship root transform.

- **`ShipView`** — on a child "Mesh Root". `Update`: interpolates from the previous
  tick's `Flight` sample to the current one over `tickIntervalSeconds / speedMultiplier`
  real seconds (store last two samples; Runtime is 10 Hz, render is 60+). Hides the
  10 Hz step entirely.
- **`ThrusterSet`** — authored list of `{ Transform nozzle; Vector3 bodyDirection; bool isMain }`.
  Each frame, maps `Flight.commandedForceBody` / `commandedTorqueBody` to per-nozzle
  intensity (dot of nozzle direction with required force; torque via nozzle offset ×
  direction). Drives VFX Graph (HDRP) or ParticleSystem emission + a small emissive
  flash. Main engine plume length ∝ main thrust fraction.
- **`DockingPortView`** — port light Free=green / Reserved=amber / Occupied=blue /
  Closed=red; clamp arms animate closed on `Occupied`, open on release; approach
  corridor guide-lights pulse toward the berth while a ship is `Approach`/`FinalDocking`.
- **`HoldingPatternView`** — beacon blink on queued ships; small HUD tag "Holding — #2
  in queue" pulled from `QueuePosition` (UI can reuse later).
- **`VoyageTrailView`** — optional short fading ribbon on cruise; off by default.

Slickness checklist the acceptance ticket will look at: no visible stepping at 1×–10×;
the ship rotates *before* it accelerates (flip-and-burn is visible); it decelerates
into the approach corridor rather than stopping dead; RCS puffs match the actual
correction direction; capture snaps the clamps, not the ship.

---

## 6. Scene content (S04/S05)
- `DockingControlComponent` + ports on CommandPod (2 ports, 2 holding slots), Farm (1
  port, 1 slot), Water Processor (1 port, 1 slot). Berth/approach transforms placed
  ~3 ship lengths apart along a clear axis.
- Ice Asteroid: no ports (Loiter destination).
- `ShipFlightProfile` assets: `Shuttle.asset`, `MiningShip.asset` (heavier, slower).
- `SimulationManager.gameHoursPerRealSecond = 1/60`.
- Mesh roots separated from ship roots; `ShipView`, `ThrusterSet` on the mesh roots.
