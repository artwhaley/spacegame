# Workforce Architecture

This is the canonical workforce direction for the Synty colonist path. The
older staffing system remains in the repository for legacy compatibility, but
new workforce code must not reconnect Bob to it.

## Canonical colonist workforce path

```text
Future WorkforceManager
    authoritative regular employment registry

ColonistIdentity
    identifies a colonist

JobRoleDefinition
    identifies a kind of regular job

WorkplaceComponent
    declares roles offered by a facility and maps
    roles to physical InteractableFacility activities

Future WorkAssignment
    colonist + workplace + role + direct start/end hours

ColonistBrain
    interprets current/next shift facts and decides behavior

ColonistTargetResolver
    resolves Work to the assigned workplace/activity

ColonistActivityRunner
    performs physical work
```

The future types named above are intentionally introduced incrementally. This
document describes ownership and dependency direction; it does not claim that
the future `WorkforceManager` or `WorkAssignment` already exists.

## Locked rules

- One colonist has zero or one regular job.
- Regular shifts are direct per-assignment start/end times, not named Shift
  A/B/C references.
- Workforce authority reports facts. It never moves colonists or starts
  activities.
- Facilities own physical work behavior.
- The brain decides what Bob does.
- `ColonistActivityRunner` moves Bob's body.
- Downtime/overtime opportunities are a later separate system and do not
  mutate regular employment.

The canonical path is separate from the legacy `StaffingManager`,
`StaffingComponent`, `EmploymentAssignment`, and `ShiftPatternDefinition`
architecture. Do not extend those legacy types for Bob's new workforce path.
