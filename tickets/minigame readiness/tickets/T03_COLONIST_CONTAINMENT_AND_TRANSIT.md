# T03 — Separate Colonist Simulation Location from Transform Parenting

## Goal

Prepare real bed/seat/workplace movement without making people children/lifetime dependents of locations.

## Required model

`currentLocation` means **arrived logical location**.

Add explicit transit representation, minimally:
- origin location;
- destination location;
- in-transit flag/state;
- optional transition kind if required (walking/passenger/etc.) without creating a general action framework.

Presentation/body movement may target anchors/seat transforms, but simulation arrival is committed only when the movement/boarding/unboarding transition completes.

## Changes

1. Stop reparenting colonist root GameObjects under ships/facilities.
2. Keep colonist roots under a stable people/world root.
3. Add a presentation/body transform or small `ColonistBody`/visual component if needed.
4. Replace `MoveToLocation` semantics with explicit transition + arrival commit.
5. Boarding:
   - begin transit/boarding;
   - do not set carrier as arrived location until boarding is complete.
6. Unboarding/walking:
   - preserve old arrived location or explicit "in transit" authority until completion;
   - commit destination on actual arrival.
7. Ensure disabling/destroying a ship GameObject does not disable/destroy colonist GameObjects through hierarchy inheritance.

## Do not

- Do not keep `currentLocation = destination` while the body is still traveling.
- Do not build NavMesh/pathfinding here unless already necessary.
- Do not create a universal movement/task system.

## Tests

- `ShipDisableDoesNotRemoveOccupantsFromPopulation`
- `TransitDoesNotCommitDestinationBeforeArrival`
- `ArrivalCommitsLocationExactlyOnce`

## Acceptance

- [ ] No colonist root is parented to a ship/facility as simulation containment.
- [ ] Population/staffing registry membership survives ship disable.
- [ ] `currentLocation` is never a future destination.
- [ ] Existing instant test movement can be represented through an explicit instant transition helper if needed.
