# P05 Sprint A2 Implementation Report

## Baseline

- Baseline HEAD: `92550a957945c5dfc5ba96f238a75cb03ba150b8` (`chore: commit remaining workspace changes`).
- Current A2 work builds on the accepted Sprint A implementation at that commit.

## Sprint A component map

| Responsibility | Current class | Contract |
|---|---|---|
| Flight state | `ShuttleFlightState` | World position, velocity, rotation, angular velocity, and last accelerations. |
| Flight tuning | `ShuttleFlightProfile` | Acceleration, speed limits, pulse timing, guidance tolerances, and integration step. |
| Flight motor | `ShuttleFlightIntegrator` | Deterministic bounded integration from accelerations and simulated seconds. |
| Guidance | `ShuttleFlightGuidance`, `ShuttleRcsPulseController` | Stateless maneuver helpers and pulse/coast correction commands. |
| Route | `FlightRoute`, `FlightWaypoint` | Ordered world-space targets; waypoint kinds are Clearance, Cruise, and Approach. Cruise points pass through; Clearance and Approach require low speed. |
| Voyage | `ShuttleVoyageComponent` | Trip request, route validation/copy, phase progression, simulation ticks, and the only Shuttle-root Transform writer. |
| Docking port | `DockingPortComponent` | Authored docking/approach/clearance poses and berth reservation/capture lifecycle. |
| Shuttle probe | `ShuttleDockingProbeComponent` | Shuttle-side mating node used to calculate probe pose and capture. |

## Route seam and behavior

- `TryRequestVoyage(destination, plannedRoute, out reason)` is the provider seam. It accepts a planned route only when it begins at the current port's authored Clearance node, ends at the destination's authored Approach node, and has only non-stopping Cruise points between them.
- The voyage copies accepted waypoint data before flying. Its direct-route overload currently creates a Clearance and Approach route.
- A waypoint is reached when the probe is within its `arrivalRadius`; a waypoint marked `requiresLowArrivalSpeed` also requires velocity below `requiredArrivalSpeed`. Cruise waypoints currently have no stop requirement.
- Speed limits are in `ShuttleFlightProfile`; docking ports separately hold capture tolerances. Sprint A brakes for the low-speed destination approach, then the existing DockingTurn and FinalDocking phases perform the berth maneuver.
- The strategic route provider should plan from the origin Clearance node to the destination Approach node. It must not plan the backing-out or final berth maneuver.
- `ShuttleVoyageComponent` is the sole writer of the Shuttle root Transform. The integrator only evolves `ShuttleFlightState`.

## A2 additions and preserved assumptions

- A2 adds route planning and obstacle-clearance data before the existing voyage route-validation seam. The motor, integrator, departure clearance, DockingTurn, and FinalDocking remain the Sprint A owners of their existing responsibilities.
- The Shuttle is approximated by an authored navigation sphere plus a tunable preferred clearance. Only explicitly marked `SpaceNavigationObstacle` objects participate; decorative colliders are ignored.
- Direct routing is tested first. Detour planning is bounded, three-dimensional, deterministic, and uses swept-envelope validation. No world-sized grid is introduced.
- Automated Unity/scene tests are not run in this environment. Physical routing, visible clearance, and corner flight remain human Unity acceptance checks per `TESTING_IN_EXPLORATION_MODE.md`.

## Implementation status at handoff

- **T00:** Baseline and seams documented above.
- **T01:** Added `ShuttleNavigationProfile`, explicit `SpaceNavigationObstacle`, deterministic filtered `SpaceClearanceQuery`, and an Editor bounds recommendation / obstacle-authoring helper. No decorative collider participates unless deliberately marked.
- **T02:** Added a route planner that checks the swept direct route first. Voyage trip requests use its plan when the reusable Shuttle prefab has the planner and profile assigned; the existing custom-route overload remains available.
- **T03:** Added bounded visibility graph planning with 3D front/rear rings, deterministic A*, expansion/obstacle/node/edge budgets, newly discovered blockers, explicit failure results, and swept-volume checks on every graph edge.
- **T04:** Added greedy swept-line shortcutting and corner pass-speed caps in the navigation profile. Cruise waypoints pass without a stop; the voyage begins reverse-burn braking ahead of a lower pass-speed constraint, then accelerates again after the corner. Scene gizmos can show candidates, graph edges, raw/simplified routes, pass speeds, obstacle bounds, and Shuttle envelope.
- **T05:** Added a safety overlay during strategic cruise. It probes along actual velocity (or commanded route direction at low speed), brakes through the existing flight integrator, holds without snapping velocity, and replans from the stopped Shuttle with a tunable cooldown. A failed replan leaves the Shuttle held.
- **T06:** Plan results expose Direct/Visibility, obstacle/candidate/edge/round counts, route length ratio, diagnostics, and planning wall time. The seeded stress suites and flight execution evidence have not been generated or run.
- **T07:** Human crowded-field acceptance is pending. `BobInSpace.unity` currently contains no authored asteroid field, so mark representative obstacles using **GameObject > Spacegame > Mark As Navigation Obstacle** in the test scene before evaluating crowded routes.
- **T08/T09:** Not implemented. The ticket gates sparse local A* on T06 evidence and the human T07 verdict.

## Human Unity acceptance still required

1. Open `Assets/BobInSpace.unity` and press Play. Click the opposite port button in the Flight panel; inspect the Voyage component and confirm `Direct`, one edge sweep, and no detour points.
2. Add a marked asteroid across that route. Confirm the planner reports `Visibility`, shows a clear detour in Scene gizmos, and the Shuttle passes the corner without stopping.
3. Arrange an out-of-plane opening and then several staggered asteroids. Confirm the route clears the navigation envelopes and route statistics remain within configured budgets.
4. During cruise, move a marked obstacle into the Shuttle's forward corridor. Confirm it brakes through acceleration, holds safely if no route is available, and resumes after the corridor clears or a safe replan is found.
5. Enclose the destination, confirm a bounded diagnostic and no launch. Repeat representative routes at 10x, 100x, and the practical fast-forward speed.

No Unity session, compile, automated tests, or stress fixture execution was run here. These physical/gameplay results require your Play Mode acceptance under the project testing policy.
