# T03 — Daily Schedule Dump and True Acceptance

## Outcome

Developers can dump one truthful table showing every colonist's planned daily
duty/off-duty windows alongside their actual current state and recent duty
evidence. A live run demonstrates that the whole staffing simulation follows
the 24-hour clock.

## Schedule dump implementation

Add to `StaffingManager`:

```csharp
public string BuildDailyScheduleTable(float absoluteGameHour)
public string LastScheduleDump { get; }
```

Add a `[ContextMenu("Dump Daily Work/Off-Duty Schedule")]` command that uses the
current absolute game hour, stores the returned table in serialized
`lastScheduleDump`, and writes the complete table with one `Debug.Log` call.
Follow every formatting and data-source rule in `01_LOCKED_DESIGN.md`.

Keep formatting helpers private unless they express generally reusable clock
behavior, in which case they belong on `SimulationTime`. Do not add a UI panel,
CSV exporter, editor-only dependency, or transport-history query.

## Automated tests

Add focused tests proving the table:

- contains one row each for a facility worker, a pilot, and an unassigned
  colonist;
- sorts deterministically;
- reports A/B/C and wraparound planned windows correctly;
- labels schedule complements as `Off duty` while preserving actual Sleeping,
  Resting, Passenger, WaitingForTransport, Working, or OnDutyCrew state;
- displays current fatigue and exhaustion latch;
- reports active duty or the newest completed `DutyRecord` with start/end
  fatigue;
- reports invalid/missing assignment data without throwing;
- does not mutate any colonist, assignment, duty record, contract, or ship.

Add or extend a PlayMode test that runs across at least 48 simulated hours and
records state at these boundaries:

```text
Day 1 00:00  A begins
Day 1 08:00  A ends; B begins when assigned
Day 1 16:00  B ends; C begins only when assigned
Day 2 00:00  A repeats
Day 3 00:00  second midnight crossed without drift
```

Use tolerances appropriate to the configured simulation tick. Assert window
membership through production state, worker activity, or crew readiness rather
than expecting an event at an impossible sub-tick instant.

## Manual live-scene gate

Run `Assets/SpaceSim.unity` for at least 50 game hours and inspect the schedule
dump plus the normal log. Record exact observed timestamps in
`STAFFING_PATCH_ACCEPTANCE.md`.

Required observations:

- Farm Shift A does not restart at hour 16. It remains unstaffed until the next
  explicitly covered window.
- Shuttle pilots assigned A/B can cover at most `00:00-16:00`; the shuttle parks
  during the empty C window and resumes with A on the next day if the pilot is
  eligible and physically available.
- The mining ship with only A assigned is unavailable for the remaining 16
  scheduled hours of each day.
- A pilot finishing accepted work may return after the nominal boundary, but
  this does not activate the outgoing or next empty shift.
- Fatigue still rises and falls at the authored per-hour rates and the exhausted
  latch still clears only at or below `0.20`.
- Logs cross Day 1, Day 2, and Day 3 with monotonically increasing underlying
  duty/contract timestamps and no recurring exceptions or state-transition
  spam.

## Final verification

Run, in order:

```text
dotnet build ColonyPrototype.Runtime.csproj --nologo
dotnet build ColonyPrototype.Tests.csproj --nologo
dotnet build ColonyPrototype.PlayModeTests.csproj --nologo
Unity EditMode tests
Unity PlayMode tests
live SpaceSim smoke run
```

Compilation alone is not acceptance. If another Unity editor instance blocks a
batch run, use the open editor or record the remaining runtime gate as pending;
do not claim the patch complete.
