# How It Works — the minigame in plain English

The colony: **one Command Center** (8 beds, the main warehouse, two docking ports),
**one Farm** (joined to the Command Center by a corridor), **one Water Processor**
(floating off on its own — shuttle-served), **one Shuttle**, **one Mining Ship**, an
**ice asteroid** and a **rock asteroid** nearby. **Eight colonists**: three Pilots,
two Farm Technicians, three Builders.

This document explains what happens when you press Play, who owns which fact, and
what I changed in the plan and why. It is written for a human, not an agent.

---

## 1. The heartbeat

There is one clock: `SimulationManager`. Ten times a real second it says "another
slice of game time has passed" and calls everything that registered to be ticked, in
a fixed order:

1. **People** (priority 100) — who is on shift, who is walking, who is tired.
2. **Production** (200) — farms and processors convert stuff.
3. **Logistics planning** (300) — who needs what, which vehicle gets which job.
4. **Physical movement** (400) — ships fly, colonists walk, cargo transfers.
5. **Reporting** (900) — ledgers write down what just happened for the UI.

Nothing moves outside of a tick. That's why pause is perfect, speed is a multiplier,
and the whole thing is deterministic. Visual smoothness (ships gliding, thruster
puffs) is a separate layer that reads the tick results and interpolates between them
— it never pushes back.

At the target clock, one game-hour is about one real minute at 1×. A day is ~24
minutes; you'll play at 4×–10×.

---

## 2. A day in the colony — what actually happens

**00:00 — Shift A starts.** The staffing system (`StaffingManager`) looks at every
colonist's *employment* — a record saying "you work at this workplace, in this role,
on this shift." Farm Tech #1 is on Farm / Farm Operator / Shift A. He's at home in the
Command Center. The system asks the **route resolver**: "how does he get from the
Command Center to the Farm?" There is a corridor, so the answer is *walk*. He gets a
walking task, his status becomes `Walking`, and a minute later (game time) he arrives.
His location is now "Farm" — and only now, because *arrival means arrived*.

Pilot #1 is on Shuttle / Pilot / Shift A. The Shuttle is docked at the Command Center
(his home), so the ship's crew-duty component boards him directly: he's now the
**responsible pilot** — a temporary lease, not employment. Employment says who *works*
here; the lease says who is *at the controls right now*.

**00:05 — The Farm wakes up.** The Farm's converter asks its performance component,
"am I operational, and how fast?" The performance component asks every provider it
has — right now just the staffing component — which counts *active* qualified
workers (assigned + present + on shift + working + has the class). One Farm Tech →
the production-rate curve says 0.65. The converter runs the hydroponics recipe at 65%
speed, pulling Water from the Farm's own inventory, putting Food into the Farm's own
inventory. The converter never knows a human exists. Staffing never knows what a
recipe is.

**01:00 — The Farm's water is getting low.** The Farm's **stock policy** says "keep
Water above 20, refill to 60." It publishes a *demand*: "Farm wants 40 Water,
priority 6." It does not know where water comes from. The Water Processor's policy
says "export anything above 10 Water." It publishes a *supply*. The **logistics
manager** matches them — and only when a vehicle is actually free does it reserve the
water at the source and create a **freight contract**. No vehicle, no contract, no
reservation: demand is cheap to publish and cheap to withdraw.

**01:10 — The Shuttle flies.** The Shuttle's transport executor accepts the contract
and hands the voyage to the **voyage component**: *undock* (push off the port on
RCS), *cruise* (turn, burn, flip, brake), *request a berth* at the Water Processor.
One port, it's free → granted → *approach* along the port axis → *final docking* →
clamps. Now the executor loads: the reserved 40 Water moves from the Processor's
inventory to the Shuttle's cargo inventory over a couple of game-minutes (you can
watch the number tick). Undock, cruise back, request a berth at the Farm — the Farm
has one port and the Mining Ship happens to be unloading ice there? No — ice goes to
the Command Center. But if a port *were* busy, the Shuttle would sit at a **holding
slot**, beacon blinking, "Holding · #1", until the port frees up. Dock, unload,
contract complete. Pilot #1 has been accruing fatigue the whole time because flying is
work.

**Meanwhile — the Mining Ship.** Its pilot boards at the Command Center at 00:00 too.
The extraction controller looks at what the Command Center *needs* (its stock policies
for Ice and Regolith) and picks the asteroid whose resource is shortest. It flies to
the ice asteroid and *loiters* — no port, it just station-keeps — while the collector
pulls Ice out of the finite deposit into the ship's hold. Full, or the destination's
capacity is met → fly home, berth at the Command Center, unload. The Command Center's
export policy then lets the Shuttle carry Ice to the Water Processor on the next
freight contract. The Processor (automated — no staffing role, so it's always
operational at 1.0) turns Ice into Water. The loop closes.

**08:00 — Shift change.** Shift A ends. Farm Tech #1's duty state moves to
`CompletingCommittedWork`/`ReturningHome`; the resolver says *walk*; he walks home and,
because his fatigue is above zero, `Sleeping`. Fatigue drops while he sleeps in a real
bed. Farm Tech #2 (Shift B) walks the other way. For a few minutes the Farm has zero
active workers → production-rate curve index 0 → 0.0 → the converter stops. It
doesn't break; it waits. Pilot #1's shift is over too: the ship stops *accepting* new
contracts, finishes the one it's on, flies home crew-only, and drops him at the port.
Pilot #2 boards. Employment for both is untouched; only the lease moved.

**All day — eating and drinking.** The population consumer draws Food and Water from
the Command Center's inventory for everyone every hour. When the shelves are empty, the
draw fails, and each colonist's **needs** (nutrition, hydration) start falling. Low
needs make people recover more slowly and work worse — a colonist-level multiplier
that the staffing component folds into its contribution. Zero for long enough → the
population manager runs the one **kill** command that unwinds everything in order:
end duty, release employment, pull them off any ship or walk, record it, remove them.
Their workplace's active count drops by one, the curve gives less, and the HR screen
shows the vacancy. Population zero → game over.

**When you build (later days).** You drag a Habitat ghost; it snaps to a node on the
Command Center. Confirm → a **construction site** appears. It's a workplace with an
inventory whose capacity is exactly the material cost and a stock policy that says
"import 30 Regolith, foreground, priority 8." That demand enters the same logistics
pipe as water. Builders (Shift A) get an EVA link to the site and walk out. Progress
only ticks when all materials are present and builders are active. Done → the real
Habitat prefab replaces the site, beds appear, the two homeless colonists get proper
rest. Build a corridor to the Water Processor and, the next shift, its freight still
flies (freight always flies — corridors move *people*), but any staff you add there
would walk. Demolish the corridor and they'd fly again.

---

## 3. Who owns what

One owner per fact. If two things could disagree about something, one of them is the
owner and the other is a view.

| Fact | Owner | Everyone else… |
|---|---|---|
| How many of resource X are here | `InventoryComponent` on that object | reads it; racks and panels are views |
| Whether a facility can run and how fast | `FacilityPerformanceComponent` (aggregating providers) | converters read `IsOperational` / multiplier; never count workers |
| Who works where, in what role, which shift | `EmploymentAssignment` on the colonist, mutated only by `StaffingManager.Assign/Unassign` | the allocator and HR screen are clients |
| Who is at the ship's controls right now | `ShipComponent.ResponsiblePilot` (temporary lease) | employment persists off-shift; the lease doesn't |
| Where a colonist *is* | `ColonistAgent.currentLocation` — the last place they *arrived* | transit origin/destination are separate fields; views animate the gap |
| That a colonist is walking somewhere | `PedestrianTransitComponent` | the staffing reconciler leaves walkers alone |
| That cargo/passengers must move from A to B | `TransportContract`, owned by `ContractManager` | vehicles execute it; demand publications are not contracts |
| Who is aboard a ship | `PassengerCarrierComponent` | contracts list who *should* be; the carrier says who *is* |
| Where a ship is and how it's moving | `ShipVoyageComponent.Flight` (pose, velocity) and `Phase` | the ship root transform is written from it; the mesh interpolates |
| Which ship may dock where | `DockingControlComponent` at the station (ports + queue) | ships ask; they don't grab |
| Whether a colonist is on/off duty, blocked, returning | `ColonistDutyState`, written only by the staffing reconcilers | UI and history read it |
| How much stuff flowed, how long things were blocked | `Reporting/` ledgers | panels display; they never compute their own aggregates |
| Game time | `SimulationManager.CurrentGameHour` (absolute, never wraps) | hour-of-day is a view |
| What a resource, recipe, role, shift, building *is* | ScriptableObject content in `Assets/GameData` | code never hard-codes "Food" |

Two ownership rules that matter most:

- **Physical vs. logical.** Being *assigned* to the Farm and being *at* the Farm are
  different facts with different owners. That's why nobody teleports and nothing
  phantom-works.
- **Publish vs. commit.** A stock policy *publishes* a need. A contract *commits* a
  vehicle and reserves stock. The gap between them is deliberate — it's where the
  arbitration lives, and it's why tiny pointless shipments don't happen.

---

## 4. The layers (and which way the arrows point)

```
Content  (ScriptableObjects: resources, recipes, roles, shifts, buildings, flight profiles)
   ▲
Runtime  (the simulation — everything in §3)
   ▲
Presentation  (ShipView, ColonistView, thrusters, port lights, your Facilities presenter, environment)
   ▲
UI  (HUD, panels, HR screen, colony report, build menu)
```

Arrows mean "may reference." Runtime never knows Presentation or UI exist. Delete every
Presentation and UI component and the colony runs identically — that's the test.
Presentation reads sim state and animates. UI reads sim state and ledgers and calls
*commands* — small validated methods that return a result you can print
(`Assign() → RejectedMissingClass`).

---

## 5. What I changed in the plan, and why

You had a strong simulation with no game around it. Most of what I did is ordering and
seams, not new rules.

1. **Stopped building infrastructure first.** Three packets in a row hardened a sim
   nobody could play. From here, infrastructure is built only on the day a visible
   feature needs it. `DAY_BY_DAY_PLAN.md` is 26 days each ending in "press Play and
   see X."

2. **Made corridor-vs-shuttle a first-class mechanic.** Before, every location was an
   island reachable only by ship. Now a colonist's commute goes through one resolver:
   corridor path → walk; no path → shuttle; neither → visibly blocked. Corridors cost
   materials once; shuttles cost pilots and ports forever. That's the strategy.

3. **Gave ships one brain.** Three components each hand-rolled "fly to X and dock"
   over a `MoveTowards`. Now `ShipVoyageComponent` owns undock → cruise → request
   berth → hold → approach → dock, with a real 6DOF Newtonian model that we integrate
   ourselves (no PhysX) so it's deterministic and speed-safe. The three old callers
   just say "go there" and wait.

4. **Added docking ports and queues.** Stations have finite ports; ships request a
   berth and hold if none is free. Port count becomes a build decision.

5. **Slowed the clock ~60×.** At one game-hour per real second nothing is watchable.
   Every rate is per game-hour so nothing else changes.

6. **Split `StaffingManager`** (1,063 lines) into five single-purpose classes behind
   the same facade, *before* the allocator and the walking system add more writers
   to it.

7. **Chose hybrid control.** You set a target headcount per role per shift and a
   priority; an allocator fills it through the same `Assign()` the HR screen uses.
   Pin anyone you want to hand-manage. Turn the allocator off and it's fully manual.

8. **Made construction reuse logistics and staffing.** A site is an inventory with an
   import policy plus a Builder role. No wallet; the colony delivers and builds.
   Placement is node-snapping on a flat plane — no terrain — because there is no
   ground in space and inventing one is a month of work.

9. **Added a Presentation layer and a UI layer** as separate assemblies, with a
   `ModuleSockets` convention on every prefab (attachment nodes, dock ports,
   workstations, beds, mesh root) so ships, your Facilities presenter, and
   construction all use the same sockets.

10. **Added three read models for the UI** — flows per resource per inventory,
    blocked time per subject per reason, plain-English duty summaries — so the HR
    screen and colony report can say "Water Processor: 6.5h blocked, no eligible
    pilot" without the UI doing math.

11. **Made the Water Processor automated in the minigame** (no staffing role) so it
    demonstrates the shuttle-served trade-off through freight alone without spending
    one of eight colonists on it. Staffing it later is a content toggle.

12. **Split the scene into additive scenes** (`Managers`, `Base`, `Environment`,
    `UI`) so parallel agents stop colliding on one file, and moved the starting colony
    into a `ScenarioDefinition` asset so "new game" is data.

13. **Planned environment as a generator tool, not hand-made files** — faceted
    asteroids, HDRP volumetric dust, motes, a scatter tool that keeps dock approaches
    clear.

14. **Dropped tests as a gate.** They stay as design artifacts inside tickets; the gate
    is compile + the day's observable.

## 6. What I deliberately did not change

The inventory authority, the recipe/staffing split, explicit employment, the pilot
lease, publish-then-commit logistics, discrete quantity rules, extraction staying
outside freight arbitration, no auto-vacancy-filling at the staffing level, and
finish-your-current-flight-before-reassignment. Those were right, and the plan is built
to protect them.
