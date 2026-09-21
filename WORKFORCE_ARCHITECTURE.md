# Workforce Architecture

This is the canonical workforce direction for the Synty colonist path. The
older staffing system remains in the repository for legacy compatibility, but
new workforce code must not reconnect Bob to it.

## Canonical colonist workforce path

```text
WorkforceManager
    authoritative regular employment registry

ColonistIdentity
    identifies a colonist

JobRoleDefinition
    identifies a kind of regular job

WorkplaceComponent
    declares roles offered by a facility and maps
    roles to physical InteractableFacility activities

WorkAssignment
    colonist + workplace + role + direct start/end hours

DailyShiftWindow
    owns daily recurring start/end schedule

ScheduledWorkOccurrence
    derived absolute instance of a recurring assignment

ColonistBrain
    interprets current/next shift facts and decides behavior

ColonistTargetResolver
    resolves Work to the assigned workplace/activity

ColonistActivityRunner
    performs physical work
```

The canonical types above are introduced incrementally. The current Bob slice
connects the workforce facts to the brain's explicit Work lifecycle while
keeping physical execution in the interaction runner.

## Locked rules

- One colonist has zero or one regular job.
- Regular shifts are direct per-assignment start/end times, not named Shift
  A/B/C references.
- Workforce authority reports facts. It never moves colonists or starts
  activities.
- Facilities own physical work behavior.
- The brain decides what Bob does.
- `ColonistActivityRunner` moves Bob's body.
- Downtime/overtime opportunities are a later separate system and do not
  mutate regular employment.

The canonical path is separate from the legacy `StaffingManager`,
`StaffingComponent`, `EmploymentAssignment`, and `ShiftPatternDefinition`
architecture. Do not extend those legacy types for Bob's new workforce path.

## Implemented scheduling primitive

`DailyShiftWindow` is the canonical recurring per-worker schedule value. It
stores a direct daily start hour and end hour, supports ordinary and overnight
windows, uses half-open interval semantics, and rejects equal, non-finite, or
out-of-range hours. Eight hours is not special, and no replacement shift
assets or named Shift A/B/C vocabulary is part of this path.

## Implemented regular employment facts

`WorkforceManager` is the single canonical owner of the regular employment
registry. A colonist has zero or one `WorkAssignment`, and valid assignment
replacement is atomic. The manager reports employment facts only; it does not
wake, move, animate, or start activities for colonists.

`WorkplaceComponent` validates that a role is offered by a facility and maps it
to a physical activity. `WorkforceManager` uses that public contract and
enforces the role's scheduled capacity with half-open recurring-window overlap
semantics. Scheduled capacity is an employment planning constraint;
`InteractableFacility` reservation groups remain physical runtime contention.

`ScheduledWorkOccurrence` derives concrete absolute start/end hours from a
recurring `WorkAssignment`. `WorkforceManager` can answer current, next, and
current-or-next shift queries, including overnight occurrences. `ColonistBrain`
consumes those facts: at shift start it resolves and requests Work, and at shift
end it requests a physical Stop, remains in the Work lifecycle during exit,
then returns Idle only after the runner releases the request and reservation.

## Implemented decision and resolution seam

`ColonistBrain` now consumes the manager's current/next schedule facts. It
wakes from Sleep for an active shift or a shift beginning within the 0.5 game-
hour lead window, remains Idle during that preparation window, and chooses
Work only when the shift is currently active. It does not contain Farm-specific
knowledge or perform target lookup itself.

`ColonistTargetResolver` resolves the Work purpose through the colonist's
regular assignment, the assigned `WorkplaceComponent`, and its role binding.
`ColonistActivityRunner` remains responsible for the physical request,
reservation, navigation, and animation lifecycle. Shift-end stopping is
implemented as a graceful physical exit: the brain requests Stop, waits while
the runner exits and releases the reservation, and only then becomes Idle.

## Canonical hunger and food ownership

`ColonistStatsComponent` owns personal Hunger and its physiological thresholds.
`ColonistBrain` decides when Bob should Eat. `FoodManager` discovers available
public `FoodServiceComponent` opportunities; it never moves Bob or reserves a
station. `ColonistTargetResolver` converts that discovery into an `ActivityTarget`.
`ColonistActivityRunner` owns reservation, navigation, entry, active execution,
exit, and release. The Cafeteria's `InteractableFacility` owns Eat choreography,
and Hunger recovery is applied only during a genuinely active matching Eat.

Food inventory integration is intentionally deferred. `PopulationResourceConsumer`
migration is not part of this slice.

## OffDuty, biological priority, and canonical history

`ColonistStatsComponent` exposes ordinary Hunger at 60, Critical Hunger at 90,
hard Sleepiness at 70 Fatigue, and proactive Rest Preferred at 60 Fatigue.
The explicit brain policy is: critical biological survival, current regular
Work, hard Sleep, ordinary Hunger, proactive Rest, then discretionary OffDuty.
Ordinary Eat remains permitted during the 0.5 game-hour Work preparation
window; newly-started Sleep and OffDuty do not begin in that window. Critical
Hunger may interrupt current Work or wake Sleep. The preparation window is a
departure-policy boundary, not a period in which Bob must stand idle.

`OffDutyComponent` authors discretionary facility activities and
`OffDutyManager` discovers the nearest fitting opportunity without reserving
it. `ColonistFreeTimePlanner` calculates a conservative discretionary budget
from biological headroom and, when a next shift exists, the preparation window
and authored Sleep recovery. Travel time is intentionally treated as zero for
this first slice. Optional staffing requirements use physical active Work at
the matching workplace/role, not schedule presence alone.

`SimulationLogManager` is the canonical structured event/history seam. It owns
bounded in-memory `SimulationLogEntry` records and JSONL output; each entry has
sequence, game hour, real timestamp, semantic event key, category, severity,
subjects, and structured fields. `ReadinessHistory` is only a compatibility
adapter. Human-readable Console lines are renderings of structured records,
not the authoritative data. The generic interaction package emits structured
`ActivityLifecycleEvent` telemetry without depending on the game logger.
