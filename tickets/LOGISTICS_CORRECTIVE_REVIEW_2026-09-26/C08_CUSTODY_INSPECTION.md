# C08 — Make custody queries and transfer events truthful

Priority: P2. Corrects T02/T09. Status: implemented in source; Unity acceptance pending. Follow C04–C07.

## Source-confirmed mismatches

`FreightDeliveryJob` exposes inspection quantities at manager lines 200–209, but their definitions do not cover all current execution shapes:

- A loaded shuttle has no walking `LegProgress`, so `CarriedQuantity` returns 0 even when the job's cargo token holds 10 aboard.
- Completing an intermediate walking leg stores its staged token and clears `LegProgress` (1027–1028). `StagedQuantity` then returns 0 while cargo waits at the dock. Shuttle staging also lacks walking progress.
- During a multi-load final leg, `ArrivedAtDestination` increases before final-delivery accounting (951–965). `StagedQuantity` uses that historical counter, so 5 already delivered to the consumer reads as staged.
- `AcceptedWorkerRemainder` uses the shipment's remaining final-delivery quantity. On a first walking leg of 20, staging the first 5 does not reduce shipment quantity, so the worker remainder still reports 20 instead of 15.
- `CustodyInventory` exposes only the current token owner; a multi-load intermediate leg can simultaneously own origin, carried and staged claims. A single inventory is not a complete custody view in that state.

Separately, `CompleteCurrentLeg` logs `logistics.inventory_transfer` once in the common path (904) and again in the non-walking path (1057). A single shuttle deposit becomes two economic transfer events.

Primary sources: [FreightLogisticsManager.cs](../../Assets/Scripts/ColonyPrototype/Logistics/P4b/FreightLogisticsManager.cs), [SupplyChainDebugLog.cs](../../Assets/Scripts/ColonyPrototype/Logistics/P4b/SupplyChainDebugLog.cs).

## Correction

- Define carried stock, intermediate staged stock, final delivered amount, and current accepted-leg remainder separately and derive them from existing claims/progress at the owning boundary.
- Make simultaneous custody inspectable through a small read-only snapshot/query. Avoid a second writable physical ledger or an inventory scan across the colony.
- Keep historical moved/delivered counts explicitly historical. Never present final accepted consumer stock as reserved intermediate cargo.
- Emit one physical-transfer event per actual transfer with the existing demand/allocation/leg/execution correlation. Other state-change events may remain, but must not duplicate economic movement under the same key.

## Acceptance

Use a compact custody-boundary regression covering: shuttle loaded with 10 → carried 10; dock holding 10 awaiting assignment → staged 10; final accepted leg 20 after delivering 5 → final delivered 5, worker remainder 15, no consumer stock classified as staged; initial walking leg 20 after staging 5 → worker remainder 15, staged 5, origin 15. Include a split origin/carrier/stage snapshot without counting a unit twice.

Capture one shuttle deposit through the real completion path and verify exactly one `logistics.inventory_transfer` event with the actual amount. Duplicate acknowledgment emits no new transfer. Validate the visible job/query data against inventories in Unity when running the other tickets' acceptance; do not build player UI in this ticket.
