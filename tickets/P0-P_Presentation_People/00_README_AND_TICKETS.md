# Packet P0-P — Presentation: People and Facilities (Days 12–13)

Depends on P0-0 (Presentation asmdef, `ModuleSockets`), P0-A T06 (`PedestrianTransit`).
Constitution: `ARCHITECTURE_CONSTITUTION.md`; rule 2 is the whole packet — nothing here
writes simulation state.

## Design (locked)

- **`ColonistView`** (`Presentation/People/`) on the colonist prefab's mesh child. Each
  frame reads `ColonistAgent` + `PedestrianTransitComponent`:
  - `Walking` → position = lerp(`LegOrigin.evaSpawn`, `LegDestination.evaSpawn`,
    `LegProgress01`) with a small corridor-following spline when a `TransitLink` has an
    authored path transform; face direction of travel; walk animation.
  - Aboard a ship (`currentLocation == ship anchor` or listed in a
    `PassengerCarrierComponent`) → body hidden.
  - `Working` → handed to the Facilities presenter; `Sleeping` → bed slot;
    `Resting`/`Idle`/`WaitingForTransport` → idle spot near `evaSpawn` or a port.
  - Death → despawn on `PopulationManager` unregister (P0-D).
- **Facilities presenter** — your existing MonoBehaviour, ported into
  `Presentation/Facilities/FacilityPresenter.cs` and adapted:
  - Slots come from `ModuleSockets.workstations` and `ModuleSockets.beds`.
  - Subscribes to **`StaffingComponent.ActiveWorkersChanged`** (the one Runtime change:
    an event raised when the active worker set changes) and to the population registry
    for sleepers at this anchor.
  - Assigns arrived `Working` colonists to free workstation slots (arbitrary order —
    the sim has no ordered slots), plays the work loop until they stop being active;
    sleepers to bed slots. Slot release on any state change.
  - Never moves a colonist whose body is mid-walk; waits for arrival.
- **Pilot boarding** (small): when `ShipComponent.ResponsiblePilot` changes, the body
  walks from `evaSpawn` to the ship's port and hides; on release, reverse.

## Tickets
- **P01** `ColonistView` walking/hiding/idle. Observable: Farm Tech visibly walks the
  corridor at the right pace and arrives exactly when the sim commits arrival.
- **P02** `StaffingComponent.ActiveWorkersChanged` (Runtime, P0-A owner signs off) +
  `FacilityPresenter` port with workstation/bed slots. Observable: two farm workers at
  two stations animating; sleepers in bunks; disabling staffing clears the stations.
- **P03** Pilot boarding walk; ship-aboard hiding. Observable: pilot walks to the port,
  disappears, ship undocks after.
- **P04** Acceptance: delete every Presentation component → sim identical over 24h.
