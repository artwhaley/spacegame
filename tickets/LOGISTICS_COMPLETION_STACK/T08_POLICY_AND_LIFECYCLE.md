# T08 — Close obligation lifecycle without abandoned reservations

Dependency: T07. Primary surfaces: freight manager and providers, stock policy commands, shuttle cancellation/pause paths, inventory/worker lifecycle hooks, necessary personnel runner lifecycle.

## Required semantics

| Event | Behavior |
|---|---|
| Worker shift/need release request | Complete ENTIRE accepted obligation, then release; do not accept more. |
| Stop new imports / lower target | Stop creating excess new children. Preserve accepted child obligations and their destination/custody until completed or explicitly recovered. |
| Cancel an unaccepted/provisional child | Roll back provisional claims; quantity returns to uncovered if the demand remains active. |
| Explicitly cancel a child before first physical execution, still wholly at original source | Atomically release its execution/stock claim and reopen its remaining quantity on an active demand; forbidden once a staged/final-leg obligation exists. |
| Cancel demand with accepted children | Mark retiring/no-new-dispatch; accepted children still finish. Final state must distinguish cancelled-with-deliveries from satisfied. |
| Provider disabled/paused | Preserve exact progress/custody; show pause; resume once enabled without duplicate transfer. |
| Route temporarily unavailable | Hold and retry the same obligation; keep ownership visible. |
| Executor permanently unavailable | Manager owns recovery; whole remaining obligation is explicitly reassigned only when a replacement can accept. |

## Work

1. Implement narrow validated policy/cancel/pause/recovery commands needed by these cases. Do not expose raw order/job state setters for UI.
2. Replace expiration-by-publication assumptions that ignore committed work. Distinguish a publisher removed/paused from a still-active demand waiting for capacity. Do not falsely satisfy or orphan a committed order.
3. Normal empty-handed boundaries between loads do NOT authorize release. Finish the accepted final-leg lot; use T04 semantics. An explicit pre-execution cancellation command is different from a worker asking to go off duty.
4. On disable, preserve custody and stop advancing that provider, including callbacks that otherwise bypass its tick. On resume, revalidate references/claims and continue from the actual loaded/staged state.
5. On irreversible loss of an executor, transfer recovery responsibility to Logistics before dropping its execution association where lifecycle permits. Retain manager-owned recovery records referencing existing surviving inventories/claims. Do not assume a destroyed worker's inventory still exists.
6. Guard supported removal commands against destroying loaded cargo/occupied vehicles. Arbitrary Unity debug destruction is not a supported cargo-recovery mechanic: detect invalid custody and report an explicit fault, never claim success or recreate stock. Record this physical limitation honestly; no disaster/salvage framework in this packet.
7. Recover to the intended destination whenever possible. Any exceptional redirection must select a real reachable existing inventory with capacity and explicitly transfer the remaining obligation/claims. Do not add a universal search algorithm solely for hypothetical destruction.
8. Align personnel shuttle-result observation with simulation pause/progression. Completion of gameplay routes must not advance new activities while paused merely because `Update` polls a completed request. Preserve visual interpolation and the existing activity routing seam.

## Verification

Automated: owner-level cancellation/accounting and pause/resume idempotency where subtle; one narrow lifecycle regression if engine behavior requires it. Do not test every possible component teardown permutation. A08, A10, A18, A19.

Human Unity: disable/re-enable walking and shuttle providers both empty and loaded; lower target with accepted cargo; request release mid-final-leg. Inventory/claims and responsibility remain explainable and resume without duplicates.

## Done

Supported player actions never create ownerless reserved cargo. Physical impossibility remains visible and unresolved rather than being hidden by a terminal success state.
