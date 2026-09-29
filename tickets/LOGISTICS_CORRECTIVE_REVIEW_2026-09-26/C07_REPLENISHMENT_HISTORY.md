# C07 — Keep terminal history out of replenishment decisions

Priority: P2. Corrects T08/T09. Status: implemented in source; Unity acceptance pending.

## Reproduction and source trace

Configure a consumer with target 100, reorder threshold 20, minimum 1. Publish a demand while on-hand is zero and transport is unavailable, so it has no child commitments. Fill the inventory to 100 by a legitimate local producer or stock mutation and publish again. The old order becomes closed and policy-retired. Consume one unit and publish at on-hand 99.

`FindOpenOrder` still selects the closed record because `IsPolicyRetired` is true (manager lines 2103–2110). The existing-order branch calculates uncovered need and reopens it (403–424), bypassing the reorder gate that applies only to a new order (434–440). A new 1-unit import can dispatch at 99. The record is also classified as recent/terminal and eligible for pruning when closed without commitments (465–466, 2158–2166). If pruned first, the same stock correctly waits until the threshold. History retention changes gameplay.

Primary source: [FreightLogisticsManager.cs](../../Assets/Scripts/ColonyPrototype/Logistics/P4b/FreightLogisticsManager.cs).

## Correction

- Separate an active demand with accepted children from a finished historical demand. Closed, zero-commitment records must not bypass normal replenishment admission.
- Once the prior demand is finished, apply the reorder threshold/minimum admission rules before starting a new replenishment cycle. Retain historical quantities and IDs for inspection.
- Continue updating a genuinely live demand and protecting accepted children when targets change. Lowering a target must not cancel staged work, reduce worker assignments, or erase the retired amount.
- Ensure pruning is behavior-neutral. Do not repair this by retaining all history forever or making another mutable demand ledger.

## Acceptance

Add a compact owner test for the numeric sequence above: target satisfied by local stock, consume 1, no new active demand at 99; consume down through threshold, legitimate new demand starts. Repeat with the terminal record absent/pruned and require identical behavior.

Add only the closely related live-child variation: target reduction while accepted work exists preserves that work and exact delivered/committed/retired totals. This belongs at the demand/publication owner boundary and needs no scene, worker simulation, or private-field-name assertions.
