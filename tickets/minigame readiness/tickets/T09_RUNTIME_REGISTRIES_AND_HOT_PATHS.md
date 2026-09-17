# T09 — Runtime Registries; Remove Per-Tick Scene Scans

## Goal

Make live state discoverable without per-tick `FindObjectsByType` and without losing inactive-owned relationships.

## Changes

1. Add the smallest appropriate registration path for ships used by staffing/crew-return logic.
2. Reuse an existing registry (`LogisticsManager.TransportVehicles`) where it is semantically sufficient; otherwise add a narrowly scoped read-only ship registry.
3. Register/unregister on enable/disable as appropriate, but preserve enough ownership state to reconcile temporarily inactive objects deliberately.
4. Replace per-tick/per-colonist `FindObjectsByType<ShipComponent>()` calls.
5. Keep discovery scans only as startup/recovery fallback, not hot-path truth.
6. For colony-wide UI, expose read-only registries/collections for inspectable facilities/ships/colonists/inventories using the smallest composition that works.
7. Do **not** add persistence/stable GUID infrastructure solely for this ticket.

## Tests / profiling

- no runtime `FindObjectsByType<ShipComponent>` after startup during a simulated day;
- inactive ship with leased pilot is not mistaken for "ship no longer exists";
- registry recovers across manager replacement.

## Acceptance

- [ ] No per-tick ship scene scans.
- [ ] Colony UI can enumerate relevant runtime objects without scanning every frame.
- [ ] Startup fallback still recovers misordered creation.
