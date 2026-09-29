# T01 — Make inventory claims and reads trustworthy

Dependency: T00. Primary surfaces: `Economy/InventoryComponent.cs`, food reservation/commitment code in `Core/`, `InventoryRackView`, inventory editor and direct API consumers; focused inventory/food tests.

## Problem

Public mutable entries bypass quantity authority; capacity queries can expand storage; food uses ownerless aggregate reservations. Later shipment splitting needs strong, reusable inventory ownership rather than more freight-side quantity patches.

## Work

1. Keep serialized authoring compatible, but expose read-only runtime entry facts. Audit all `GetEntry`/`Entries` callers before changing signatures. Do not rename serialized fields without migration.
2. Move legacy capacity migration/overfull authored-stock preservation to an explicit initialization/editor migration boundary. `Capacity` and `FreeCapacity` reads must not grow capacity or emit repair mutations. Preserve old stock during migration and report invalid runtime mutations.
3. Preserve owned transfer atomicity: source, destination, and destination claim are coherent before either change event. Preserve token identity validation and stale-token rejection.
4. Add/use the narrow owned-consumption operation needed by meals: consume the requested quantity from that commitment's token exactly once, or fail without consuming another meal's claim. Move food reservation, commit, and release to owned tokens. Do not change meal cost, nutrition, access, or physical eating lifecycle.
5. Keep legacy aggregate reservation APIs only where real legacy callers remain. Do not reconnect modern food/freight to them. Document remaining compatibility users.
6. Reconciliation after externally removed stock must expose invalid/short claims to their owners; no caller should assume its old requested quantity is still reserved. Do not make arbitrary getters the only place repair notifications occur.

## Verification

Automated: retain/run quantity/capacity/owned-transfer invariants. Add only missing focused protection for one meal's release/consumption not touching another claim, and non-mutating capacity reads. These are stable ownership rules. No serialized YAML or property-name tests.

Human Unity: inspect authored inventories after migration; verify stock is conserved, ordinary dining consumes once, and cancelling an approach releases only its meal. Cover A01 and A17.

Optional diagnostics: record a before/after inventory snapshot during migration; remove one-off instrumentation after evidence is captured.

## Done

All updated callers compile; existing stock survives initialization; routine reads are side-effect free; food and freight each have identifiable claims. No new capacity-reservation framework is introduced.
