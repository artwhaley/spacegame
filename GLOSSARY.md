# Glossary

Terms as used in the code and the planning documents. Code names in backticks.

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

**Contract** (`TransportContract`) — a committed transport obligation (freight or passenger) owned by `ContractManager`. Created only after a vehicle wins arbitration.

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

**Stock policy** (`ResourceStockPolicyComponent`) — per-resource import/export rules on an inventory (thresholds, targets, priority, retain). Identifies resources, never sources.

**Target** (`StaffingTarget`) — desired headcount per role per shift; what the allocator fills.

**Tick** — one advance of `SimulationManager`; 0.1 real seconds; runs tickables in priority order 100 → 200 → 300 → 400 → 900.

**Voyage** (`ShipVoyageComponent`) — the single authority for a ship's movement: Docked → Undocking → Cruise → RequestingBerth → Holding → Approach → FinalDocking → Docked, or Loitering / Blocked.
