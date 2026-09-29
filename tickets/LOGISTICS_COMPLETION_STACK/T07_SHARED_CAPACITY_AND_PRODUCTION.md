# T07 — Make shared storage work for mixed inputs and conversion

Dependency: T06. Primary surfaces: `LogisticsStockComponent`, demand publication/projected capacity, `ResourceConverterComponent`, a narrow atomic inventory mutation operation if needed, inventory/recipe editor validation and relevant tests.

## Decisions

Use explicit resource targets for mixed stores. Keep freight incoming projection as soft planning, not a second inventory capacity-claim framework. Another legitimate inventory writer may fill room and cause unloading to wait; T05/T06 must now make that recoverable.

## Work

1. Validate an entire proposed stock-policy change BEFORE applying it. Reject nonfinite/negative/illegal discrete amounts, duplicate conflicting resource policies, invalid thresholds, and combined target budgets beyond shared capacity. Return a usable result/reason; logging an error after mutating is insufficient for a later UI.
2. Permit `targetFull` only for an unambiguous single-resource policy. Migrate/diagnose existing authored data conservatively. Do not silently empty inventory or change existing quantities to satisfy a new target.
3. Compute continuous recipe feasibility from available inputs and net shared-capacity growth. For progress p, final usage is current usage minus total consumed*p plus total produced*p. Nonpositive net growth needs no extra free room. Respect per-resource availability and aggregate duplicate resource entries before validation/mutation.
4. For batches, allow starting with inputs occupying the space outputs will use. Consume inputs once. Keep batch work-in-progress if outputs cannot fit at completion. Retry output emission without consuming inputs or advancing production accounting again. Do not require future capacity to be permanently reserved at batch start.
5. Make each recipe mutation all-or-nothing from observers' perspective, using one inventory-owned operation if necessary. Consumers must not see partial multi-resource consumption then rollback. Inputs cannot steal unrelated owned reservations.
6. Project incoming freight across ALL resources sharing a destination, and each real staging point, without counting already-staged physical stock again. T03 discrete sizing must still work when continuous resources leave fractional free capacity.
7. Keep recipe code independent of workers; continue consuming facility performance. Do not change authored production rates to cover a storage bug.

## Verification

Automated: stable arithmetic cases: full 10 inputs → 5 outputs succeeds; continuous net-decreasing transform works at full capacity; net-increasing output respects space; blocked batch emits once when room returns; invalid mixed policy leaves previous policy unchanged. Keep tests at inventory/converter/policy owner level. A01, A16, A17.

Human Unity: mixed-input processor can import both configured targets and run; fill its output space during a batch and then clear it. Verify it waits and resumes with conserved inputs/output.

## Done

Shared capacity constrains the complete operation sensibly. Target configuration cannot make every resource independently demand the whole warehouse.
