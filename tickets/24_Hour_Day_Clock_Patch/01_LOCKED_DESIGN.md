# Locked Design Contract

These decisions are complete. The execution agent must not substitute another
time model without obtaining a new product decision.

## 1. Absolute time and calendar time are different views

`SimulationManager.currentGameHour` remains the authoritative monotonic elapsed
time. Keep the existing `CurrentGameHour` public property for callers and saved
scene compatibility. It must not wrap or reset at 24.

Add `Assets/Scripts/ColonyPrototype/Core/SimulationTime.cs` as the shared pure
time utility with this public surface:

```csharp
public static class SimulationTime
{
    public const float HoursPerDay = 24f;
    public static int DayIndexAt(float absoluteGameHour);   // zero-based
    public static int DayNumberAt(float absoluteGameHour);  // one-based display
    public static float HourOfDayAt(float absoluteGameHour);// [0, 24)
    public static string FormatTimestamp(float absoluteGameHour);
    public static string FormatHourOfDay(float hourOfDay);
}
```

Use positive modulo for `HourOfDayAt`. Runtime simulation time is non-negative;
clamp invalid serialized `currentGameHour` values to zero in
`SimulationManager.OnValidate`, but keep the pure modulo helper deterministic for
negative test input.

Expose these convenience properties on `SimulationManager`:

```csharp
public int CurrentDayIndex
public int CurrentDayNumber
public float CurrentHourOfDay
```

All duration and rate calculations continue to use absolute hours and
`deltaGameHours`. Only daily scheduling and human-readable presentation use
hour-of-day.

## 2. Timestamp formatting is centralized

`SimulationTime.FormatTimestamp` returns `Day N HH:MM`, where Day 1 contains
absolute hours `0..<24`. Convert the absolute hour to the nearest whole minute
for display and carry a rounded `24:00` into the next day as `00:00`.

Examples:

```text
0.00  -> Day 1 00:00
8.10  -> Day 1 08:06
24.00 -> Day 2 00:00
49.25 -> Day 3 01:15
```

`SimulationLog` prefixes entries with this formatter. Do not change the stored
absolute timestamps on contracts or duty records.

## 3. Staffing shift patterns are daily schedules

`ShiftPatternDefinition` no longer owns `cycleHours`. Remove that serialized
field and all callers/tests/authoring instructions that set it.

`ShiftDefinition.startHour` is an hour of day in `[0, 24)`. Its
`durationHours` is in `(0, 24]`. `ShiftPatternDefinition.IsShiftActive` reduces
the supplied absolute game hour through `SimulationTime.HourOfDayAt` and then
tests the named window. A window may cross midnight; for example, start `20`,
duration `8` covers `20:00-24:00` and `00:00-04:00`.

Overlapping shift definitions remain legal. Assignment capacity is still
enforced per role and shift ID. The system does not infer, add, or fill
assignments.

## 4. The canonical content exposes three windows

Rename the existing asset and its `.meta` together, preserving its GUID:

```text
Assets/GameData/Shifts/TwoShift8x8.asset
    -> Assets/GameData/Shifts/Daily8HourShifts.asset
```

Update its content to:

```text
m_Name: Daily8HourShifts
stableId: daily-8-hour-shifts
displayName: Daily 8-Hour Shifts
Shift A: start 0,  duration 8
Shift B: start 8,  duration 8
Shift C: start 16, duration 8
```

The asset semantic is intentionally changing before any shipped save format
exists, so updating its stable ID is required. Preserving the Unity GUID keeps
the three current scene references valid.

Do not assign anyone to Shift C during migration. Current A/B employment remains
unchanged. Adding C to the schedule only makes the third window available for a
future explicit player assignment.

## 5. Off duty is not synonymous with sleeping

The planned schedule has a duty window and its daily off-duty complement. The
runtime activity determines what actually happens in that complement:
transport, Sleeping, Resting, or another physical state. The diagnostic table
must label the planned complement `Off duty`, not `Sleeping`.

Fatigue behavior remains:

```text
work:  +0.10/hour * role exertion multiplier
sleep: -0.10/hour * habitat restfulness multiplier
stop threshold: 0.90
recovered threshold: 0.20
```

Changing the day length must not change these rates or thresholds.

## 6. Daily schedule diagnostics are observational

Add a deterministic, read-only schedule table builder to `StaffingManager` and
an Inspector context-menu command that stores and logs the latest table. It must
read `KnownColonists`, `currentEmployment`, the daily shift definition,
`ColonistStatusComponent`, and duty history. It must not mutate assignments,
activities, fatigue, contracts, or ship state.

The table includes one row per known active colonist, sorted by display name and
then instance ID, with these columns:

```text
Colonist | Home | Workplace | Role | Shift | Work | Off duty | Now | Fatigue | Exhausted | Last duty
```

Formatting rules:

- normal window: `00:00-08:00`; off duty `08:00-24:00`;
- wrap window: `20:00-04:00 (+1d)`; off duty `04:00-20:00`;
- 24-hour duty: work `00:00-24:00`; off duty `none`;
- unassigned: job columns `-`, work `none`, off duty `all day`;
- missing/invalid shift: show `INVALID` rather than throwing;
- `Now` is the actual `ColonistActivity`, not an inferred planned state;
- `Last duty` comes from active duty or `DutyHistory`, never passenger
  contracts, and includes absolute formatted start/end, worked hours, fatigue
  start/end, and end reason when available.

Use one multiline `Debug.Log` call for the table, not one `SimulationLog` entry
per row. Keep a serialized multiline `lastScheduleDump` string and expose it
read-only for Inspector/test access.

## 7. Future 8-on/8-off rotation is out of scope

Do not model a repeating 16-hour rotation by restoring a configurable cycle on
`ShiftPatternDefinition`. When that feature is designed, it must be a separate
rotation/schedule policy capable of spanning calendar-day boundaries and must
resolve its fatigue consequences explicitly.
