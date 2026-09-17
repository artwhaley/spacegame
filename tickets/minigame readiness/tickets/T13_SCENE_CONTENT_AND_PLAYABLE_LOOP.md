# T13 — Scene/Content Corrections for the Playable Slice

## Goal

Make the authored slice visually/selectably coherent and economically observable long enough to evaluate gameplay.

## Scene corrections

1. Ensure logical dock and visible transform/port agree for all ships at startup.
2. Correct Mining Ship placement.
3. Add colliders/selectable surfaces consistently to every intended selectable facility/ship/asteroid.
4. Add/verify EventSystem only if the chosen UI input path requires it.
5. Establish a simple coherent placeholder scale convention; do not create a scale framework.
6. Keep simulation anchors separate from decorative mesh roots so models can be swapped without changing authority.

## Economy/content tuning

The reviewed content showed Food demand materially above supply.

Tune **content**, not architecture, so the intended farm/water/mining/shuttle loop can run for at least 72 game-hours without an unavoidable permanent Food shortage.

Acceptable levers include:
- consumption rate;
- authored staffing coverage;
- farm throughput curve;
- starting buffer.

Do not add starvation, morale, health, or automatic staffing in this packet.

## Inventory visuals

Create or validate the first rack/box presenter as a pure projection of `InventoryComponent`:
- rebuildable;
- no authoritative quantity;
- no mutation from visual destruction;
- standardized recognizable cargo representation.

## Verification

Fresh 72h smoke:
- no unavoidable permanent Food shortage;
- water/ice/farm/logistics loop visibly cycles;
- at least one freight movement and one personnel movement complete;
- all intended selectable objects can be selected;
- no logical/visual start-position mismatch.

## Acceptance

- [ ] Slice can demonstrate the economic/logistics loop for 72h.
- [ ] Scene object selection is coherent.
- [ ] Placeholder transforms/ports agree with simulation state.
- [ ] Inventory visuals cannot affect inventory state.
