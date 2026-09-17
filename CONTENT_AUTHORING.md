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

Create a GameObject with `LocationAnchor`, `InventoryComponent`, and `ResourceConverterComponent`. Add inventory entries with capacities, assign the recipe, and point `productionRateEffect` at a `FacilityEffectDefinition`. Add `ResourceStockPolicyComponent` if the facility imports inputs or exports outputs.

Staffing is a facility concern, never a recipe concern. To make the facility need workers, add `StaffingComponent` (with a `ShiftPatternDefinition` and one or more `StaffingRoleDefinition`s) and `FacilityPerformanceComponent`, then set the converter's `performance` reference. To make it automated, add no required role - the converter sees `IsOperational == true` and runs at `1.0` multipliers. See `STAFFING_AUTHORING.md`.

Every colonist prefab carries `ColonistStatusComponent`. Fatigue is `0..1`, rises
by `0.10` per game hour while Working, and falls by `0.10` per game hour while
Sleeping, modified by role exertion and home restfulness. At `0.90` the worker
leaves immediately; recovery clears the exhaustion latch at `0.20`. Assignments
are immediate and explicit (`workplace + role + shift`); there is no pending job
or automatic vacancy filler.

All simulation schedules use a 24-hour day. `SimulationManager.CurrentGameHour`
is still the absolute elapsed hour; `SimulationManager.CurrentHourOfDay` is the
daily view used by shifts and future UI. The committed `Daily8HourShifts`
pattern exposes A `00:00-08:00`, B `08:00-16:00`, and C `16:00-24:00`. Assigning
one, two, or three of those shifts is always explicit; an unassigned window is
uncovered. The future 8-hours-on/8-hours-off rotation is not represented by a
shift pattern.

## 6. Configure import, export, and priority

On the stock policy, add one entry per resource. Enable normal import, set reorder threshold, target stock, minimum/maximum shipment, and a priority from 1 to 10. Enable export separately and set `retainStock` when local reserve must be protected. The policy does not point at a source facility.

## 7. Make a warehouse-like background buffer

Give a GameObject a `LocationAnchor`, `InventoryComponent`, and `ResourceStockPolicyComponent`. Add an entry with `backgroundFillEnabled`, a background target, and spare inventory capacity. Add export policy to a source inventory. Background work will use idle ordinary transport only when no eligible foreground candidate exists.

## 8. Assign classes and skills to a colonist

On a `ColonistAgent`, add one or more `WorkerClassDefinition` assets to `classes`. Add `SkillRating` entries to `skills` and assign a `SkillDefinition` plus proficiency. Required classes are hard eligibility; skills affect only rules that explicitly name them.

## 9. Configure a Shuttle disposition

On a Shuttle's `TransportVehicleComponent`, assign its `ShipComponent`, cargo inventory, and passenger carrier. Choose `Neutral`, `FreightOnly`, `PersonnelOnly`, `PreferFreight`, or `PreferPersonnel`. Preferences apply only when passenger and freight candidates have equal priority.

## 10. Staff a shuttle or extraction ship

Add `StaffingComponent` and `ShipCrewDutyComponent` to the ship. Point the
roster at the ship anchor, an explicit shift pattern, and the Pilot role; set
`ShipComponent.crewStaffing`, `operatingRole`, `crewChangeBase`, and
`initialDock`. Assign pilots through `StaffingManager.Assign` using the roster,
role, and named shift. All assigned pilots must have the same crew-change base
as their `home`. Employment persists while off duty, but only one eligible
on-shift pilot is boarded as `ResponsiblePilot`; shift end or exhaustion causes
safe completion and crew-only return before handover. Empty shifts leave the
ship unavailable.

## 11. Configure a resource collector/extraction ship

Compose a GameObject with `LocationAnchor`, `InventoryComponent`, `ShipComponent`, `ShipMovementComponent`, `ResourceCollectorComponent`, and `ExtractionMissionController`. Staff it as described above, then assign a collectable resource and extraction rate to the collector, and a finite `ResourceDeposit`, unload location, unload inventory, and optional destination stock policy to the mission. The mission will not launch when the destination has no useful capacity or need.

## Verification

Use the EditMode tests under `Assets/Tests/EditMode` for quantity, recipe, staffing, policy, dispatch, conservation, and no-code composition coverage. In the scene, inspect each component's active recipe, blocked reason, inventory, stock entries, and priority before running the Phase 2 loop.
