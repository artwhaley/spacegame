# T03 — Fulfill demands with feasible shipments and repeated trips

Dependency: T02. Primary surfaces: freight candidate/route construction and acceptance, service quote/accept boundaries, shuttle availability/capacity query used for planning.

## Work

1. Replace whole-demand fit/rejection with feasible child quantity selection. For each route/provider use uncovered demand, available source stock, shared projected destination/staging room, and the provider's appropriate load/leg limits. Normalize discrete quantities after taking these limits.
2. Allow several providers to accept children of the same demand, including within a dispatch pass while feasible candidates remain. Recompute uncovered and capacity after each success. A failed accept releases any provisional stock/worker claim before another candidate is tried.
3. Keep loops bounded: no positive progress means stop this pass; do not busy-loop on an unavailable provider. Leave uncovered demand pending for subsequent trips. Do not reserve all remaining demand merely because one carrier could eventually make many trips.
4. Single carrier capacity 10 serving demand 40 must deliver four bounded 10-unit shipments over repeated availability. Mixed capacities 20/10/5/5 must be able to cover that same parent exactly. Existing accepted legs may still make multiple walking loads when the upstream shipment is larger than a worker's carry capacity.
5. Permit source/destination already equal to the route's transfer stock. Generate only necessary legs: walking-only, shuttle-only, walking→shuttle, shuttle→walking, or walking→shuttle→walking. No zero-distance pretend walking job just to satisfy a constructor. Accept the correct first provider.
6. Retain source and intermediate reservations throughout actual transfers. Do not reserve a worker for every future leg at initial dispatch. Awaiting a future provider is explicit pending work with custody, not a completed allocation.
7. Treat the minimum shipment setting as a normal batching preference. The final legal remainder of an open demand must remain deliverable; do not close it as fulfilled because it is below that preference.
8. Use deterministic ranking without hardcoded freighter/shuttle/drone names. Preserve emergency demand priority and dedicated-labor preference; cost need not become a global optimum. Include actual repeated walking return distance where the quote compares multi-load labor, and do not silently price flight as a free transport advantage.

## Verification

Automated: focused allocation/accounting cases for 40/10, 20+10+5+5 with mixed completion order, fractional free room for discrete cargo, failed acceptance rollback, and a final below-minimum remainder. Reuse T02's small collaborators. Protect numeric allocation and rollback, not a scene's exact route choreography.

Human Unity: demand larger than one shuttle, multiple shuttles where authored, and food already in the departure depot. Verify cargo progresses and totals stay exact. A03–A06.

## Done

More source stock cannot eliminate an otherwise valid smaller shipment; transport capacity limits shipments rather than the parent demand. Physical future vehicle types remain explicitly unimplemented.
