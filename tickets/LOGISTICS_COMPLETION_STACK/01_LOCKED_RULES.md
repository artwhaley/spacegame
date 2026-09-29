# Locked gameplay and architecture rules

These rules incorporate the owner's response to the September 25 audit. They govern this stack over conflicting suggestions in that audit or historical packets. Code symbol names below map to current concepts; retain names unless a rename is necessary.

## 1. Demand, shipment, leg, trip, and worker obligation are different

| Concept | Meaning | Current mapping |
|---|---|---|
| Demand | The requester must receive Q units of a resource | `FreightOrder` |
| Shipment / child contract | A bounded share of one demand, with its own source and cargo route | `FreightAllocation` + `FreightDeliveryJob` |
| Leg | One part of that shipment's route between physical stock points | `LogisticsRouteLeg` |
| Trip/load | A carrier's one physical load/movement; may serve part of an accepted leg | Execution/provider state |
| Accepted worker obligation | The finite amount the worker agreed to finish on a leg | Work execution + lease |

Do not revive legacy `TransportContract` merely because the owner uses the word contract. One parent demand with modern child allocations expresses that requirement.

The owner's numerical intent is capacity-sized fulfillment: demand 40 with carrying capacity 10 can be four shipments of 10 over repeated trips. Ten is the load size, not an instruction to create ten shipments totaling 100. Another valid fulfillment is 20 by freighter, 10 by shuttle, and 5 each by two drones. Reuse a carrier after its previous work completes. A provider's temporary unavailability is not proof that the parent demand is impossible.

## 2. Finish accepted final-leg work in full

The owner explicitly established this through playtesting: a worker assigned final-leg cargo must move all that assigned cargo. They do not stop because the consumer now has a small safety buffer, a single load was delivered, their shift ended, or their release request became pending.

Example: 20 units staged at a dock, an employee accepts all 20, and their carry capacity is 5. They perform four loads and clear that assigned 20 before normal release. If hunger becomes critical after the first load, record the pending release and finish the accepted obligation; do not silently leave 15 reserved at the dock. The brain continues owning biology; the work owner controls commitment completion. This packet does not change hunger/fatigue rates or invent a needs exception to the owner's rule.

Apply this finish-accepted-job policy consistently to dedicated porters and borrowed employees. Admission excludes workers already ineligible under the current critical-need/work rules. A pending release prevents accepting NEW obligations or growing an existing obligation; it does not shrink one already accepted. Whole obligations can be sized sensibly at admission. Do not split/relabel an already accepted 20 into four independent fives to evade completion.

Exceptional recovery is different from normal release. If an executor becomes unusable, Logistics may atomically transfer responsibility for the ENTIRE outstanding obligation to an eligible replacement, keeping exact stock claims and progress. The old owner must not release first and leave unowned work. If no safe replacement/route exists, keep an explicit recoverable hold with custody, reason, and next action. Never pretend impossible work completed.

## 3. Accounting invariant

For an ordinary open demand:

`requested = delivered + committedOutstanding + uncovered`

All quantities are nonnegative within the project's epsilon. `delivered` counts only physically accepted final deliveries. `committedOutstanding` counts each active child's remaining final-delivery obligation ONCE, irrespective of the number of route legs, trips, inventories, or providers. Source reservation, carrier stock, and intermediate staging are physical custody, not extra progress toward fulfillment.

For demand 40: commitments 20+10+5+5 mean delivered 0, committed 40, uncovered 0. Delivering 10 changes this to 10/30/0. An explicitly cancelled child with 5 still at its original source, before execution begins and with no staged/final-leg obligation, changes it to 10/25/5. This is not permission to cancel an accepted staged final leg. Moving 20 into an intermediate dock changes no demand totals. Retrying a callback changes none twice.

Do not shrink original allocation totals to hide lost/released amounts; separate original amount, final delivered amount, and live remainder where needed. Derive totals from owned child state or update them through one manager-owned mutation path. No independently writable duplicate ledger.

## 4. Ownership

- Inventory alone owns physical quantities and stock claims. Changes use validated commands; views get read-only facts.
- Logistics owns demand, child allocations, committed outstanding, custody transfers, and leg completion.
- Work services select/own eligible workers. The brain does not acquire freight-specific policy.
- Providers own movement and report correlated facts. The manager should not branch on concrete walking/shuttle execution classes to implement lifecycle rules.
- Shuttle manager owns requests/batching; service owns trip execution and payload handshakes; voyage/docking own physical flight and berth occupancy.
- A completed transport request means its payload was accepted at the destination. A ship arriving is insufficient.
- Employment remains canonical `WorkforceManager` data; do not reconnect modern workers to legacy staffing/transport.

## 5. Dispatch and progress

- Allocate feasible quantities across available providers; do not require one provider to carry an entire demand or reject a demand because it is larger than one vehicle.
- Keep uncovered demand alive while waiting for the next trip/provider. Never reserve the same source units or promise the same destination capacity twice.
- Routes may start/end at a transfer depot. Omit unnecessary walking legs. Preserve explicit custody at each actual handoff.
- Walking a 20-unit accepted leg with capacity 5 is legal, even if the upstream carrier moved 20 at once.
- Prefer eligible dedicated porters before borrowing employees for equivalent work. Producer assistance and consumer emergency pickup stay distinct policies in their work service; emergency employees can initiate local pickup and finish staged final legs.
- Minimum shipment is a dispatch batching preference, not permission to declare an outstanding accepted demand satisfied with an undelivered tail. Permit a final legal remainder smaller than that minimum when needed to finish the demand. Discrete quantities remain whole.
- Waiting, paused, blocked-needing-recovery, cancelled, and completed have explicit meanings. Keep stale report rejection and exactly-once transfers.

## 6. Storage and production decisions for this stack

- Keep shared physical capacity; no weighted volumes or bin framework.
- `targetFull` is valid only when it is unambiguous for a single-resource stock policy. Mixed-resource stores use explicit targets whose combined configured target budget fits capacity. Reject invalid new policy configurations atomically; diagnose old invalid content without silently deleting stock.
- Use the existing freight-level incoming projection across all resources and route handoffs. Do not introduce a second inventory-wide capacity-reservation framework in this stack. Competing legitimate production can still consume room: unload waits/retries with custody retained. This limitation must be visible and documented.
- Evaluate continuous recipes using net capacity growth after all inputs/outputs. For batches, consume inputs once, retain work-in-progress, and wait for output room if another writer fills the freed space before completion. Do not repeatedly consume inputs while waiting.

## 7. Verification and scope

Read `TESTING_IN_EXPLORATION_MODE.md` before changing tests. Protect owner-level numeric rules, exactly-once transfers, stale-callback rejection, and known regressions. Physical routes, dock placement, animation, speed behavior, and emergent labor receive explicit Unity acceptance.

The mixed-provider example requires a usable extensibility seam and a small deterministic proof, not implementation of physical freighters or drones. Preserve the current MonoBehaviour architecture. Deliver narrow commands/queries for later UI; do not build UI here.
