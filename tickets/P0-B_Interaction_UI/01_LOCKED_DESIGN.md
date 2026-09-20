# P0-B Working Design Reference

> **STATUS: INACTIVE DRAFT / REFERENCE.** The layout, technology, and panel details are
> not current contracts.

## 1. Camera (`Presentation/Camera/`)
`OrbitPanZoomCamera : MonoBehaviour` driven by `InputSystem_Actions` (add a `Camera`
map: Pan (WASD/edge), Orbit (RMB drag), Zoom (wheel), Focus (F)). Pivot-based: pivot on
the y = 0 plane, yaw/pitch/distance with clamps; `FocusOn(Transform)` eases the pivot.
Presentation-only; uses `Time.unscaledDeltaTime`.

## 2. Selection (`UI/Selection/`)
```csharp
public enum SelectableKind { None, Facility, Colonist, Ship, Deposit, Port, ConstructionSite }
public sealed class SelectionModel {           // one instance, owned by UiRoot
    public SelectableKind Kind; public Component Target;   // the sim component
    public event Action Changed;
    public void Select(Component c); public void Clear();
}
```
`Presentation/Selection/SelectableMarker : MonoBehaviour` sits on sim roots that have
colliders (`LocationAnchor` adds a BoxCollider already; ships get one on the root) and
names its kind + target component. `SelectionRaycaster` (UI) raycasts on click,
ignoring the `Environment` layer. `SelectionHighlight` (Presentation) swaps an emissive
rim material on the mesh root of the selected object.

## 3. UI shell (`UI/`)
- `UI.unity` → `UiRoot` (UIDocument, PanelSettings, theme USS from the art bible).
- Layout: top HUD strip; right selection panel (kind-specific content); bottom-left
  alert list; full-screen modals: HR, Colony Report, Build (P0-C).
- `HudView`: `Day N · HH:MM`, pause/1×/2×/4×/10× → `SimulationManager.SetPaused/
  SetSpeedMultiplier`; population; Food/Water on hand + `NetPerHour` arrow from the flow
  ledger.
- `AlertsView`: subscribes `ReadinessHistory.OnRecorded`; shows the last 8 events whose
  type matches an allow-list (`ship.docked`, `*.blocked`, `duty.exhausted`,
  `route.unreachable`, `colonist.death`); click → `SelectionModel.Select(subject)`
  when the subject resolves.

### Runtime seam added by U01
```csharp
public static event Action<HistoryEvent> ReadinessHistory.OnRecorded;   // fired inside Record()
public readonly struct HistoryEvent { public readonly float gameHour; public readonly string type, subject, detail, correlation; }
```

## 4. Reporting read models (Runtime `Reporting/`, `ISimulationTickable` priority 900)
All three are plain MonoBehaviours in `Managers.unity`, mutate nothing, and expose
read-only snapshots. Buckets are **game-hours**.

```csharp
public sealed class ResourceFlowLedger {
    // subscribes InventoryComponent.OnChanged for every inventory in InventoryComponent.Inventories
    // and late-registered ones; computes delta = onHand(now) - onHand(last) per (inventory, resource)
    public FlowSample Get(InventoryComponent inv, ResourceDefinition res);   // In24h, Out24h, NetPerHour, HoursUntilEmpty
    public FlowSample GetColonyTotal(ResourceDefinition res);
}
public sealed class BlockedTimeLedger {
    // consumes OnRecorded: opens an interval on any "*.blocked" / duty Blocked / ship.phase→Blocked|Holding,
    // closes it on the next transition for the same subject; also tracks Holding separately.
    public IReadOnlyList<BlockedInterval> For(string subject);                // reason, startHour, endHour (NaN if open)
    public float HoursBlocked24h(string subject); public IReadOnlyList<BlockedSummary> Top(int n);
}
public sealed class DutyReportBuilder {
    // pure over ColonistStatusComponent duty records + shift pattern
    public DutySummary Summarize(ColonistAgent c, float sinceHour);  // shiftsScheduled, shiftsWorked, hoursWorked, hoursScheduled,
                                                                      // lateArrivals, exhaustedExits, workplaceUnavailable, sentences[]
}
```
`DutySummary.sentences` are the human-readable lines, e.g.
`"Day 3 08:00–16:00 Farm Operator: worked 5.2h of 8h, left Exhausted at 13:12."`

## 5. Panels (`UI/Panels/`), all read-only + narrow commands
- **FacilityPanel** (target `FacilityPerformanceComponent` or its anchor): header
  (name, operational, blockers); **Inventory table** rows = resource: onHand /
  reserved / capacity / In24h / Out24h / Net/h / hours-until-empty; **Production**
  (`ResourceConverterComponent.ActiveRecipe`, `Progress`, `BatchProgress`,
  `BlockedReason`, multiplier); **Staffing** rows = role × shift: assigned/cap, active
  now, `[Open in HR]`; **Policy** (per resource import/export toggles → existing
  commands); **Blocked time (24h)** from the ledger.
- **ShipPanel** (`ShipComponent`): `VoyagePhase`, dock/port, `QueuePosition`,
  responsible pilot (link), `TryGetAvailability` text, cargo table with flows, current
  contract (type, from → to, priority, `TransferProgress01`), `[Cancel contract]` →
  `CancelCurrentContract()` with result text, blocked/holding time (24h).
- **ColonistPanel** (`ColonistAgent`): activity, `CurrentDutyState` + `DutyBlocker`,
  fatigue bar, needs bars (hidden until P0-D), employment (workplace/role/shift, link),
  home, location/transit, **Recent duty** = `DutyReportBuilder.sentences` (last 5),
  `[Open in HR]`.
- **DepositPanel / PortPanel**: remaining stock and extraction rate; port state,
  occupant, queue.

## 6. HR screen (`UI/HR/`)
Full-screen modal. Left: colonist table (name, classes, employment, state, fatigue,
`Blocked?`). Right: workplace tree (`FacilityPerformanceComponent.Facilities` +
ships' `crewStaffing`) → role → shift rows with `assigned / cap` and, after P0-C,
`target` and priority. Selecting a row shows **Candidates**: every colonist with
`AssignmentResult` + `reason` from `GetAssignmentCandidates`, eligible first, with
`[Assign]`; assigned workers show `[Unassign]`. Every command result is echoed in a
status line via `StaffingManager.Describe(result)`. Filter: eligible only / blocked
only / off-shift now.

## 7. Colony report (`UI/Report/`)
Sections: **Population** (count, avg fatigue, blocked count); **Resources** (colony
totals per resource: onHand, In24h, Out24h, Net/h, hours-until-empty, sparkline of 24
buckets); **Blocked time** (`BlockedTimeLedger.Top(20)`: subject, reason, hours, last
seen, click → select); **Duty failures** (all colonists' summaries filtered to
lateArrivals/exhausted/unavailable, as sentences); **Transport** (contracts
completed/cancelled 24h, avg open→assigned wait, holding hours per port).
Alert rules evaluated at 4 Hz from ledgers: Food/Water `HoursUntilEmpty < 12`;
colonist blocked > 2h; port holding > 1h; any site with materials missing > 8h (P0-C).

## Forbidden
Writing Runtime fields; per-frame rebuilds; `FindObjectsByType`; new gameplay commands;
uGUI; any panel computing an aggregate that a ledger should own.
