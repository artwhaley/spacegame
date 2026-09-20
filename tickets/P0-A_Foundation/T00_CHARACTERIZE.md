# T00 — Characterize `StaffingManager` before the split

Read `00_README_FIRST.md` and `01_LOCKED_DESIGN.md` Part 1 first.

## Goal
Produce a written map of `StaffingManager.cs` so T01–T04 can move code without
reinterpreting it. No source changes.

## Must create
- `tickets/P0-A_Foundation/T00_CHARACTERIZATION.md`

## May modify
- nothing

## Steps
1. For every method in `Assets/Scripts/ColonyPrototype/People/StaffingManager.cs`,
   record: name, line range, which target class from the locked design it moves to,
   every private field it reads or writes, and every other method it calls.
2. List every external caller of a `StaffingManager` public member (grep the Runtime
   and Tests folders). These signatures are frozen.
3. List every place `ColonistDutyState` is written (must all be inside
   `StaffingManager`; if not, report it — do not fix it).
4. Write down the exact `SimulationTick` call order as it exists today.
5. Record the current line count.

## Acceptance
- [ ] Every method is mapped to exactly one target class.
- [ ] Frozen public surface is listed with file:line of each caller.
- [ ] No source file changed (`git status` shows only the new markdown).
