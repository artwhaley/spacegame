# T06 — One Duty-Phase Writer, Truthful Semantics

## Goal

Make duty state safe for direct use by management UI.

## Required semantics

Recommended states:
- `ReleasedResting`
- `ScheduledShift`
- `Blocked`
- `AcceptingNewWork`
- `CompletingCommittedWork`
- `ReturningHome`

If an explicit `ExecutingCommittedWork` state is cleaner than overloading `AcceptingNewWork`, add it. The critical rule is semantic truth, not preserving an underspecified enum.

## Changes

1. Identify one authoritative computation path in `StaffingManager` (or an equally narrow owner).
2. Compute/set duty phase once per colonist per simulation tick after reconciliation.
3. Remove phase writes from storage/helper methods that should only store duty records.
4. Remove duplicate/competing writes from `ShipCrewDutyComponent` except for state it uniquely owns; feed facts into the authoritative computation instead.
5. `CompletingCommittedWork` means:
   - release boundary reached;
   - already accepted Transport/Extraction work still must complete.
6. If no committed operation exists and the responsible pilot is returning to base, state is `ReturningHome`.
7. `Blocked` covers scheduled duty that cannot currently proceed, with a reason.
8. In-transit passenger state must derive from actual contract destination; never label "ReturningHome" merely because shift is inactive.
9. A pilot actively executing committed work must not be labeled `AcceptingNewWork` unless the dispatch system truly allows additional work.

## Tests

Drive:
- facility worker normal shift;
- pilot normal accepting state;
- pilot active committed transport;
- release during committed work;
- release with no committed work;
- blocked ship/no dock;
- passenger traveling toward workplace while off-shift/reassigned.

## Acceptance

- [ ] One authoritative phase writer.
- [ ] No phase contradicts actual destination/commitment.
- [ ] UI can display phase + blocker/release reason without consulting implementation internals.
