# P05 Sprint A Implementation Report

## A00 — Characterize the existing Shuttle

### Baseline

- Baseline HEAD: `e4104fc4ca6bd6f6fbe828aafdfa96769b657255`
- Existing user working-tree changes observed before A00: `Assets/Editor/Logistics/P4bModularSceneAuthoring.cs`, `Assets/GameData/Recipes/SimpleFarmFood.asset`, `Assets/Prefabs/Airlock.prefab`, `Assets/Readme.asset`, `Assets/bobandfriends_modular.unity`, untracked `Assets/BobInSpace.unity`, `Assets/BobInSpace.unity.meta`, and `Assets/_Recovery.meta`.
- A00 did not edit those files.

### Existing movement and state authority

- `SimulationManager` advances registered `ISimulationTickable` components in fixed logical simulation steps. Callers pass elapsed game-hours to movement; `ShipMovementComponent` itself is not ticked or scheduled.
- `ShipMovementComponent.MoveToward` is the existing movement authority. It moves the component's root with `Vector3.MoveTowards`, using a speed expressed per game-hour. It has no inertia, rotation, acceleration, or braking model.
- The root-position writer is `ShipMovementComponent.MoveToward(Vector3, float)`. Existing callers are `TransportExecutorComponent`, `ExtractionMissionController`, and `ShipCrewDutyComponent`. `ShipMovementComponent` does not write rotation.
- `ShipComponent` is the existing logical dock/travel authority: it owns `currentDock`, `movementPhase`/`traveling`, and the movement lease. Its `BeginUndocking`, `MarkDeparted`, `BeginDocking`, and `TryCompleteArrival` methods update logical state; they do not move the Transform.

### Shuttle and port authoring

- `Assets/Prefabs/Shuttle.prefab` has no legacy movement, ship-state, transport, passenger, extraction, or crew-duty components. It is a Shuttle root with authored visual prefab children and a docking child named `node_docking`.
- Shuttle body axes are local +Z forward and +Y up. The probe is at local `(0, 0, -5.35)` with local yaw 180°, so it sits behind the root and faces the rear mating direction.
- `Assets/Prefabs/Shuttle Variant.prefab` is a variant of Shuttle; it inherits the base prefab's authored content.
- `Assets/Prefabs/Airlock.prefab` has `nodeDocking` at local `(-3.25, 1, -23.91)`, yaw 180°. The in-progress authored `nodeApproach` and `nodeClearance` are both at approximately `(-3.25, 1, -42.21)`, yaw 180°. The author confirmed that sharing this pose is intentional; these transforms remain untouched.
- `Assets/BobInSpace.unity` contains one Shuttle prefab instance and two linked Airlock prefab instances. The second Airlock has an authored 3D rotation; port orientations must come from their authored nodes, not the line between stations.

### Legacy movement dependencies

- Runtime users of `ShipMovementComponent`: `TransportExecutorComponent`, `ExtractionMissionController`, and `ShipCrewDutyComponent`.
- Existing edit-mode fixtures add the legacy component in `ExtractionCompositionTests`, `PilotDutyTests`, `TransportExecutorTests`, and `UnifiedDispatchTests`.
- The Sprint A Shuttle prefab is isolated from this legacy path today. These systems remain in place for their current fixtures and other vehicles. The new voyage component must be the sole root-pose writer on the Sprint A Shuttle.

### A00 result

- Ambiguity resolved: current legacy root-transform writes originate in `ShipMovementComponent`; the Shuttle prefab in the authored test scene does not currently have that component or the legacy operation components.
- Tests: none added or run for this characterization-only ticket.
- Behavior changes: none.
- Files changed: `P05_SPRINT_A_IMPLEMENTATION_REPORT.md`.
- Limitations: real flight and visual acceptance have not been performed.

## A01 — Flight state, profile, and integrator

### Checkpoint

- Sprint baseline HEAD: `e4104fc4ca6bd6f6fbe828aafdfa96769b657255`
- A01 start HEAD: `fbcc7f7f` (A00 report checkpoint).

### Implementation

- Added `ShuttleFlightProfile` and the initial `ShuttleFlightProfile.asset` with main acceleration 8 m/s², RCS acceleration 2 m/s², 25 m/s cruise speed, 60°/s² angular acceleration, 90°/s angular speed, and 0.1 s integration substeps.
- Added authoritative state for world position/velocity, rotation, world-space angular velocity, and last applied accelerations.
- Added a bounded kinematic integrator with constant-acceleration translation, speed-cap crossing, bounded angular acceleration/rate, normalized quaternion updates, and deterministic internal substeps.
- Kept the integrator independent of scene objects, ports, routes, and runtime clocks. Callers supply simulated seconds and command accelerations.

### Verification and files

- Added 11 focused EditMode tests for inertial math, caps, normalization, determinism, grouping independence under constant commands, and zero elapsed time.
- Verification: the A01 runtime files and EditMode test source compiled with the installed Roslyn compiler against UnityEngine and NUnit references. Unity EditMode execution remains pending; the Unity Editor is already open on this project, and I have not launched a competing editor process.
- Files changed: `Assets/Scripts/ColonyPrototype/Vehicles/Flight/ShuttleFlightProfile.cs`, `ShuttleFlightState.cs`, `ShuttleFlightIntegrator.cs`, their Unity metadata, `Assets/GameData/Flight/ShuttleFlightProfile.asset` and its metadata, `Assets/Tests/EditMode/ShuttleFlightIntegratorTests.cs` and its metadata, and this report.
- Concurrent authoring preserved: `Assets/Prefabs/Shuttle.prefab` gained a user-authored `RCS` transform group and six nozzle markers during A01. That file is unstaged and excluded from this checkpoint.
- Limitations: the integrator is not yet attached to a Shuttle; no scene movement or visual acceptance is claimed.

## A02 — Guidance and route seam

### Checkpoint

- Sprint baseline HEAD: `e4104fc4ca6bd6f6fbe828aafdfa96769b657255`
- A02 start HEAD: `2be707fe` (A01 checkpoint).

### Implementation

- Added ordered `FlightRoute` and `FlightWaypoint` data, including Cruise, Approach, and Clearance semantics, an unobstructed direct-route factory, and progression checks that do not stop at ordinary Cruise waypoints.
- Added stateless guidance for main-burn alignment, bounded RCS commands, cruise-speed coasting, braking distance/angle, rotation-time prediction, shortest-path angular control, and RCS settling toward a point.
- Limited authored and commanded RCS acceleration to at most half of main acceleration. The starter asset remains at 25%.
- Guidance and route data do not inspect colliders, asteroids, or scene objects.

### Verification and files

- Added 10 focused EditMode tests for thrust alignment, RCS bounds, coasting, settling, 3D orientation convergence, route shape, Cruise progression, and stopping calculations.
- Verification: A01 and A02 runtime sources and both test files compiled with Roslyn against the installed UnityEngine and NUnit assemblies. Unity EditMode execution remains pending for the active-editor reason recorded above.
- Files changed: `Assets/Scripts/ColonyPrototype/Vehicles/Flight/FlightRoute.cs`, `ShuttleFlightGuidance.cs`, their Unity metadata, the RCS validation in `ShuttleFlightProfile.cs`, `Assets/Tests/EditMode/ShuttleFlightGuidanceTests.cs` and its metadata, and this report.
- Limitations: route creation is a direct two-endpoint route; voyage execution and scene wiring are not implemented yet.

## A03 — Docking ports and probe semantics

### Checkpoint

- Sprint baseline HEAD: `e4104fc4ca6bd6f6fbe828aafdfa96769b657255`
- A03 start HEAD: `b281f459` (A02 checkpoint).

### Implementation

- Added `ShuttleDockingProbeComponent` with a serialized node reference. Its editor validation wires the existing `node_docking` spelling as well as `nodeDocking`; runtime code reads only the serialized reference.
- Added `DockingPortComponent` with serialized docking, approach, and clearance nodes; Free/Reserved/Occupied/Closed state; single-Shuttle reservation/occupancy; idempotent Open/Close; and explicit configuration/capture-tolerance validation.
- Added `DockingPoseUtility` to solve Shuttle root position and rotation from an offset probe for arbitrary 3D target poses.
- Added the probe component to `Shuttle.prefab` and a port component with child references and starter capture tolerances to `Airlock.prefab`. `Shuttle Variant` inherits its probe from the base prefab.
- The two prefab files still contain user-authored RCS markers and approach/clearance nodes. Their new component wiring is saved in the working tree but excluded from the A03 commit so those in-progress human-authored asset changes are not swept into a code checkpoint.

### Verification and files

- Added 7 focused EditMode tests for arbitrary probe offsets/orientations, port reservation and occupancy ownership, release, closure, authored odd angles, and invalid configuration.
- Verification: all current flight runtime sources and A01–A03 test sources compiled with Roslyn against the installed UnityEngine and NUnit assemblies. Unity EditMode execution remains pending for the active-editor reason recorded above.
- Files changed: `Assets/Scripts/ColonyPrototype/Vehicles/Flight/ShuttleDockingProbeComponent.cs`, `DockingPortComponent.cs`, `DockingPoseUtility.cs`, their Unity metadata, `Assets/Tests/EditMode/DockingPortTests.cs` and its metadata, `Assets/Prefabs/Shuttle.prefab`, `Assets/Prefabs/Airlock.prefab`, and this report.
- Limitations: no voyage state machine or mechanical capture controller exists yet; the scene still has not been exercised in Play Mode.
