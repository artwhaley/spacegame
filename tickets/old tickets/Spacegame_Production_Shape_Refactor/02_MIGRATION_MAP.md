# Current → Target Migration Map

| Current concept | Target |
|---|---|
| `ResourceType` enum | `ResourceDefinition` assets |
| float quantities with no discrete rule | same numeric storage + enforced `Fractional/Discrete` semantics |
| Farm hardcoded water→food | `RecipeDefinition` + `ResourceConverterComponent` |
| Water Processor hardcoded ice→water | same converter primitive with different recipe |
| Farm Water demand | `ResourceStockPolicyComponent` |
| Command Post Food/Water demand | `ResourceStockPolicyComponent` |
| Water Processor / Farm supply registration | stock-policy export configuration |
| future warehouse behavior | stock policy with background fill + export retention |
| `ColonistRole` | multi-class definitions + independent skills |
| Farm assigned farmers | `StaffingComponent` + `WorkScheduleComponent` |
| Command Post consumption | `HabitationComponent` + `PopulationResourceConsumer` |
| `ShuttleController` | Ship + Movement + PassengerCarrier + TransportVehicle + TransportExecutor |
| freight/passenger separate assignment paths | one dispatch arbitration |
| numeric prototype priorities like 50/100 | 1–10 |
| no vehicle work preference | `TransportDisposition` |
| `MiningShipController` | Ship + Movement + ResourceCollector + ExtractionMission |
| `ResourceDeposit.ResourceType` | `ResourceDefinition` |
