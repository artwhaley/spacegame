# S01 — `DockingPortComponent` and `DockingControlComponent`

Independent of S02. Read the historical §1 proposal in `01_LOCKED_DESIGN.md`; the API
is not fixed until the current docking experiment earns it.

## Must create
- `Assets/Scripts/ColonyPrototype/World/Docking/DockingPortComponent.cs`
- `Assets/Scripts/ColonyPrototype/World/Docking/DockingControlComponent.cs`
- `Assets/Scripts/ColonyPrototype/World/Docking/BerthRequest.cs` (result struct, request record, enums)
- `Assets/Tests/EditMode/DockingControlTests.cs`

## May modify
- nothing else. Do not touch ships yet; startup adoption of already-docked ships is
  implemented here but only exercised in S04.

## Tests (define the contract)
1. Two ports free, one request → `Granted`, port `Reserved`, queue empty.
2. Two ports, three requests → two `Granted`, third `Queued(0)`.
3. `ReleaseBerth` on a granted ship → head of queue becomes `Granted` on the *next
   RequestBerth poll*, its port `Reserved`.
4. Priority: later request with priority 9 beats earlier request with priority 3.
5. Idempotent: re-requesting while queued returns `Queued` with current position; no
   duplicate entry.
6. `CancelRequest` removes the entry and re-indexes positions.
7. All ports disabled → `Denied / AllPortsClosed`; queued ships remain queued (a closed
   port reopening grants normally).
8. `TryOccupy` with a port the ship did not reserve → false, no mutation.
9. `HoldingSlotFor` with 1 slot and 3 queued ships → all get slot 0 (no NRE).
10. `DockingControl.ForAnchor` returns null for an anchor with no control; registry
    drops the control on disable.

## Forbidden
- Time-limited reservations, port sizes/classes, one-way ports, fees.
- Any reference to voyages, contracts, or guidance.

## Acceptance
- [ ] Each file ≤ 250 lines. Enums and structs in `BerthRequest.cs`.
- [ ] Observable: in a scratch scene, a control with two ports and three
      `ShipComponent` requesters shows `Queue.Count == 1` and two `Reserved` ports in
      the Inspector after one Play-Mode tick.
