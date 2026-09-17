# T02 — Daily Shift Semantics and Content Migration

## Outcome

Every staffing schedule repeats on the canonical 24-hour day. The current scene
offers three eight-hour windows but retains its existing explicit assignments,
leaving uncovered windows genuinely uncovered.

## Production changes

1. Remove `cycleHours` from `ShiftPatternDefinition`.
2. Replace cycle-relative shift evaluation with hour-of-day evaluation through
   `SimulationTime.HourOfDayAt`.
3. Update `ShiftDefinition` so its window helper assumes a 24-hour day and
   correctly handles ordinary, wraparound, and full-day windows.
4. Update validation to require `startHour` in `[0, 24)` and `durationHours` in
   `(0, 24]`. Preserve duplicate-ID rejection and allow overlapping windows.
5. Rename and migrate the canonical asset exactly as specified in the locked
   design. Move its `.meta` in the same operation so GUID
   `55556666777788889999000011112222` is preserved.
6. Confirm all three current `Assets/SpaceSim.unity` references still resolve to
   that GUID. Keep every colonist's existing A/B assignment unchanged; add no C
   assignment.
7. Remove `cycleHours` assignment from `StaffingTestHarness.Pattern` so every
   test-created pattern uses daily semantics automatically.

## Required test corrections

Update `Assets/Tests/EditMode/StaffingTests.cs` to prove:

- A: active `0..<8`, inactive at 8 and 16, active again `24..<32`;
- B: active `8..<16`, inactive at 16 and 24, active again `32..<40`;
- C: active `16..<24`, inactive at 24 and 32, active again `40..<48`;
- an 8-hour window starting at 20 wraps through midnight until 04:00;
- invalid start/duration and duplicate IDs are rejected;
- overlapping authored windows remain valid.

Correct the facility-performance tests:

- replace any claim that A+B provides continuous coverage;
- prove one A worker gives 8 scheduled active hours and 16 uncovered hours;
- prove A+B gives 16 active hours and an uncovered `16..<24` window;
- prove A+B+C can provide all-day scheduled coverage when each shift has an
  explicitly assigned qualified worker;
- keep throughput-curve assertions independent from schedule coverage.

Correct the staffing and pilot tests:

- an A worker/pilot must not return to work or board at hour 16;
- a two-pilot A/B shuttle must be uncrewed/parked during `16..<24`;
- an explicitly assigned C worker/pilot can work/board in that window;
- A becomes eligible again at hour 24, subject to the existing location and
  fatigue rules;
- no test infers work history from passenger contracts.

## Documentation migration

Update current top-level documentation:

- `ARCHITECTURE.md`
- `CONTENT_AUTHORING.md`
- `STAFFING_ARCHITECTURE.md`
- `STAFFING_AUTHORING.md`
- `STAFFING_PATCH_ACCEPTANCE.md` when runtime evidence is available

State that schedules are daily, show A/B/C, and distinguish absolute hours from
hour-of-day. Remove current documentation that calls the live pattern a 16-hour
cycle. Historical ticket packets may remain unchanged; the supersession notice
in this packet is authoritative.

## Acceptance

Run a focused scene-shaped test for 48 absolute hours and assert the actual
coverage windows, not just isolated `IsShiftActive` calls. Specifically reject
the regression where A restarts at absolute hour 16.
