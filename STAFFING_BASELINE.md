# Staffing Baseline (T00)

Measured before the staffing refactor. The packet was authored against a branch
that still had `FarmController`; the repository had already advanced through the
production-shape refactor, so this documents the **actual** pre-staffing baseline
used as the parity target.

## Actual composition that replaced `FarmController`

- `Assets/Scripts/ColonyPrototype/People/StaffingComponent.cs` held a manual
  Inspector `List<ColonistAgent> assignedWorkers` and exposed
  `AssignedCount`, `PresentCount`, `WorkingCount`, `GetWorkingWorkers()`, and
  `CountWorkingWithClass(...)`. It did not allocate jobs.
- `Assets/Scripts/ColonyPrototype/People/WorkScheduleComponent.cs` owned the
  work/rest cycle, activity transitions, and passenger requests.
- `Assets/Scripts/ColonyPrototype/Economy/RecipeStaffingRule.cs` put staffing
  requirements **inside the recipe**, which this packet deliberately removes.
- `Assets/Scripts/ColonyPrototype/Production/ResourceConverterComponent.cs`
  read `staffing.GetWorkingWorkers()` and applied the recipe's staffing rule.

## 1. How the Farm identified assigned farmers

Inspector data: the Farm's `StaffingComponent.assignedWorkers` listed two
`ColonistAgent`s (`400000307`, `400000308`) in `Assets/SpaceSim.unity`.

## 2. How the 8h work / 8h rest cycle was represented

`WorkScheduleComponent` with `workDurationHours = 8`, `restDurationHours = 8`
and a `WorkScheduleState` machine (`AwaitingWorkTransport`, `Working`,
`AwaitingReturnTransport`, `Resting`). A 16-hour cycle: 8 hours work, 8 hours rest.

## 3. Exactly when passenger contracts were created

`WorkScheduleComponent.RequestWorkTransport()` / `RequestReturnTransport()` ran
on each simulation tick. Each call created one contract only when the whole
assigned list was gathered at one end and no passenger contract was already
active for any of them (`ContractManager.HasActivePassengerForAny`).

## 4. How a farmer moved among WaitingForTransport / Passenger / Working / Resting

- `BeginWorking()` set every assigned farmer to `Working` once `AllWorkersAt(workplace)`.
- `EndWorkShift()` set them to `WaitingForTransport` and requested the return trip.
- `BeginResting()` set them to `Resting` once `AllWorkersAt(home)`.
- `PassengerCarrierComponent.TryBoardPassengers` set `Passenger`;
  `TryUnboardPassengers` set `Idle`.

## 5. Did all farmers move as one group?

Yes. `AllWorkersAt(...)` required every assigned worker at the same location, and
one contract carried the entire list.

## 6. Work / rest durations

8 hours work, 8 hours rest, 16-hour repeat.

## 7. Production rate with 0, 1, and 2 physically present farmers

`HydroponicFood.asset`: continuous, `0.5 Water -> 1 Food` per hour, staffing rule
`minimumWorkers = 2`, `maximumEffectiveWorkers = 2`, `requiredClass = Farm Technician`,
`additionalWorkerBonus = 0`.

- 0 working farmers -> `IsStaffed` false -> `StaffingBlocked`, **0 Food/hour**.
- 1 working farmer -> below the minimum -> `StaffingBlocked`, **0 Food/hour**.
- 2 working farmers -> multiplier 1.0 -> **1 Food/hour**.

So the two-worker rate (1 Food/hour) is the 100% reference for the refactor.

## 8. Water consumption with 0, 1, and 2 workers

Scaled by the same blocked/running gate: 0 at 0 and 1 workers, **0.5 Water/hour**
at 2 workers.

## 9. Passenger contract priority

`WorkScheduleComponent.passengerPriority = 10` on the 1..10 scale
(`TransportPriorityRules.Clamp`). No legacy 100/90/60 scaling was still present,
so the staffing model passes its 1..10 priority straight through.

## 10. How Shuttle unload changed passenger activity

`PassengerCarrierComponent.TryUnboardPassengers` set each passenger to `Idle`.
`StaffingManager` corrects that to `Working`/`Resting` on the next staffing tick
from shift and location.

## 11. Code relying on `ColonistRole`

None. The enum had already been removed by the earlier production refactor; the
`classes` list had replaced it. No staffing code depends on it.

## 12. Code relying on `assignedWorkplace`

Only the serialized `ColonistAgent.assignedWorkplace` field and its
`Person.prefab` / scene values. No behavioural code read it. It is removed in
T01/T02.

## Parity target

After migration, two `Farm Technician`s on `Shift A` of `TwoShift8x8` must
reproduce `8 x 1.0` full-output hours per 16-hour cycle (ignoring commute
delays), and splitting them onto `Shift A`/`Shift B` must produce the intended
`16 x 0.65 = 10.4` equivalent hours while the system does **not** choose that
arrangement on its own.
