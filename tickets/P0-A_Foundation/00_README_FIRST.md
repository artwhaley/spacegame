# Packet P0-A — Foundation: Staffing Split + Walkable/Shipped Base

Read this file, then `01_LOCKED_DESIGN.md`, then your ticket. Do not read other tickets
unless yours names them as a dependency.

## What this packet delivers

1. **Epic G** — `StaffingManager` (1,063 lines) is split into five single-responsibility
   classes behind an unchanged facade. Zero behavior change.
2. **Epic H** — Colonists move between logical locations by **walking** through authored
   corridor links or by **shuttle** when no walkable path exists. Which one applies is a
   consequence of how the player laid out the base. This is the strategic core of the
   game: corridors are built once and lock geometry; shuttle links cost pilots, fuel-of-
   time, and vehicle availability every single commute.

## Ticket order

```text
T00 characterize  ──► T01 G1 ──► T02 G2 ──► T03 G3 ──► T04 G4 ──┐
                                                                ├──► T07 H3 ──► T08 accept
                      T05 H1 ──► T06 H2 ────────────────────────┘
```

G tickets are strictly sequential (each edits the same file). H1/H2 can run in parallel
with G. H3 needs both branches.

## Architectural constitution (binding)

1. **Dependency direction is one-way:** `Content` ← `Runtime` ← `Presentation` ← `UI`.
   This packet is Runtime-only. Do not create Presentation or UI code.
2. **Views are read-only projections.** Not applicable here; do not add views.
3. **Mutations go through narrow validated commands** returning a result enum.
   `Assign() → AssignmentResult` is the template.
4. **One authority per fact.** `ColonistAgent.currentLocation` is the last *arrived*
   anchor. `ColonistAgent.currentEmployment` is employment. `TransportContract` is a
   ship-transport obligation. `PedestrianTransitComponent` is a walking obligation.
   Nothing caches a second copy.
5. **Simulation advances only from `SimulationTick(deltaGameHours)`.** No `Update`.
   Register in `OnEnable`, unregister in `OnDisable`, via
   `SimulationManager.RegisterTickable/UnregisterTickable`.
6. **Cross-cutting influences on facilities are `IFacilityPerformanceProvider` channels.**
   Not touched here.
7. **Registries, never scene scans, on hot paths.** `FindObjectsByType` only in
   `Awake` startup discovery. Follow the `ShipComponent.Ships` static registry pattern.
8. **New facility = composition + content.** A corridor is a `TransitLinkComponent`
   on a GameObject, not a `CorridorController`.
9. **Explicit state, no lying booleans.** Add enum values, not flags. A disabled link
   is a *closed* link and produces a blocker, never a teleport or silent fallback.
10. **File ceiling ~400 lines / one responsibility per class.**
11. **Runtime-authoritative state lives in `[SerializeField]` fields** with read-only
    accessors.
12. **Acceptance is observable behavior** in the scene at speed, plus one focused
    EditMode test per ticket as a design artifact. Running the Unity Test Runner is
    *not* a gate for this packet; compiling is.

## Rules for the agent

- Modify only the files your ticket lists under **May modify**. Create only the files
  under **Must create**. If you believe another file must change, stop and report why
  instead of changing it.
- Do not add: save/load, construction, UI, camera, allocator, power, needs, morale,
  NavMesh, pathfinding beyond the link graph, or any new ScriptableObject types.
- Do not rename or remove any public member of `StaffingManager`; external callers
  (`ColonistAgent`, `ShipCrewDutyComponent`, `StaffingComponent`, tests) depend on it.
- Match existing style: 4-space indent, `private` explicit, `[SerializeField]` for
  runtime state, `SimulationLog.Log` for diagnostics, `ReadinessHistory.Record` for
  transitions.
- Finish by reporting: files changed, public API added, and the one-paragraph
  observable behavior that proves the ticket in `Assets/SpaceSim.unity`.
