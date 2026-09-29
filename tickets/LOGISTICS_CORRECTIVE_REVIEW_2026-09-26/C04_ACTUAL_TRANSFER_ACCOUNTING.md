# C04 — Reconcile physical transfer results before blocking

Priority: P2, critical accounting invariant under a supported exceptional mutation. Corrects T01/T02/T05. Status: implemented in source; Unity acceptance pending.

## Reproduction and source trace

Give a loaded final leg a 5-unit owned cargo claim. Through the public inventory command, remove 2 units from that cargo inventory before arrival; provide at least 5 free destination spaces. This is a deliberate owner-boundary reproduction, not an assertion that normal meal/recipe execution removes another owner's cargo.

`InventoryComponent.Remove` explicitly reconciles and reduces reservations (lines 365–383); `RemovingStockTrimsNewestOwnedReservationFirst` already documents this supported behavior. The remaining token is active for 3. `TransferOwnedToInternal` permits partial final transfers and returns 3 after actually moving it (599–617).

`CompleteCurrentLeg` accepts any active carried token, calculates requested transfer from unreconciled job/progress quantity, and calls that transfer. Its walking branch then blocks and returns before updating progress or `RecordFinalDelivery` (943–965). Its non-walking branch does likewise (1051–1054). Result: destination gained 3, delivered did not; the consumed token is inactive, and future retries fail the initial claim check. An intermediate transfer has different, all-or-nothing semantics and should retain them.

Primary sources: [InventoryComponent.cs](../../Assets/Scripts/ColonyPrototype/Economy/InventoryComponent.cs), [FreightLogisticsManager.cs](../../Assets/Scripts/ColonyPrototype/Logistics/P4b/FreightLogisticsManager.cs), [InventoryComponentTests.cs](../../Assets/Tests/EditMode/InventoryComponentTests.cs).

## Correction

- Treat the inventory operation's actual moved amount as authoritative. Any physical final deposit must update the child's and parent's delivery accounting exactly once before returning a failure/wait result.
- Reconcile carried progress and surviving claims at this boundary. If accepted quantity has genuinely disappeared, retain the outstanding obligation in a truthful recovery hold with a stock-shortfall reason; do not label it a destination-capacity problem or silently mark it fulfilled.
- An up-front exact-claim check may prevent a partial move, but any operation that can return a positive partial result must still be accounted for correctly. Keep Inventory as the physical authority.
- Preserve accepted worker responsibility and existing atomic intermediate transfer-and-reserve behavior. No automatic cargo recreation, salvage subsystem, or relaxed reservation ownership.

## Acceptance

Add one compact manager/inventory regression for a 5-unit final shipment reduced to 3. If 3 is deposited, require delivered 3, committed outstanding 2, no remaining physical cargo claim, and an explicit shortfall hold. Retry the same acknowledgment and prove it cannot credit or deposit again. Exercise both the across-load and single-load completion branches without fake workers/ships.

Also retain the ordinary capacity wait: intact claim, insufficient destination room, zero movement and zero credit, then exact delivery after space is freed. Confirm staging still gives zero parent-delivery credit. This is an owner invariant; it does not require a new physical loss mechanic or a broad PlayMode suite.
