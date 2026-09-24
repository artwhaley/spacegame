# First UI Pass — Specification and Build Plan

**Status:** Specification. No code has been written against this document yet.
**Target:** `Assets/bobandfriends_modular.unity` (canonical Bob-and-Friends architecture).
**Technology:** Unity UI Toolkit (UIElements) runtime — `UIDocument` + UXML + USS + C# presenters.
**Grounded in:** the actual public surface of `WorkforceManager`, `WorkplaceComponent`,
`ColonistBrain`, `ColonistStatsComponent`, `ResourceConverterComponent`,
`FacilityPerformanceComponent`, `InventoryComponent`, `FreightLogisticsManager`,
`SimulationManager`, and `SimulationLogManager`. Every binding named below is verified
to exist today unless it appears in §4 (Prerequisites).

---

## 1. Purpose and scope

The simulation already produces everything a management UI needs to display, and the
runtime already emits structured, entity-identified events. What is missing is the
player's window into it. This first pass delivers four surfaces:

1. **Colony Overview** — the state of the settlement at a glance.
2. **Colonist** — one person: needs, intent, schedule, cargo, route, history.
3. **Facility** — one building: production, inventory, staffing, blockers, inbound freight.
4. **HR Manager** — the centerpiece: assign people to jobs and shifts, with real validation.

Plus shared chrome: a **command bar**, a **command palette**, and a **log console**.

Explicitly out of scope for this pass: construction/placement, save/load, scenario
bootstrap, audio, and the legacy `SpaceSim` stack. The UI reads the canonical
Bob-and-Friends runtime and never the legacy `ContractManager` / `StaffingManager` /
`PopulationManager` / `ColonistStatusComponent` path.

---

## 2. Design language — how "slick and excellent" is actually achieved

Four rules. They are load-bearing, not decoration.

### 2.1 One window grammar

Every window is the same object: a **frame** (title, entity chip, pin state, close),
an optional **tab strip**, a **body**, and an optional **history drawer**. New windows
are then a UXML file plus a presenter, never a new interaction model. This is what makes
the UI *extensible* rather than merely complete.

### 2.2 Explainability is a first-class feature

The audit found fault text scattered across components: `ColonistBrain.LastDecisionReason`,
`ResourceConverterComponent.BlockedReason`, `FacilityPerformanceComponent.BlockSummary`,
`PersonnelRouteRunner.LastFailureReason`, `FreightDeliveryJob` block reasons,
`WorkExecutionLease` release state. The UI's job is to collapse all of them into one
consistent affordance:

> **Any red, amber, or stalled state is hoverable and explains itself, in one sentence,
> with the runtime's own words.**

Concretely, a single `ExplainTooltip` manipulator takes a list of `(label, reason)`
pairs and renders a small card. A red chip with no explanation is a spec violation.

### 2.3 The UI is aware of simulation time

This is the detail that will make it feel professional:

- **Day-cycle chrome.** The root element's USS class tracks `SimulationManager.CurrentHourOfDay`
  (`night` / `dawn` / `day` / `dusk`). Borders and accent tints shift subtly. Cost: one class
  toggle per sample.
- **Speed-damped motion.** All USS transition durations and presenter tween lengths scale by
  `PresentationTime.SpeedFactor`. At 10× the UI animates normally; at 1000× transitions collapse
  to zero so panels do not strobe while the colony runs fast. The simulation already supports
  a 1000× multiplier with a 250-second debt cap, so this is not hypothetical.
- **Live values tick, but never jitter.** Refresh is sampled at a fixed real-time cadence
  (§3.4), not per simulation tick.

### 2.4 No art dependency

Vitals (hunger, fatigue, stimulation, relaxation, meal progress) are drawn with `Painter2D`
through `VisualElement.generateVisualContent`. Rings, arcs, and gauges need no textures, no
sprite atlases, and no import settings. The entire first pass can ship with zero new art
assets, which is exactly what a vertical slice needs.

---

## 3. Architecture

### 3.1 Assembly boundary

`ColonyPrototype.Runtime` must not reference UI. The dependency direction recorded in
`ARCHITECTURE_OWNERSHIP.md` is `Content ← Runtime ← Presentation ← UI`, and this is the pass
that finally creates the far end of it.

```
Assets/Scripts/ColonyPrototype/UI/ColonyPrototype.UI.asmdef
    references: ColonyPrototype.Runtime, Colony.Interactions.Runtime, UnityEngine.UIElements
```

Rules:
- UI may read Runtime. Runtime may not know UI exists.
- UI never calls `GetComponent` on gameplay objects. It reads read-models (§3.3).
- UI never mutates simulation state except through the documented command APIs
  (`WorkforceManager.Assign`/`Unassign`, `ResourceConverterComponent.TrySelectRecipe`,
  `SimulationManager.SetPaused`/`SetSpeedMultiplier`).

### 3.2 Object graph

```
UiRoot (UIDocument host)
 ├── CommandBar            pause / speed / clock / alert bell
 ├── WindowHost            draggable, pinnable, z-ordered window frames
 │    ├── ColonyWindow
 │    ├── ColonistWindow
 │    ├── FacilityWindow
 │    ├── HrWindow
 │    └── LogWindow
 ├── CommandPalette        Ctrl+K overlay
 └── ToastStack            transient validation + alert messages
```

`UiRoot` owns the `UIDocument`, the `PanelSettings`, and one `UiClock`. `WindowHost` owns
layout, drag, pin, and focus. Windows own nothing global.

### 3.3 The read-model seam — the important decision

Windows bind to **read-models**: plain C# objects that project runtime state into immutable
snapshots. This is the single highest-leverage choice in this document.

```
Runtime components ──> ReadModel Builder ──> Immutable snapshot ──> Window presenter
```

Why it matters here specifically:

| Benefit | Consequence for this project |
|---|---|
| UI never holds component references | No `GetComponent` in UI, no lifetime hazards during domain reload |
| Snapshots are plain data | **EditMode-testable with no scene and no Unity Test Runner dependency** — directly addresses the project's "tests are design artifacts, never run" problem |
| Projection is centralized | The entity-id logging work pays off once, not per window |
| Read cost is visible | One `SimulationTick`-free sampling path with obvious O(n) behavior |

A read-model is rebuilt on demand from `SimulationManager.Instance.CurrentGameHour` so the
same snapshot builder produces deterministic output for a given simulation time — which makes
golden-snapshot tests possible.

### 3.4 Refresh model

Three cadences, deliberately separate:

| Cadence | Trigger | Used by |
|---|---|---|
| **Fast** (10 Hz real time) | `UiClock.Schedule` | Command bar clock, open window headline numbers |
| **On demand** | Window open, tab change, entity selected, explicit refresh | All detail tables and lists |
| **Push** | `SimulationLogManager.EntryRecorded` | Log console, alert bell, history drawers |

Rationale: `WorkforceManager.IsGenuinelyOnDuty` and `IsActivelyStaffing` scan assignments and
call `GetComponent` internally. `FreightLogisticsManager.FindBestCandidate` is
O(services × supplies) per order. **The UI must not call those in a per-tick or per-frame
loop.** Fast-cadence bindings are restricted to cheap reads (`SimulationManager` scalars,
counts already computed by managers). Anything expensive is explicit-refresh only and is
annotated in the presenter with a comment naming the O() cost.

### 3.5 Selection seam

Nothing in the runtime can be selected today: `STATE_OF_THE_PROJECT.md` records zero
`Input.` and zero `Raycast` hits in runtime. The UI needs selection, and the camera will
eventually need it too, so it belongs in Runtime, not UI.

```
Assets/Scripts/ColonyPrototype/Presentation/Selection/
    ISelectable.cs            // marker + DisplayName + optional SelectionAnchor
    SelectionService.cs       // current selection + SelectionChanged event
    CameraSelectionRaycaster.cs // reads pointer, resolves ISelectable, publishes
```

`SelectionService` is a read-model input: `UiRouter` subscribes to `SelectionChanged` and
opens the matching window. The raycaster must ignore pointers over the UI
(`PanelSettings` + `UIDocument` will not do this for us when the pointer is over a
transparent full-screen panel — the raycaster checks
`uiRoot.panel.Pick(pointerPosition)` first).

---

## 4. Prerequisites (must land before the windows)

These are small, and two of them are things the recent architecture audit already flagged.

### P-1. A modern colonist registry
`PopulationManager` registers `ColonistAgent`, which is **not on the modern prefab**. There is
no runtime list of `ColonistIdentity`. Every window needs one.

Recommendation: add a single generic registry and use it here, rather than adding a fourth
hand-rolled static list (the audit found three: `LogisticsStockComponent.Active`,
`WalkingFreightWorkService.Active`, `InventoryComponent.Inventories`).

```
Assets/Scripts/ColonyPrototype/Core/ComponentRegistry.cs
    sealed class ComponentRegistry<T> where T : Component
        Active : IReadOnlyList<T>
        // registers in OnEnable, unregisters in OnDisable,
        // clears via RuntimeInitializeOnLoadMethod(SubsystemRegistration)
        // optional stable ordering by hierarchy key for jitter-free lists
```

Adopt it for the new registries now; retrofitting the existing three is optional follow-up.

### P-2. A workplace registry
HR assigns against `WorkplaceComponent` (that is the type that *offers* roles), but nothing
enumerates workplaces. `FacilityPerformanceComponent.Facilities` is performance-only and is
not every facility. Add `WorkplaceComponent` to the registry from P-1.

### P-3. `PanelSettings` + `UiRoot` shell
One `PanelSettings` asset with `scaleMode = ScaleWithScreenSize` (reference 1920×1080),
one GameObject with `UIDocument` + `UiRoot`. No `EventSystem` is required for runtime
UI Toolkit; the project is on Input System 1.20 and Unity 6000.5, whose `InputForUI`
backend feeds UI Toolkit directly.

### P-4. Theme assets
`theme.uss` (tokens), `base.uss` (primitives), `windows.uss`, `hr.uss`. Tokens as USS custom
properties so the whole look retunes from one file:

```css
:root {
  --surface-0: #0f1115;  --surface-1: #161a21;  --surface-2: #1e242e;
  --line: #2b3442;       --text-hi: #e8edf5;    --text-lo: #93a1b5;
  --accent: #4ea3ff;     --ok: #46c98b;  --warn: #e8b64c;  --bad: #e2604f;
  --radius: 10px;        --gap: 12px;
  --motion-fast: 90ms;   --motion-med: 180ms;   /* scaled by PresentationTime.SpeedFactor at runtime */
}
```

---

## 5. Window specifications

Each spec lists: purpose, layout, **exact bindings**, and interactions. If a value is not in
the binding column, the window is not allowed to display it.

### 5.1 Command Bar (always visible)

| Element | Binding | Notes |
|---|---|---|
| Clock + day | `SimulationManager.CurrentDayNumber`, `CurrentHourOfDay`, `SimulationTime.FormatTimestamp` | `D3 14:30` |
| Pause / play | `SimulationManager.paused`, `TogglePaused()`, `SetPaused(bool)` | |
| Speed | `speedMultiplier`, `SetSpeedMultiplier(float)` | Presets 1 / 10 / 60 / 300 / 1000 |
| Pace health | `SimulationDebtSeconds`, `PaceShortfallSeconds`, `AdmissionLimitHitCount` | Amber when the admission cap is engaging — the UI can now *show* a sim that cannot keep up |
| Alert bell | `SimulationLogManager.EntryRecorded` filtered to `Severity != "Info"` | Badge count; opens Log console filtered to Warning+ |
| Window buttons | — | Colony / Colonist / Facility / HR / Log |

The pace-health readout is worth calling out: those four telemetry scalars already exist and
are currently invisible. Surfacing them turns an invisible performance cliff into a visible
one.

### 5.2 Colony Overview

**Purpose:** answer "is the colony okay?" in one screen.

**Layout:** KPI strip → alert rows → resource ledger → logistics summary → live ticker.

| Block | Binding |
|---|---|
| Population | count of `ComponentRegistry<ColonistIdentity>.Active` |
| Assigned / unassigned | `WorkforceManager.Assignments.Count`; population minus that |
| On duty now | `WorkforceManager.IsGenuinelyOnDuty` per assigned colonist *(expensive: explicit refresh only)* |
| In distress | `ColonistStatsComponent.IsCriticallyHungry` / `IsExhausted` / `IsStarving` per colonist → clickable list |
| Resource totals | `InventoryComponent.Inventories` × `Entries` → `GetOnHand` summed by `ResourceDefinition` |
| Logistics state | `FreightLogisticsManager.Orders` (open/uncovered), `Jobs` by `FreightJobState`, `FreightLogisticsManager` blocked job reasons |
| Freight services | `WalkingFreightWorkService.Active` with `RoutineFreightEnabled` / `EmergencyFreightEnabled` / `ActiveExecutionCount` |
| Live ticker | last N of `SimulationLogManager.Entries` |

**Interactions:** every row is a link — a hungry name opens that `ColonistWindow`; a resource
opens a ledger detail; a blocked job opens its facility; a ticker line opens the Log console
pre-filtered to that `EventKey`.

### 5.3 Colonist Window

**Purpose:** "what is this person doing, why, and are they okay?"

**Layout:** identity header → vitals column → live intent → schedule strip → logistics → history drawer.

| Element | Binding |
|---|---|
| Name | `ColonistIdentity.DisplayName` |
| Employment | `WorkforceManager.TryGetAssignment` → `Workplace`, `Role.DisplayName`, `Shift` |
| Vitals rings | `Hunger`, `Fatigue`, `StimulationNeed`, `RelaxationNeed` vs `HungryThreshold`/`CriticalHungerThreshold`/`SleepyThreshold`/`RestPreferredThreshold`/`ExhaustionThreshold`, `StimulationNeedThreshold`, `RelaxationNeedThreshold` |
| Distress chips | `IsHungry` / `IsCriticallyHungry` / `IsStarving` / `IsSleepy` / `IsExhausted` / `NeedsStimulation` / `NeedsRelaxation` |
| Live state | `ColonistBrain.State` → chip; `LastDecision` + `LastDecisionReason` → **ExplainTooltip** |
| Doing right now | `ColonistActivityRunner.Phase`, `CurrentActivityId`, `ActiveFacility` |
| Route | `PersonnelRouteRunner.State`, `Status`, `CurrentLegEstimatedDistance`, `LastFailureReason` |
| Carrying | worker `InventoryComponent` entries (the freight path uses the colonist's own inventory) |
| Working for | `ColonistBrain.ActiveWorkExecutionLease` present → "work execution held by `<owner>`" |
| Shift strip | `TryGetCurrentOrNextShift` → `ScheduledWorkOccurrence.StartGameHour`/`EndGameHour`, `TimeUntilStart` |
| Free-time plan | `ColonistFreeTimePlanner.Calculate` → `MaximumSafeDiscretionaryDuration`, `RequiredProtectedSleepDuration`, `ProjectedFatigueAtWork`, `Reason` |
| Meal | `ColonistBrain.FoodMealCommitment` → resource, amount, `HungerRecovery`, `DurationGameHours`, `Completed`/`Consumed` |
| History | `SimulationLogManager.Query(subject: colonist)` |

**The free-time plan panel is the sleeper feature.** `ProtectedSleepRequired` and
`ProjectedFatigueAtWork` are already computed and explain *why* a colonist stops working at a
given hour. Nobody has ever been able to see it.

**Interactions:** Assign to job (opens HR pre-filled with this person) · Send to… (future) ·
Follow (future camera) · copy stable key for bug reports.

### 5.4 Facility Window

**Purpose:** "what is this building doing, and what is stopping it?"

**Layout:** header → status banner → production panel → inventory ledger → staffing row → inbound freight → history drawer.

| Element | Binding |
|---|---|
| Identity | GameObject name / `WorkplaceComponent.name`; `WorkplaceExecutionMode` |
| Status banner | `FacilityPerformanceComponent.IsOperational`, `BlockSummary`, `BlockReasons` |
| Production | `ResourceConverterComponent.State`, `Progress`, `ThroughputMultiplier`, `BlockedReason`, `BatchActive`, `BatchProgress` |
| Recipe | `activeRecipe`, `availableRecipes`, `TrySelectRecipe(recipe, out reason)` → reason shown inline on failure |
| Effective rate | `FacilityPerformanceComponent.GetMultiplier(productionRateEffect)` |
| Inventory | local `InventoryComponent.Entries`: onHand / reserved / capacity / free |
| Staffing | workplace `WorkplaceComponent.Roles` → `WorkplaceRoleBinding.Role`, `MaximumConcurrentScheduledWorkers`; assigned workers via `WorkforceManager.Assignments` where `Workplace == this`; active count via `IsActivelyStaffing` |
| Freight policy | local `LogisticsStockComponent.Policies` → role, `targetStock`/`targetFull`, `reorderThreshold`, `emergencyThreshold`, `minimumPickup` |
| Inbound | `FreightLogisticsManager.Jobs` where `Destination == this`; per-job `State`, `Quantity`, `RetryAtTick`, `IsEmergencyWork` |
| Outbound history | `FreightLogisticsManager` completed jobs for this source |

**Interactions:** click a staffed role → HR pre-filtered to that workplace + role ·
click a policy threshold → tooltip explaining the trigger in game hours · click a blocked
job → its reservation state.

### 5.5 Log Console

Full-height virtualized `ListView` over `SimulationLogManager.Entries`, with filter chips
derived from the data itself, not hardcoded:

- **Category** — distinct `SimulationLogEntry.Category` (Logistics, Work, Food, System…).
- **Severity** — Info / Warning / Error.
- **Subject** — click any entity and filter to it.
- **Event key** — distinct `EventKey` (e.g. `logistics.job_assigned`).
- **Game hour range** — uses `Query(minimumGameHour, maximumGameHour)`.
- **Fields** — expandable `SimulationLogField` key/value table per row.
- **Reveal JSONL** — `SimulationLogManager.JsonlPath`.

Row rendering uses `SimulationLogEntry.Render()` for the one-line form and the structured
fields for the detail form. `ListView` virtualization is mandatory: the manager is bounded
but the list is long.

---

## 6. HR Manager — the centerpiece

**Purpose:** assign people to jobs and shifts, and make the consequences visible *before*
the player commits.

### 6.1 Three panes

```
┌ Jobs ────────────┬ People ──────────────┬ Coverage ───────────────────────┐
│ Farm             │ [B] Bob              │ 00  04  08  12  16  20  24      │
│  • Farm Worker   │     Farm Worker      │ ▓▓▓▓▓▓░░░░▓▓▓▓▓▓▒▒░░░░░░░░      │
│    slate 08–16   │     ▓▓▓▓▓▓▓░░░       │ ░░░░░░░░▓▓▓▓▓░░░░░▒▒▒▒▒▒        │
│  • Farm Worker   │ [D] Dana             │                                 │
│    slate 00–08   │     Porter           │ ⚠ 04:00–08:00 under capacity    │
│ Cafeteria        │     (unassigned)     │                                 │
│  • Chef          │                      │                                 │
└──────────────────┴──────────────────────┴─────────────────────────────────┘
```

### 6.2 Jobs pane
Tree of `WorkplaceComponent` → `WorkplaceRoleBinding`. Each role row shows
`MaximumConcurrentScheduledWorkers` and current filled count. Selecting a role becomes the
drop target and scopes the People pane.

### 6.3 People pane
Virtualized list of colonists. Each row: name, current `Role.DisplayName` + workplace,
a mini shift bar, and a state chip from `ColonistBrain.State`.

### 6.4 Shift timeline — the interaction that carries the window

A 24-hour canvas per colonist row, painted by dragging.

- **Paint** — pointer down + drag across the row sets `DailyShiftWindow(startHour, endHour)`.
- **Snap** — 30-minute increments by default; hold `Alt` for free placement.
- **Wrap** — dragging past the right edge produces an overnight window
  (`startHour > endHour`), which `DailyShiftWindow` already supports natively
  (`DurationHours` wraps, `ContainsHourOfDay` wraps, `GetSegments` splits).
- **Adjust** — drag either edge to resize.
- **Clear** — right-click.
- **Ghost preview** — on drag, the prospective member of a list is rendered in full color
  and the current one in outline.

Autosave on pointer release, never on drag (see §6.6).

### 6.5 Coverage heat strip — the "excellent" part

Beneath the timeline, per role, a 96-slot strip (15-minute resolution) sampled from
`DailyShiftWindow.ContainsHourOfDay` across all assignments for that role:

| Slot state | Meaning |
|---|---|
| `covered` | active scheduled workers ≥ 1 |
| `full` | active scheduled workers == `MaximumConcurrentScheduledWorkers` |
| `under` | fewer than `Max` but ≥ 1 |
| `gap` | zero scheduled workers while the workplace has demand |

`gap` slots are clickable: they select every colonist assigned to that role and highlight
which of their shift edges would close the gap. This turns the heat strip from a display into
a **diagnostic tool**, and it is the single feature most likely to make the HR screen feel
genuinely good rather than merely functional.

Optionally show a second row of "wanted" coverage derived from
`WorkforceManager.HasEnoughActiveWorkers(workplace, role, minimumActiveWorkers,
minimumRemainingHours, hour)` for facilities that declare a minimum.

### 6.6 Validation — never a modal, never a surprise

`ValidateAssignment(colonist, workplace, role, shift)` already returns seven precise outcomes.
Map each to a human sentence, in the runtime's own vocabulary:

| `WorkAssignmentResult` | UI copy |
|---|---|
| `Applied` | *(no message; row commits)* |
| `RejectedMissingColonist` | "Pick a colonist." |
| `RejectedMissingWorkplace` | "Pick a workplace." |
| `RejectedMissingRole` | "Pick a job." |
| `RejectedInvalidShift` | "That shift window isn't valid." + `DailyShiftWindow.Validate` text |
| `RejectedRoleNotOffered` | "`{workplace}` doesn't offer `{role}`." |
| `RejectedAtScheduledCapacity` | "`{role}` is already fully scheduled at `{workplace}`." |

Rules:
- Validate **during drag** via `ValidateAssignment` (it is pure — it performs no mutation),
  so the drop target shows validity before release.
- On release, call `Assign(...)` and surface the returned result through `ToastStack` only if
  it is not `Applied`.
- Never construct a state the runtime would reject. The UI must not have a "success" path the
  simulation disagrees with.

### 6.7 Time-aware affordance

Starting a drag into the shift timeline or the coverage strip **auto-pauses** the simulation
(`SetPaused(true)`), and restores the prior speed on drop. Rationale: an hour of game time is
24 real seconds at the current 1× baseline and far faster at 60×+. Editing a schedule while
the clock runs is the single most frustrating thing a management UI can do. Make the pause
visible in the command bar so the player is never confused about why time stopped.

---

## 7. Interaction and keymap

| Input | Action |
|---|---|
| `Esc` | Close focused window (or clear selection) |
| `Tab` | Cycle Colonist window through the roster |
| `1`–`5` | Toggle Colony / Colonist / Facility / HR / Log |
| `Ctrl+K` | Command palette |
| `Space` | Pause / resume |
| `,` / `.` | Speed down / up |
| Click entity | Open its window via `SelectionService` |
| `Ctrl+drag` | Multi-select in lists |
| `F` | Follow selected (stub; reserved for the future camera) |

The command palette sources its entries from the same read-models as the windows
("Go to Bob", "Go to Cafeteria", "Open HR", "Pause", "Filter log: Warning"). It costs almost
nothing once read-models exist and it is the cheapest way to make the UI feel authored.

---

## 8. Data contract summary (traceability map)

One row per window, so any displayed number can be traced to its authority. This table is the
answer to "where does this come from?" and should be kept current.

| Window | Reads (authority) | Writes (commands) |
|---|---|---|
| Command Bar | `SimulationManager`, `SimulationLogManager` | `SetPaused`, `TogglePaused`, `SetSpeedMultiplier` |
| Colony | `ComponentRegistry<ColonistIdentity>`, `InventoryComponent.Inventories`, `FreightLogisticsManager`, `WalkingFreightWorkService`, `WorkforceManager` | — |
| Colonist | `ColonistIdentity`, `ColonistBrain`, `ColonistStatsComponent`, `ColonistActivityRunner`, `PersonnelRouteRunner`, `WorkforceManager`, `ColonistFreeTimePlanner`, `SimulationLogManager.Query` | — (assign routes into HR) |
| Facility | `WorkplaceComponent`, `FacilityPerformanceComponent`, `ResourceConverterComponent`, `InventoryComponent`, `LogisticsStockComponent`, `FreightLogisticsManager` | `ResourceConverterComponent.TrySelectRecipe` |
| HR | `WorkforceManager.Assignments`, `WorkplaceComponent.Roles`, `JobRoleDefinition`, `DailyShiftWindow` | `WorkforceManager.Assign` / `Unassign` |
| Log | `SimulationLogManager.Entries` / `Query` | — |

**Deliberate omission:** no window binds `ColonistStatusComponent`, `StaffingComponent`,
`PopulationManager`, `ContractManager`, `LogisticsManager`, or `ResourceStockPolicyComponent`.
Those are the legacy stack and must not leak into the first pass.

---

## 9. Performance and determinism budgets

| Budget | Target | Enforcement |
|---|---|---|
| Fast-cadence sample cost | O(population) cheap reads only | Presenter review; expensive calls annotated |
| No `GetComponent` in UI | 0 | Code review; read-models resolve once |
| No per-tick UI work | 0 | `UiClock` is real-time, not `ISimulationTickable` |
| List virtualization | `ListView` for >20 rows | Log console and People pane are mandatory |
| Stable ordering | no row reorder without a data change | Sort by hierarchy key / `StableId`, never by dictionary order |
| GC in steady state | no per-sample allocation for unchanged rows | Reuse row `VisualElement`s; only rebuild on structural change |

The determinism rule matters because the audit flagged three separate duplications of the
"stable hierarchy key" helper. The UI should reuse **one** of them, not add a fourth —
and should sort by stable key everywhere so the roster never reshuffles under the player.

---

## 10. Build plan

Ticket ids follow the project's existing style. Each phase is independently shippable and
each ends with something visible.

### Phase 0 — Seams and shell *(unblocks everything)*
| Ticket | Deliverable |
|---|---|
| UI-T00 | `ComponentRegistry<T>` in Core; adopt for `ColonistIdentity` and `WorkplaceComponent`. EditMode tests for register/unregister/reset. |
| UI-T01 | `SelectionService` + `ISelectable` + `CameraSelectionRaycaster` in `Presentation/Selection`. UI-pick guard. |
| UI-T02 | `ColonyPrototype.UI` asmdef; `PanelSettings`; `UiRoot` + `WindowHost` (drag, pin, z-order, close). |
| UI-T03 | `theme.uss` / `base.uss` tokens and primitives; day-cycle class toggle; speed-damped motion helper. |

**Acceptance:** an empty styled window opens, drags, pins, and closes over the running scene,
with no per-frame allocation.

### Phase 1 — Chrome
| Ticket | Deliverable |
|---|---|
| UI-T04 | Command bar: clock, pause, speed presets, pace health. |
| UI-T05 | Log console: virtualized list, filters, field detail, JSONL reveal. |
| UI-T06 | `ToastStack` + `ExplainTooltip`. |

**Acceptance:** speed and pause drive the simulation; the log console renders the existing
`SimulationLogManager` stream with working filters.

### Phase 2 — Colonist
| Ticket | Deliverable |
|---|---|
| UI-T07 | `ColonistReadModel` + EditMode snapshot tests (no scene). |
| UI-T08 | Vitals rings via `Painter2D`; distress chips; state chip with explain tooltip. |
| UI-T09 | Live intent: activity, route, cargo, work-execution lease. |
| UI-T10 | Schedule strip + free-time plan panel. |
| UI-T11 | History drawer bound to `Query(subject:)`. |

**Acceptance:** selecting Bob shows his state, why he changed it, what he's carrying, and his
next shift — and every red chip explains itself.

### Phase 3 — Facility
| Ticket | Deliverable |
|---|---|
| UI-T12 | `FacilityReadModel` + EditMode snapshot tests. |
| UI-T13 | Status banner + production panel + recipe switcher with inline reasons. |
| UI-T14 | Inventory ledger + staffing row + freight policy + inbound jobs. |

**Acceptance:** a facility with a blocked converter shows the block reason and the recipe
reason, and the staffing row equals what HR shows.

### Phase 4 — HR Manager
| Ticket | Deliverable |
|---|---|
| UI-T15 | `WorkforceReadModel`: workplaces, role capacities, assignments, coverage sampling. |
| UI-T16 | Jobs tree + People list, wired to selection. |
| UI-T17 | Shift timeline: paint, snap, wrap, resize, clear, ghost preview. |
| UI-T18 | Coverage heat strip with clickable gaps. |
| UI-T19 | Validation mapping + drag-time validity + toast on rejection. |
| UI-T20 | Auto-pause on edit, restore on drop. |

**Acceptance:** assign Dana to Farm Worker 08–16 by dragging; the shift appears on the
timeline, coverage recomputes, and an over-capacity drop is refused with the runtime's own
reason. Then swap Bob and Dana and confirm no code change is needed — the same
interchangeability bar the freight slice was held to.

### Phase 5 — Polish and proof
| Ticket | Deliverable |
|---|---|
| UI-T21 | Command palette. |
| UI-T22 | Day-cycle chrome + motion damping pass. |
| UI-T23 | Keyboard map, focus ring, `:focus` states, accessibility pass (≥4.5:1 on token pairs). |
| UI-T24 | PlayMode smoke: open each window, assert no console errors, assert selection→window routing. |

---

## 11. Test strategy

Two tiers, and the first tier is the reason the read-model seam exists:

1. **EditMode, no scene.** Read-models are pure projections; build a synthetic snapshot and
   assert the exact rendered row set. This covers §6.6's entire validation matrix and the
   coverage sampler, headlessly. Given this project has never run the Unity Test Runner, a
   suite that needs no scene is the only kind likely to actually be executed.
2. **PlayMode smoke.** Window open/close, selection routing, no console errors, and one
   end-to-end HR assignment.

Manual acceptance (matching `TESTING_IN_EXPLORATION_MODE.md`) remains for feel: drag quality,
snap satisfaction, coverage readability, and motion at 1000×.

---

## 12. Risks and decisions to record

| # | Risk | Mitigation / decision needed |
|---|---|---|
| R1 | UI calling expensive workforce queries per frame | Fast-cadence allowlist in §3.4; explicit refresh elsewhere. **Decide:** acceptable staleness for on-duty counts. |
| R2 | Read-model duplication of runtime logic | Read-models may reshape, never recompute. Any *rule* (e.g. coverage minimums) must come from runtime (`HasEnoughActiveWorkers`), not be re-derived. |
| R3 | Registry proliferation | Adopt `ComponentRegistry<T>`; do not add a fourth ad-hoc static list. |
| R4 | `FacilityPerformanceComponent` references legacy `StaffingComponent` for a blocker | Modern facilities without `StaffingComponent` are unaffected, but the branch is legacy residue. Note it; do not extend it. |
| R5 | Auto-pause surprising the player | Always reflect pause state in the command bar; restore prior speed exactly. |
| R6 | HR becomes a god-window | Split by pane from the start (Jobs / People / Coverage) so each can grow a tab without a rewrite. |
| R7 | USS sprawl | Tokens in `theme.uss` only; no literal colors in window USS. |

Decisions worth promoting to `DECISION_BACKLOG.md`:

- **DB-051** — Is UI a separate assembly with a read-model seam (recommended), or in-process
  direct binding?
- **DB-052** — Does the game auto-pause while the player edits a schedule?
- **DB-053** — Is a "wanted coverage" model authored (per role minimum), or derived from
  active demand? Depends on `HasEnoughActiveWorkers` callers gaining a source of truth for
  `minimumActiveWorkers`.
- **DB-054** — Should `ComponentRegistry<T>` retrofit the three existing ad-hoc registries now
  or later?

---

## 13. Why this shape

The simulation's contract seams are already good: `WorkExecutionLease` is a clean ownership
contract, `IActivityApproachRouter` is a correct dependency inversion, `IPersonnelRouteProvider`
keeps NavMesh out of policy, and `SimulationLogManager` emits structured, entity-identified
events. The first UI pass should *cash in* those seams rather than work around them:

- **Read-models** cash in the fact that runtime state is queryable and immutable-ish.
- **Entity history drawers** cash in the entity-id logging that already exists.
- **Explain tooltips** cash in the `*Reason`/`BlockedReason`/`BlockSummary`/`LastDecisionReason`
  fields that are currently visible only in the console.
- **HR validation** cashes in `ValidateAssignment`'s seven precise results.
- **Coverage** cashes in `DailyShiftWindow`'s native wrap-around semantics.

Nothing in this spec requires a new simulation concept. That is the point: this pass makes the
existing architecture legible, and it does so without adding a second source of truth.
