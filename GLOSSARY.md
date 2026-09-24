# Glossary

Terms are split so a planned type cannot masquerade as shipped code. Code names are in
backticks.

## Exists in current code/content

- **Colonist** — `ColonistAgent`, with `currentLocation`, transit fields, classes,
  skills, and an optional `EmploymentAssignment`.
- **Employment** — `EmploymentAssignment`; staffing commands validate changes.
- **Facility performance** — `FacilityPerformanceComponent` plus
  `IFacilityPerformanceProvider` channels.
- **Habitation** — `HabitationComponent`, which currently exposes capacity and a
  restfulness multiplier.
- **Inventory** — `InventoryComponent`, the quantity authority.
- **Freight order** — `FreightOrder`, an open request for a resource and quantity at a requester.
- **Freight allocation** — `FreightAllocation`, committed cargo quantity assigned to a source and complete route.
- **Logistics route leg** — `LogisticsRouteLeg`, one loaded-cargo movement segment between stock locations.
- **Walking freight service** — `WalkingFreightWorkService`, a workplace-owned provider that quotes and accepts walking work for one freight leg.
- **Work execution lease** — `WorkExecutionLease`, the generic hold/deferred-release token for work already committed to an owner.
- **Population consumption** — `PopulationResourceConsumer`, aggregate consumption
  from a habitation inventory with shortage state.
- **Interactable facility** — `InteractableFacility` in
  `Packages/com.asteroidcolony.interactions`; its embedded activities and sequences are
  the authoring unit for the extracted local interaction system.
- **Activity runner / motor / animation driver** — `ColonistActivityRunner`,
  `ColonistMotor`, and `ColonistAnimationDriver`; package runtime components that
  execute local movement and animation.
- **Contact rig** — optional `ContactRigDriver` integration with Animation Rigging.
- **Ship movement phase** — `ShipMovementPhase` on `ShipComponent` in the current
  repository.

## Proposed / working terms

- **Active worker** — a future presentation/runtime query combining employment, arrived
  location, shift, activity, and eligibility; do not treat this as a new authority yet.
- **Route resolver / Walk–Ship–Blocked** — the owner-stated strategic direction; the
  first implementation should earn only the seam the real commute needs.
- **Voyage authority** — a working refactor direction for consolidating ship movement;
  it does not lock custom 6DOF or queue policy.
- **Docking berth / holding slot** — authoring concepts to test during physical docking,
  not a final queue schema.
- **Workforce allocator** — a possible client of employment commands; no autonomous
  algorithm is locked.
- **Scenario definition** — a future bootstrap representation to derive from the
  slice that exists when New Game is built; no full field list is current.
- **Site plane / module sockets** — future content/presentation concepts only where an
  immediate consumer proves they are needed.

## Historical pre-install glossary

The older entries below are retained because packet links may reference them. Read them
as historical terminology unless the term appears in the current-code section above.

**Active worker** — assigned + physically present + shift active + activity `Working` + holds the role's class. Only active workers contribute to facility effects.

**Allocator** (`WorkforceAllocator`) — fills staffing targets by calling `Assign()`; a client of the employment API, never an owner. Skips pinned colonists.

**Anchor** (`LocationAnchor`) — a logical place a colonist or cargo can be. The base unit of "where".

**Arrival means arrived** — `currentLocation` changes only when the body has finished moving; transit origin/destination are separate fields.

**Attachment node** — a socket on a module where a corridor or another module connects (`ModuleSockets.attachmentNodes`).

**Berth / port** (`DockingPortComponent`) — one physical docking position at a station; Free / Reserved / Occupied / Closed.

**Blocked** — an explicit state with a human-readable reason (duty, voyage, pedestrian, converter). Never a silent fallback.

**Building definition** (`BuildingDefinition`) — content describing something the player can place: prefab, cost, build hours, footprint.

**Command** — a narrow validated method that mutates sim state and returns a result enum (`Assign() → AssignmentResult`). The only way UI changes anything.

**Conduit** (Phase 2) — a resource link between two inventories: build once, flow forever. The resource analogue of a corridor.

**Construction site** (`ConstructionSiteComponent`) — a temporary workplace with an inventory sized to the cost and an import policy; progresses only with materials present and Builders active.

**Content** — ScriptableObject data under `Assets/GameData`: resources, recipes, classes, skills, roles, shifts, effects, buildings, flight profiles, scenarios.

**LEGACY TRANSPORT TERM — `TransportContract`** — the pre-P4b committed freight/passenger obligation owned by `ContractManager`. New B2 work extends `FreightOrder`, `FreightAllocation`, and `LogisticsRoutePlan` instead.

**Corridor** (`TransitLinkComponent`) — an authored/built walkable link between two anchors with a traversal time. Disabled = closed = blocker.

**Demand / supply** (`FreightDemand`, `FreightSupply`) — published needs and offers from stock policies; cheap, uncommitted, vehicle-agnostic.

**Dock control** (`DockingControlComponent`) — a station's dock master: ports, holding slots, and the berth queue.

**Duty state** (`ColonistDutyState`) — the work-obligation phase (`ReleasedResting`, `ScheduledShift`, `Blocked`, `AcceptingNewWork`, `CompletingCommittedWork`, `ReturningHome`). One writer: the staffing reconcilers.

**Employment** (`EmploymentAssignment`) — workplace + role + shift. Exactly one per colonist; persists off-shift; mutated only via `StaffingManager.Assign/Unassign`.

**EVA link** — a temporary corridor from a construction site to the nearest module in range so builders can walk to it.

**Facility performance** (`FacilityPerformanceComponent`) — aggregates `IFacilityPerformanceProvider`s into `IsOperational` and per-effect multipliers. Consumers never learn why.

**Field / knowledge** (`ResourceField` / `SurveyKnowledge`) — the true resource distribution vs. what the colony believes. UI reads knowledge only.

**Flight profile** (`ShipFlightProfile`) — physical numbers for a ship class (mass, thrust, RCS, torque, speed caps, substep).

**Holding** — a voyage phase: station-keeping at a holding slot while queued for a berth.

**Lease** — `ShipComponent.ResponsiblePilot`: who is at the controls now. Temporary; distinct from employment. Also the movement-owner lease (`TryClaimMovement`) that says which system may move a ship.

**Ledger** (`ResourceFlowLedger`, `BlockedTimeLedger`) — a read model aggregating over game time for the UI; mutates nothing.

**Loiter** — a voyage destination kind with no berth (deposit work point, survey target).

**Module** — a facility prefab with `ModuleSockets`; the unit of construction and of the walkable base.

**Packet** — a folder of agent tickets with a README and a locked design; owns specific directories and one scene.

**Pinned** — a colonist excluded from the allocator; hand-managed.

**Read model** — Runtime code that observes sim events and exposes aggregates; the only source of numbers for panels.

**Reconciler** (`ColonistReconciler`, `PilotDutyReconciler`) — per-tick logic that turns employment + location + shift into activity and duty state.

**Route resolver** (`RouteResolver`) — walk if a corridor path exists; else ship if a personnel vehicle exists; else Unreachable. No third option.

**Scenario** (`ScenarioDefinition`) — data describing the starting colony; built through the construction completion path.

**Site plane** (`SitePlane`) — a placement plane (origin + normal). The base is plane 1; outposts are new planes. Corridors never cross planes.

**Socket** — a named transform on a prefab used by other systems (`ModuleSockets`).

**LEGACY stock policy** (`ResourceStockPolicyComponent`) — the pre-P4b per-resource import/export registration used by `LogisticsManager`. Modern P4b stock publication uses `LogisticsStockComponent`.

**Target** (`StaffingTarget`) — desired headcount per role per shift; what the allocator fills.

**Tick** — one advance of `SimulationManager`; 0.1 real seconds; runs tickables in priority order 100 → 200 → 300 → 400 → 900.

**Voyage** (`ShipVoyageComponent`) — the single authority for a ship's movement: Docked → Undocking → Cruise → RequestingBerth → Holding → Approach → FinalDocking → Docked, or Loitering / Blocked.
