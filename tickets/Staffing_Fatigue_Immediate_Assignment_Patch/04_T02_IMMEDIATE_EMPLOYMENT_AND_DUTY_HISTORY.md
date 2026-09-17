# T02 — Immediate Employment and Duty History

Depends on T01.

## Goal

Replace pending-until-home assignment with immediate, atomic employment changes and explicit work-history closure.

## Required production changes

### Remove pending employment

From `ColonistAgent`, `StaffingManager`, tests, prefab, scene YAML, docs, and diagnostics, remove:

- `pendingEmployment`;
- `hasPendingEmploymentChange`;
- `AssignmentResult.Pending`;
- pending counters and pending-application methods;
- all text promising application after return home.

Do not leave hidden compatibility behavior.

### Assignment validation order

`Assign(colonist, workplace, role, shiftId)` must validate, in this order, before mutation:

1. colonist is non-null/known or can be registered;
2. workplace and its location/pattern are valid;
3. role is non-null, valid, and offered by the workplace;
4. shift id exists in the workplace pattern;
5. colonist has the role's required class;
6. the target role+shift has capacity, excluding the same colonist when they already occupy that exact assignment.

If any check fails, return the specific rejection and preserve existing assignment, activity, active duty, and transport state.

On success:

- close active duty as `Reassigned` if the assignment actually changes;
- replace `currentEmployment` immediately;
- update counts immediately;
- reconcile the colonist immediately using the T03 entry point. Until T03 lands, that entry point may retain current route behavior, but it must not defer employment.

Assigning the identical workplace/role/shift is idempotent: return Applied without closing/restarting duty.

### Unassignment

On `Unassign`:

- close active duty as `Unassigned`;
- clear `currentEmployment` immediately;
- update counts immediately;
- reconcile travel immediately;
- repeated unassignment is an Applied no-op.

### Counts and performance

Every assigned-worker query and active-worker query reads only `currentEmployment`. A reassigned Working colonist must disappear from the old workplace's active contribution synchronously with successful assignment, even before transport begins.

## Focused tests

Add/update EditMode tests proving:

- immediate assignment from home, workplace, third location, WaitingForTransport, and Working;
- immediate unassignment from those locations;
- old facility contribution disappears immediately;
- identical assignment is idempotent;
- invalid class/role/shift/workplace/capacity requests preserve the old job and active duty;
- capacity excludes the same colonist on an identical assignment but counts them when moving to another filled target;
- reassignment closes duty with `Reassigned` and unassignment with `Unassigned`;
- no public or serialized pending-employment state remains.

Transport-in-flight behavior is tested in T03 PlayMode tests, not faked here.

## Acceptance gate

- Focused tests pass.
- Repo search finds no pending-employment symbols or user-facing pending-assignment language.
- A successful assignment changes authoritative employment immediately.
- A rejected assignment has no side effects.

