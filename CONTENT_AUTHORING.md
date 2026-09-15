# Content Authoring Guide

The current scene can be extended through assets and component composition; a new facility does not need a bespoke C# controller.

## 1. Add a Fractional resource

1. In the Project window choose `Create > Asteroid Colony > Resource Definition`.
2. Set a unique lowercase `stableId`, a display name, and `Quantity Mode = Fractional`.
3. Save it under `Assets/GameData/Resources/`.

## 2. Add a Discrete resource

Create another `Resource Definition`, give it a unique stable ID, and set `Quantity Mode = Discrete`. Use whole numbers for inventory capacity, stock targets, recipe amounts, and shipments.

## 3. Add a Continuous recipe

1. Choose `Create > Asteroid Colony > Recipe Definition`.
2. Set a unique stable ID, `Execution Mode = Continuous`, and a positive duration.
3. Add fractional input/output `ResourceAmount` entries.
4. Assign the recipe to a `ResourceConverterComponent` through `availableRecipes` and `activeRecipe`.

## 4. Add a Batch/discrete recipe

Set `Execution Mode = Batch`, use complete discrete input/output amounts, and choose a positive duration. Inputs are consumed at batch start; discrete outputs appear only when the batch completes.

## 5. Compose a converter facility

Create a GameObject with `LocationAnchor`, `InventoryComponent`, and `ResourceConverterComponent`. Add inventory entries with capacities, assign the recipe, and add `StaffingComponent` when the recipe has a staffing rule. Add `ResourceStockPolicyComponent` if the facility imports inputs or exports outputs.

## 6. Configure import, export, and priority

On the stock policy, add one entry per resource. Enable normal import, set reorder threshold, target stock, minimum/maximum shipment, and a priority from 1 to 10. Enable export separately and set `retainStock` when local reserve must be protected. The policy does not point at a source facility.

## 7. Make a warehouse-like background buffer

Give a GameObject a `LocationAnchor`, `InventoryComponent`, and `ResourceStockPolicyComponent`. Add an entry with `backgroundFillEnabled`, a background target, and spare inventory capacity. Add export policy to a source inventory. Background work will use idle ordinary transport only when no eligible foreground candidate exists.

## 8. Assign classes and skills to a colonist

On a `ColonistAgent`, add one or more `WorkerClassDefinition` assets to `classes`. Add `SkillRating` entries to `skills` and assign a `SkillDefinition` plus proficiency. Required classes are hard eligibility; skills affect only rules that explicitly name them.

## 9. Configure a Shuttle disposition

On a Shuttle's `TransportVehicleComponent`, assign its `ShipComponent`, cargo inventory, and passenger carrier. Choose `Neutral`, `FreightOnly`, `PersonnelOnly`, `PreferFreight`, or `PreferPersonnel`. Preferences apply only when passenger and freight candidates have equal priority.

## 10. Configure a resource collector/extraction ship

Compose a GameObject with `LocationAnchor`, `InventoryComponent`, `ShipComponent`, `ShipMovementComponent`, `ResourceCollectorComponent`, and `ExtractionMissionController`. Assign a qualified Pilot class to the ship, a collectable resource and extraction rate to the collector, and a finite `ResourceDeposit`, unload location, unload inventory, and optional destination stock policy to the mission. The mission will not launch when the destination has no useful capacity or need.

## Verification

Use the EditMode tests under `Assets/Tests/EditMode` for quantity, recipe, staffing, policy, dispatch, conservation, and no-code composition coverage. In the scene, inspect each component's active recipe, blocked reason, inventory, stock entries, and priority before running the Phase 2 loop.
