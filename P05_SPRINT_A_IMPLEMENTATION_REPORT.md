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
