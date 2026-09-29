# C02 — Keep cancelled shuttle requests terminal

Priority: P1. Corrects T06. Status: implemented in source; Unity acceptance pending.

## Reproduction and source trace

Assign two ready passenger requests to one trip. While the shuttle repositions or waits before boarding, cancel one unboarded request; leave the other live.

`ShuttleTransportRequest.TryCancel` accepts the cancellation (lines 134–140 in `ShuttleTransportContracts.cs`). `PrepareAndDepart` correctly skips it during loading (service lines 415–420), but successful departure calls `SetState(InTransit)` for every manifest entry (483–484). Pilot-wait loops have the same unfiltered transition. `SetState` only rejects the identical state (contracts lines 118–125), so Cancelled becomes InTransit. Destination processing then calls `Complete` on that passenger request (service lines 541–543), despite no boarding.

This can also revive a terminal freight entry. History pruning and correlation removal cannot repair a trip that still holds and mutates the old request object.

Primary sources: [ShuttleTransportContracts.cs](../../Assets/Scripts/ColonyPrototype/Vehicles/ShuttleTransportContracts.cs), [ShuttleServiceComponent.cs](../../Assets/Scripts/ColonyPrototype/Vehicles/ShuttleServiceComponent.cs).

## Correction

- Make terminal request states monotonic at their owner. A stale transition cannot revive Completed or Cancelled.
- Filter all trip-wide state propagation to applicable live entries, including pilot waits and departure. Audit completion side effects as well: rejected completion must not set `IsPhysicallyTransferred` on a cancelled payload.
- Ensure live manifest counts/readiness and trip completion ignore cancelled, untransferred entries. Keep already-boarded cancellation refusal and custody rules intact.
- Completion notification is emitted exactly once, only after actual payload acceptance. Cancellation emits no successful completion.

## Acceptance

Add a focused request/manifest lifecycle regression with two entries: cancel one before boarding, propagate pilot wait and departure, complete the other, and verify the cancelled entry stays Cancelled/untransferred with zero Completed events. Repeat duplicate terminal transitions and ensure the completed live entry emits once. Test the real owner behavior, not enum names or private field layouts.

In Unity, cancel one unboarded passenger from a mixed/live manifest while repositioning. Remaining passengers/freight must travel and complete normally, and the cancelled actor must neither board nor be reported as delivered. Run the ordinary already-loaded cancellation-refusal case as a companion observation.
