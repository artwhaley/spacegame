# T05 — Passenger/Vehicle Authority and Teardown Recovery

## Goal

Ensure destroyed/disabled transports or occupants cannot strand the simulation.

## Authority contract

- `TransportContract.passengers` = transport obligation/manifest.
- `PassengerCarrierComponent` = physical onboard occupancy authority.
- Colonist transit/arrived location must reconcile with those but must not become a third independent manifest.
- Freight reservation remains owned by existing inventory/contract logic.

## Changes

1. Do not clear a live serialized passenger roster unconditionally during ordinary initialization.
2. Expose carrier occupancy read-only outside the carrier.
3. Prune destroyed/null passengers safely.
4. On vehicle destruction or terminal loss:
   - release movement lease;
   - cancel or reopen active contract according to state;
   - unwind/release freight reservation when contract is cancelled;
   - resolve surviving passengers to a valid recoverable state/location;
   - make the work eligible for redispatch where appropriate.
5. On passenger destruction:
   - remove/prune them from physical occupancy and manifest processing;
   - surviving passengers must still unload/complete correctly.
6. On contract-manager replacement/reconstruction:
   - reconcile any physical onboard state whose contract disappeared;
   - do not leave `Passenger` activity forever with no obligation.
7. Open, not-yet-committed passenger contracts may be cancelled/rebuilt on reassignment; accepted/in-flight trips still finish.

## Tests

- destroy vehicle during reserved freight;
- destroy vehicle with passengers onboard;
- destroy one passenger from a multi-passenger manifest;
- replace ContractManager during passenger trip;
- reassignment cancels only uncommitted/open commute, not committed flight.

## Acceptance

- [ ] No reservation remains owned by a dead/cancelled vehicle contract.
- [ ] No surviving colonist stays Passenger forever with no contract.
- [ ] No carrier capacity remains consumed by destroyed occupants.
- [ ] Accepted flights remain immutable unless the vehicle is destroyed/operation is explicitly cancelled.
