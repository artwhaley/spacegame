# T10 — Narrow UI-Safe Commands/Queries and Inventory View Contract

## Goal

Give the upcoming management UI safe seams without creating a generalized command architecture.

## Staffing

1. Expose a non-mutating assignment validation API using the exact same rules as `Assign`.
2. Make the eligible-candidate query honor workplace, role, shift, capacity, crew-base, class, and existing operation constraints.
3. Return candidate rejection reason so UI can disable/explain choices rather than offer invalid selections.

## Other commands

As actual existing fields require player mutation, add narrow methods:
- recipe selection;
- transport disposition / category enablement;
- stock-policy edits;
- simulation pause/speed;
- any ship command already supported by the current design.

Do not invent commands for features that do not exist.

## Encapsulation

Close direct public mutation for runtime-authoritative fields the UI would otherwise poke:
- employment;
- arrived/transit location;
- passenger roster;
- ship dock/movement phase/lease;
- active contract assignment/state;
- active recipe if `SelectRecipe` is the intended validator.

Inspector-authoring fields may remain serialized where appropriate.

## Inventory presentation

1. Racks/boxes are pure views of `InventoryComponent`.
2. They never own or mutate a second count.
3. Add an `OnChanged` event if it materially simplifies rack refresh; it is allowed but not required to establish correctness.
4. Do not add capacity mutation APIs unless current gameplay/UI actually needs to change capacity at runtime.

## Acceptance

- [ ] UI can determine valid staffing choices before applying them.
- [ ] Applying a command still returns authoritative success/failure.
- [ ] Rack visuals can be destroyed/rebuilt with zero inventory effect.
- [ ] Direct mutation of core runtime authority from UI is unnecessary.
