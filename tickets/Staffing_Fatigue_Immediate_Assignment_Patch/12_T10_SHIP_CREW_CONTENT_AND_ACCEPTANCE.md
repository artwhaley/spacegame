# T10 — Ship crew content and true acceptance

Status: content migrated and implementation complete; licensed Unity acceptance
run remains pending because the project is open in another editor instance.

## Outcome

The live scene uses the corrected crew model and the obsolete single-pilot fields are gone. A multi-cycle runtime run is the remaining gate for proving handover, fatigue, logistics, production, extraction, and lifecycle resilience together.

## Scene and data migration

1. Keep the real Pilot class and Pilot role asset. Pilot requires the Pilot class, uses exertion 1, requires one active operator for flight, and permits one assignment per shift.
2. Add a `StaffingComponent` to each crewed ship and connect its `workplaceLocation` to the ship anchor, `shiftPattern` to an explicit pattern asset, and `offeredRoles` to Pilot.
3. Keep Pilot 1 assigned to Shuttle 1 shift A. Add one distinct scene colonist, Pilot 3, using the existing Person prefab and Pilot class; give Pilot 3 the same Command Post home and assign them to Shuttle 1 shift B. Use the existing `Daily8HourShifts` pattern. This is the live two-pilot rotation used for runtime acceptance; its C window remains empty.
4. Keep Pilot 2 assigned to the mining ship shift A. Give the mining ship the same `Daily8HourShifts` pattern and leave shifts B and C explicitly empty; the mining ship is intentionally unavailable outside A. Do not move Pilot 2 into Shuttle 1's roster.
5. Set both ships' `crewChangeBase` and `initialDock` to Command Post for startup. All three pilots use Command Post as home and begin physically at Command Post. Update ship spatial/anchor authoring as needed so the authored initial dock is truthful; do not claim a dock that disagrees with the ship's initial state.
6. Remove serialized data for `assignedPilot`, `requiredPilotClass`, `pilotRole`, `pilotShiftPattern`, and `pilotShiftId`. Fix references through Unity serialization and stable asset GUIDs; do not hand-edit generated project files.
7. Do not change farm staffing assignments, priorities, converter-based production, or unrelated p2.5 resource content to make this acceptance pass.

## Inspector and logs

Expose on the ship/crew components:

- crew-change base and current dock;
- current movement owner;
- responsible pilot;
- assigned crew rows by role/shift through `StaffingComponent`;
- active duty and release request/reason;
- current crew-return destination;
- one stable readiness blocker.

Log boarding, release request, operation completion, crew-return start, arrival/disembark, and rejected assignment/dispatch once per transition. Avoid per-tick spam. Passenger cancellation logs include contract ID and reason.

## Required automated acceptance

- Scene-shaped startup produces no pilot-home passenger contract and no self-passenger deadlock. The on-shift pilot boards directly at base.
- Run enough simulated time for at least three actual handovers. Each outgoing pilot returns/disembarks, each incoming pilot boards only during their shift, fatigue/recovery change at the configured rates, and employment never changes merely because a shift ended.
- During the run, farmers reach the Farm during an active staffed shift and the real `ResourceConverterComponent` increases food when supplied with water.
- The shuttle completes multiple ordinary contracts and never remains permanently in Loading.
- The mining ship completes extraction/unload and a pilot release/return cycle without concurrent movement ownership.
- Remove one shift assignment during the test: the shuttle parks through that uncovered window and resumes when a staffed eligible window begins.
- Change an assigned crew member's home to a different anchor: assignment becomes ineligible with the precise diagnostic and no teleport or base rewrite.
- Exercise component add/disable/re-enable and manager replacement during an accepted trip and during crew return. State resumes once and finishes.

## Manual runtime gate

Run the actual `SpaceSim` scene in the already-open Unity editor when available. Observe at least one shift handover and inspect the log for recurring exceptions, repeated cancellation/assignment spam, stuck loading, overlapping movement owners, remote boarding, or pilots sleeping aboard.

Record exact automated results and the runtime evidence in `STAFFING_PATCH_ACCEPTANCE.md`. Compilation does not count as test execution. If the editor lock prevents batch execution, run tests in the open editor or record the remaining gate as pending; do not mark the ticket accepted.

## Documentation

Update `ARCHITECTURE.md`, `CONTENT_AUTHORING.md`, `STAFFING_ARCHITECTURE.md`, and `STAFFING_AUTHORING.md` to state:

- a ship is a staffed workplace with explicit role/shift assignments;
- employment persists off duty;
- the responsible-pilot lease is temporary physical control;
- all crew assigned to one ship share its fixed crew-change-base habitat;
- accepted work finishes before safe base return and handover;
- empty shifts intentionally leave a ship unavailable.

Remove descriptions of a pilot permanently owning a shuttle or a ship having a single authored assigned pilot.

## Definition of done

All relevant EditMode and PlayMode suites pass, the Unity project compiles with its real assembly definitions, runtime evidence is recorded, and repository search finds no production reference to the obsolete single-pilot authoring/API. The acceptance document must specifically show a sleeping assigned pilot while another assigned pilot operates the same shuttle.
