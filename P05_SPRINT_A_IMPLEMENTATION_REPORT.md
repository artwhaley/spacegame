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

## A04 — Deterministic berth-to-berth voyage phase machine

### Checkpoint

- Sprint baseline HEAD: `e4104fc4ca6bd6f6fbe828aafdfa96769b657255`
- A04 start HEAD: `4ffb7c32` (A03 checkpoint).

### Implementation

- Added `ShuttleVoyageComponent` as the Shuttle root movement authority. It registers with `SimulationManager`, advances only from logical simulation hours converted to simulated seconds, integrates bounded substeps, and writes the root pose once per logical tick.
- Implemented observable Undocking, AlignDeparture, CruiseAccelerating, CruiseCoasting, FlipForBraking, CruiseBraking, Approach, DockingTurn, FinalDocking, Captured, Docked, and Blocked phases.
- Voyages validate the occupied source berth and authored route before reserving the destination. Invalid requests leave both pose and berth state unchanged. During flight the destination reservation is checked continuously and released on structural failure.
- Undocking preserves the captured attitude until the Shuttle probe reaches the source clearance point at low speed; the source berth is released only at that boundary.
- Cruise routes are consumed as world-space probe targets. Cruise waypoints progress without a stop requirement. The flip trigger includes conservative full stopping distance, predicted turn time multiplied by speed, and a tunable safety margin. Main braking thrust stays off during the inertial flip.
- Approach and final docking use bounded RCS acceleration and angular feedback. Capture requires the authored probe position, relative speed, angle, and angular-rate tolerances before solving the exact root pose and occupying the destination.
- Added reusable port capture-tolerance validation and inspector diagnostics plus context-menu debug requests for optional Port A/Port B references.

### Verification and files

- Added five focused EditMode tests covering request rejection, reserve-before-undock, a long multi-Cruise-point trip through odd-angle port transforms, flip/coast/braking and final backwards approach, capture rejection at speed/angle limits, and five alternating voyages without accumulated dock-pose drift or leaked reservations.
- Verification: the current `ColonyPrototype.Runtime` sources and `ColonyPrototype.Tests` sources compiled successfully with the Unity 6000.5.9f1 Roslyn response files. The EditMode test suite has not been executed; Unity Editor is already open on this project, so I did not launch a competing editor process.
- Files changed: `Assets/Scripts/ColonyPrototype/Vehicles/Flight/ShuttleVoyageComponent.cs` and metadata; the capture-check helper in `DockingPortComponent.cs`; `Assets/Tests/EditMode/ShuttleVoyageTests.cs` and metadata; and this report.
- Prefab/scene changes remain unstaged and excluded from this checkpoint. The Airlock/ Shuttle component wiring and human-authored node/RCS changes are still in the working tree.

### Limitations

- Synthetic test sources compile but still need EditMode execution and a visual Play Mode flight in `BobInSpace.unity`.
- No Rigidbody, collision response, obstacle-aware planning, crew gate, passenger/cargo behavior, or legacy transport integration was added.

## A05 — Scene catch-up and first real flight (in progress)

### Checkpoint and scene review

- A05 start HEAD: `17ea8093` (A04 checkpoint).
- Re-read the current `git status` and reviewed the human-authored `BobInSpace.unity`, `Shuttle.prefab`, and `Airlock.prefab` changes before editing. The scene also contains the new Shuttle-follow camera; the working tree includes authored Shuttle RCS markers and a thruster audio asset. These changes are preserved.
- The base `Shuttle.prefab` now has `ShuttleVoyageComponent` with `ShuttleFlightProfile.asset` assigned and its local docking probe reference wired. `Shuttle Variant` inherits this reusable setup. No legacy `ShipMovementComponent` is present on the Shuttle prefab.
- Both Airlock prefab ports already contain serialized docking/approach/clearance references. The confirmed co-located approach/clearance transforms remain unchanged.
- The user's mating-axis correction is now the reusable convention: the Airlock node's blue +Z faces outward and the Shuttle probe's blue +Z faces into the port, so the axes oppose when captured. Capture, approach, docking-turn, and final-pose calculations all use that convention.

### Starting berth and scene wiring

- The user confirmed the `nodeApproach` and `nodeClearance` transforms should remain together, and approved placing the Shuttle at Port A to establish its initial captured berth.
- Set the Shuttle scene instance to the mating root pose at Port A: root position `(-1.144696, 1.20681164, 79.34)` with 180° yaw. This places the probe at Port A's authored docking position with the two blue +Z axes opposing.
- Set Port A's scene-instance state to `Occupied` by this Shuttle's docking probe. The Shuttle scene instance now references Port A as `currentDock`, and both scene ports through the optional debug targets. Port B remains `Free`.
- The Shuttle and Airlock prefab assets retain reusable flight profile, probe, voyage, and port wiring. The berth state and specific port links are scene-instance overrides. Both Airlock node layouts are unchanged.
- `BobInSpace.unity` had no `SimulationManager`, so voyage requests could not advance. Added a scene-local manager at 1× with a 0.02 simulated-second step (50 Hz) so the Shuttle receives smooth low-compression movement updates. Added an on-screen panel with Port A/B dispatch buttons, phase/speed/berth state, and rejection/block reasons; trips begin when the opposite-port button is clicked.
- The camera was targeting the Shuttle prefab asset instead of the live scene instance. Rewired it to the scene transform, added a runtime fallback to the active Shuttle, and enabled right-drag orbit and wheel zoom alongside Q/E, W/S, and R/F.

### Verification and limitation

- Human acceptance: the user reports successful Play Mode travel checks at multiple play speeds. No automated tests or Unity Editor run were performed for these corrections. No A05 commit has been created.

## A06 — Shuttle RCS smoke visualization

### Implementation

- Added `ShuttleRcsSmokeController` to the reusable `Shuttle` prefab. It discovers the direct nozzle-marker children under the authored `RCS` transform and creates one lightweight Particle System per marker at runtime, so new direct-child nozzles are picked up without adding per-nozzle script wiring.
- `ShuttleRcsPulseController` now applies short linear and angular accelerations separated by inertial coasts. It starts a counter-pulse when stopping distance or angle requires braking. The normal main-engine forward burn remains the cruise acceleration path. `ShuttleVoyageComponent` buffers each actual RCS pulse for presentation, including pulses that start and finish within one rendered frame.
- `ShuttleRcsSmokeController` consumes those pulse events instead of repeating smoke on its own timer. Nozzle selection uses the authored blue +Z exhaust direction, opposite thrust direction, and torque from each nozzle's lever arm. A Play Mode preview action emits one burst from every authored nozzle while the Shuttle is stationary.
- Added a transparent smoke-puff texture and an HDRP Unlit transparent material derived from the project's existing HDRP FX sprite material. Each particle now receives its size, lifetime, velocity, and opacity through `EmitParams`; the running Particle System is never reconfigured by an Inspector edit. Puff size starts at 0.13 units, twice the midpoint of the last visible size range, with eight particles per burst. Larger puffs spawn farther outside the nozzle to avoid hiding inside the hull.
- Flight authority stays in `ShuttleVoyageComponent` and its `ShuttleFlightProfile`: acceleration, speed limits, pulse timing, and deadbands. The voyage publishes local acceleration directions and normalized pulse strength. `ShuttleRcsSmokeController` consumes those events and uses a separate `ShuttleRcsVfxProfile` for appearance only. The Voyage Inspector exposes flight settings; the Smoke Inspector exposes VFX settings and Preview Burst.
- Default VFX tuning aims for a short, narrow exhaust cone: 0.055 unit particle width, 0.085 second lifetime, 5 units/second exit speed, 7 degree cone half angle, 18 particles per pulse, and 0.85 opacity. Particles spawn along a short cone so the jet appears already formed, then expire quickly.

### Verification

- Automated verification: no permanent test added. Per `TESTING_IN_EXPLORATION_MODE.md`, this visual prefab behavior belongs to human Unity acceptance.
- Human Unity acceptance: open `Assets/BobInSpace.unity`, press Play, select the live Shuttle, click Preview Burst, and tune Jet Width, Particle Lifetime, Exit Speed, Cone Half Angle, and Particles Per Pulse on the Smoke component. Confirm the jet is brief and directional with no Unity duration warning. Launch a trip and tune pulse duration and coast spacing on the Voyage component; use the separate Save VFX Profile and Save Flight Profile buttons to retain chosen values.
- Unity Editor and Play Mode were not run here; the user performs the project's visual acceptance check.
