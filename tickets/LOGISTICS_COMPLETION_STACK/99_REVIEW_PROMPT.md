# Final adversarial review prompt

Review the final implementation of `tickets/LOGISTICS_COMPLETION_STACK` against `01_LOCKED_RULES.md`, current source, and recorded evidence. This prompt can be run by the implementing agent as a checklist or handed to a separate reviewer by the user. It does not itself authorize spawning another agent/task or require a different model.

Do not use the original audit's superseded between-load release/small-emergency-buffer suggestions as requirements. The owner deliberately requires completion of all assigned final-leg cargo.

## Follow the actual state transitions

1. Demand 40 is served by children 20/10/5/5, completed in different orders. Show where delivered/committed/uncovered change and why each physical unit is counted once. Intermediate stock must not count as delivered.
2. One capacity-10 vehicle eventually carries demand 40 in four shipments. Does source stock increasing to 100 make the route disappear? Can a depot be the first/last stock point? Can a below-minimum final remainder finish?
3. An employee accepts 20 dock units, carries 5 each trip, then becomes critically hungry or ends shift after trip one. Locate the full-obligation completion condition. Reject any implementation that relabels the remaining 15 as unowned/unassigned simply to release the worker.
4. Fail a final pickup and then restore the route. Is the retry state legal? Do stale execution/route reports remain inert? If a worker becomes unusable, who owns every remaining claim before and after replacement?
5. Load some manifest items, delay another, then retry. Fill destination staging during flight, arrive, and later free capacity. Can anything report success before deposit, load twice, move the shuttle away with unresolved cargo, or lose a delayed acknowledgment after pruning?
6. Cancel one unboarded passenger. Deny initial reposition. Delay a pilot while the shuttle starts at the requested destination. Occupy then free a berth. Verify symmetric ownership and explicit progress rather than dock-location inference.
7. Offer a ready shuttle alongside an unstaffed one. Does dispatch choose a serviceable provider without a supposedly read-only query acquiring leases or reserving docks?
8. Run a full-input, net-decreasing recipe and a batch whose output space fills mid-work. Confirm atomicity, no stolen reservations, and exactly-once inputs/outputs.
9. Change stock targets, pause providers, and run for several days. Trace live claims, retiring demand, bounded history, and global-pause behavior. No cleanup may treat a recoverable wait as terminal.

## Architecture and evidence

- Inventory, demand accounting, worker execution, and voyage remain separate owners.
- No extra backend, global command/event framework, hardcoded drone/freighter class branches, or speculative physical vehicle system.
- Generic interfaces are used behaviorally; passing reflection/property-name tests is not sufficient.
- Changes preserve user baseline, scene GUIDs, current meal behavior, workforce separation, and modern fixture isolation from legacy transport.
- Tests protect stable rules/regressions with small setups. Human observations establish navigation/animation/docking claims.
- Compile results are separated from executed tests and physical observations; new source inclusion is checked.
- Pending physical rows are explicitly pending, never inferred from source correctness.

Return prioritized actionable findings with concrete triggers and source references, or state which checks passed and the limits of available evidence. A missing physical observation is an evidence gap; a wrong code transition is a defect. Keep them separate. Do not demand tests of every branch or implementation-name conformity.
