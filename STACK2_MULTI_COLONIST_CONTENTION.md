# SPACEGAME — STACK 2
# MULTI-COLONIST CONTENTION AND INDEPENDENT LIVES

## EXECUTION INSTRUCTION

Execute **S2-0 through S2-8 sequentially and unattended**.

Do not stop after one ticket.

Successful completion of one ticket is not completion of this stack.

After each ticket:

1. run relevant EditMode tests if the environment permits;
2. repair implementation/test failures;
3. continue automatically;
4. do not repeatedly fight Unity licensing/server failures.

Unity Play Mode remains a human verification gate unless an already-running authorized Editor can execute it safely.

Do not mutate Assets/Bob.unity.

This stack uses the dedicated integration fixture:

~~~text
Assets/bobandfriends.unity
~~~

---

# AUTHORITATIVE BASELINE

Repository:

~~~text
https://github.com/artwhaley/spacegame
~~~

Stack authored against:

~~~text
main @ 98830b179590c26af9346c5ab5e23aac561654be
~~~

Fixture-creation commit:

~~~text
98830b179590c26af9346c5ab5e23aac561654be
Add Bob and friends multi-colonist test fixture
~~~

Reorient to current main before editing.

If newer commits have landed, preserve their behavior and adapt this packet to the actual current implementation.

---

# PURPOSE

Stack 1 proved the behavior of one colonist and introduced:

~~~text
Hunger / critical Hunger
Fatigue / proactive Rest
Stimulation / Relaxation

Work
Sleep
Eat
OffDuty

staff-dependent public Food
employee self-service

staff-dependent OffDuty
per-activity cooldown

structured simulation logging
~~~

Stack 2 asks a different question:

> Do those systems remain correct when several independent colonists use them at the same time?

The target is **not** sophisticated crowd AI.

The target is a small colony where:

~~~text
four colonists
have independent stats
have independent decisions
have independent cooldown histories
have independent work/sleep assignments

while sharing:

one Food seat
limited recreation
staff-dependent services
the same WorkforceManager
the same FoodManager
the same OffDutyManager
the same SimulationLogManager
~~~

This stack must expose and remove single-Bob assumptions before physical Food economy, modular facilities, shuttle travel and management UI are built on top.

---

# CURRENT FIXTURE

Assets/bobandfriends.unity is intentionally a functional test fixture rather than final game art.

It contains:

## Colonists

~~~text
Bob
    job: Farmer
    shift: 08:00–16:00
    bed: Sleep01

Alice
    job: Cafeteria Worker
    shift: 06:00–18:00
    bed: Sleep02

Charlie
    job: Command Operator
    shift: 08:00–16:00
    bed: Sleep03

Dana
    job: UNASSIGNED
    bed: Sleep04
~~~

Dana deliberately remains unassigned.

Do not assign Dana a permanent job in this stack.

Dana is intended to become a useful free colonist now and a pilot candidate later.

## Facilities

~~~text
CommandPod
    Sleep01
    Sleep02
    Sleep03
    Sleep04
    Command

Farm
    Farm

Cafeteria
    Eat
    ServeFood

Recreation
    play
    relax
~~~

## Recreation

~~~text
play
    stimulation recovery: 60 / game-hour
    relaxation recovery: 0
    cooldown key: play
    cooldown: 12 hours

relax
    stimulation recovery: 0
    relaxation recovery: 60 / game-hour
    cooldown key: relax
    cooldown: 12 hours
~~~

## Food

The Cafeteria is authored to require the physically active Cafeteria Worker for public service and to allow:

~~~text
SelfServicePolicy = AssignedWorkers
~~~

Alice must therefore be capable of feeding herself while the public counter is closed.

## Timing

The fixture starts around:

~~~text
05:30
~~~

so Alice's 06:00 shift becomes relevant quickly.

Starting needs are deliberately staggered.

Do not normalize all four colonists to identical stats.

The asymmetry is useful.

---

# ARCHITECTURE LOCK

Preserve the existing ownership model.

~~~text
ColonistStatsComponent
    owns one colonist's changing personal state

ColonistBrain
    makes one colonist's decisions

ColonistAssignments
    owns personal assignments such as bed

WorkforceManager
    owns colony work assignment/schedule truth

FoodManager
    discovers Food for a particular requester

OffDutyManager
    discovers discretionary activity for a particular requester

ColonistActivityRunner
    physically reserves/moves/performs/exits for one colonist

InteractableFacility
    owns physical activity choreography/reservations

SimulationLogManager
    records shared structured history
~~~

Do not create:

~~~text
ColonyBrain
global colonist state machine
generic queue manager
generic OpportunityManager
Utility AI
crowd controller that commands colonists
Bob-specific behavior
Alice-specific behavior
~~~

Multiple colonists must remain multiple instances of the canonical colonist architecture.

---

# S2-0 — REORIENT, IMPORT AND VERIFY THE FIXTURE

## Goal

Establish that bobandfriends.unity is a valid current integration scene before changing gameplay.

Inspect at minimum:

~~~text
Assets/bobandfriends.unity

canonical colonist prefab

ColonistIdentity
ColonistStatsComponent
ColonistAssignments
ColonistTargetResolver
ColonistBrain
ColonistActivityRunner
ColonistOverheadDisplay

WorkforceManager
WorkplaceComponent

FoodManager
FoodServiceComponent

OffDutyManager
OffDutyComponent
OffDutyCompletionHistory

SimulationManager
SimulationLogManager
~~~

## Unity import gate

If Unity Editor access is available without disrupting the human:

1. open/import bobandfriends.unity;
2. allow script/scene deserialization;
3. confirm no missing scripts;
4. confirm no missing object references;
5. confirm no compile errors introduced by the fixture;
6. save the scene only if Unity performs harmless serialization normalization.

Do not use this as an excuse to modify Bob.unity.

If Unity cannot be run safely, perform static YAML/reference inspection and clearly leave the import check for the human.

## Fixture assertions

Verify exactly four canonical colonist instances:

~~~text
Bob
Alice
Charlie
Dana
~~~

Each must have the canonical composition.

No colonist receives a special-purpose replacement brain or runner.

Verify four different sleep targets:

~~~text
Bob     -> CommandPod / Sleep01
Alice   -> CommandPod / Sleep02
Charlie -> CommandPod / Sleep03
Dana    -> CommandPod / Sleep04
~~~

Verify only three Workforce assignments.

Dana must be absent from the Workforce assignment roster.

## Automated fixture validation

Add an EditMode scene-fixture test if practical using Unity scene loading/editor APIs.

At minimum assert:

~~~text
scene exists
4 colonist identities
unique display names
4 distinct Sleep activity IDs
3 Workforce assignments
Dana unassigned

Farm role/activity valid
Cafeteria role/activity valid
Command role/activity valid

play OffDuty binding valid
relax OffDuty binding valid

FoodService staff requirement valid
AssignedWorkers self-service valid
~~~

Do not create a runtime BobAndFriendsManager just to validate the fixture.

This is test/editor responsibility.

---

# S2-1 — REMOVE SINGLE-COLONIST / SHARED-STATE ASSUMPTIONS

## Goal

Audit every system that was previously exercised primarily by Bob and prove that runtime mutable state belongs to the correct colonist.

Hard-audit:

~~~text
ColonistBrain
ColonistStatsComponent
ColonistAssignments
ColonistTargetResolver
ColonistActivityRunner

OffDutyCompletionHistory

FoodManager seeker signature/deduplication
OffDutyManager queries
SimulationLogManager
~~~

## Per-colonist state that MUST NOT be shared

Prove that these are per-colonist:

~~~text
brain state
current Sleep target
current Work target
current Eat target
current OffDuty opportunity

stop-request flags

latest free-time plan

Fatigue
Hunger
Stimulation
Relaxation

threshold transition state

OffDuty completion/cooldown history

last brain decision signature
~~~

Static mutable state is forbidden for any of those.

## Cooldown isolation

Explicitly test:

~~~text
Bob completes play
→ Bob/play enters cooldown

Dana has never completed play
→ Dana/play is NOT on cooldown
~~~

Likewise:

~~~text
Dana completes relax
→ Charlie/relax remains unaffected
~~~

Semantic cooldownKey is shared across equivalent activities for **one colonist**, not across the colony.

## Food-query isolation

FoodManager requester-aware deduplication already exists.

Prove:

~~~text
Bob asks for Food
Alice asks for Food
Dana asks for Food

their selection/no-target logging does not suppress each other
~~~

No manager may retain one global "last target" whose value changes the behavior of another colonist.

## Stats cache audit

ColonistStatsComponent may cache the active facility's OffDutyComponent.

Confirm that cache is instance-local and cannot make:

~~~text
Bob's active recreation
~~~

affect:

~~~text
Alice's effective leisure recovery
~~~

## Tests

Add focused isolation tests rather than relying only on Play Mode.

---

# S2-2 — NORMALIZE STRUCTURED LOG SUBJECTS TO COLONIST IDENTITY

## Goal

Before building UI, make "show me Bob's history" mean one thing.

At present, colonist-originated events may use different components as the primary log subject:

~~~text
ColonistBrain
ColonistStatsComponent
ColonistIdentity
~~~

That is acceptable for a developer console but poor as a future player-facing history model because querying the ColonistIdentity may miss events emitted by sibling components.

Normalize **game-side colonist events** so their canonical primary subject is the colonist's ColonistIdentity when one exists.

## Apply to

At minimum:

~~~text
ColonistBrain decision events

Brain-bridged activity lifecycle events

OffDuty completion/interruption events

ColonistStats threshold transition events

Food discovery events
    already requester-aware; preserve that
~~~

Do not alter the generic interaction package to know about ColonistIdentity.

The bridge in game-side code may translate:

~~~text
generic runner event
→ structured game event subject = colonist identity
~~~

## Facility relationship

For an activity involving a facility:

~~~text
PrimarySubject = colonist identity
SecondarySubject = facility
~~~

This gives future UI both:

~~~text
click Bob
→ Bob history

click Cafeteria
→ events involving Cafeteria
~~~

## System events

Manager/session/system events may continue to use manager/system subjects where appropriate.

Do not force every log entry to have a colonist.

## Tests

Prove:

~~~text
query by Bob ColonistIdentity
returns Bob need/decision/activity events

query by Alice
does not return Bob-only events

query by Cafeteria
returns Bob/Alice activity events involving Cafeteria

multiple colonists performing events at same game hour
remain separately identifiable
~~~

Do not solve permanent save-game identity here.

Runtime identity is sufficient for this slice.

---

# S2-3 — BED INDEPENDENCE AND SIMULTANEOUS BIOLOGY

## Goal

Prove personal assignment scales beyond one person.

The four beds intentionally live on one CommandPod InteractableFacility, but use distinct activity IDs and reservation groups.

## Test simultaneous Sleep demand

Drive all four colonists above Sleepy threshold in an automated test or controlled fixture scenario.

Expected:

~~~text
Bob     reserves Sleep01 / Bed01
Alice   reserves Sleep02 / Bed02
Charlie reserves Sleep03 / Bed03
Dana    reserves Sleep04 / Bed04
~~~

All four may sleep concurrently.

One sleeper must not block another simply because they share the same facility.

## Test assignment isolation

Changing Bob's Sleep assignment must not alter Alice's.

If one bed is deliberately reserved/unavailable:

~~~text
only the colonist assigned to that bed is blocked
~~~

Do not implement public bed discovery/fallback in this stack.

Personal bed assignment remains authoritative.

## Physical lifecycle

For each colonist independently verify:

~~~text
request
reserve own bed group
navigate
entry
Sleep active
wake/exit
release
~~~

No teleporting.

## No duplicate ownership

A given reservation group must never have two owners simultaneously.

Add reservation-level tests if current coverage does not already prove this with different colonist owners.

---

# S2-4 — SHARED FOOD CAPACITY AND RETRY WITHOUT QUEUES

## Goal

Prove multiple hungry colonists can share one scarce Eat reservation without duplicate occupancy or a new queue architecture.

Current fixture deliberately has one public Eat reservation group.

## Scenario

While Alice is physically working ServeFood:

~~~text
Bob becomes Hungry
Dana becomes Hungry
~~~

Both are individually eligible for public Food.

Only one can own Eat01 at a time.

Expected:

~~~text
first successful colonist
    reserves
    walks
    eats

other colonist
    does NOT reserve same group
    remains free to reconsider/retry

after first diner releases
    second may acquire and eat
~~~

## Important non-goal

Do NOT add:

~~~text
FoodQueue
WaitingLineManager
ticket numbers
fairness scheduler
restaurant host
~~~

yet.

This stack proves scarcity and retry.

Queues can be added if actual gameplay later demonstrates they are needed.

## No manager reservation

Preserve:

~~~text
FoodManager discovers
ColonistActivityRunner reserves
~~~

FoodManager must not hold seats on behalf of a query.

## Discovery race semantics

A candidate discovered as available may become unavailable before reservation.

The losing colonist must recover cleanly:

~~~text
RequestActivity returns false
or
target disappears during seeking

→ brain returns/reconsiders
→ no stuck EatSeeking
→ no leaked reservation
~~~

## Starvation sanity

Without adding fairness infrastructure, verify a two-colonist one-seat meal sequence terminates sensibly:

~~~text
first diner eventually stops being Hungry
second diner eventually gets a turn
~~~

If deterministic update ordering causes the same still-hungry colonist to repeatedly reacquire the seat and permanently starve another, fix the narrow cause.

Do not build a general queue unless that is genuinely required to resolve the demonstrated failure.

---

# S2-5 — STAFFED CAFETERIA AS A MULTI-COLONIST SYSTEM

## Goal

Turn the Stack 1 staffing rules into an actual cross-colonist physical scenario.

Alice is the Cafeteria Worker.

Bob, Charlie and Dana are consumers.

## Public opening

Before Alice is physically active at ServeFood:

~~~text
public Cafeteria access = unavailable
~~~

Merely being:

~~~text
assigned
scheduled
walking to work
entering activity
~~~

does not open public service.

When Alice is genuinely active at ServeFood:

~~~text
public service opens
~~~

## Alice self-service

Alice remains allowed to Eat through:

~~~text
AssignedWorkers
~~~

when she is assigned to the Cafeteria but not currently working.

No second cafeteria worker is required merely for Alice to feed herself.

## Critical Hunger cascade

Exercise:

~~~text
Alice Working
→ Alice reaches Critical Hunger
→ Alice requests physical Work exit
→ public Cafeteria closes
→ Alice self-serves Eat
→ Alice reaches non-critical state / normal Eat completion policy
→ if shift remains current:
      Work wins again
→ Alice returns to ServeFood
→ public Cafeteria reopens
~~~

Do not keep Alice simultaneously Working and Eating.

## Existing diner continuation

Exercise:

~~~text
Bob already genuinely Eating
Alice leaves ServeFood
~~~

Expected:

~~~text
Bob keeps Eating

new public diners cannot begin

Alice may feed herself if eligible
~~~

For this slice, genuine Eat activation remains the FoodService proxy for:

~~~text
service fulfilled / diner possesses meal
~~~

Do not hard-code that proxy outside FoodService semantics.

Future explicit worker/customer meal-service choreography must remain possible.

## Seeking diner loses access

If Dana is still:

~~~text
walking / entering / EatSeeking
~~~

when Alice leaves:

~~~text
Dana has NOT yet been served
→ stop/unwind
→ reconsider
~~~

No leaked reservation.

## Logging

The event stream should make the causal story readable:

~~~text
Alice critical hunger
Alice leaves ServeFood
Cafeteria becomes unavailable to new diners
Alice selects self-service Eat
Bob continues existing meal
Dana loses not-yet-fulfilled access
Alice finishes
Alice returns to work
service resumes
~~~

Do not invent prose-driven game logic.

Structured fields remain authoritative.

---

# S2-6 — RECREATION CONTENTION AND DIVERGENT CHOICES

## Goal

Use play and relax to prove multiple colonists can make different discretionary choices from independent drive state.

## Scenario A — same desired activity

Configure two colonists with:

~~~text
Stimulation above threshold
Relaxation below threshold
~~~

Only one play reservation is available.

Expected:

~~~text
one gets play
other does not duplicate-occupy it
~~~

The loser may remain Idle/retry.

It must not select relax unless Relaxation is actually an unmet active drive.

## Scenario B — both drives active

Configure Dana with both drives active while play is occupied.

Expected:

~~~text
primary drive evaluated

if primary's valid opportunity unavailable
and secondary drive also exceeds threshold
→ secondary may be selected

Dana can relax while Bob plays
~~~

This is desirable apparent complexity produced by simple independent drives.

## Scenario C — cooldown isolation

After Bob completes play:

~~~text
Bob/play cooling down
~~~

Dana must still be able to choose play if Dana's personal history permits it.

After Dana completes relax:

~~~text
Charlie/relax remains available
~~~

## Scenario D — independent need recovery

While Bob plays:

~~~text
Bob Stimulation decreases
~~~

Alice's/Charlie's/Dana's Stimulation values must not change because of Bob's activity.

Likewise Relaxation.

## No random personality yet

Do not add:

~~~text
preference weights
personality traits
friend groups
random choice
addiction
social simulation
~~~

The point is independence, not richness.

---

# S2-7 — WORKFORCE INDEPENDENCE AND UNASSIGNED DANA

## Goal

Prove several simultaneous regular jobs work without turning WorkforceManager into an actor controller.

The fixture has:

~~~text
Bob
    Farm / Farmer

Alice
    Cafeteria / Cafeteria Worker

Charlie
    CommandPod / Command Operator

Dana
    no assignment
~~~

## Work start

At the appropriate shifts:

~~~text
each assigned colonist independently resolves their own Work target

each physically travels to their own workplace

each becomes Working only when their own activity is genuinely active
~~~

No colonist may inherit another colonist's assignment.

## Dana

Dana must:

~~~text
have no current/next Work obligation

never enter WorkSeeking because someone else has a shift

remain available for biology and OffDuty
~~~

Do not invent unemployment behavior yet.

Idle/leisure/needs are sufficient.

## Worker absence effects

Alice's absence has the special public-service consequence already described.

Bob's and Charlie's absence currently need only affect their own job presence.

Do not invent Farm production or Command bonuses in this stack.

Those consequences arrive through later economy/system work.

## Assignment replacement test

Use WorkforceManager API in tests:

~~~text
assign Dana to a role
→ only Dana changes assignment

unassign Bob
→ Alice/Charlie assignments remain

reassign Bob
→ no duplicate assignment records
~~~

Do not mutate the default fixture to permanently employ Dana.

---

# S2-8 — INTEGRATION HARDENING, REPORT AND HUMAN PLAYTEST

## Automated adversarial audit

Before closeout, search specifically for:

~~~text
static mutable colonist decision state

shared OffDuty history

global Food selection signature

Bob-specific object lookup/name checks

FindObjectOfType<Colonist...> assumptions

code assuming exactly one ColonistIdentity

code assuming exactly one Sleep activity

one colonist's activity changing another's stats

one colonist's cooldown blocking another

logs whose subject cannot be traced to the actual colonist

reservation release owned by the wrong colonist

Workforce assignment leakage

Dana accidentally receiving a job
~~~

Using manager singletons is fine where the manager represents colony/world authority.

Using singleton state for an individual colonist is not.

## Test suite

Run relevant tests including:

~~~text
ColonistStatsTests
ColonistBrainTests
ColonistAssignmentsTests
ColonistTargetResolverTests
ColonistActivityRunner tests
ColonistFreeTimePlannerTests

FoodManagerTests
FoodServiceAccessTests

OffDutyManagerTests
OffDutyCompletionHistoryTests

WorkforceManagerTests
WorkplaceComponentTests

SimulationLogManagerTests

new multi-colonist isolation tests
new bobandfriends scene fixture tests
new contention tests
~~~

Run the full EditMode suite if practical.

Run:

~~~text
git diff --check
~~~

## Report

Create:

~~~text
STACK2_MULTI_COLONIST_REPORT.md
~~~

Include:

~~~text
starting SHA
ending SHA

files changed

fixture modifications if any

tests added
tests executed
pass/fail counts

environment limitations

single-colonist assumptions found
single-colonist assumptions repaired

manual Play Mode results
or explicit statement that Play Mode was not human-verified

remaining known problems
~~~

Never claim human Play Mode verification unless a human actually performed it.

---

# HUMAN ACCEPTANCE SCRIPT

Open:

~~~text
Assets/bobandfriends.unity
~~~

Use the canonical fixture unless a corrective ticket had to repair an authoring error.

## TEST A — FOUR PEOPLE ARE ACTUALLY INDEPENDENT

At startup confirm Bob, Alice, Charlie and Dana all exist as canonical colonist prefab instances.

Inspect their needs and brain states.

They should not all make the same decisions merely because one of them changed state.

## TEST B — ALICE OPENS THE CAFETERIA

Run through Alice's 06:00 shift start.

Expected:

~~~text
before Alice physically works:
    public food unavailable

Alice walks/enters ServeFood

when genuinely Working:
    public food becomes available
~~~

Assignment or clock time alone is not enough.

## TEST C — TWO HUNGRY COLONISTS, ONE SEAT

Make Bob and Dana Hungry while Alice serves.

Expected:

~~~text
only one owns Eat01

one eats

other waits through normal reconsideration/retry

after release:
    other can eat
~~~

Inspect reservations/logs.

Never two diners in one reserved slot.

## TEST D — ALICE FEEDS HERSELF

While Alice is assigned to Cafeteria but public service is closed, make Alice Hungry.

Expected:

~~~text
Alice may self-serve
without a second worker
~~~

If she becomes Critically Hungry while serving:

~~~text
leave Work
counter closes
self-serve
eat
return to Work if shift remains
counter reopens
~~~

## TEST E — BOB KEEPS HIS SANDWICH

Have Bob genuinely Eating.

Cause Alice to leave ServeFood.

Expected:

~~~text
Bob continues Eating

new diners cannot start
~~~

This verifies:

~~~text
service availability
!=
continuation of fulfilled meal
~~~

## TEST F — FOUR DISTINCT BEDS

Force all four colonists Sleepy with no Work/Hunger override.

Expected:

~~~text
Bob     -> Sleep01
Alice   -> Sleep02
Charlie -> Sleep03
Dana    -> Sleep04
~~~

All four should be able to sleep concurrently.

## TEST G — PLAY VERSUS RELAX

Make:

~~~text
Bob:
    Stimulation high
    Relaxation low

Charlie:
    Relaxation high
    Stimulation low
~~~

Expected:

~~~text
Bob chooses play
Charlie chooses relax
~~~

Then give Dana both needs high while one opportunity is occupied.

Observe valid fallback behavior.

## TEST H — COOLDOWN IS PERSONAL

Let Bob complete play.

Before Bob's 12-hour cooldown expires, make Dana strongly need Stimulation.

Expected:

~~~text
Dana can still play
~~~

Bob's personal recreation history must not become colony history.

## TEST I — THREE WORKERS, ONE UNASSIGNED

Run through 08:00.

Expected:

~~~text
Bob     -> Farm
Alice   -> Cafeteria
Charlie -> Command
Dana    -> no job
~~~

Dana should continue making biological/discretionary decisions.

## TEST J — LOG AUDIT

After at least several game-hours, inspect structured history.

For each colonist, it must be possible to answer:

~~~text
what need changed?

what did they decide?

what target did they choose?

did they reserve it?

when did activity become genuinely active?

why did it end?

what did they do next?
~~~

Filtering/querying by Bob identity must not produce Alice-only events.

Filtering/querying the Cafeteria must show events involving both worker and customers.

This acceptance matters because Stack 6 will build player UI on top of this history.

---

# CORRECTIVE-TICKET RULE

The scene was authored programmatically from Bob.unity.

If Unity import reveals a fixture-authoring error such as:

~~~text
missing reference
bad scene-local file ID
bad prefab override
activity anchor in wrong hierarchy
invalid role reference
colonist spawning off NavMesh
~~~

fix the fixture directly as a corrective part of Stack 2.

Do not redesign runtime architecture merely to accommodate a malformed fixture.

Conversely, if the scene correctly exposes a real multi-colonist runtime bug, fix the runtime system rather than hiding the problem in scene authoring.

Document which category each correction belonged to.

---

# EXPLICIT NON-GOALS

Do not implement in Stack 2:

~~~text
Food inventory
Farm production feeding Cafeteria
meal resources
physical plates
waiter/customer service choreography

reservation queues
restaurant waiting lines

personality
friendship
social needs
addiction
recreation quality/preference

facility prefab extraction
local NavMesh modules
NavMesh link architecture

shuttle passenger transport
pilot job

management UI
colony UI

save/load

procedural construction
~~~

Do not turn Dana into a pilot yet.

Those are later roadmap stacks.

---

# STACK 2 ACCEPTANCE

Stack 2 is complete when:

~~~text
bobandfriends.unity imports cleanly.

Four canonical colonists run simultaneously.

Their mutable state is independent.

Their OffDuty cooldown histories are independent.

Their Food queries/log dedupe are independent.

Their beds are personal and can be used concurrently.

Three independent Work assignments function.

Dana remains genuinely unassigned.

A staffed Cafeteria opens/closes based on Alice physically Working.

Alice may self-serve without a second worker.

Two public diners contend correctly for one Eat reservation.

An already-served diner can finish after staffing disappears.

play and relax create divergent leisure behavior.

shared recreation capacity cannot be double-occupied.

structured logs identify the actual colonist as canonical subject.

facility history remains queryable through secondary subject.

No queue system or Utility AI was introduced.
~~~

---

# WHAT THIS HANDS TO STACK 3

After Stack 2 passes, the simulation will have enough independent people and scarce capacity to make physical Food economy meaningful.

Stack 3 can then safely make:

~~~text
Farm
→ Food inventory

Cafeteria
→ requires Food

Eat
→ consumes actual Food
~~~

because there will already be:

~~~text
multiple consumers
a real Food worker
service outages
reservation contention
individual Hunger
observable causal logging
~~~

That is the first point at which a resource shortage can create a genuine colony-level failure cascade rather than merely changing Bob's private state.
