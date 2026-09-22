# Leisure Drives And Food Access (Stack 1)

This document describes the soft leisure drives added to the colonist stats, the per-activity
recreation cooldown, and the separate questions that food service now answers. It complements
`WORKFORCE_ARCHITECTURE.md`, `STAFFING_ARCHITECTURE.md`, and `OFFDUTY_AUTOMATED_REPORT.md`.

## Leisure

- **Stimulation and Relaxation live in `ColonistStatsComponent`.** They are soft discretionary
  drives: `0` is fully satisfied, a larger number is a stronger unmet drive, and nothing in this
  stack attaches a health or death consequence to them.
- They accumulate at their own baseline rates (`4 / game-hour` each by default) and are clamped
  only at zero. They are allowed to exceed their thresholds (`50` by default).
- **`OffDuty` is the opportunity domain, not the need itself.** `OffDutyComponent` describes what
  recreation exists; the drives describe why the colonist would want it.
- **Activities declare which drives they satisfy through recovery rates.**
  `OffDutyActivityBinding.stimulationRecoveryPerGameHour` and
  `relaxationRecoveryPerGameHour` are authored as positive numbers; the effective stat rate while
  the activity is *genuinely active* is `baseline - recovery`. An activity may satisfy both (a
  hike can be stimulating and relaxing), and there is deliberately no `Active/Passive` enum.
- Only genuine physical activity counts. Walking to Play, entering it, exiting it, and released
  state all receive the plain baseline rate. `ColonistStatsComponent` resolves the activity from
  `ColonistActivityRunner.ActiveFacility` / `ActiveActivityId` and the sibling `OffDutyComponent`;
  it never reads `ColonistBrain.State`.
- **Activity cooldown and drive satisfaction are separate mechanisms.** Drive satisfaction stops
  endless hopping among equivalent activities; the cooldown stops repeatedly choosing the exact
  same attraction.
- **Cooldown begins only after the completed planned active duration.** `OffDutyCompletionHistory`
  is a plain C# object owned by `ColonistBrain`. Discovery, reservation, walking, entry, becoming
  active and early interruption all leave the activity immediately eligible again.
- Cooldown is keyed by the authored semantic key (`cooldownKey`, falling back to `activityId`), not
  by facility instance, so two bowling lanes can share one `bowling` cooldown.
- Drive selection order in the brain: after critical biological needs, work, sleep, hunger, work
  preparation and proactive rest, the brain compares normalized pressure
  (`need / threshold`). The larger pressure is tried first, with a deterministic tie break in
  favour of Stimulation. If that drive has no valid opportunity and the other drive is also above
  its own threshold, the other drive may be chosen. A relaxing activity is never selected merely
  because "something recreational exists" while Stimulation is the only unmet drive.

## Food access

Four different questions are kept separate:

```text
structural configuration != public service availability != requester-specific access
!= continuation of an already-started meal
```

- `FoodServiceComponent.IsConfigured` means the service is *authored correctly*: the facility and
  `Eat` activity exist, the activity is externally requestable, the reservation group and approach
  anchor are configured, the recovery rate is valid, and (when staffing is required) the required
  workplace exists, offers the required role and demands at least one active worker.
  It explicitly does **not** mean a worker is standing there this second. An already-eating
  colonist therefore never loses Hunger recovery because the waiter clocked out.
- `FoodServiceComponent.EvaluateAccess(requester, gameHour)` answers "can *this* colonist use this
  service right now?" and returns `Unavailable`, `Public` (no staffing required),
  `PublicStaffed` (a worker is physically Working the required role right now) or `SelfService`.
- Food staffing requires worker *presence*, not worker coverage for a planned duration:
  `minimumRemainingHours` is `0`. Once the meal is physically underway the diner finishes it.
- `FoodSelfServicePolicy` is `None`, `AssignedWorkers`, or `Everyone`. `AssignedWorkers` permits a
  requester whose workforce assignment is to the required workplace **and** required role. The
  requester does not need to be on shift; that is the point.
- `FoodManager` discovers services for a specific `FoodQuery` (seeker, position, game hour) rather
  than for a position alone, and logs selection and no-target facts per seeker
  (`food.target_selected`, `food.no_target`, with `accessMode`).
- Lifecycle: before the meal genuinely begins (`EatSeeking`) the requester must remain eligible. If
  public staffing or self-service permission disappears while walking or entering, the request
  stops and unwinds. Once `ColonistActivityRunner.IsActivityActive` reports the `Eat` activity
  truly active, only structural target validity matters, and the diner is not ejected.

### Cafeteria worked example

```text
Alice is the Cafeteria Worker and is physically Working
    -> Cafeteria is publicly open (PublicStaffed)
    -> Bob may discover Eat

Alice's shift ends or she leaves Work
    -> Cafeteria is publicly closed to new diners
    -> Alice is still an assigned Cafeteria Worker
    -> Alice may self-serve (SelfService)

Alice is unassigned or reassigned elsewhere
    -> employee self-service disappears

Bob is already Eating when Alice leaves
    -> Bob finishes his current meal
    -> no new public diners are admitted
```

Critical Hunger during food-service work stays a physical interruption, not a magical one:

```text
Alice becomes Critically Hungry while Working
    -> she physically leaves ServeFood
    -> public service closes while she is the sole worker
    -> she remains an assigned Cafeteria Worker
    -> employee self-service lets her request Eat
    -> she physically Eats, then returns to Work if the shift remains active
    -> public service reopens
```

## Component ownership

- **Do not create separate components for every personal stat family.**
  `ColonistStatsComponent` remains the owner of personal changing numeric state; that is why
  Stimulation and Relaxation were added there rather than in a new `ColonistWellbeingComponent`.
- `WorkplaceComponent` remains, because workplace policy has real independent ownership.
- `FoodServiceComponent` remains, because food-service policy has real independent ownership.
- `OffDutyComponent` remains, because recreation opportunity policy has real independent ownership.
- `OffDutyCompletionHistory` is a plain C# helper owned by `ColonistBrain`; a MonoBehaviour was not
  justified merely to hold completion timestamps.

No `NeedsComponent`, `ServiceManager`, `OpportunityManager`, Utility AI, or generic intent
framework was introduced, and no Food inventory or resource economy exists yet.

---

# Multi-Colonist Expectations (Stack 2)

Stack 2 changes nothing about the ownership model above. It adds the rule that every piece of
*changing colonist state* is per instance, and that shared managers only ever answer questions
about a colonist they were handed.

## Per-colonist state that must never be shared

```text
brain state (Idle / Seeking / Active ...)
current Sleep, Work, Eat and OffDuty targets
stop-request flags and the latest free-time plan
Fatigue / Hunger / Stimulation / Relaxation
threshold-transition state
OffDuty completion and cooldown history
last decision signature (log deduplication)
```

None of these may live in a `static` field, and no manager may keep a single global "last target"
whose value changes what a different colonist does. Manager singletons
(`WorkforceManager.Instance`, `FoodManager.Instance`, `OffDutyManager.Instance`,
`SimulationLogManager.Instance`) are colony/world authority and remain shared by design.

## Canonical log subject

Colonist-originated events use the **ColonistIdentity** as the primary subject, not whichever
sibling component happened to emit them:

```text
ColonistBrain decision / offduty / activity-bridge events   primary = identity
ColonistStatsComponent need-threshold transitions            primary = identity
FoodManager discovery events                                 primary = seeking identity

activity involving a facility                                secondary = facility
```

This makes "show me Bob's history" one meaning even though Bob's needs, decisions and physical
activity lifecycle are recorded by three different components, and it keeps
`click Cafeteria -> events involving the Cafeteria` working through the secondary subject.
Management/system events may still use manager or system subjects.

## Personal cooldowns are not colony memory

```text
Bob completes play -> Bob/play enters cooldown
Dana has never completed play -> Dana/play is NOT on cooldown
```

The cooldown key is semantic (`play`, `relax`), not a facility instance, and the history object is
owned by one colonist's brain. Shared `relax` capacity is contended by *reservation*, not by
cooldown, so one colonist finishing an activity never removes the activity from anyone else.

## Shared capacity without queues

One public `Eat01` seat and one `Play01`/`Relax01` seat are shared by reservation group. There is
still no queue, waiting line, ticket number or fairness scheduler, and there must not be one yet:

```text
FoodManager discovers for a specific requester; it never reserves on anyone's behalf
ColonistActivityRunner reserves and releases; a lost race leaves it Idle, not stuck
A losing colonist stays free to reconsider on a later tick and leaks nothing
The next colonist may take the seat only after the current owner releases it
```

## Staffed service for several colonists at once

Service availability is evaluated per requester, at the same instant, from the same facility:

```text
Alice physically Serving -> outsider Bob and outsider Dana both get public_staffed
Alice off shift, still assigned -> Alice gets self_service, outsiders get unavailable
Alice reassigned away -> employee self-service disappears

Alice critically hungry while Serving
    -> she leaves Work, the counter closes to NEW diners
    -> she remains an assigned worker, so she may self-serve
    -> she eats, returns to Work if the shift remains, and the counter reopens

Bob already Eating when Alice leaves
    -> Bob keeps his meal; only structural target validity is re-checked
    -> a colonist still Seeking (walking/entering) loses access and unwinds
```

No colonist may be Working and Eating at the same time, and no worker is required merely for an
assigned employee to feed herself.
