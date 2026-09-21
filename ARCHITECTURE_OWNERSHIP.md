# Architecture Ownership — Code-Grounded Reference

> **Status: FACT where a current type/path is named below.** This document records
> ownership that can be checked in the repository. Proposed presentation seams are
> labeled as working hypotheses elsewhere.

## Current runtime ownership

| Fact | Current owner | Evidence / boundary |
|---|---|---|
| Resource quantity | `InventoryComponent` | Inventory APIs own on-hand, reserved, and capacity state. |
| Facility operational state and effect multipliers | `FacilityPerformanceComponent` plus `IFacilityPerformanceProvider` implementations | Consumers ask for operational state/multipliers; recipe code does not count workers. |
| Canonical regular employment | `WorkforceManager` and `WorkAssignment` | The manager owns the zero-or-one assignment registry, validates offered roles and scheduled capacity, and reports facts without moving or starting colonist activities. |
| Legacy employment | `ColonistAgent.currentEmployment` / `EmploymentAssignment` and staffing commands | This remains the pre-canonical implementation for legacy code. Do not reconnect the canonical Synty colonist path to it. |
| Last arrived logical location | `ColonistAgent.currentLocation` | `BeginTransit`/`CompleteTransit` keep transit separate from arrival. |
| Population-level consumption | `PopulationResourceConsumer` on a habitation/inventory | It consumes aggregate resources and reports aggregate shortage state; it does not model personal needs. |
| Housing capacity/restfulness seam | `HabitationComponent.capacity` and `restfulnessMultiplier` | These are explicit fields; visual bed transforms do not automatically own capacity. |
| Ship movement phase and current dock | `ShipComponent` | The current repository uses `ShipMovementPhase`; a future one-voyage owner remains a working refactor direction. |
| Local interaction authoring | `InteractableFacility` in `Packages/com.asteroidcolony.interactions` | Activities, anchors/targets, animation segments, placement corrections, sequences, and optional contacts are facility-owned. |
| Local interaction execution | `ColonistActivityRunner`, `ColonistMotor`, `ColonistAnimationDriver` | The package drives local navigation/animation and reservation lifecycle; it does not own employment or production. |
| Optional character contacts | `ContactRigDriver` and Animation Rigging targets/constraints | Contacts are optional per character and should not be required for ordinary activity execution. |

## Dependency direction

The intended direction is `Content ← Runtime ← Presentation ← UI`: higher layers may
read lower-layer truth, while runtime must not depend on presentation or UI. The current
interaction package contains a reusable runtime/editor layer and is deliberately
game-content agnostic; it does not imply that the project has already implemented every
presentation or UI layer described by historical packets.

## Near-term handoff boundaries

- The Day-1 avatar/NavMesh work may move a visual body locally while leaving logical
  location and simulation state alone.
- The Day-2 facility cycle may consume active-worker truth and drive local movement and
  animation. Production remains owned by the existing recipe/inventory/performance
  path.
- The Day-3 boarding presentation may show a body approaching and disappearing into a
  ship, but `TransportContract`, `PassengerCarrierComponent`, and the responsible-pilot
  lease remain the transport truths.

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
