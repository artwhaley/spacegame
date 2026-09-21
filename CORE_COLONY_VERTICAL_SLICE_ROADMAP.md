# SPACEGAME — CORE PLAYTEST ROADMAP

## Objective

Reach the smallest playable colony that proves the core premise:

> The player assigns people, jobs, beds, facilities and priorities; colonists physically live those decisions; production, logistics, schedules and biological needs interact; failures propagate through the colony in understandable ways; and the player can intervene and recover.

This is **not** the minimum collection of technical systems.

It is the minimum version that lets us answer:

> Is managing and watching this little colony actually fun?

The current OffDuty / biological-planning / structured-log stack lands first.

After that, build the following ticket stacks in order.

---

# TARGET PLAYTEST

The first serious playtest should contain approximately 4–6 colonists.

Example cast:

```text
Bob
    Farmer

Alice
    Cafeteria worker

Charlie
    Shuttle pilot

Dana
    General colonist / consumer

Optional additional colonists
    relief worker
    second general colonist
```

Facilities:

```text
Habitat
    assigned beds

Cafeteria
    Eat
    cafeteria Work activity

Farm
    Farmer Work activity
    produces Food

Recreation
    Play

Shuttle / transit infrastructure

Remote site
    Farm and/or storage far enough away to require Shuttle
```

The player must be able to:

```text
assign jobs
assign shifts
assign beds

inspect colonists
inspect facilities

watch needs
watch decisions
watch physical activity

see inventories/resources
see structured history

create staffing/logistics failures
repair them
```

---

# STACK 1 — STAFFED SERVICES AND PERSONAL ACCESS

## Purpose

Prove that public activities can depend on workers actually being present.

This is the bridge between:

```text
employment
```

and:

```text
the rest of the colony being able to use a service.
```

It also solves the Alice sandwich problem.

---

## Important distinction

A service has two separate questions:

### Question A

> Is this service currently available to the general public?

Example:

```text
Is the Cafeteria serving colonists right now?
```

That may require:

```text
Alice assigned as Waiter
AND
Alice currently on shift
AND
Alice physically performing the Waiter work activity
```

### Question B

> Is this particular colonist personally allowed to use this service even though it is not publicly staffed?

Example:

```text
Alice is off duty.
The cafeteria is closed.

Can Alice walk into the kitchen and make herself a sandwich?
```

Yes.

These must not be represented by the same boolean.

---

## Initial service access policy

Extend `FoodServiceComponent` with a narrow access policy.

Conceptually:

```text
Public availability:
    requires staffed role = Cafeteria Worker

Self-service:
    Assigned workers may self-serve = YES
```

A useful initial enum may be:

```text
SelfServicePolicy

None
AssignedWorkers
Everyone
```

For the Cafeteria:

```text
Public use requires active worker.

SelfServicePolicy = AssignedWorkers
```

Therefore:

```text
Alice working
→ Bob can Eat
→ Alice can Eat

Alice off duty
→ Bob cannot Eat
→ Alice can Eat

Nobody assigned to Cafeteria
→ Bob cannot Eat
→ Alice is no longer an assigned worker and cannot use employee self-service
```

Whether an entirely unstaffed kitchen should eventually permit everybody to make food is a content decision:

```text
SelfServicePolicy = Everyone
```

could represent that later.

---

## Important Alice behavior

If Alice becomes hungry while working:

```text
Alice leaves Work
→ public Cafeteria service temporarily becomes unavailable
→ Alice performs Eat using her employee self-service permission
→ Alice finishes
→ if her shift remains active, she returns to Work
→ public service becomes available again
```

That temporary closure is desirable.

It gives staffing consequences physical meaning.

We do NOT invent:

```text
Alice simultaneously Working and Eating
```

just to keep the Cafeteria magically open.

---

## Personalized discovery

Food discovery must therefore eventually know **who is asking**, not merely:

```text
Vector3 seekerPosition
```

The FoodManager query should be capable of evaluating:

```text
seeker identity
position
service availability
personal service-access exception
```

FoodManager still reports opportunities.

It does not command Alice or Bob.

---

## Shared staffing truth

FoodService and OffDuty staffing rules will now share a conceptual question:

> Are the required workers physically providing this service for long enough?

A small shared helper/query is acceptable.

Do NOT create a generic `OpportunityManager`.

Keep:

```text
FoodManager
OffDutyManager
WorkforceManager
```

with their existing responsibilities.

---

## This stack proves

```text
Alice works
→ Cafeteria opens

Alice leaves
→ Cafeteria closes

Alice can still feed herself

Alice returns
→ Cafeteria reopens

Bob sees service availability change dynamically
```

---

# STACK 2 — MULTI-COLONIST CONTENTION

## Purpose

Stop proving systems with one immortal test subject.

Create a small real population using the canonical colonist architecture.

Target:

```text
4–6 colonists
```

All use the same:

```text
ColonistIdentity
ColonistStatsComponent
ColonistAssignments
ColonistTargetResolver
ColonistBrain
ColonistMotor
ColonistAnimationDriver
ColonistActivityRunner
ColonistOverheadDisplay
```

No Bob-specific behavior.

---

## Content

Create different initial assignments.

Example:

```text
Bob
    Farmer

Alice
    Cafeteria Worker

Charlie
    Shuttle Pilot

Dana
    Unassigned / general colonist
```

Give everyone:

```text
bed
needs
schedule where applicable
```

Use staggered values initially so everyone does not become Hungry/Sleepy on exactly the same simulation tick.

---

## Prove reservation contention

Deliberately expose limited capacity.

Examples:

```text
one food-service reservation
multiple hungry colonists

one recreation slot
multiple off-duty colonists

limited beds
```

Correct outcome:

```text
Manager discovery finds viable opportunities.

Runner attempts reservation.

Only actual available capacity succeeds.

Loser remains free to reconsider/retry.
```

No manager-level fake reservations.

No duplicated queue system yet.

---

## Prove workforce contention

Multiple colonists should allow us to observe:

```text
worker absent
service unavailable

worker returns
service available

two people want same opportunity
only one gets it

different people independently make different choices
```

---

## Logging acceptance

The structured history must let us reconstruct individual stories.

Example:

```text
Bob:
Hungry
→ selected Cafeteria
→ lost reservation to Dana
→ reconsidered
→ later acquired Eat

Alice:
Working
→ Critical Hunger
→ left Work
→ Cafeteria closed
→ self-served
→ returned to Work
```

If those histories cannot be understood from the log, fix observability before moving on.

---

# STACK 3 — PHYSICAL FOOD ECONOMY

## Purpose

Replace infinite imaginary sandwiches with the first complete economic survival loop.

This is the point where Hunger becomes economically meaningful.

---

## Ownership rule

There must be exactly one canonical consumption path for personal Food.

The existing aggregate population Food consumption must no longer also consume the same Food resource after individual Eating becomes authoritative.

Avoid:

```text
PopulationResourceConsumer consumes Food
AND
Alice consumes Food when Eating
```

That would double-charge the colony.

---

## Initial production chain

Keep it brutally small:

```text
Farm
    produces Food

Food exists in inventory

Cafeteria
    needs Food to serve a meal

Eating
    consumes Food
    reduces Hunger
```

No recipes with twelve ingredients.

No dietary system.

No meal quality.

No cooking skill yet.

---

## Food service availability

Cafeteria publishes Eat only if:

```text
staff requirement is satisfied
OR
requester qualifies for self-service

AND

sufficient Food exists for the meal
```

Discovery still does not consume the Food.

Consumption happens at a clearly defined physical lifecycle boundary.

Prefer:

```text
meal/resource committed when genuine Eat begins
```

with explicit behavior if commitment fails.

Do not consume Food merely because Bob looked at the Cafeteria.

---

## Prevent race problems

Two colonists discovering the last meal must not both magically eat it.

Reservation/resource commitment needs one canonical owner and atomic-enough behavior for this simulation.

Document the chosen lifecycle explicitly.

---

## Failure behavior

```text
no Food
→ Cafeteria cannot actually feed Bob
→ Eat opportunity unavailable
→ Hunger continues rising
```

Logging should distinguish:

```text
food.no_target
reason = no_inventory
```

from:

```text
reason = unstaffed
reason = reserved
reason = inaccessible
```

---

## This stack proves the first economic feedback loop

```text
Farmer works
→ Food produced
→ Food becomes available
→ Cafeteria can feed colonists
→ colonists remain capable of working
```

And the reverse:

```text
Farm fails
→ Food inventory declines
→ meals disappear
→ Hunger rises
→ workers begin leaving jobs
```

---

# STACK 4 — MODULAR FACILITY PREFABS AND LOCAL NAVIGATION

## Purpose

Stop treating the colony as one permanently baked walking surface.

Build the preliminary architecture for:

```text
constructable station modules
reconfigurable stations
multiple connected interior spaces
```

without building the construction game yet.

---

## Convert major facilities into self-contained prefabs

At minimum:

```text
Habitat
Farm
Cafeteria
Recreation
```

Each prefab owns:

```text
geometry
activity anchors
facility components
local NavMesh data/surface
connection points
```

A facility should be independently placeable without manually reconstructing all of its internal navigation.

---

## Explicit connection points

Create a small concept representing:

```text
this module can connect to another navigable module here
```

Connections generate or manage the necessary NavMesh links between local navigation surfaces.

For this slice, manual scene assembly is fine.

We are proving:

```text
module A
↔
module B
↔
module C
```

can be rearranged and navigation can be rebuilt/reconnected predictably.

---

## Acceptance

Physically move/rearrange the prefabs in the test scene.

Reconnect them.

Verify colonists can still navigate:

```text
Habitat
→ Cafeteria
→ Recreation
→ Work
```

without each arrangement requiring hand-authored navigation hacks.

---

## Important future compatibility

The connection representation should someday support:

```text
door
corridor
airlock
lift
shuttle dock
```

But do not implement all of those now.

---

# STACK 5 — INTER-FACILITY / SHUTTLE TRAVEL

## Purpose

Prove that not every destination is reachable by walking.

Separate part of the colony far enough away that Bob must use transport.

---

## Initial topology

Prefer two physical areas:

```text
Station Core
    Habitat
    Cafeteria
    Recreation

Remote Site
    Farm
    possibly storage
```

Walking cannot bridge them directly.

A shuttle can.

---

## Travel lifecycle

A colonist target may require a multi-stage journey:

```text
walk to Shuttle pickup
reserve/request transport
board
sit
travel
disembark
walk to destination
perform destination activity
```

The ColonistBrain should still think:

```text
I need to Work at Farm.
```

It should not become a Shuttle AI.

A travel/route layer translates that destination into physical movement stages.

---

## Shuttle staffing

Charlie must actually be available as the required pilot.

No pilot:

```text
Shuttle cannot provide transport.
```

This makes employment affect mobility.

---

## Passenger contention

One shuttle has real capacity.

Several colonists may want it.

Do not teleport.

Do not create one fake shuttle per traveler.

---

## Freight

Reuse the existing logistics architecture where practical.

The strongest version of this playtest eventually has:

```text
Bob farms remotely
Food must return from Farm
to the station/cafeteria
```

so Shuttle failure can affect both:

```text
worker commute
AND
resource movement
```

Do not force both into the very first transport ticket if doing so destabilizes the passenger route.

Passenger transport is the first acceptance gate.

Freight integration follows within this stack or an immediately adjacent corrective ticket.

---

## Travel estimates

This is where the deferred concept starts becoming necessary.

Initially implement a deliberately simple estimate derived from known route stages.

The free-time planner eventually needs:

```text
time to destination
+
activity duration
+
time from there to next obligation
+
protected sleep
```

Do not attempt perfect prediction.

An approximate truthful estimate is better than continuing to assume travel = 0 once the shuttle exists.

---

# STACK 6 — FIRST MANAGEMENT UI

## Purpose

Stop using the Inspector as the game.

Build only the UI necessary to play the vertical slice.

---

## Colonist panel

Click a colonist.

Show:

```text
name

Fatigue
Hunger

current brain state
current physical activity
current destination

bed assignment

job
role
shift
workplace

next obligation

recent structured events
```

Provide management controls only where appropriate.

Initial controls:

```text
assign/change bed
assign/change job
assign/change shift
```

Do not expose debugging implementation details as game UI unless useful.

---

## Facility panel

Click a facility.

Show:

```text
facility name

activities

current reservations/users

workers assigned
workers physically present

service status

inventory where applicable

recent structured events involving facility
```

Examples:

```text
Cafeteria

Status:
CLOSED — no Cafeteria Worker present

Food:
7 meals available

Assigned:
Alice — 08:00–16:00

Current users:
none
```

Or:

```text
OPEN

Alice — working
Bob — eating
```

---

## Colony management screen

First colony-level screen should solve the painful cross-entity tasks:

```text
Jobs
Beds
```

Possibly tabs:

```text
Colonists
Jobs
Housing
```

It does not need to become RimWorld.

The critical interaction is:

> I can see the vacancy/problem and fix the assignment without hunting GameObjects in Unity.

---

## Structured log UI

Use the canonical event system already built.

Allow simple filtering:

```text
All

Colonist
Facility

category
```

This is also where future flavor rendering can plug in.

For now developer-ish prose is acceptable.

---

# STACK 7 — PLAYTEST STATE / REPEATABILITY

## Purpose

Make the scenario practical to repeatedly play and break.

This is intentionally smaller than a final save-game system.

---

## Minimum requirement

Provide one reliable way to start from a canonical scenario.

Possibilities:

```text
a canonical CorePlaytest scene

plus

a Reset Scenario developer action
```

and/or a minimal save/load snapshot if it is straightforward with the current architecture.

The important requirement is repeatability.

We need to stop manually rebuilding:

```text
Alice's shift
Bob's bed
Food inventory
starting Hunger
Shuttle pilot assignment
```

every time we want to test something.

---

## If minimal persistence is implemented

Preserve only what this playtest actually needs:

```text
game time

colonist needs

bed assignments

work assignments
shifts

facility inventories

important resource quantities
```

Do not build migration/version compatibility infrastructure unless the current persistence architecture already provides it naturally.

Full persistence is not a blocker to the very first playthrough.

Repeatable setup is.

---

# STACK 8 — CORE FAILURE-CASCADE PLAYTEST

## Purpose

Stop adding architecture.

Play the game.

This stack is largely integration repair, instrumentation, balancing and acceptance.

---

# Canonical Scenario

Suggested starting colony:

```text
Bob
    Farmer

Alice
    Cafeteria Worker

Charlie
    Shuttle Pilot

Dana
    General colonist

Optional:
Erin
    relief/general worker
```

Physical layout:

```text
STATION CORE

Habitat
Cafeteria
Recreation
Shuttle Dock


REMOTE SITE

Farm
Remote storage
Shuttle Dock
```

Resources:

```text
small starting Food reserve

Farm is capable of replacing consumed Food
if Bob can work and logistics function
```

---

# Playtest 1 — Healthy Colony

Run multiple game-days.

Observe:

```text
wake
eat
commute
work
return
eat
recreate
sleep
```

Not every colonist should perform the same sequence.

The colony should approximately sustain itself.

---

# Playtest 2 — Cafeteria Staffing Failure

Remove Alice from the Cafeteria job or shift.

Expected:

```text
public Cafeteria service closes

Alice may still self-serve if she remains an assigned worker
under the authored policy

other colonists cannot use the service

Hunger rises
```

Assign/return a worker.

Expected:

```text
service reopens
colonists discover food
recovery begins
```

---

# Playtest 3 — Alice Survival Override

Allow Alice to become Critically Hungry while working.

Expected:

```text
Alice leaves Work
Cafeteria temporarily closes
Alice self-serves
Alice eats enough to leave Critical Hunger
Alice reevaluates
Alice returns to Work if shift remains
Cafeteria reopens
```

This is an important emergent test.

---

# Playtest 4 — Shuttle Failure

Remove Charlie or otherwise eliminate Shuttle service.

Expected consequences:

```text
Bob cannot reliably reach remote Farm

and/or

Food cannot return from remote Farm

Food availability falls

Hunger problems eventually develop
```

Restore transport.

The colony should be capable of recovering if the damage has not progressed too far.

---

# Playtest 5 — Capacity Contention

Cause multiple colonists to want:

```text
Eat
or
Play
or
Shuttle
```

at the same time.

Watch reservations resolve physically.

No teleporting.

No duplicate occupancy.

No manager pretending a reservation exists when the runner does not own it.

---

# Playtest 6 — Bad Management Cascade

Deliberately create:

```text
bad shift assignment
or
missing worker
or
transport failure
```

Look for the full chain:

```text
management mistake

→ physical worker absence

→ production/service/logistics problem

→ resource/service shortage

→ biological need

→ colonist decision changes

→ additional work disruption
```

Then correct the original management problem.

The simulation should produce a believable recovery rather than requiring manual state repair.

---

# FINAL CORE-CONCEPT ACCEPTANCE

We call this vertical slice successful when a human can say:

### People exist as physical actors

They visibly:

```text
sleep
eat
work
recreate
travel
```

rather than simply changing stat labels.

### Facilities matter physically

Workers occupy real workplaces.

Customers occupy real service positions.

Capacity exists.

### Management decisions matter

Changing:

```text
job
shift
bed
staffing
```

changes the physical simulation.

### Economy matters

Food is:

```text
produced
transported/stored
served
consumed
```

and absence has consequences.

### Transport matters

Distance is real.

The Shuttle solves a real problem.

### Biology matters

Colonists protect themselves.

They do not obediently starve to death because a shift exists.

### Failures propagate

One missing worker can produce downstream consequences.

### Failures are understandable

The structured history tells us:

```text
what happened
who did it
where
why
what followed
```

### Player intervention works

The player can diagnose the problem through the UI and change assignments to recover.

If those things are all true, we have proved the core game.

---

# ORDER OF EXECUTION

After the current OffDuty/logging stack:

```text
STACK 1
Staffed Services + Self-Service
        ↓
STACK 2
Multi-Colonist Contention
        ↓
STACK 3
Physical Food Economy
        ↓
STACK 4
Modular Facility Prefabs / NavMesh Links
        ↓
STACK 5
Shuttle / Inter-Facility Travel
        ↓
STACK 6
Management UI
        ↓
STACK 7
Repeatable Playtest State
        ↓
STACK 8
Failure-Cascade Playtest + Corrections
```

There is one reasonable parallel path:

Once Stack 2 stabilizes the assignment model, early UI work can proceed while the navigation/transport stacks are being developed.

Do not let that parallel work change the canonical simulation architecture.

---

# DEFERRED, NOT DEFEATED

The vertical slice architecture should leave room for these without implementing them yet.

## Food

Later:

```text
food types
preferences
quality
dietary restrictions
allergies
meal variety
cooking skill
menus
prices
```

FoodManager can eventually rank candidates using those facts.

---

## Recreation

Later:

```text
preferences
social groups
friends
mood
novelty
quality
price
personality
```

OffDutyManager can eventually rank opportunities using those facts.

---

## Planning

Later:

```text
accurate walking estimates
shuttle schedules
connection delays
route risk
queuing estimates
```

The existing free-time calculation grows from:

```text
activity duration + protected rest
```

into:

```text
travel there
+
wait
+
activity
+
travel onward
+
protected rest
```

---

## Workforce

Later:

```text
essential workers
minimum staffing
overtime
relief workers
shift handoff
qualifications
skill bonuses
labor shortages
```

Keep these in the workforce domain rather than putting workplace fields on every generic facility activity.

---

## Services

The Alice sandwich distinction should survive.

Future access policies may represent:

```text
public only when staffed

employees may self-serve

residents may self-serve

everyone may self-serve

members only

staff only
```

This is **authorization/availability for a particular requester**, not merely a facility-open boolean.

---

## Station construction

The modular NavMesh work is explicitly preliminary infrastructure for:

```text
place module
rotate module
connect module
disconnect module
rebuild route graph
```

Do not build the base-construction UX during this vertical slice.

---

# PRINCIPLE TO PRESERVE THROUGH ALL STACKS

The game should remain explainable as a collection of small authorities:

```text
Stats
    tell us Bob is hungry.

Workforce
    tells us Bob has a shift.

FoodService
    tells us whether this colonist can obtain food here.

FoodManager
    finds viable food opportunities.

OffDutyManager
    finds viable discretionary opportunities.

Route/Transport system
    tells us how Bob can physically get there.

Brain
    decides what Bob should do.

ActivityRunner
    physically carries out the chosen interaction.

Facility
    owns the choreography.

Economy
    owns the actual resources.

SimulationLog
    records what happened and why.
```

No one manager should become the god object that knows everything and moves everyone.

That boundary is more important than minimizing component count.

---

# MILESTONE NAME

When Stack 8 passes:

**Core Colony Vertical Slice**

At that point, stop treating the project primarily as an architecture prototype.

The next planning cycle should be driven by what was boring, confusing, frustrating, or compelling during the actual playtest.
