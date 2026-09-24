# UI Pass 1 — Specification & Build Plan

**Windows to see a colonist, a facility, and the whole colony. An HR manager to assign jobs and shifts.**
Status: SPEC — not started. Technology: **Unity UI Toolkit (UXML/USS/UIDocument)**.
Target: Unity 6000.5.9f1, `bobandfriends_modular.unity`, modern Bob-and-Friends stack only.

---

## 0. Why UI Toolkit (and what "slick" means here)

The project has **zero existing UI framework code** (no Canvas/UGUI, no UIDocument, no UXML/USS). We
are choosing the stack from scratch:

- **UI Toolkit** gives us USS theming (one stylesheet restyles every window), UXML layout that
  designers can open in UIBuilder, C# controllers that map 1:1 onto our presenter layer, and
  native Input System support (the project already polls `Keyboard.current` in
  `RTSCameraController` — same input stack).
- Runtime UI Toolkit in Unity 6 needs no EventSystem for pointer/keyboard routing; a `UIDocument`
  + `PanelSettings` is the entire surface.
- If a future need exceeds UI Toolkit (true world-space diegetic panels on facility hulls), the
  fallback is UGUI for *that surface only* — presenters in this spec are UI-framework-agnostic by
  design (Section 7), so the swap is skin-deep.

**Experience principles** (what "slick and excellent" means concretely):

1. **Instrument, don't decorate.** Every pixel answers a question a designer of a space colony
   would ask. Numbers are tabular monospace; labels are quiet; state is color-coded once and
   everywhere (Section 4).
2. **Windows feel like hardware.** Draggable, pinnable, remembered layout; glass panels with
   hairline borders; 120–240 ms eased micro-motion (hover, slide, toast). Nothing bounces.
3. **Zero-latency honesty.** UI reads live simulation state every frame the window is open
   (cheap fields) and *never* caches domain truth. The UI is a pure observer except for one
   deliberate write path (HR → `WorkforceManager`) and clock speed.
4. **Empty states are features.** "No open orders — Cafeteria is stocked" beats a blank table.

---

## 1. Product scope

### In scope (Pass 1)

| Surface | Purpose | Keyboard |
|---|---|---|
| **HUD shell** | Clock/day, speed control (⏸ 1× 2× 4×), alert toasts, selection hint | — |
| **Colonist Dossier** | Everything about one colonist | `1` (contextual: select → opens) |
| **Facility Panel** | Everything about one facility/workplace | `2` (contextual) |
| **Colony Command** | Whole-colony dashboard, 4 tabs | `3` |
| **Crew Office (HR)** | Assign jobs & shifts; the showpiece | `4` |
| **Selection service** | Click-to-select colonists & facilities in the world | LMB, dbl-click = focus, `Esc` |

### Explicit non-goals (Pass 1)

```text
No save/load of game state (UI layout persistence IS in scope, Section 7.6)
No localization
No world-space diegetic panels (nameplate overlay pins are in scope, Section 5.2)
No right-click orders, no drag-orders in world, no shuttle/flight UI
No UI for issuing freight contracts (logistics is READ-ONLY in Pass 1)
No settings/options screen, no key rebinding screen
No touch/gamepad support
No runtime UI for the legacy stack (LogisticsManager/ContractManager/Staffing/ColonistAgent)
```

---

## 2. Domain facts available (our read-model sources)

The UI adds **no domain logic**. It reads exactly these existing seams (verified against code):

| Fact | Source |
|---|---|
| Roster *(missing today — added in T01)* | `ColonistRegistry.Active` **(new, Section 7.2)** |
| Name | `ColonistIdentity.DisplayName` |
| Brain state (9 states) | `ColonistBrain.State` |
| Last decision | `ColonistBrain.LastDecision` / `LastDecisionReason` |
| Needs + thresholds + rates | `ColonistStatsComponent`: `Fatigue`, `Hunger`, `IsSleepy`, `ShouldPreferRest`, `IsCriticallyHungry`, `StimulationNeed`/`RelaxationNeed` + `NeedsStimulation`/`NeedsRelaxation`, `Effective*PerGameHour`, `*Threshold` |
| Work assignment / shift | `WorkforceManager.TryGetAssignment`, `TryGetCurrentOrNextShift`, `TryGetCurrentDuty` |
| Work lease | `ColonistBrain.ActiveWorkExecutionLease` (`IsActive`, `HasPendingReleaseRequest`) |
| Current activity | `ColonistActivityRunner`: `CurrentActivityId`, `ActiveActivityId`, `Phase`, `CurrentReservationGroup`, `IsExitInProgress` |
| Route diagnostics | `PersonnelRouteRunner`: `State`, `Status`, `CurrentLeg`, `CurrentLegType`, `LastFailureReason`, `CurrentPlan.TotalEstimatedDistance` |
| Carried cargo | colonist `InventoryComponent.Entries` (`onHand`/`reserved`/`available`/`capacity`) |
| Meal / leisure in progress | `ColonistBrain.FoodMealCommitment`, `OffDutyTarget`, `OffDutyActiveDuration` |
| Per-colonist history | `SimulationLogManager.Query(subject: identity)` (brain logs under `ColonistIdentity`) |
| Facility activities / sequences | `InteractableFacility.Activities`, `.Sequences`, `TryGetBinding`, `TryGetSequence` |
| Reservations | `InteractableFacility.TryGetReservation(group)`, `.IsReserved(group)`, `ActiveReservationCount` |
| Workplace roles & capacity | `WorkplaceComponent.Roles` (`WorkplaceRoleBinding`: `Role`, `ActivityId`, `MaximumConcurrentScheduledWorkers`), `ExecutionMode`, `DutyAnchor` |
| On-duty-now check | `WorkforceManager.IsActivelyStaffing` / `HasEnoughActiveWorkers` |
| Facility stock policy | `LogisticsStockComponent.Policies` (role, `targetStock`/`targetFull`, `reorderThreshold`, `emergencyThreshold`, `minimumPickup`, `ResolveTarget`) |
| Inventories (facility, colony) | `InventoryComponent` + static `InventoryComponent.Inventories` |
| Food service | `FoodServiceComponent` menu + access mode (`FoodManager.DescribeAccessMode`) |
| Production | `ResourceConverterComponent` (recipes, queues) |
| Freight orders / jobs | `FreightLogisticsManager.Orders` / `.Jobs` (`FreightOrder.Id/Requested/Delivered/Committed/Uncovered/IsOpen`, `FreightDeliveryJob.Id/State/Quantity/HasPickedUp/Reservation/RoutePlan/WalkingExecution.PositioningDistance`) |
| Open services | `WalkingFreightWorkService.Active`, `LogisticsStockComponent.Active` |
| Event feed | `SimulationLogManager.EntryRecorded` (live) + `Query(...)` + `JsonlPath` |
| Clock / speed | `SimulationManager.CurrentGameHour`, `CurrentTick`, `SetSpeedMultiplier`, `PresentationSpeedFactor` |

**Write path (exactly one in Pass 1):** `WorkforceManager.Assign(colonist, workplace, role, shift)`
and `Unassign(colonist)` — including `ValidateAssignment` for live conflict feedback
(`WorkAssignmentResult.RejectedAtScheduledCapacity` etc.). Clock speed is a presentation control,
not domain state.

---

## 3. Information architecture

```text
┌──────────────────────────────────────────────────────────────────────────────┐
│ HUD TOP BAR                                                                  │
│  ✦ COLONY OS        Day 12 · 14:20 (cycle 158.3h)   ⏸  1× [2×] [4×]   ⚠ 2   │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   (free-floating draggable windows, persisted layout)                        │
│    ┌─────────────┐        ┌──────────────────┐        ┌────────────────┐     │
│    │ Dossier     │        │ Facility Panel   │        │ Colony Command │     │
│    └─────────────┘        └──────────────────┘        └────────────────┘     │
│    ┌────────────────────────────────────────────────┐                        │
│    │ Crew Office (HR)                               │                        │
│    └────────────────────────────────────────────────┘                        │
│                                                                              │
├──────────────────────────────────────────────────────────────────────────────┤
│ SELECTION HINT BAR   "Click a colonist or facility · 1 Dossier · 4 Crew Office"│
└──────────────────────────────────────────────────────────────────────────────┘
TOAST STACK (top-right, slides in, auto-fades; rose/amber/mint severity)
WORLD OVERLAY PINS (per selected entity: name + status ring, Section 5.2)
```

Windows are **floating, draggable, resizable (min-size clamped), pinnable** (pin = always on top),
collapsible to title bar, and their rect/layout persists across Play sessions (JSON in
`persistentDataPath`). `Esc` closes the focused window or clears selection.

---

## 4. Design language — "Flight Direct"

One USS theme stylesheet (`Assets/UI/Themes/flight-direct.uss`) with custom-property tokens.
Every window consumes tokens only — no hard-coded colors anywhere else.

```text
SURFACES     glass:        rgba(13,17,23,0.88)      hairline:   rgba(148,163,184,0.16)
             raised:       rgba(30,37,48,0.92)      glow-focus: rgba(255,180,84,0.35)
TEXT         primary #E5E9F0   dim #8B95A7   micro-labels: 10px uppercase, letter-spacing 1px
ACCENTS (state colors — used identically in every window)
             work/amber #FFB454    logistics/cyan #56C8E0   leisure/violet #A78BFA
             ok/mint #4ADE80       warn/amber-deep #F59E0B  alert/rose #F87171
NEEDS GAUGE  track rgba(255,255,255,0.06)  fill thresholds mirror ColonistStatsComponent
             thresholds (dim tick at HungryThreshold, rose tick at CriticalHungerThreshold, …)
TYPE         Liberation Sans (already in project) for text; numbers rendered with fixed-width
             style class (.num) so columns align; window titles 12px uppercase
GEOMETRY     8px grid; window radius 10px; controls radius 4px; 1px hairline borders
MOTION       hover 120ms ease-out (USS transition) · window open 180ms slide+fade (UiTweener)
             toast 240ms slide-in, 6s dwell · urgent state = 1.6s opacity pulse on status chip
             Nothing elastic. Nothing longer than 300ms. Respect "reduce motion" toggle in HUD.
```

USS supports hover/focus transitions natively; anything requiring transforms-on-open uses the
small `UiTweener` helper (scheduled eased property animation — USS has no keyframes).

---

## 5. Window specifications

### 5.1 Colonist Dossier

```text
┌─ ● BOB — FARMER ─────────────────────────── ⌖ ─ □ ─ ✕ ─┐
│ STATE  ▸ Working · Farm / farmer.loop          (amber)   │
│ LAST   colonist.decision.work · current_work            │
├─────────────────────────────────────────────────────────┤
│ NEEDS                          ETA-to-threshold          │
│  Hunger   ▮▮▮▮▮░░░░░  41%      hungry in 5.2h            │
│  Fatigue  ▮▮▮░░░░░░░  28%      sleepy in 3.0h            │
│  Stim     ▮▮▮▮▮▮░░░░  62%      wants stimulation         │
│  Relax    ▮░░░░░░░░░   9%                               │
├─────────────────────────────────────────────────────────┤
│ WORK                                                     │
│  Farm · Farmer · shift 06:00–14:00   [on shift · 2.3h left]│
│  Next: Day 13, 06:00 (in 15.7h)      lease: none         │
├─────────────────────────────────────────────────────────┤
│ ACTIVITY / ROUTE                                         │
│  farmer.loop @ Farm   reservation: farm-station-1        │
│  route: Completed · Walk 34.2m · legs 1 · failure —      │
├─────────────────────────────────────────────────────────┤
│ CARGO            ▮▮▯ 2/10 Food                           │
├─────────────────────────────────────────────────────────┤
│ HISTORY  (SimulationLogManager.Query(subject: identity)) │
│  14:03 colonist.decision.work      current_work          │
│  13:58 food.meal_completed        hunger 22              │
│  13:41 logistics.pickup_completed  10 Food               │
└─────────────────────────────────────────────────────────┘
```

- **ETA-to-threshold**: `(threshold − value) / Effective*PerGameHour`, capped display ">1d".
  Pure presentation math over existing rates — the slick detail people remember.
- **Needs gauges** show threshold ticks so "why did he leave work?" is answerable at a glance.
- HISTORY rows are clickable → highlight the logged target facility (focus camera).
- Header `⌖` focuses camera on the colonist.
- Live bindings refresh at UI tick (Section 7.4); HISTORY re-queries only when new entries match
  the subject (`EntryRecorded` filter).
- **Empty/error**: colonist destroyed → window shows "subject left the world" and offers close.
  Manager missing (test scenes) → section shows "WorkforceManager not in scene".

### 5.2 World overlay pins (selection feedback)

- Selected entity gets an overlay pin drawn with `RuntimePanelUtils.TransformPoint` each frame
  (cheap, no world-space canvas): status ring (state color), display name, and for colonists a
  mini needs pip. Pin fades in 180ms.
- Selection highlight on the mesh: Pass 1 uses a soft under-glow decal/projector-free approach —
  a simple ring `VisualElement` projected at the feet position; full mesh outline is a stretch
  goal (Section 9).
- Double-click entity = camera focus (delegate into `RTSCameraController` via a tiny
  `IFocusTarget` seam added to it — one method `FocusOn(Vector3)`).

### 5.3 Facility Panel

```text
┌─ ▲ FARM — Depot / Producer ──────────── ⌖ ─ □ ─ ✕ ─┐
│ [ Activities ] [ Staffing ] [ Stock ] [ Freight ]    │  ← tabs
├──────────────────────────────────────────────────────┤
│ ACTIVITIES            res?  users/cap                 │
│  farm-work      farm-station-1   free                │
│  eat            cafeteria-table  RESERVED (Bob)      │
│  (sequence: farm-cycle — 2 steps, externally OK)     │
├──────────────────────────────────────────────────────┤
│ STAFFING                                              │
│  ☉ Farmer  [▮▯] 1 slot   now: Bob (on shift)         │
│  ☉ Porter  [▮▯] 1 slot   now: Dana (on shift)        │
│  [+ assign] → opens Crew Office pre-targeted         │
├──────────────────────────────────────────────────────┤
│ STOCK (LogisticsStockComponent)                       │
│  Food  Producer  target F  reorder 10  emerg 2  min 5 │
│  on-hand ▮▮▮▮▮▮▮▯▯▯ 62/100   inbound 10 (job freight-job-000003)│
└──────────────────────────────────────────────────────┘
```

- **Stock bars** draw reorder/emergency ticks exactly like the Dossier draws needs ticks — one
  gauge idiom for all thresholds in the game.
- **Freight tab**: `FreightOrder`s where `Requester == this stock`, `FreightDeliveryJob`s where
  `Source`/`Destination` == this stock: demandId, allocationId, state chip
  (`Assigned/TravelingToPickup/…/Blocked`), useful quantity, personnel route distance,
  `Reservation.IsActive` chip. This is the B1 debug surface, productized.
- **[+ assign]** deep-links to Crew Office with workplace pre-selected (Section 5.5).

### 5.4 Colony Command (dashboard)

Four tabs, all read-only:

**Overview** — the "is my colony okay?" glance:
```text
 POPULATION   4 colonists   ▸ 2 working · 1 eating · 1 sleeping
 NEEDS HEAT   hunger ▂▂▁  fatigue ▄▅▃  stim █▆▃  relax ▃▁▁   (per-colonist pips)
 WORKFORCE    4/4 slots filled   Command Center: unstaffed (allowed)
 ALERTS       ▸ Cafeteria Food below reorder (10 requested)     [amber]
              ▸ Charlie route failed: no_pedestrian_route        [rose]
```
- Needs "heat" is a row of per-colonist pips colored by worst threshold crossed.
- Alerts are generated from the structured log stream (severity ≥ Warning, plus domain rules like
  `FreightJobState.Blocked`) — the same pipeline as the toast stack.

**Logistics** — freight ledger:
- Orders table: demandId, requester, resource, requested/delivered/committed/**uncovered** bars,
  open/closed chip, age in game-hours.
- Jobs table: jobId, allocationId, resource+qty, source→destination, state chip, current freight
  leg (`pickup`/`delivery`), useful quantity, personnel route distance, positioning distance,
  reservation state. Filter chips by state (Blocked is default-highlighted).
- Services: `WalkingFreightWorkService.Active` list with routine/emergency capacity config.

**Inventory** — colony-wide totals: per-resource Σ on-hand / Σ reserved / Σ capacity across
`InventoryComponent.Inventories`, with per-site breakdown on expand. Delta sparkline (UI samples
totals each UI tick into a 60-sample ring buffer — presentation-only).

**Feed** — the structured log, filterable by category (Decision/Food/OffDuty/Work/Logistics/System),
severity, and free-text event key. Shows `entry.Render()`-style rows; row click selects the entry's
subject in the world. Footer shows `JsonlPath` + "copy path" button (debugger bait).

### 5.5 Crew Office (HR manager) — the showpiece

```text
┌─ ★ CREW OFFICE ───────────────────────────────── ✕ ─┐
│ ROSTER        │  ASSIGNMENT BOARD        │ SHIFT · 24h  │
│ 🔍 __________ │  FARM                    │ 00  06  12 18│
│ ┌───────────┐ │   Farmer   [■▢] Bob   ✎ │ Bob  ▓▓▓▓░░░░│
│ │ ● Bob     │ │   Porter   [■▢] Dana  ✎ │ Alice ░░▓▓▓▓░│
│ │ ▮▮▮▮ ▮▮▮░ │ │ AIRLOCK                  │ Dana ▓▓▓▓▓▓░░│
│ │ 06–14     │ │   Porter   [■▢] Dana  ✎ │ heat ▁▃█▇▅▂▁ │
│ └───────────┘ │ SHUTTLE BASE             │              │
│ ┌───────────┐ │   Pilot    [■▢] Charlie✎│ [Apply Shift]│
│ │ ● Alice   │ │ COMMAND CENTER           │              │
│ │ ▮▮░░ ▮▮▮▮ │ │   CommandOp [▢▢] —   +  │← vacancy     │
│ └───────────┘ │                            │              │
└───────────────────────────────────────────────────────┘
```

Three synchronized panes:

**A. Roster** — cards for every `ColonistRegistry.Active` colonist: status dot (state color),
name, two mini need bars (hunger/fatigue), current shift window, and a "busy" chip when
`ActiveWorkExecutionLease != null` or an activity is active. Filter box; sort by name/state.

**B. Assignment board** — every `WorkplaceComponent` (from workforce assignments ∪ discovered
workplaces via registry) with its `WorkplaceRoleBinding` slots. Each slot shows capacity pips
(`MaximumConcurrentScheduledWorkers`), the assignee(s), and:
- **`✎` (edit)** on an assigned slot → shift editor popover (C) for that colonist.
- **`+` on a vacancy** → assign flow: pick a colonist from a mini-roster flyout → shift editor.
- **Drag**: drag a roster card onto a slot = assign flow (the slick path). Invalid drops show a
  rose snap-back + reason chip (`RejectedRoleNotOffered`, `RejectedAtScheduledCapacity`, …).
- MobileDuty workplaces show a "duty anchor ✓/✗" hint; FacilityActivity workplaces show the bound
  activityId (this surfaces the WorkplaceComponent contract honestly).

**C. Shift editor + 24-hour timeline** — the piece that must feel like a pro tool:

- Timeline: 24 hour columns (00–24, wrap-safe). Each colonist row renders its
  `DailyShiftWindow` as a block; **wrap-midnight shifts render as two segments** (mirrors
  `DailyShiftWindow.GetSegments()` semantics) and are edited as one logical block (dragging the
  late segment moves the whole window).
- **Editing**: drag block to move (snap 30 min), drag edges to resize (min 1h), type exact times
  in `HH:MM–HH:MM` fields as an alternative. `DailyShiftWindow.Validate` errors surface inline
  ("start and end must differ").
- **Capacity heat underlay**: per workplace-role, a background band under the timeline showing
  concurrent-scheduled-worker counts over the day (0 → glass, at-capacity → amber-deep,
  over-capacity → rose). The current edit shows a **ghost block** + live heat delta *before*
  commit, computed by `WorkforceManager.ValidateAssignment(colonist, workplace, role, draftShift)`
  — so `RejectedAtScheduledCapacity` is felt, not discovered.
- **Apply/Revert/Unassign** buttons. Apply calls `WorkforceManager.Assign(...)`; result toast:
  "Alice → Cafeteria Worker, 12:00–20:00" or the rejection reason verbatim.
- **Coverage strip** (top of timeline): for the selected workplace-role, who is on shift *right
  now* (`TryGetCurrentDuty`), remaining hours, and next gap ("unstaffed from 14:00").
- Emergency/routine freight service config (`WalkingFreightWorkService`) is displayed read-only
  under the slot ("Porter also runs routine freight, cap 10/run") so HR understands side duties —
  no editing in Pass 1.

**HR write-path rules:** the window may only call `WorkforceManager.Assign/Unassign`. All
validation display comes from `ValidateAssignment`'s `WorkAssignmentResult` — the UI invents no
rules (POLICY decides; UI presents).

---

## 6. Interactions & input map

| Input | Effect |
|---|---|
| LMB on entity | select (replaces selection) |
| LMB on empty world | clear selection (`Esc` too) |
| Double-click entity | focus camera (`RTSCameraController.FocusOn`) |
| `1` / `2` / `3` / `4` | toggle Dossier / Facility / Command / Crew Office (contextual windows bind to selection) |
| `Space` | pause/Resume (speed 0 ↔ last) |
| `Tab` | cycle selection through `ColonistRegistry.Active` (rapid triage) |
| Drag title bar / edges | move / resize window (persisted) |
| `Shift+Drag` roster card onto slot | assign flow (plain drag also works) |

Conflicts to verify at T02 against `RTSCameraController`'s existing `Keyboard.current` bindings
(likely WASD/arrows) — number row and F-keys are free. While the pointer is over any panel
(`ColonyUi.PointerOverUi`) or a text field has focus, camera pan/zoom input is suppressed.

---

## 7. Technical architecture

Layering honors the constitution: **Content ← Runtime ← Presentation ← UI**.

### 7.1 Assembly & folders

```text
Assets/UI/                                  UXML, USS, PanelSettings, fonts (artifacts)
Assets/Scripts/ColonyPrototype/Presentation/UI/
    ColonyUiController.cs     root UIDocument, wiring, shortcut routing
    UiWindowManager.cs        open/close/focus/z-order/pin/layout persistence
    UiWindowBase.cs           chrome (title, ✕, □, ⌖, pin), tween open/close
    SelectionService.cs       singleton-lite; Current: Object; Changed event
    WorldOverlayPins.cs       per-frame projected pins for selection
    UiRefreshScheduler.cs     dirty flags + 5 Hz scheduled ticks via IVisualElementScheduledItem
    UiTweener.cs              eased property animations (USS has no keyframes)
    ThemeTokens.cs            USS custom-property names as consts
    ReadModels/
        ColonistReadModel.cs  FacilityReadModel.cs  ColonyReadModel.cs  ShiftDraft.cs
    Widgets/
        NeedsGauge.cs         threshold-tick gauge (used by Dossier, Facility, Roster)
        StateChip.cs          brain/job state chip with pulse
        Sparkline.cs          60-sample ring buffer renderer
        ShiftTimelineControl.cs   custom VisualElement (T06's whole job)
    Windows/
        ColonistDossierWindow.cs  FacilityWindow.cs
        ColonyCommandWindow.cs    CrewOfficeWindow.cs   (+ tab subviews)
Assets/Scripts/ColonyPrototype/People/ColonistRegistry.cs   (DOMAIN addition, T01)
```

Read-models are dumb projections (getters only) so windows never stitch 3 systems together per
fact — traceable in one place when domain moves. **No domain file references Presentation/UI.**

### 7.2 The one domain addition: `ColonistRegistry`

The modern stack has no way to enumerate colonists (legacy `PopulationManager` counts
`ColonistAgent` — wrong stack). Add `ColonistRegistry` exactly in the mold of
`LogisticsStockComponent.Active`: static `IReadOnlyList<ColonistIdentity> Active`,
add/remove in `ColonistIdentity.OnEnable/OnDisable`, `ResetStatics` via
`RuntimeInitializeOnLoadMethod(SubsystemRegistration)`. ~30 lines, no policy, no behavior.
(One focused edit-mode test is justified here — registry population is a central seam.)

### 7.3 Refresh policy (no per-frame domain polling)

| Source | Mechanism |
|---|---|
| Clock, speed, selected-window cheap fields | UI tick (10 Hz for clock, 5 Hz for tables) |
| Inventory changes | `InventoryComponent.OnChanged` → mark dirty |
| Log stream (toasts, feed, alerts) | `SimulationLogManager.EntryRecorded` (already an event) |
| Brain/activity state | polled at UI tick only while its window is open |
| Sparkline sampling | UI tick, presentation-only ring buffers |

Windows subscribe on open and unsubscribe on close. All handlers defend against Unity fake-null.

### 7.4 Input & camera seam

- `ColonyUi.PointerOverUi` computed from `panel.visualTree.Pick(mousePosition)`; exposed as a
  static read-model so `RTSCameraController` gates its own input with one `if` (the only change to
  that file).
- `RTSCameraController.FocusOn(Vector3)` (new public method, ~10 lines) serves double-click focus.

### 7.5 The single write path

`CrewOfficeWindow → ShiftDraft → WorkforceManager.ValidateAssignment` (live ghost feedback) →
`WorkforceManager.Assign/Unassign` (commit) → toast reflects `WorkAssignmentResult`. Nothing else
in UI Pass 1 mutates domain state. This keeps the audit's ownership rule intact: the UI is
POLICY-adjacent presentation; WorkforceManager remains the assignment authority.

### 7.6 Layout persistence

`UiWindowManager` saves `{windowId, x, y, w, h, collapsed, pinned}` to
`persistentDataPath/ui-layout.json` on quit and on change (debounced). Corrupt/absent file →
carefully chosen defaults (Crew Office centered, others docked to corners). "Reset layout" button
in HUD overflow menu (⋯).

### 7.7 Robustness budget

- Every window renders correctly with *any* manager missing (test scenes) — section-level empty
  states, never exceptions.
- Destroyed-object staleness: read-models resolve Unity objects per-tick and return `IsAlive`.
- Text overflow: ellipsis + tooltip; long display names must not break chrome.
- Discrete vs continuous resources formatted via `ResourceQuantityRules`/`IsDiscrete`.
- Scale: `PanelSettings` scale mode = `ConstantPhysicalSize` with DPI-aware fallback; UI must be
  legible at 1080p and 1440p.
- Performance: ≤ 2 ms/frame UI cost with all four windows open (visual tree diffing — reuse
  `VisualElement`s, never rebuild lists wholesale; virtualize the Feed over ~50 visible rows).

---

## 8. Build plan (ticket stack)

Dependencies flow top-down; T03/T04/T05 are parallelizable after T02. Sizes are gut estimates.

| Ticket | Deliverable | Depends | Size |
|---|---|---|---|
| **UI-T00** Foundation | `Assets/UI/` + PanelSettings + flight-direct.uss tokens; `ColonyUiController`, `UiWindowBase`/`UiWindowManager` (drag/resize/collapse/pin + layout persistence), `UiTweener`, HUD top bar (clock via `CurrentGameHour`, speed buttons via `SetSpeedMultiplier`), shortcut routing, `ColonyUi.PointerOverUi` + `RTSCameraController.FocusOn` seams | — | M |
| **UI-T01** Registries & read-models | `ColonistRegistry` (domain, +1 seam test); `ColonistReadModel`, `FacilityReadModel`, `ColonyReadModel`, `ShiftDraft`; `UiRefreshScheduler` | T00 | S |
| **UI-T02** Selection & overlay pins | `SelectionService`, world picking (colliders on colonists/facilities), `WorldOverlayPins`, double-click focus, `Tab` cycle, Esc semantics | T01 | S |
| **UI-T03** Colonist Dossier | Full window per §5.1 incl. `NeedsGauge` + ETA-to-threshold, history list bound to `Query(subject)`, `StateChip` | T02 | M |
| **UI-T04** Facility Panel | 4 tabs per §5.3 incl. reservation view, staffing pips, stock gauges w/ ticks, freight ledger tab; `[+ assign]` deep-link stub | T02 | M |
| **UI-T05** Crew Office — assignment | Roster cards, assignment board w/ slots & vacancies, drag-to-assign + flyout path, shift fields (text), `WorkAssignmentResult` toasts, `[+ assign]` deep-link | T01 | M |
| **UI-T06** Crew Office — shift timeline | `ShiftTimelineControl`: block drag/resize, 30-min snap, wrap-midnight logical blocks, capacity heat underlay, ghost-block live validation via `ValidateAssignment`, coverage strip | T05 | **L** |
| **UI-T07** Colony Command | 4 tabs per §5.4: Overview (needs heat, alerts), Logistics ledger (orders + jobs + services), Inventory totals + sparklines, Feed w/ filters + JsonlPath | T01 | M |
| **UI-T08** Polish & runbook | Motion sweep (tweens, pulses, toasts), empty states audit, shortcuts card (hold `?`), reduce-motion toggle, perf pass (≤2 ms), **manual acceptance runbook** | T03–T07 | S |

Per `TESTING_IN_EXPLORATION_MODE.md`: **no PlayMode automation, no UI automation tests.** One
edit-mode test only (registry population — a central seam). Everything else is instrumented and
judged in the Play runbook below. "Instrument architecture, not details."

---

## 9. Stretch (explicitly after Pass 1 sign-off)

- Full mesh selection outline (renderer feature) instead of foot ring
- Radial context menu on right-click (order verbs arrive with future gameplay passes)
- Colonist portrait `RenderTexture` camera for dossier header
- Timeline: multi-select rows, shift templates ("day shift" presets), copy-shifт-to-day
- Command palette (`Ctrl+K`) for window/selection navigation
- Localization-ready string table (deferred by design)

---

## 10. Definition of Done

```text
All four windows open/close with 1–4 and mouse, layouts persist across Play runs.
The ONLY domain mutations from UI are WorkforceManager.Assign/Unassign and clock speed.
No scene scans at runtime (ColonistRegistry + existing Active registries only).
No per-frame domain polling outside the 5/10 Hz UI scheduler.
Every window renders graceful empty states with zero managers in scene.
HR: assign, edit shift (incl. wrap-midnight), unassign — with capacity violations
    shown live as ghost/heat BEFORE commit and rejected with the manager's own reason.
Theme is token-driven; no stray colors; motion ≤ 300ms; reduce-motion honored.
UI cost ≤ 2 ms/frame with all windows open at 1080p.
Gameplay parity: colony behaves identically with all windows closed (UI is inert).
```

## 11. Manual acceptance runbook (the real test)

```text
1.  Play bobandfriends_modular. HUD shows Day/time; speed 1×/2×/4× visibly changes sim pace.
2.  Click Bob → pin appears. Press 1 → Dossier: state follows Bob live (walk/work/eat/sleep),
    ETA-to-thresholds tick, history appends decisions as he makes them.
3.  Click Farm → press 2 → Facility: activities + reservation state match the world
    (reserve an activity with a colonist and watch the chip flip to RESERVED).
4.  Press 3 → Command: Inventory tab totals match on-site inventories; Logistics tab shows the
    Cafeteria Food order and Dana's job progressing through TravelingToPickup → pickup → …;
    Feed streams; alerts fire when Food drops below reorder.
5.  Press 4 → Crew Office. Drag Alice onto Command Center/CommandOp slot → shift editor opens;
    set 08:00–16:00; heat ghost shows overlap with Bob's slot capacity; Apply → toast "Alice →
    Command Operator, 08:00–16:00"; board updates; Alice reports to Command Center at 08:00
    (PROVES the UI wrote through the real WorkforceManager path).
6.  Set an over-capacity shift on Farm/Farmer → ghost turns rose, Apply is refused with
    "RejectedAtScheduledCapacity" verbatim. Fix it; Apply succeeds.
7.  Make a wrap-midnight shift (22:00–06:00) → renders as two segments, drags as one block.
8.  Unassign Dana from Porter via the board → freight jobs stop being picked up (observe in
    Logistics tab), confirming read-models reflect real state, not cached copies.
9.  Close all windows (Esc ×4) → colony behavior unchanged; reopen → layouts remembered.
10. With UI idle (no windows open), Profiler shows UI ≤ 2 ms.
```

**B1/HR parity note:** step 5 is the architectural proof — a UI-planned assignment physically
moving a colonist is the same POLICY→ROUTING→EXECUTION chain the sprint built, entered from a new
caller (the UI) with zero rewrites. If that step works, Pass 1 is wired to the real seams.
