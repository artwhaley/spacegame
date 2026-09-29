# C06 — Distinguish a paused walking provider from a lost owner

Priority: P2. Corrects T08. Status: implemented in source; Unity acceptance pending.

## Reproduction and source trace

Accept a walking shipment, then destroy only its `WalkingFreightWorkService` component while leaving the worker, route runner and cargo inventory alive. Repeat once before pickup and once while carrying/staging part of an accepted leg.

`OnDisable` unregisters the service; there is no destruction handoff (work service lines 60–64). `WalkingFreightExecution.SimulationTick` stops when `Service` is null/inactive (129–132), and route callbacks defer under the same condition (344–357). However, `ExecutorAvailable` checks worker/inventory/route runner, not existence of the service that owns execution (60–62).

The manager's `HandleUnavailableExecutors` skips that execution because it still reports available (1177–1196). The job can remain Assigned/Traveling with a lease and claim but no possible future tick. This fails the original stack's promised explicit permanent-owner fault; it is not a request to implement automatic salvage.

Primary sources: [WalkingFreightExecution.cs](../../Assets/Scripts/ColonyPrototype/Logistics/P4b/WalkingFreightExecution.cs), [WalkingFreightWorkService.cs](../../Assets/Scripts/ColonyPrototype/Logistics/P4b/WalkingFreightWorkService.cs), [FreightLogisticsManager.cs](../../Assets/Scripts/ColonyPrototype/Logistics/P4b/FreightLogisticsManager.cs).

## Correction

- Expose provider lifetime accurately through the existing execution contract. A temporarily disabled service and a permanently missing execution owner require different handling.
- A disabled provider preserves its accepted work and presents a paused/waiting reason; re-enable must process retained callbacks once. Inspect whether the worker's physical route should pause as well, without moving navigation into Logistics.
- When the service is permanently gone, use the existing owner-loss path: safely release only untouched original-source work under the existing cancellation rule; retain all started/staged outstanding responsibility and surviving claims in an explicit recovery hold.
- Ensure callback subscriptions and worker leases do not silently remain attached to an owner that can never resume. Do not release a live accepted remainder just to clear a lease. A safe replacement, if implemented, must take the whole remaining obligation atomically; otherwise keep a truthful hold.

## Acceptance

Use a small lifetime/availability regression for service destroyed with worker surviving; assert the manager recognizes owner loss. Keep it focused on Unity component lifetime only if that is required to reproduce Unity-null behavior.

In Unity, separately disable/re-enable and destroy the service, before pickup and after a load. Disable must resume once with unchanged claims and no new assignment accepted during the pause. Destruction must stop presenting the job as normally traveling and expose the retained quantity/custody/recovery reason. Verify the full 20-at-capacity-5 obligation is preserved if interrupted after the first load. No automatic salvage or newly invented normal-release exception.
