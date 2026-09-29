# C01 — Size children for serviceable transport capacity

Priority: P1. Corrects T03/T06. Status: implemented in source; Unity acceptance pending.

## Reproduction and source trace

Put 40 units in a departure depot with sufficient destination room and a minimum shipment of 1. Register active shuttle A with capacity 20 and no available pilot, and shuttle B with capacity 10 and an available pilot. Require a shuttle leg.

1. `FreightLogisticsManager.FindShuttleLeadingCandidate` sizes the child from `GetMaximumShuttleCargoCapacity` (lines 1627–1632, 1767–1778). The maximum includes A without checking pilot/readiness.
2. `ShuttleManager.TryQuoteFreightLeg` accepts that 20 through the same broad capacity calculation (lines 321–337, 371–381 in `ShuttleTransportContracts.cs`). Acceptance creates a request, not an available-carrier commitment.
3. `FindAvailableShuttle` requires `CanStartTripNow` and capacity for the fixed child (lines 613–629). A is ineligible and B is too small. Further dispatch can commit the remaining demand into another 20. No source-state resplitting makes B useful.

The walking-to-shuttle route builder uses the same maximum-capacity assumption. Cargo can therefore be staged for a shipment that the available fleet cannot move.

A second counterexample is demand 40, minimum shipment 20, and only capacity-10 providers. The direct walking filter (manager lines 1452–1457), multimodal filter (1563–1565), and shuttle-leading filter (1700–1703) reject feasible 10-unit loads while the parent has more than 20 uncovered. The final-tail exception never gets a chance to help.

Primary sources: [FreightLogisticsManager.cs](../../Assets/Scripts/ColonyPrototype/Logistics/P4b/FreightLogisticsManager.cs), [ShuttleTransportContracts.cs](../../Assets/Scripts/ColonyPrototype/Vehicles/ShuttleTransportContracts.cs), [ShuttleServiceComponent.cs](../../Assets/Scripts/ColonyPrototype/Vehicles/ShuttleServiceComponent.cs).

## Correction

- Establish a side-effect-free planning-capacity query that excludes an unstaffed or blocked large ship but still includes a healthy piloted ship that is busy with its current trip. Planning may commit a child to queued future service; actual trip assignment must continue to require immediate readiness.
- Bound actual commitments to feasible acceptance. Leave uncovered quantity for later availability. Revalidate at acceptance and roll back provisional claims on rejection.
- Preserve purposeful future-leg planning while a provider is temporarily busy. Do not blindly require every later leg to be immediately idle; ensure its chosen shipment size has a serviceable path or an explicit safe replan for untouched work.
- Treat the minimum as a batching preference. When all feasible carriers are smaller, make progress with legal smaller children. Preserve whole-unit normalization and final tails.
- Never resplit an already accepted worker obligation to evade finishing it. Do not introduce physical drones/freighters or a scheduling framework.

## Acceptance

Use a small owner-boundary regression that exercises candidate selection/acceptance with capacity/readiness collaborators, not merely `Mathf.Min` or demand arithmetic. Show that the ready 10 can serve the 40 despite unstaffed A, and that a busy but healthy piloted shuttle remains eligible for a future quoted leg while actual scheduling waits for it to finish. Failed acceptance leaves no leaked reservation/commitment; minimum 20 does not prevent four legal 10-unit shipments. Retain the 20/10/5/5 accounting case and discrete tail case.

In Unity, observe repeated voyages with A unstaffed and B ready, then make A serviceable and verify it can participate without duplicating commitments. Repeat once with a walking first leg and once with stock already at the dock. Log parent/child IDs and delivered/committed/uncovered totals. Quotes must not acquire pilots or docks.
