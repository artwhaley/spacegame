# Packet P0-C — Build & Staff (Days 14–19) — DRAFT, lock after P0-A/P0-S acceptance

Everything discussed so far about construction and the allocator, in one place. Turn
this into `01_LOCKED_DESIGN.md` + tickets once `TransitLink`, `EmploymentRegistry`,
docking ports and `ModuleSockets` exist on disk (names may shift slightly).

## Content (Day 14)
- `Regolith` `ResourceDefinition` (Discrete). `RockDeposit` prefab (`ResourceDeposit`).
- `Builder` `WorkerClassDefinition`; `Builder` `StaffingRoleDefinition` (min 1, cap 3,
  exertion 1.2); `ConstructionRate` `FacilityEffectDefinition` with curve 0/0.6/1.0/1.3.
- `BuildingDefinition : ScriptableObject`: `displayName`, `completedPrefab`
  (must carry `ModuleSockets`), `cost: ResourceAmount[]`, `buildHours`,
  `footprint: Bounds`, `category`, `isCorridor`, `maxCorridorLength`, `evaRange`,
  `unlocked = true`. Entries: Corridor, Habitat (4 beds), Farm, Water Processor,
  Dock Port Module (adds a port to an adjacent module — later).
- Command Pod stock policy: foreground import Regolith to a buffer (target 60).
- `ExtractionMissionController`: pick from the deposit registry within
  `maxRangeMeters` by highest uncovered foreground demand at the unload destination.

## Placement (Day 15)
- `SitePlane` (origin, normal, `planeId`); starting base = plane 1.
- `PlacementRules.Evaluate(def, pose, plane) → PlacementResult`
  {`Valid`, `Overlaps`, `OffPlane`, `CorridorTooLong`, `CorridorNotStraight`,
  `CorridorIntersects`, `NoNodeInReach`, `Locked`}.
  Modules: footprint bounds vs. all existing footprints; snap to the nearest free
  `attachmentNode` pair within snap radius else free placement at 15° rotation steps.
  Corridors: pick two nodes on different modules, same plane, straight segment ≤ max,
  no intersection with any footprint.
- `ConstructionManager.TryPlace(def, pose) → PlacementResult` (creates a site on
  `Valid`), `Demolish(anchor) → DemolishResult`.
- Presentation: `PlacementGhost` (mesh from `completedPrefab.meshRoot`, tinted
  valid/invalid, node snap preview). UI: `BuildMenu` lists definitions with cost and
  whether the colony currently holds the materials (informational only).

## Construction site (Day 16)
- `ConstructionSiteComponent` composed on a runtime-built GameObject:
  `LocationAnchor` + `InventoryComponent` (entries = cost, capacity = cost) +
  `ResourceStockPolicyComponent` (foreground import each cost item, priority 8) +
  `StaffingComponent` (Builder role, shift pattern Daily8HourShifts, one shift offered
  by default) + `FacilityPerformanceComponent` + `ModuleSockets` (evaSpawn only).
- Tick: `if (AllMaterialsPresent) progress += GetMultiplier(ConstructionRate) × delta / buildHours`.
  Blocked reasons: `AwaitingMaterials(list)`, `NoActiveBuilders`, `Unreachable`.
- EVA link: on creation, find the nearest module `attachmentNode` on the same plane
  within `evaRange`; spawn a `TransitLinkComponent` tagged `temporary`. Otherwise
  shuttle-served (needs a port? — **open**: sites get a temporary "EVA port" that
  accepts passenger contracts only, or builders are dropped at the nearest module and
  EVA from there. Recommend the latter: no ports on sites; shuttle unloads at nearest
  module with a port; EVA link from there).
- Completion: consume materials, instantiate `completedPrefab` at pose, copy
  `LocationAnchor.displayName`, register sockets/ports, transfer any staff employment
  to the new module if roles match (else unassign), remove temporary links, destroy
  site. For corridors: create the permanent `TransitLinkComponent` between the two
  nodes; `traversalHours` from length.

## Demolish (Day 17)
Cancel contracts touching its inventories, release reservations, unassign staff
(they route home), cancel voyages targeting its ports (ships abort to nearest berth),
remove links, refund a fraction of cost to the nearest inventory with capacity, destroy.

## Staffing targets and allocator (Day 18)
- `StaffingTarget { role, shiftId, target }` list on `StaffingComponent`; `workPriority`
  1–10 on `FacilityPerformanceComponent` (or the staffing component — decide at lock).
  Commands: `SetTarget(role, shift, n) → TargetResult`, `SetPriority(n)`,
  `SetPinned(colonist, bool)`.
- `WorkforceAllocator : ISimulationTickable` priority 90, in `Managers.unity`, toggle
  `enabledByPlayer`. Each tick (throttled to once per game-hour):
  1. For each workplace by descending priority, each role×shift below target: take
     eligible candidates from `EmploymentRegistry.GetAssignmentCandidates`, excluding
     pinned and colonists whose current workplace has higher priority; `Assign`.
  2. For roles above target: `Unassign` the most recently assigned non-pinned worker
     (never mid-flight; the registry already guarantees flights finish).
  3. Hysteresis: no reassignment of the same colonist within 4 game-hours.
- HR screen v2: target ±, priority slider, pin toggle, allocator on/off, "allocator
  changed this" badge on rows for one game-hour.

## Forbidden in P0-C
Power, research, module upgrades, terrain, vertical stacking, fabrication chains,
multiple planes (seam only), immigration.
