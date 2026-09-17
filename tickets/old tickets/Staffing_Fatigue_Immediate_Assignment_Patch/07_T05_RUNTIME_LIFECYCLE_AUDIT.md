# T05 — Runtime Lifecycle and Registration Audit

Depends on T04.

## Goal

Make the independent-MonoBehaviour architecture reliable when components and managers are added, disabled, re-enabled, or destroyed in arbitrary order.

## Tickable inventory

At packet authoring, the known `ISimulationTickable` implementations were:

- `StaffingManager`
- `StaffingComponent`
- `ResourceConverterComponent`
- `PopulationResourceConsumer`
- `ResourceStockPolicyComponent`
- `LogisticsManager`
- `TransportExecutorComponent`
- `ExtractionMissionController`

Re-run `rg "ISimulationTickable" Assets/Scripts` and include any new implementation. Do not leave one on the legacy registration path.

## SimulationManager implementation

Implement the registry and priority design in `01_LOCKED_DESIGN.md`.

Required details:

- static desired registry accepts registrations before `Instance` exists;
- each entry has one stable ordinal assigned on first registration;
- an enabled object registers in `OnEnable` and unregisters in `OnDisable`;
- re-enable may receive a new ordinal after unregister; it must still appear once;
- `SimulationManager.Awake` adopts all still-live, enabled desired entries;
- manager destruction does not erase desired registrations belonging to enabled components;
- replacing the manager adopts them without components being toggled;
- use pending-add/pending-remove buffers or a snapshot so callbacks during a tick cannot corrupt iteration;
- additions during a tick begin next tick;
- a behaviour disabled before its turn in the current tick is skipped by checking `isActiveAndEnabled`;
- remove destroyed Unity objects safely despite interface references;
- priority sort uses ascending priority, then ordinal;
- default priority is `1000`.

Assign priorities exactly as follows:

```text
100  StaffingManager
200  StaffingComponent
200  ResourceConverterComponent
200  PopulationResourceConsumer
300  ResourceStockPolicyComponent
300  LogisticsManager
400  TransportExecutorComponent
400  ExtractionMissionController
1000 any future tickable that does not explicitly implement priority
```

If the audit finds another existing tickable, classify it by responsibility: activity/scheduling `100`, production/consumption `200`, policy/planning `300`, or physical execution `400`, and record the classification in `STAFFING_PATCH_ACCEPTANCE.md`. This is classification of an existing component, not permission to invent a new phase. `StaffingManager` must run before `ResourceConverterComponent` so threshold departure is reflected in that tick's production.

Delete direct `SimulationManager.Instance.Register/Unregister` calls and Start-only registration from tickables.

## Population registry

Change `ColonistAgent` to register/unregister on enable/disable, not Start/destroy only. When `PopulationManager` becomes active, it must discover all active enabled colonists once and reconcile its list. A manager created after colonists therefore sees them. Prune disabled/destroyed entries and prevent duplicates.

## Vehicle registry

Apply the same outcome to `TransportVehicleComponent` and `LogisticsManager`: vehicle OnEnable/OnDisable registration plus one manager-side active-vehicle discovery when the manager appears. A later manager must see already enabled vehicles. No per-tick scene scan.

## Singleton discipline

For every manager touched:

- set `Instance` only to the active manager;
- clear it in `OnDestroy`/`OnDisable` only when `Instance == this` as appropriate;
- log or reject a second simultaneous active manager rather than silently stealing the singleton;
- make disable/re-enable semantics explicit and tested.

Do not broaden this ticket into a service locator or dependency-injection framework.

## Essential PlayMode tests

For simulation tickables, colonists, and vehicles, cover:

1. member exists first, manager appears later;
2. manager exists first, member appears later;
3. member disables and stops participating;
4. member re-enables and participates once;
5. member is destroyed and disappears;
6. manager is destroyed/replaced while member stays enabled;
7. a member disables itself or another member during a tick without skipped/duplicate/unexpected calls;
8. a member added during a tick begins on the next tick;
9. staffing priority executes before converter priority;
10. repeated enable/disable never produces duplicate registry entries.

Use small test tickables/providers. Do not rely on log strings as the primary assertion.

## Acceptance gate

- All lifecycle tests pass.
- Every tickable uses the unified registration API.
- No enabled component is lost solely because its manager was created later.
- No disabled component continues simulation work.
- No per-frame/per-tick global discovery was introduced.
