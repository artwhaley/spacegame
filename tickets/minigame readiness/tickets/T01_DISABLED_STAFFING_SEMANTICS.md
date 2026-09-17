# T01 — Disabled Staffing Must Block, Not Become Automation

## Goal

Make live disable/re-enable of a staffed facility semantically trustworthy.

## Problem

A disabled staffing provider can disappear from facility performance evaluation. The default "no providers" snapshot is correctly operational at 1.0 for genuinely automated facilities, but that makes a staffed facility whose staffing component was disabled look automated. Workers can also remain in a working state.

## Implementation

1. Preserve the distinction:
   - no staffing provider authored => automation/default behavior may remain operational;
   - staffing provider authored but disabled => publish/produce an explicit staffing blocker.
2. Make `StaffingManager` treat a disabled employment workplace/staffing component as unavailable.
3. End/release facility duty with the existing `WorkplaceUnavailable` semantics.
4. Re-enable should allow normal reconciliation to restore operation when requirements are met.
5. Do not special-case resources or recipes.

## Likely files

- `FacilityPerformanceComponent.cs`
- `StaffingComponent.cs`
- `StaffingManager.cs`
- focused tests

## Tests

Add one integration-style test equivalent to:

`DisabledStaffingProviderBlocksFacilityAndReleasesWorker`

It must:
- author a staffed facility;
- assign a valid worker;
- establish operational output;
- disable the staffing component;
- assert facility is not operational and blocker identifies staffing;
- assert worker is no longer `Working`;
- re-enable and assert normal recovery.

## Do not

- Do not change automated facilities with no staffing provider.
- Do not move staffing into recipes.
- Do not auto-reassign workers.

## Acceptance

- [ ] Disabled staffing cannot produce default 1.0 performance.
- [ ] Worker duty/activity becomes truthful.
- [ ] Automated no-provider facility still works as before.
- [ ] Re-enable recovers without manual reconstruction.
