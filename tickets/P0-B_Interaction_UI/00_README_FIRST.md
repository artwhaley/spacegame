# Packet P0-B — Interaction, Panels, Reporting, HR

> **STATUS: INACTIVE DRAFT / REFERENCE.** This packet's panel and UI-stack decisions
> were written before a real management question existed. Keep the read-model reasoning;
> choose the first UI surface from observed play.

Read this, then the historical design reference, then your ticket in `02_TICKETS.md`;
neither is an active execution contract until the current window promotes it.

## What this packet delivers

The player can see and touch the colony: camera, selection, a UI Toolkit shell with
time controls and alerts, read-only panels for **colonist / facility / ship / colony**,
an **HR screen** for assigning jobs with exact validation reasons, **resource in/out
and flow rates**, and **human-readable reports** of time spent blocked, colonists
failing to do their jobs, and transport waiting.

## Why it's shaped this way

UI is a projection over existing truth plus a handful of narrow commands. The only
Runtime additions are (a) an in-memory event on `ReadinessHistory`, and (b) three
**read models** in a new `Reporting/` folder that aggregate over game time (flows,
blocked durations, duty summaries). Read models tick at priority 900, mutate nothing,
and are the single source for every number the UI shows — panels never compute
aggregates themselves.

## Ticket order (maps to `DAY_BY_DAY_PLAN.md` Days 7–11, 23)

```text
U00 camera + selection (Day 7)
U01 UI shell + time + alerts (Day 8)      ← adds ReadinessHistory.OnRecorded
U02 reporting read models (Day 9)
U03 facility panel ─┐
U04 ship panel     ─┼─ (Day 10)
U05 colonist panel ─┤
U06 HR screen v1   ─┘ (Day 11)
U07 colony report screen + alert rules (Day 23)
U08 acceptance
```

## Constitution points that bite here
- **Rule 1:** `UI` asmdef references Runtime and Presentation. Nothing references UI.
- **Rule 2:** panels bind to read-only accessors and read models. Grep for any
  assignment into a Runtime field from `UI/` = failure.
- **Rule 3:** every button calls a validated command and shows its result enum as text.
  Commands available today: `StaffingManager.Assign/Unassign`, `SimulationManager.
  SetPaused/SetSpeedMultiplier`, `ResourceStockPolicyComponent` mode commands,
  `TransportExecutorComponent.CancelCurrentContract`, `ExtractionMissionController.
  CancelMission`, `ContractManager.Cancel`. New commands are added by the packet that
  owns the system (P0-C adds targets/priority/pin), never by UI.
- **Rule 7:** panels enumerate `FacilityPerformanceComponent.Facilities`,
  `InventoryComponent.Inventories`, `ShipComponent.Ships`, `StaffingManager.Instance.
  KnownColonists`, `LogisticsManager.Instance.TransportVehicles`, `ContractManager.
  Instance.Contracts`. No `FindObjectsByType`.

## Rules for the agent
Same as P0-A. Additionally: UI Toolkit only (`UIDocument`, UXML, USS); no uGUI, no
IMGUI at runtime. Rebuild panels from data on a 4 Hz timer or on selection change, not
per frame. Strings for humans live in one `UiText` static class so they can be
localized later.
