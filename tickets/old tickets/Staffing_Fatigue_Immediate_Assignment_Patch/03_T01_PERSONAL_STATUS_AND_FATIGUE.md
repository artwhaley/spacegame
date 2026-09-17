# T01 — Colonist Personal Status and Fatigue Domain

Depends on T00.

## Goal

Add colonist-owned fatigue and duty-history primitives without yet changing staffing travel behavior.

## Production changes

### New personal-status component

Create:

- `Assets/Scripts/ColonyPrototype/People/ColonistStatusComponent.cs`
- its `.meta`

Implement the exact defaults, latch rules, methods, and 32-record history specified in `01_LOCKED_DESIGN.md`.

Required public surface:

```csharp
float Fatigue { get; }
bool IsExhausted { get; }
IReadOnlyList<DutyRecord> DutyHistory { get; }
bool HasActiveDuty { get; }
void ApplyWorkFatigue(float deltaGameHours, float exertionMultiplier)
void ApplySleepRecovery(float deltaGameHours, float restfulnessMultiplier)
void AdjustFatigue(float delta)
void BeginDuty(StaffingComponent workplace, StaffingRoleDefinition role, string shiftId, float gameHour)
void AddWorkedTime(float deltaGameHours)
void EndDuty(float gameHour, DutyEndReason reason)
```

`BeginDuty` is idempotent for the same active workplace/role/shift. If called for a different duty while one is active, close the previous record as `Reassigned` before starting the new one. `EndDuty` is a no-op when no duty is active. Store completed records oldest-to-newest and evict the oldest over the cap.

Do not create a generic dictionary-of-stats framework. This component is the future home for explicit personal-status fields, but this ticket adds only fatigue and duty records.

### Role exertion

In `StaffingRoleDefinition`:

- add serialized `exertionMultiplier = 1f`;
- validate it is finite and non-negative;
- clamp invalid Inspector input to a safe non-negative value in `OnValidate`;
- make `Validate` reject null `requiredClass`.

### Habitat restfulness

In `HabitationComponent`:

- add serialized `restfulnessMultiplier = 1f`;
- expose an effective non-negative value;
- validate/clamp non-finite or negative authoring values.

### Colonist activity

Add `Sleeping` to `ColonistActivity`. Do not infer fatigue from `Resting`; only `Sleeping` recovers it.

## Focused tests

Add EditMode tests for:

1. default fatigue is zero and not exhausted;
2. one work hour at multiplier 1 adds exactly `0.10`;
3. one sleep hour at multiplier 1 removes exactly `0.10`;
4. work multiplier `1.5` adds `0.15` per hour;
5. rest multiplier `1.5` removes `0.15` per hour;
6. all mutation paths clamp to `0..1`;
7. threshold sets the exhaustion latch and recovery above `0.20` does not clear it;
8. reaching `0.20` clears it;
9. negative/non-finite arguments cannot corrupt fatigue;
10. duty begin is idempotent, worked time accumulates, end reason is stored, and history caps at 32;
11. role validation rejects null class and invalid exertion;
12. habitation validation handles invalid restfulness.

Use real, temporary `WorkerClassDefinition` instances in role tests. Do not weaken class validation for test convenience.

## Acceptance gate

- Focused EditMode tests pass.
- Existing tests compile with `Sleeping` added.
- No staffing tick changes have been made yet.
- Fatigue and duty state are serializable and inspectable on a colonist component.
