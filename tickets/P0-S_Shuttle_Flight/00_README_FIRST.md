# Packet P0-S — Shuttle Voyages: Ports, Queueing, 6DOF Flight, Presentation

Read this, then `01_LOCKED_DESIGN.md`, then your ticket.

## What this packet delivers

Shuttles become the visible heartbeat of the game. A voyage is one authoritative
sequence — **load → undock → cruise → request berth → hold if none free → approach →
capture → unload** — flown by a sim-owned 6DOF Newtonian flight model and rendered by a
presentation layer that interpolates, fires RCS/main-engine effects, and animates ports.

## Why this is a refactor first

Three components today (`TransportExecutorComponent`, `ExtractionMissionController`,
`ShipCrewDutyComponent`) each hand-roll "go to X and dock" with `MoveToward` and direct
phase mutation. This packet gives ships **one** voyage authority
(`ShipVoyageComponent`) and turns those three into clients that issue one command and
poll one state. Nothing about contracts, dispatch, crew leases, or extraction
semantics changes.

## Ticket order

```text
S00 characterize ──► S01 ports + dock control ──► S03 voyage component ──► S04 migrate callers ──► S06 accept
                     S02 flight model + guidance ─┘                         S05 presentation ─────┘
```

S01 and S02 are independent and can run in parallel. S05 (Presentation asmdef) needs
S03's read-only surface and can start as soon as S03 compiles.

## Relationship to other packets

- Touches only `Vehicles/`, `Extraction/`, a new `World/Docking/`, a new
  `Presentation/Ships/`, and `SimulationManager` clock defaults. Disjoint from P0-A
  (`People/`, `World/Transit/`) and P0-B (`UI/`). Safe to run concurrently.
- P0-A's `RouteResolver` step 5 checks *existence* of a personnel vehicle, not port
  availability. Queueing at ports is a voyage concern; it stays here.

## Architectural constitution (binding)

Identical to `tickets/P0-A_Foundation/00_README_FIRST.md` §"Architectural constitution".
Points that bite hardest here:

- **Rule 1:** `Presentation` asmdef references Runtime; Runtime never references it.
  Thruster VFX, interpolation, clamps, port lights are Presentation. Flight *state* is
  Runtime.
- **Rule 4:** `ShipVoyageComponent.FlightState` is the ship's pose authority. The
  ship root transform is written from it once per tick. Presentation interpolates a
  *child* mesh root and never writes the ship root.
- **Rule 5:** Flight integrates from `SimulationTick`. No `FixedUpdate`, no
  `Rigidbody`, no `Time.deltaTime` in Runtime.
- **Rule 9:** Waiting for a port is `Holding`, not "InFlight with a flag". Denied is
  `Blocked` with a reason. A station with no `DockingControlComponent` blocks; it does
  not silently accept.
- **Rule 11:** Every flight/voyage/queue field is `[SerializeField]`.

## Rules for the agent

Same as P0-A. Additionally:
- Do not add collisions, avoidance, fuel, damage, weapons, orbital mechanics, gravity,
  or autopilot for the player. This is a transport shuttle, not a space sim.
- Do not change `TransportContract`, `ContractManager`, `LogisticsManager`,
  `TransportVehicleComponent`, or any staffing/crew employment rule.
- Numbers in the flight profile are placeholders; tune for look, then report them.
