# P4b Physical Walking Logistics acceptance

## Author the modular fixture

1. Open `Assets/bobandfriends_modular.unity` in Unity.
2. Run **Colony > Logistics > P4b > Configure Modular Fixture**. This incremental command adds the Airlock at Farm's unused connection point, migrates Food from the legacy station inventory into Cafeteria's local inventory, and assigns Dana to Porter duty while preserving the existing module layout.
3. Run **Colony > Logistics > P4b > Validate Modular Fixture**. The Console should report that authoring passed.
4. Confirm the Airlock connector is paired with Farm's open connector and that the scene NavMesh visualization shows a traversable link at the join.

## Play Mode checks

1. Start the modular scene. Confirm Farm production changes only Farm's inventory and Cafeteria consumption changes only Cafeteria's inventory.
2. Let Cafeteria reach its reorder threshold. Confirm one fixed Food order is opened and the assigned carrier walks to the source, picks up the reserved quantity, and walks to Cafeteria.
3. Compare eligible source/carrier choices: the dispatcher ranks them by `pickup quantity / full NavMesh trip distance` (carrier-to-source plus source-to-destination); equal scores break ties by distance, then a stable hierarchy key.
4. When Cafeteria reaches the emergency threshold, confirm an actively staffed Cafeteria worker can take an authorized work excursion. Before pickup the excursion can be cancelled if the worker is no longer eligible; after pickup the worker completes the run through delivery, including across a shift boundary.
5. Watch Food totals across Farm, carrier cargo, and Cafeteria during pickup and delivery. The total should remain constant at both inventory change notifications; delivery is partial only when destination capacity is unavailable, and cargo remains with the carrier.
6. Confirm the scheduled Porter goes to its duty anchor when on shift and returns there after delivery, while ordinary off-duty colonists do not take routine jobs.

NavMesh travel and animation remain human acceptance checks. The EditMode regression covers allocation-scoped reservations and conservation across inventory transfer events.
