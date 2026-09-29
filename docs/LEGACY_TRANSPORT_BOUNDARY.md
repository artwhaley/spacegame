# Legacy transport boundary

The repository contains two transport generations. B2 shuttle routing and freight work extend the modern logistics model below; modern fixtures must not activate the legacy authorities.

## Legacy

- `LogisticsManager`, `ContractManager`, and `TransportContract` implement the pre-P4b demand/contract/vehicle-arbitration flow.
- `TransportExecutorComponent`, `TransportVehicleComponent`, and `PassengerCarrierComponent` implement that generation's vehicle execution and passenger custody.
- `FreightDemand`, `FreightSupply`, `TransportDispatchCandidate`, `TransportPriorityRules`, and `ResourceStockPolicyComponent` support the legacy flow.
- `StaffingManager` and `ShipCrewDutyComponent` still contain compatibility consumers of legacy passenger transport. They remain in their current locations because they also own non-transport staffing and pilot-duty behavior.

These legacy scripts are grouped under `Assets/Scripts/ColonyPrototype/LegacyTransport/`. Their C# namespace, assembly, and Unity `.meta` GUIDs are preserved so older content and serialized references continue to resolve.

## Modern

- `LogisticsStockComponent` owns validated per-resource targets for one shared-capacity inventory.
- `FreightOrder` is the parent demand; bounded `FreightAllocation` children let several providers
  satisfy that demand with repeated or heterogeneous shipments. Final deposits alone increase
  delivered quantity; staging changes custody only.
- `FreightLogisticsManager` owns parent accounting, child commitments, source/staging claims,
  and delivery. It exposes uncovered and retired quantity separately and preserves accepted
  obligations when a publisher stops or lowers its target.
- `IFreightLegProvider` / `IFreightLegExecution` is the provider seam. The existing walking
  work service supports dedicated porters, producer assistance, and emergency employee labor.
  Accepted final-leg worker quantity completes across as many trips as required, including
  after a shift or need release becomes pending.
- `ShuttleManager` owns modern requests and trip scheduling; `ShuttleServiceComponent` owns
  payload custody and acknowledgement; `ShuttleVoyageComponent` owns movement. Cargo remains
  aboard until the destination inventory accepts it.
- `WorkExecutionLease` is the generic deferred-release contract for borrowed employee work.

B2 shuttle work extends these modern types. It does not create a `TransportContract` or make `LogisticsManager` the authority for a modern freight route.

## Fixture enforcement

`LegacyTransportSceneGuard.Validate` rejects active `LogisticsManager`, `ContractManager`, and `TransportExecutorComponent` instances in the B1 and P4b modern fixtures. The current modular and commute scenes use modern freight and shuttle authorities; fixture commands continue to call the same guard. Disabled legacy components are reported only when activated; the guard does not rewrite unrelated scenes.

Modern P4b source must not reference `LogisticsManager`, `ContractManager`, or `TransportContract`. No compatibility bridge is currently documented or required.

## Audit note

Legacy extraction and passenger/staffing compatibility consumers remain in place for existing content. They are not a fallback for modern freight. The modern shuttle path is implemented, but scene-level repeated cargo, berth, and pause/recovery acceptance still requires observed Unity runs; no physical drone/freighter implementation is implied.
