# Packet Index

This is a reference index, not an execution queue. The active queue is
`planning/CURRENT_WINDOW.md`; a packet is dispatchable only when that file promotes it.

## Current status

| Packet | Current reading |
|---|---|
| `tickets/P0-0_Skeleton/` | **SUPERSEDED / REFERENCE** — old skeleton ordering and socket assumptions. |
| `tickets/P0-A_Foundation/` | **REFERENCE** — staffing split remains useful; old walk specifics are not an API contract. |
| `tickets/P0-S_Shuttle_Flight/` | **REFERENCE / WORKING** — one voyage-authority intent retained; 6DOF, queue, and holding details are provisional. |
| `tickets/P0-B_Interaction_UI/` | **INACTIVE DRAFT / REFERENCE** — old UI plan; choose technology from the real management question. |
| `tickets/P0-P_Presentation_People/` | **SUPERSEDED / REFRAMED** — local interactable-facility prototype integration now leads. |
| `tickets/P0-C_Build_And_Staff/` | **DRAFT / REFERENCE** — trigger-gated; do not lock until the visible loop earns it. |
| `tickets/P0-D_Live_And_Die/` | **DRAFT / REFERENCE** — needs, death, housing, and scenario remain experiments. |
| `tickets/P0-E_Environment/` | **HORIZON / REFERENCE** — use only when the active slice demonstrates the need. |
| `tickets/P1-X_Exploration/` | **HORIZON / STUB** — wait for Phase 0 evidence. |

The extracted interactable-facility source is now local at
`Packages/com.asteroidcolony.interactions`; Day 2 must inspect that code rather than
reconstructing it from the old P0-P prose.

## Historical pre-install index

The packet table and dependency graph below preserve the old ownership/reasoning map.
Their old `written`/`locked` labels do not override the status table above.

Status legend: `written` = README + design + tickets existed at the time; `planned` =
named in the old roadmap; `done` = old acceptance ticket passed.

| Packet | Epics | Directories owned | Status | Days |
|---|---|---|---|---|
| `tickets/P0-A_Foundation/` | G (StaffingManager split), H (walk/ship routing) | `People/`, `People/Staffing/`, `World/Transit/` | written | 3–6 |
| `tickets/P0-S_Shuttle_Flight/` | S (ports, queueing, 6DOF voyages, ship presentation) | `Vehicles/`, `Vehicles/Flight/`, `Extraction/`, `World/Docking/`, `Presentation/Ships/` | written | 3–6, 12–13 |
| `tickets/P0-B_Interaction_UI/` | A (camera/selection/shell), I (panels), R (reporting), HR (staffing screen) | `UI/`, `Presentation/Camera/`, `Presentation/Selection/`, `Reporting/` | written | 7–11, 23 |
| `tickets/P0-0_Skeleton/` | Scene split, asmdefs, `ModuleSockets`, prefab conversion, clock | scenes, `Prefabs/`, asmdefs, `Core/ModuleSockets` | written (K01–K06) | 1–2 |
| `tickets/P0-P_Presentation_People/` | E (ColonistView, Facilities presenter port, pilot boarding walk) | `Presentation/People/`, `Presentation/Facilities/` | written (P01–P04) | 12–13 |
| `tickets/P0-C_Build_And_Staff/` | B (content, placement, sites, corridors, demolish), C (targets + allocator) | `Construction/`, `Content/BuildingDefinition`, `Presentation/Construction/`, `People/Staffing/WorkforceAllocator` | **draft** — lock after P0-A/P0-S acceptance | 14–19 |
| `tickets/P0-D_Live_And_Die/` | D (needs, death, housing), F (scenario, session frame, playtest) | `People/Needs/`, `Scenario/`, `UI/Session/` | **draft** — lock after Day 13 | 20–22, 25–26 |
| `tickets/P0-E_Environment/` | Environment generator, dust, scatter, lighting | `Assets/Editor/Environment/`, `Art/Environment/`, `Environment.unity` | written (README; tickets in `ENVIRONMENT_ASSETS.md`) | 24 |
| `tickets/P1-X_Exploration/` | Resource field, survey knowledge, scanners, scan view, discovery | TBD | stub — Phase 1 | — |

## Dependency graph between packets

```text
P0-0 skeleton ──► everything
P0-A ─┬─► P0-C (allocator needs EmploymentRegistry; sites need TransitLink)
      └─► P0-P (ColonistView needs PedestrianTransit)
P0-S ─┬─► P0-B ship panel (VoyagePhase, queue position)
      └─► P0-C (sites/modules need dock ports on prefabs)
P0-B ─┬─► P0-C build menu, HR v2
      └─► P0-D session frame, colony report
P0-P ─── independent of P0-B; both need P0-0's Presentation asmdef
P0-E ─── independent; only touches Environment.unity
```

## Rules of engagement

- A packet edits only the directories it owns and only the scene it owns
  (`Base.unity` → P0-C/P0-D via bootstrap; `UI.unity` → P0-B; `Environment.unity` →
  P0-E; `Managers.unity` → P0-0/P0-D).
- Cross-packet seams are added by the *owning* packet as a tiny, named change and
  listed here when they land:
  - `StaffingComponent.ActiveWorkersChanged` (event) — owner P0-A, consumer P0-P. Day 13.
  - `ReadinessHistory.OnRecorded` (event) — owner P0-B (Reporting), consumer UI. Day 8.
  - `ModuleSockets` — owner P0-0; consumers P0-S (ports), P0-P (slots), P0-C (nodes).
  - `ExtractionMissionController.deposits[]` — owner P0-C. Day 14.
- Lock the next packet's `01_LOCKED_DESIGN.md` only after the packets it depends on
  have passed acceptance; designs written against unbuilt seams drift.
