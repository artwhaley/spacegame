# STACK 2 — MULTI-COLONIST CONTENTION AND INDEPENDENT LIVES — EXECUTION REPORT

> **Status: code and tests written, NOT executed.**
> `PLAY MODE HAS NOT BEEN CLAIMED AS VERIFIED.` No EditMode test in this report was run, because
> this session was explicitly instructed not to launch, close or drive any Unity process, and not
> to run headless. Everything below that says "verified" means *verified statically* by reading
> source and by running an out-of-Unity check against the real files — never by executing Unity.

---

## 1. Baselines

```text
Stack 1 code (leisure drives, requester-aware food)   8c8d870e
Fixture + packet baseline (this stack authored against) 98830b17 / d5f3db3a
Checkout when Stack 2 execution began                 d5f3db3a (in sync with origin/main)
Working tree at report time                           d5f3db3a + uncommitted changes below
```

Nothing in this stack was committed; the working tree still contains the unrelated Synty/HDRP
material and prefab edits that other threads left in place, untouched.

## 2. Environment limitations

```text
Unity Editor            never launched, opened, focused or closed by this session
Headless / batchmode     not attempted
Play Mode                not entered
Test Runner              not invoked
Compilation              not observed by this session (no compiler was run)
```

Everything that could be checked without Unity was checked (section 7).

## 3. Files changed

Modified:

```text
Assets/Scripts/ColonyPrototype/People/ColonistBrain.cs          +5 log-subject sites, +LogSubject
Assets/Scripts/ColonyPrototype/People/ColonistStatsComponent.cs +identity field, +LogSubject, +1 site
LEISURE_AND_FOOD_ACCESS.md                                      +multi-colonist section
```

Added:

```text
Assets/Tests/EditMode/MultiColonistIsolationTests.cs      (+ .meta)   13 tests
Assets/Tests/EditMode/FoodContentionTests.cs              (+ .meta)    7 tests
Assets/Tests/EditMode/BobAndFriendsFixtureTests.cs        (+ .meta)   12 tests
STACK2_MULTI_COLONIST_REPORT.md                           (this file)
```

No scene, prefab, material or ProjectSettings file was modified. `Assets/Bob.unity` was not
touched (it was only read as a reference for script-guid resolution).

## 4. Ticket results

### S2-0 — Reorient, import and verify the fixture

The fixture was inspected as data: scene documents, prefab-instance overrides, facility activity
bindings, workplace role bindings, the food service, the off-duty bindings, the workforce roster
and the clock. The Unity import gate (open the scene, confirm no missing scripts/references, save
if Unity normalizes serialization) belongs to the human step and was **not** performed.

Confirmed authored facts:

```text
four canonical colonist prefab instances, no special-purpose brains or runners
displayName    Bob / Alice / Charlie / Dana (unique)
starting state Bob f20 h25 s60 r10 | Alice f15 h45 s10 r60 | Charlie f25 h20 s20 r60 | Dana f10 h50 s70 r70
sleep target   Bob Sleep01 / Alice Sleep02 / Charlie Sleep03 / Dana Sleep04
workforce      exactly three assignments (Farm/Farmer, Cafeteria/Cafeteria Worker, Command/Command Operator)
               Dana absent from the roster
CommandPod     Sleep01..Sleep04 -> Bed01..Bed04, plus Command -> Command01
Cafeteria      Eat -> Eat01, ServeFood -> ServeFood01
Recreation     play -> Play01, relax -> Relax01
play binding   cooldownKey play, cooldown 12, stimulation 60, relaxation 0
relax binding  cooldownKey relax, cooldown 12, stimulation 0, relaxation 60
FoodService    eatActivityId Eat, recovery 60, requiresStaff true,
               requiredWorkplace = Cafeteria WorkplaceComponent,
               requiredRole = CafeteriaWorker.asset, minimumActiveWorkers 1,
               selfServicePolicy AssignedWorkers
job roles      all three workplace role guids resolve to real JobRoleDefinition assets
clock          05:30 (5.5)
```

Fixture authoring errors found: **none.** The corrective-ticket rule therefore produced no
corrections — there was no missing reference, bad scene-local file id, bad prefab override,
misplaced anchor or invalid role reference to repair, and no runtime bug was blamed on scene
authoring.

Automated fixture validation was added as `BobAndFriendsFixtureTests` (12 tests). It reads the
scene **as text** rather than opening it, deliberately:

- opening the scene in an EditMode test would change which scene a human has open in the Editor;
- the assertions are about authored content, which the serialized document states directly;
- the test is therefore hermetic and cannot be affected by (or affect) editor state.

Two consequences to accept knowingly: it cannot observe "missing script" the way Unity's
inspector does, so `EveryScriptReferencedByTheFixtureResolvesLocallyOrInTheWorkingScene` instead
asserts that every `m_Script` guid in the fixture resolves either to a local script meta or to a
script already referenced by the working `Bob.unity`; and it does not deserialize prefab
overrides, so it validates the authored values rather than the imported result. The human import
gate covers the rest.

### S2-1 — Remove single-colonist and shared-state assumptions

Audited for static mutable colonist state across `Assets/Scripts/ColonyPrototype` and the
interactions package:

```text
static mutable fields found: SimulationManager.nextRegistrationOrdinal,
                             PresentationTime.source
static per-colonist state found: none
```

Every mutable field named by the packet is already per instance:

```text
brain state, current Sleep/Work/Eat/OffDuty targets, stop flags, free-time plan  -> ColonistBrain instance
Fatigue / Hunger / Stimulation / Relaxation, threshold state                     -> ColonistStatsComponent instance
OffDuty completion + cooldown history                                            -> owned by one brain
last decision signature (log de-duplication)                                     -> ColonistBrain instance
Food query de-duplication                                                        -> per-seeker list in FoodManager
OffDuty queries                                                                  -> carry the seeking identity
active recreation cache in ColonistStatsComponent                                -> instance-local, keyed by facility
```

`SimulationManager.nextRegistrationOrdinal` is a counter for deterministic tick ordering, not
colonist state; it is documented as a known consequence in section 6. No `FindObjectOfType`-style
colonist lookup exists in colony runtime code (the only `ColonistAgent` scans are the colony
roster scans in `PopulationManager`/`StaffingManager`, which are world authority by design), no
code hardcodes `Sleep01`/`Bed01`/`Eat01` activity ids, and no manager keeps a global "last target"
that changes another colonist's behaviour.

New isolation tests: `MultiColonistIsolationTests` (13 tests), covering independent needs,
independent ticking, instance-local recreation recovery, per-colonist cooldowns, per-colonist bed
resolution, assignment isolation, and workforce roster isolation.

### S2-2 — Normalize structured log subjects to colonist identity

**This was the one real runtime change required by the audit.** Colonist-origin events were being
attributed to the *component that emitted them* — `ColonistBrain` for decisions, the off-duty
bridge and activity lifecycle, `ColonistStatsComponent` for need transitions — so a query by the
colonist's `ColonistIdentity` silently missed them (`SimulationLogManager.Query` matches by
subject entity id).

Both components now resolve a canonical subject:

```csharp
private UnityEngine.Object LogSubject
{
    get
    {
        if (identity == null)
            identity = GetComponent<ColonistIdentity>();

        return identity != null ? (UnityEngine.Object)identity : (UnityEngine.Object)this;
    }
}
```

Applied at all six colonist-origin sites: brain `RecordDecision`, `work.left_for_critical_need`,
`offduty.target_selected`, `offduty.completed`/`offduty.interrupted`, the activity-lifecycle
bridge, and the stats threshold transition. Facilities stay the **secondary** subject, and
`FoodManager` already used the seeking identity as primary. Manager/system events keep their
manager subjects; nothing in the interactions package learned about `ColonistIdentity`.

Tests prove a brain decision and a stats need transition both resolve to the colonist, that one
colonist's history does not contain another's events, and that facility history stays queryable
through the secondary subject while still naming the acting colonist.

### S2-3 — Bed independence and simultaneous biology

Covered by tests rather than new code; the CommandPod already exposes four activities with four
reservation groups.

```text
each colonist resolves only its own assigned activity id
the four assignments map to four distinct reservation groups
all four groups can be held concurrently by four different owners
a fifth owner cannot duplicate an occupied bed group
releasing one bed lets another colonist take it
reassigning one colonist's bed leaves every other assignment alone
```

No public bed discovery or fallback was added; personal assignment remains authoritative.

### S2-4 — Shared food capacity and retry without queues

No queue, waiting line, ticket or fairness scheduler was introduced, and no new code was needed:
`ColonistActivityRunner.StartActivityRequest` already treats a lost reservation race as
`Phase = Idle` with no reservation retained, so the losing colonist simply reconsiders.

```text
one Eat01 seat cannot be owned by two diners
the winner keeps the seat; the loser cannot clear it
the loser discovers no target while the seat is taken and logs its own food.no_target
the loser holds no reservation and is not stuck in a seeking state
after release the loser acquires the seat normally
```

### S2-5 — Staffed cafeteria as a multi-colonist system

Also test-only; the Stack 1 semantics hold. Tests now exercise the cross-colonist story:

```text
two outsiders discover the same counter independently at the same instant, each logging its own selection
a critically hungry server leaves Work, the counter closes to new diners,
    she keeps her assignment, self-serves, and remains eligible to return
an already-served diner finishes the meal after the server leaves (hunger recovery continues)
a diner still seeking loses access, unwinds and releases its reservation
employee self-service disappears when the employee is reassigned away
```

`FoodServiceComponent.IsConfigured` remains structural; runtime staffing and requester access stay
separate queries.

### S2-6 — Recreation contention and divergent choices

Tests only; `play`/`relax` and per-drive filtering already exist. Coverage:

```text
two colonists with different unmet drives choose different activities
a relaxation drive is never satisfied by a stimulating-only activity
a single Play01 seat cannot be double-occupied
after the seat is released the next colonist may take it
one colonist's cooldown does not remove an activity from another colonist
```

### S2-7 — Workforce independence and unassigned Dana

Tests only; `WorkforceManager` was already per-colonist in both directions.

```text
three assignments resolve independently
Dana has no current or next shift and never enters work seeking
assigning Dana changes only Dana, and keeps no duplicate record
unassigning one colonist leaves the others intact
reassigning a colonist leaves exactly one record for that colonist
```

Dana was not assigned a job in the fixture.

### S2-8 — Integration hardening, report, human playtest

Adversarial audit performed (section 6). `STACK2_MULTI_COLONIST_REPORT.md` is this file. The human
playtest is section 9.

## 5. Tests added (32, all unexecuted)

```text
Assets/Tests/EditMode/MultiColonistIsolationTests.cs  13
Assets/Tests/EditMode/FoodContentionTests.cs           7
Assets/Tests/EditMode/BobAndFriendsFixtureTests.cs    12
```

They are written in the conventions the existing suite already uses (NUnit, `AsteroidColony.Tests`,
reflection for private fields, no scene loading, no coroutines). To keep them deterministic they
set manager singletons explicitly instead of depending on whether EditMode invokes `Awake`, and
they replace manager service/provider lists with exactly the test's own, so recreation or food
that happens to live in whatever scene is open cannot win a discovery query.

**Executed: none. Pass/fail counts: not available.** Running them is the first item of the human
checklist.

## 6. Single-colonist assumptions: found, repaired, not repaired

Repaired:

```text
colonist events attributed to a sibling component instead of the colonist  -> LogSubject in brain + stats
```

Found but already correct (no change needed):

```text
no static mutable colonist state
per-colonist cooldown history
per-seeker food query de-duplication
off-duty queries carrying the seeker
instance-local recreation-recovery cache
no hardcoded activity ids or colonist lookups in colony runtime code
reservation ownership released by the owning runner
```

Deliberately not "repaired":

```text
SimulationManager.nextRegistrationOrdinal gives deterministic tick order, so when two colonists
    contend on the same tick the earlier-registered colonist wins. That is world-authority ordering,
    not shared colonist state, and it does not starve the loser (the winner leaves when its need is
    satisfied and the seat is released). No fairness scheduler was added; that is an explicit
    non-goal of this stack.
FoodManager.Awake / OffDutyManager.Awake scan the scene for services and providers. That is world
    authority over authored content, not colonist state, and it is unchanged.
```

## 7. Verification actually performed (no Unity)

```text
structural check on the 5 touched C# files      braces/parens balanced, no tabs, no trailing whitespace
git diff --check on the touched files           clean (tree-wide output is dominated by the
                                               other thread's material edits, which are untouched)
fixture assertion dry-run                       every assertion in BobAndFriendsFixtureTests was
                                               re-implemented outside Unity and run against the real
                                               Assets/bobandfriends.unity: all 27 checks pass
API cross-check                                 every type, property, field and overload used by the
                                               new tests was read from the current source
reflection field-name check                     every private field and property the tests set exists
```

The fixture dry-run earned its keep: it caught a real parser bug in the fixture test (it returned
as soon as `cooldownKey` was read, before `stimulationRecoveryPerGameHour`, so `play` and `relax`
recovery rates would never have been asserted). Both the test and the dry-run were fixed.

Not verified, and not claimed:

```text
C# compilation
test execution, pass/fail counts
scene import, missing scripts, missing references, NavMesh placement
any Play Mode behaviour
```

## 8. Remaining known problems and risks

```text
1. The whole suite is unexecuted. The likeliest early failure is a compile error in the new test
   files, and the second likeliest is an assertion that depends on EditMode singleton timing.
2. Whether EditMode invokes Awake on AddComponent is not established in this repo; the new tests
   avoid depending on it, but the Stack 1 tests do not (FoodServiceAccessTests relies on
   WorkforceManager/FoodManager instances existing after AddComponent).
3. The fixture tests validate authored text, not imported objects; the import gate still needs a
   human.
4. NavMesh placement of the four colonists and the reachability of every approach anchor is
   unverified; that is exactly the class of problem humans catch in Play Mode.
5. Meal length is implicit: an Eat request ends when Hunger reaches zero, not after an authored
   duration.
```

## 9. Human verification checklist

Unity steps:

```text
1. Let Unity import. Confirm no compile errors in ColonyPrototype.Runtime, the interactions
   package, ColonyPrototype.Tests or ColonyPrototype.Editor.
2. Run the full EditMode suite; report failures. Especially:
       MultiColonistIsolationTests, FoodContentionTests, BobAndFriendsFixtureTests,
       and the Stack 1 files (ColonistStatsTests, ColonistBrainTests, OffDutyManagerTests,
       OffDutyCompletionHistoryTests, FoodServiceAccessTests, FoodManagerTests)
3. Open Assets/bobandfriends.unity. Confirm four colonists (Bob, Alice, Charlie, Dana), no missing
   scripts, no missing references, four distinct beds, all four colonists standing on the NavMesh.
4. Confirm the colonist prefab inspector shows the Stimulation/Relaxation fields and that the
   stats component's Identity field resolves to the colonist's ColonistIdentity.
5. Confirm Human Tests A–J below, then check the structured log for each colonist.
```

Play Mode acceptance (from the packet):

```text
A  Four people are actually independent (per-colonist needs and decisions)
B  Alice opens the Cafeteria only when physically Serving (06:00 shift)
C  Two hungry colonists, one Eat01 seat: one eats, the other retries, no double occupancy
D  Alice feeds herself off shift; critically hungry while serving -> leaves Work, counter closes,
   self-serves, returns if the shift remains, counter reopens
E  Bob keeps his sandwich when Alice leaves ServeFood; new diners cannot start
F  Four distinct beds, four colonists sleeping concurrently
G  Play versus relax: Bob chooses play, Charlie chooses relax; Dana falls back validly when busy
H  Cooldown is personal: Bob's completed play does not block Dana's play
I  Three workers, one unassigned colonist at 08:00; Dana keeps making her own decisions
J  Log audit: per colonist, answer what need changed, what was decided, what target was chosen,
   whether it was reserved, when it became active, why it ended, what happened next;
   querying by Bob must not return Alice-only events; querying the Cafeteria must show both the
   worker and her customers
```

`PLAY MODE HAS NOT BEEN CLAIMED AS VERIFIED.`
