# T06 — Content, Scene, Prefab, and Documentation Migration

Depends on T05.

## Goal

Author real content and migrate the playable scene/prefab so the new behavior is present outside tests.

## Worker-class assets

Create real assets with stable ids and display names:

```text
Assets/GameData/Classes/Doctor.asset   stableId: doctor   displayName: Doctor
Assets/GameData/Classes/Nurse.asset    stableId: nurse    displayName: Nurse
```

Create `.meta` files through Unity. Do not hand-copy GUIDs. Existing Farm Technician and Pilot assets remain unchanged.

Every authored `StaffingRoleDefinition` must reference a non-null class. Test-only Doctor/Nurse roles must use the real assets when asset database access is appropriate; isolated domain tests may create temporary real `WorkerClassDefinition` objects.

## Person prefab

Add `ColonistStatusComponent` to `Assets/Person.prefab` with locked defaults. Remove pending-employment serialized fields. Verify every scene colonist inheriting from the prefab receives status exactly once.

Do not add fatigue fields directly to `ColonistAgent`.

## Existing content

- Set Farm Operator exertion to `1.0`.
- Set every habitation restfulness to `1.0`, including the Command Post/home used in `SpaceSim.unity`.
- Validate every role has a class.
- Preserve existing shifts, role capacities, effect curves, recipes, inventories, and resource-converter configuration unless a change is explicitly required by this packet.
- Keep both farmers' current explicit shift assignments; do not auto-stagger them.

## Scene migration

In `Assets/SpaceSim.unity`:

- remove pending employment overrides;
- ensure assigned workplace/role/shift references remain valid;
- ensure all colonists have a home with `HabitationComponent`;
- ensure no duplicate manager or status component is introduced;
- preserve shuttle, mining, freight, Water, Food, and production wiring.

Use Unity serialization/Inspector-compatible edits and verify the scene opens without missing-script warnings.

## Documentation

Update:

- `ARCHITECTURE.md`
- `CONTENT_AUTHORING.md`
- `STAFFING_ARCHITECTURE.md`
- `STAFFING_AUTHORING.md`

Document:

- immediate/atomic employment;
- immutable in-progress passenger trips and post-landing reconciliation;
- fatigue rates, exhaustion/recovery thresholds, role exertion, habitat restfulness;
- explicit shifts and intentionally uncovered positions;
- duty history as work truth;
- dynamic facility providers;
- simulation registry lifecycle and tick priority;
- how to author a class, role, shift, habitation, and status-bearing colonist;
- automatic call-in, rerouting, and multi-stop transport as non-current features, not TODOs required for correctness.

Remove all documentation of pending-until-home assignment and cached providers.

## Acceptance gate

- Unity imports all new assets without GUID/missing-reference errors.
- Person prefab and scene colonists have exactly one status component.
- Doctor and Nurse assets exist and role validation rejects null classes.
- Farm and Command Post values are explicitly `1.0`.
- Scene opens, runs, and preserves the resource-converter architecture.
- Repo search finds no stale pending-assignment documentation.

