# T02 — Establish parent-demand accounting and provider-neutral execution

Dependency: T01. Primary surfaces: `Logistics/P4b/FreightLogisticsManager.cs`, `LogisticsRoutePlan.cs`, `IFreightLegExecution.cs`, reports/quotes, walking/shuttle executions and their immediate callers.

## Required shape

Keep modern `FreightOrder` as the parent demand and `FreightAllocation` as its bounded child shipment. Do not add a parallel legacy-style contract system. Each child can have several legs and an accepted leg can take several physical loads.

## Work

1. Give demand accounting one manager-owned mutation path or derive totals from active children. Implement the invariant in locked rule 3. Maintain original assigned quantities separately from live remaining quantities where necessary.
2. A final deposit increments delivered/decrements committed exactly by the accepted amount. Intermediate staging does neither. Cancelled untouched quantity becomes uncovered again. Retry/duplicate completion must be idempotent.
3. Make actual custody derivable from inventory claims. Audit `FreightLegProgress` counters against origin/carrier/staged token quantities. Retain useful historical progress but remove independently writable duplicate physical quantities, or constrain their updates to the same transfer operation with reconciliation.
4. Strengthen the existing execution seam with the smallest facts/results needed for capacity, readiness, movement completion, retry, and accepted-obligation completion. Keep correlated allocation/leg/execution/route identities.
5. Replace freight-manager concrete walking/shuttle lifecycle tests with those semantic facts. Concrete services may still construct their concrete executions. Provider movement remains outside the manager.
6. Introduce only the small quote/accept capability needed by T03 so both existing provider families can participate. Keep demand accounting independent of a provider class name, actor type, or vehicle mode. Update callers in this ticket so it compiles.

Do not prebuild drone/freighter implementations, a service locator framework, or a universal job engine. Do not alter worker completion policy in this ticket.

## Verification

Automated: small owner-level accounting test for demand 40 and children 20/10/5/5, mixed completion order, intermediate staging, duplicate acknowledgment, and one cancelled unstarted child. Use capacity-only collaborators, not fake people/ships/NavMesh. This protects a critical invariant; keep the scenario compact. Retain stale correlation regressions. Cover A02 and A05.

Human Unity: smoke the unchanged basic walking route after seam changes; no claim that this proves heterogeneous physical vehicles.

Optional diagnostics: assert/log a mismatch at the owning accounting boundary during development; avoid tick-by-tick global inventory scans.

## Done

The parent can truthfully aggregate heterogeneous children and the existing providers still compile through a shared lifecycle contract. A transfer between route legs cannot count as demand fulfillment.
