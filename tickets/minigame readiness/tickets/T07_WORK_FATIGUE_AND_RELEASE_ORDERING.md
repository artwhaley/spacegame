# T07 — Reconcile Before Work/Fatigue; Preserve Pilot Return Work

## Goal

Eliminate phantom post-shift work while preserving the intentional pilot-duty rule.

## Changes

1. Reconcile worker legality/state before applying work time and fatigue for that tick.
2. Facility worker:
   - if shift ended/exhausted/workplace unavailable before the tick's work consequence, do not grant another work/fatigue increment.
3. Responsible pilot:
   - accepted work completion and required return-to-base remain work;
   - continue `workedGameHours` and fatigue while fulfilling that obligation.
4. If a ship operation is **suspended because its operation component is disabled**, do not keep charging impossible active-work fatigue forever. Represent suspension truthfully.
5. Duty-end reason:
   - if exhaustion is the actual terminal reason at release, ensure history can record it rather than always preserving an earlier `ShiftEnded` reason.
   - preserve useful release-request reason separately if needed.
6. Do not tune fatigue rates/thresholds in this ticket.

## Tests

- exact shift boundary at normal and max simulation speed: no extra facility work tick;
- pilot fatigue accrues during committed return flight;
- suspended disabled operation does not accrue unbounded active-work fatigue;
- exhaustion end reason is visible when it actually terminates duty.

## Acceptance

- [ ] No post-shift facility work/fatigue.
- [ ] Returning pilot still works/fatigues as designed.
- [ ] Suspended operation does not masquerade as productive duty.
- [ ] Duty history preserves truthful reason semantics.
