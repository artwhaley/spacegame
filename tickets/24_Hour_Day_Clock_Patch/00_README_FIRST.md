# 24-Hour Day Clock Patch

Repository: `C:\Users\artwh\BanishedInSpace`  
Unity: `6000.5.9f1`

## Purpose

Make one simulation day exactly 24 game hours everywhere that calendar time is
interpreted. One 8-hour assignment covers 8 hours and leaves 16 hours uncovered;
two consecutive 8-hour assignments cover 16 hours and leave 8 hours uncovered;
three consecutive 8-hour assignments cover the full day.

Absolute simulation time remains a monotonically increasing count of elapsed
game hours. Production, consumption, movement, fatigue, contract age, and duty
duration continue to use `deltaGameHours` or absolute elapsed hours. They must
not reset at midnight.

Read `01_LOCKED_DESIGN.md` completely before editing. Execute the tickets in
order:

1. `02_T01_CLOCK_API_AND_PRESENTATION.md`
2. `03_T02_DAILY_SHIFTS_AND_CONTENT.md`
3. `04_T03_SCHEDULE_DUMP_AND_ACCEPTANCE.md`

## Supersession

This packet supersedes earlier staffing-ticket language that allowed arbitrary
shift-cycle lengths, described `TwoShift8x8` as a 16-hour repeating cycle, or
said not to assume a 24-hour day. Earlier packets remain historical records and
must not be used to restore 16-hour repetition.

## Scope

Implement:

- a single authoritative `24` hours-per-day constant;
- derived day number and hour-of-day values without resetting elapsed time;
- day/time-formatted simulation logs;
- daily staffing shifts whose windows always repeat every 24 hours;
- three canonical 8-hour daily shift windows: A `00:00-08:00`, B
  `08:00-16:00`, and C `16:00-24:00`;
- scene/content migration with no automatic Shift C assignments;
- a read-only table dump covering every known colonist's planned schedule and
  current/most-recent actual duty state;
- focused EditMode, PlayMode, and live-scene acceptance.

Do not implement:

- an 8-hours-on/8-hours-off repeating rotation;
- work patterns whose period is not 24 hours;
- weeks, months, seasons, daylight, or calendar dates;
- fatigue-rate or threshold changes;
- automatic call-ins, relief-worker selection, overtime, or schedule rewriting;
- staffing UI or the future org-chart UI;
- resetting contracts, duty records, or any other elapsed timestamp at midnight.

The future 8-on/8-off feature requires a distinct rotation model and a product
decision about fatigue. Do not preserve `cycleHours` as a hidden route to that
feature.

## Current-work warning

The worktree contains existing staffing/fatigue/pilot changes. Preserve all
unrelated modifications. Do not reset, clean, restore, or replace files
wholesale. Rename Unity assets together with their `.meta` files so GUID
references remain intact.

## Definition of done

- Shift A is active at hours `0..<8`, then again at `24..<32`; it is not active
  at hour 16.
- Shift B is active at `8..<16`, then again at `32..<40`.
- Shift C is active at `16..<24`, then again at `40..<48`.
- A one-shift workplace is uncovered for 16 hours per day, a two-shift
  workplace for 8 hours, and a three-shift workplace for zero scheduled hours.
- The live farm and both live ships use the daily three-window pattern. Existing
  assignments remain A/B only, so Shift C begins empty.
- Logs show a day number and a 24-hour time-of-day while stored timestamps remain
  absolute elapsed game hours.
- The schedule dump includes pilots, facility workers, and unassigned colonists
  without querying transport-contract history.
- Relevant Unity EditMode and PlayMode tests pass, generated C# projects compile,
  and a live run crosses at least two midnight boundaries without a 16-hour
  shift restart.
