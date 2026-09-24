# Legacy transport boundary

The repository contains two transport generations. B2 shuttle routing and freight work extend the modern logistics model below; modern fixtures must not activate the legacy authorities.

## Legacy

- `LogisticsManager`, `ContractManager`, and `TransportContract` implement the pre-P4b demand/contract/vehicle-arbitration flow.
- `TransportExecutorComponent`, `TransportVehicleComponent`, and `PassengerCarrierComponent` implement that generation's vehicle execution and passenger custody.
- `FreightDemand`, `FreightSupply`, `TransportDispatchCandidate`, `TransportPriorityRules`, and `ResourceStockPolicyComponent` support the legacy flow.
- `StaffingManager` and `ShipCrewDutyComponent` still contain compatibility consumers of legacy passenger transport. They remain in their current locations because they also own non-transport staffing and pilot-duty behavior.

These legacy scripts are grouped under `Assets/Scripts/ColonyPrototype/LegacyTransport/`. Their C# namespace, assembly, and Unity `.meta` GUIDs are preserved so older content and serialized references continue to resolve.

## Modern

- `FreightOrder` records what resource quantity a requester needs.
- `FreightAllocation` commits a quantity, source, and complete loaded-cargo route.
- `LogisticsRouteLeg` represents one loaded-cargo movement between stock locations.
- `FreightLogisticsManager` owns allocation state, reservations, custody transfers, and completion.
- `WalkingFreightWorkService` provides workplace-owned walking execution for one leg.
- `WorkExecutionLease` is the generic deferred-release contract for committed work.

B2 shuttle work extends these modern types. It does not create a `TransportContract` or make `LogisticsManager` the authority for a modern freight route.

## Fixture enforcement

`LegacyTransportSceneGuard.Validate` rejects active `LogisticsManager`, `ContractManager`, and `TransportExecutorComponent` instances in the B1 and P4b modern fixtures. Any future B2 fixture validator must call the same guard. Disabled legacy components are reported only when activated; the guard does not rewrite unrelated scenes.

Modern P4b source must not reference `LogisticsManager`, `ContractManager`, or `TransportContract`. No compatibility bridge is currently documented or required.

## Audit note

The legacy pilot/staffing consumers are intentionally retained while B2 is not implemented. The source move is organizational only: no namespace, assembly, behavior, legacy contract, or vehicle logic is changed by this boundary work.
