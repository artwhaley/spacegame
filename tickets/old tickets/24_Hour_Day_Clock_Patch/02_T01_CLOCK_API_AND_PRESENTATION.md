# T01 — Canonical 24-Hour Clock API and Presentation

## Outcome

The game retains monotonic elapsed hours for simulation math and exposes a
single, tested 24-hour calendar view for schedules, logs, and future UI.

## Production changes

1. Add `Assets/Scripts/ColonyPrototype/Core/SimulationTime.cs` with the exact
   public surface and semantics in `01_LOCKED_DESIGN.md`.
2. Add `CurrentDayIndex`, `CurrentDayNumber`, and `CurrentHourOfDay` to
   `SimulationManager`. Keep `CurrentGameHour` as absolute elapsed time.
3. Add `OnValidate` protection so non-finite or negative serialized elapsed
   time, rates, and tick intervals cannot create invalid calendar values. Do not
   alter normal tick accumulation or tick priority behavior.
4. Change `SimulationLog` from the raw `[{hour}h]` prefix to
   `[Day N HH:MM]`. All callers continue passing only their message.
5. Do not modify contract creation times, freight-demand ages, duty timestamps,
   movement rates, production rates, consumption rates, fatigue rates, or any
   calculation using `deltaGameHours`.

## Tests

Add `Assets/Tests/EditMode/SimulationTimeTests.cs` covering:

- day/hour boundaries at `0`, `7.5`, `23.999`, `24`, `47.999`, `48`, and
  `49.25`;
- positive modulo behavior for a negative pure-helper input;
- the exact required timestamp examples;
- rounding at the minute boundary, including rollover to the next day;
- `SimulationManager` convenience properties while `CurrentGameHour` remains
  absolute and greater than 24.

Update or add a `SimulationLog` test that asserts one formatted prefix. Do not
assert Unity console noise or depend on unrelated registration messages.

## Acceptance

- Advancing from `23.9` through `24.1` never lowers `CurrentGameHour`.
- The display changes from Day 1 to Day 2 exactly through the shared formatter.
- A contract created before midnight remains older than one created after
  midnight by absolute-hour comparison.
- Runtime, EditMode-test, and PlayMode-test generated projects compile with no
  type/namespace errors.
