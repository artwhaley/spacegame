# Source Audit Snapshot Used to Author This Packet

Public repository inspected: `artwhaley/spacegame`, branch `main`, September 14, 2026.

Important observed facts at packet authoring time:

- `InventoryComponent` stored generic entries with `ResourceType`, `float onHand`, `float reserved`, `float capacity`, and exposed generic Add/Remove/Reserve/Withdraw behavior.
- `ResourceType` contained Food, Ice, Water.
- `LogisticsManager` already maintained lists of `FreightDemand` and `FreightSupply`, reconciled demand against inbound quantity, source availability, destination capacity, and Shuttle cargo capacity.
- Freight demand had priority, min/max shipment, desired quantity, inbound quantity, and planning status.
- Freight reconciliation could select and immediately assign an available `ShuttleController` before the separate open-contract assignment path; this is why T10 unifies arbitration.
- `TransportContract` represented Freight and Passenger but stored `ShuttleController assignedShuttle`.
- `ContractManager.FindBestOpenContract()` ordered by priority then contract ID.
- `FarmController` was a large combined controller owning shift/commute, Water demand, Water consumption, and Food production.
- `WaterProcessorController`, `CommandPostController`, `ShuttleController`, and `MiningShipController` were still named concrete controllers with multiple responsibilities.
- `ResourceDeposit` was already a clean finite local-resource primitive.

Before executing, re-inspect the actual current branch. If an execution agent has changed the repository since this packet was authored, preserve the packet's architectural requirements but adapt paths and migration details to the real code rather than recreating obsolete classes.
