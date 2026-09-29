# Architecture Ownership — Code-Grounded Reference

> **Status: FACT where a current type/path is named below.** This document records
> ownership that can be checked in the repository. Proposed presentation seams are
> labeled as working hypotheses elsewhere.

## Current runtime ownership

| Fact | Current owner | Evidence / boundary |
|---|---|---|
| Resource quantity | `InventoryComponent` | Inventory APIs own on-hand, reserved, and capacity state. |
| Walking-module geography | `ModuleConnectionPoint` / `ModuleNavigationTopology` / `PedestrianRouteProvider` | Connection points own links; the topology is a derived region index that publishes only walkable links; fixed freight anchors use a shared-profile NavMesh query before labor selection. |
| Stock/import policy | `LogisticsStockComponent` | Validates a complete per-resource policy set against one shared inventory capacity; reads are snapshots and policy changes are atomic. |
| Parent freight demand and cargo custody | `FreightLogisticsManager` | Owns `FreightOrder` accounting, bounded `FreightAllocation` children, inventory claims, and final-delivery credit. Intermediate staging changes custody but does not satisfy demand. |
| Freight movement provider | `IFreightLegProvider` / `IFreightLegExecution` | Walking work services and `ShuttleManager` quote/accept one bounded leg and report correlated physical facts. The manager does not branch on concrete worker or vehicle classes for demand accounting. |
| Shuttle request, trip, and physical cargo | `ShuttleManager` / `ShuttleServiceComponent` | The manager owns request scheduling and manifests; the service owns payload custody and waits for destination acceptance. `ShuttleVoyageComponent` remains the sole movement owner. |
| Production mutation | `ResourceConverterComponent` + `InventoryComponent` | The converter owns recipe progress and facility throughput; the inventory atomically applies each input/output mutation against shared net capacity. |
| Facility operational state and effect multipliers | `FacilityPerformanceComponent` plus `IFacilityPerformanceProvider` implementations | Consumers ask for operational state/multipliers; recipe code does not count workers. |
| Canonical regular employment | `WorkforceManager` and `WorkAssignment` | The manager owns the zero-or-one assignment registry, validates offered roles and scheduled capacity, and reports facts without moving or starting colonist activities. |
| Legacy employment | `ColonistAgent.currentEmployment` / `EmploymentAssignment` and staffing commands | This remains the pre-canonical implementation for legacy code. Do not reconnect the canonical Synty colonist path to it. |
| Last arrived logical location | `ColonistAgent.currentLocation` | `BeginTransit`/`CompleteTransit` keep transit separate from arrival. |
| Population-level consumption | `PopulationResourceConsumer` on a habitation/inventory | It consumes aggregate resources and reports aggregate shortage state; it does not model personal needs. |
| Housing capacity/restfulness seam | `HabitationComponent.capacity` and `restfulnessMultiplier` | These are explicit fields; visual bed transforms do not automatically own capacity. |
| Legacy ship phase and modern shuttle movement | `ShipComponent` for legacy ships; `ShuttleVoyageComponent` for modern shuttles | The modern shuttle component owns its phase, route, current dock, and movement. It does not activate the legacy ship/transport authority. |
| Local interaction authoring | `InteractableFacility` in `Packages/com.asteroidcolony.interactions` | Activities, anchors/targets, animation segments, placement corrections, sequences, and optional contacts are facility-owned. |
| Local interaction execution | `ColonistActivityRunner`, `ColonistMotor`, `ColonistAnimationDriver` | The package drives local navigation/animation and reservation lifecycle; it does not own employment or production. |
| Optional character contacts | `ContactRigDriver` and Animation Rigging targets/constraints | Contacts are optional per character and should not be required for ordinary activity execution. |
| Personal hunger | `ColonistStatsComponent` | Hunger accumulation, thresholds, and physiological deltas live with the colonist's personal stats. |
| Public food discovery and meal stock | `FoodManager` and `FoodServiceComponent` | The manager exposes configured Eat opportunities and reserves/consumes the exact meal's owned inventory claim. It does not move the colonist. |
| Personal Eat decision | `ColonistBrain` | The explicit EatSeeking/Eating lifecycle decides when Hunger is satisfied and waits for physical release. |
| Personal biological planning | `ColonistStatsComponent` and `ColonistFreeTimePlanner` | Stats own thresholds/rates; the focused planner calculates protected rest and discretionary budget without commanding activities. |
| Discretionary activity authoring/discovery | `OffDutyComponent` and `OffDutyManager` | Facilities author OffDuty bindings; the manager reports nearest fitting opportunities without reserving them. |
| Canonical structured simulation history | `SimulationLogManager`, `SimulationLogEntry`, `SimulationLogField` | One bounded structured stream owns JSONL and Console rendering; `ReadinessHistory` is compatibility-only. |

## Dependency direction

The intended direction is `Content ← Runtime ← Presentation ← UI`: higher layers may
read lower-layer truth, while runtime must not depend on presentation or UI. The current
interaction package contains a reusable runtime/editor layer and is deliberately
game-content agnostic; it does not imply that the project has already implemented every
presentation or UI layer described by historical packets.

## Near-term handoff boundaries

- The modern playable scenes use `WorkforceManager`, `ColonistBrain`, and the interaction
  runner for local work and needs. Logistics never takes ownership of regular employment.
- Routine porter, producer-assist, and emergency employee freight have separate capacity
  policies. Once a worker accepts final-leg quantity Q, the execution finishes all Q across
  as many legal trips as needed, then honors a pending release.
- Logistics geography is checked by `PedestrianRouteProvider` at fixed anchors before a
  walking work service quotes a colonist. A cross-region itinerary uses the nearest
  reachable freight dock at each walking end; every registered shuttle can serve every
  pair of distinct registered docks. Dock identity is not a shuttle-pairing rule.
- Shuttle freight remains queued across a fleet/pilot wait and can complete an accepted
  quantity over multiple shuttle trips when the available hold is smaller than the job.
- The first player UI should read demand totals, child remainder, current leg/provider,
  inventory custody, retry/block reason, worker release state, and shuttle manifest state
  from these owners. It should not create a second stock or logistics ledger.

If an implementation needs a new authority, add it only with an observable that proves
the current owner is insufficient and record the choice in `DECISION_BACKLOG.md`.

## Canonical workforce ownership

The canonical Synty colonist path is being built separately from the legacy
staffing implementation. `ColonistIdentity` owns colonist identity;
`JobRoleDefinition` owns job-role content; and `WorkplaceComponent` describes
the regular work a facility offers. `WorkforceManager` now owns the
authoritative regular employment registry and `WorkAssignment` stores each
colonist's workplace, role, and `DailyShiftWindow`.

`ScheduledWorkOccurrence` is a derived query result, not a second authority.
The manager's scheduled capacity is a planning constraint; physical
`InteractableFacility` reservations remain separate runtime contention. The
manager does not wake, move, animate, or start activities for colonists.

Legacy employment remains in `ColonistAgent`, `StaffingManager`, and
`EmploymentAssignment` only for the old path. Canonical code must not add new
employment data there.

## Hunger and food ownership

The canonical personal Hunger owner is `ColonistStatsComponent`; the decision
owner is `ColonistBrain`; public Eat discovery belongs to `FoodManager`; and
physical reservation/execution belongs to `ColonistActivityRunner`. Eat
choreography remains owned by the Cafeteria's `InteractableFacility`. Nutrition
is derived from a genuinely active matching Eat, not from brain state, proximity,
or a reservation alone. A meal reserves an owned stock token and consumes that token once
when committed; cancellation releases only that meal's remaining claim. The older
`PopulationResourceConsumer` remains a separate aggregate consumer.

## OffDuty and biological policy

The current explicit priority is critical biological survival, current regular
Work, hard Sleep, ordinary Hunger, proactive Rest, then OffDuty. Ordinary Eat
is allowed during the 30-minute Work preparation window; newly-started Sleep
and OffDuty are not. Critical Hunger can request a physical Work exit or wake
Sleep. Discretionary planning intentionally ignores travel time and requires
authored Sleep recovery when a future shift must be protected. Optional
OffDuty staffing is derived from physically active matching Work, not from a
schedule entry alone.
