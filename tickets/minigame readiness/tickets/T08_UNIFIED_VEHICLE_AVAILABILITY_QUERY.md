# T08 — One Vehicle Availability Predicate and One Explanation

## Goal

Make dispatch, logs, and UI ask the same question.

## Changes

1. Consolidate `IsAvailable`, `AvailabilityBlocker`, `ReadinessBlocker`, or equivalent overlapping rules into one authoritative query, e.g.:
   `bool TryGetAvailable(out VehicleAvailability status)`
   where status includes a human-readable blocker and optionally a stable reason enum.
2. Every dispatch decision that means "vehicle can accept new work" must derive from this query.
3. Every UI/log explanation of why the vehicle cannot accept work must derive from the same result.
4. Include all real blockers:
   - movement phase / dock safety;
   - crew staffing enabled;
   - qualified responsible pilot / active pilot duty as required;
   - active committed operation/movement lease;
   - vehicle category/disposition hard filters where relevant.
5. Distinguish:
   - vehicle itself unavailable;
   - vehicle available but no candidate is eligible.
   Do not log the former when the latter is true.

## Tests

For every blocker state:
- authoritative available boolean is false;
- reason is not "available";
- clearing only that blocker restores availability when no other blocker remains.

Also test "vehicle available, zero eligible candidate" produces a candidate/dispatch explanation rather than a vehicle-readiness lie.

## Acceptance

- [ ] No contradictory availability log is structurally possible.
- [ ] UI and LogisticsManager consume the same availability source.
