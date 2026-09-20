# S05 — Ship presentation: interpolation, thrusters, ports, holding

Depends on S03 compiling (can start before S04 lands). Read `01_LOCKED_DESIGN.md` §5.
This is the ticket that makes it look slick; take the visuals seriously.

## Must create
- `Assets/Scripts/ColonyPrototype.Presentation/ColonyPrototype.Presentation.asmdef`
  (references `ColonyPrototype.Runtime`; **never** referenced by Runtime)
- `.../Presentation/Ships/ShipView.cs`
- `.../Presentation/Ships/ThrusterSet.cs`
- `.../Presentation/Ships/DockingPortView.cs`
- `.../Presentation/Ships/HoldingPatternView.cs`
- Prefab/scene wiring for Shuttle and Mining Ship mesh roots, nozzle transforms, port
  lights and clamp arms (placeholder primitives are fine; motion and timing are not
  placeholders)

## May modify
- `Assets/SpaceSim.unity`, ship prefabs/mesh children only. No Runtime files.

## Steps
1. `ShipView`: sample `voyage.Flight` each `SimulationTick` boundary (subscribe by
   polling `SimulationManager.CurrentTick` change in `Update`), keep last two samples,
   interpolate the mesh root over the tick's real duration. Must be smooth at 1×, 4×,
   10× and hold still when paused.
2. `ThrusterSet`: map body-frame commanded force/torque to per-nozzle intensity; drive
   VFX Graph (preferred under HDRP) or ParticleSystem emission rate + emissive
   intensity; main plume scaled by main-thrust fraction; RCS puffs have ~150 ms attack
   so corrections read as discrete pulses.
3. `DockingPortView`: light colors per state; clamp arms animate over ~0.6 s real on
   Occupied/release; approach guide-lights pulse toward berth while any ship targets
   this port in Approach/FinalDocking.
4. `HoldingPatternView`: beacon blink + world-space label "Holding · #n" for queued
   ships (label can be a simple TextMesh; UI packet may replace it).

## Forbidden
- Writing any Runtime field or the ship root transform.
- Reading `Time.deltaTime` for anything except interpolation and animation of visuals.

## Acceptance (viewed, not measured)
- [ ] No visible stepping at any speed; paused ships are perfectly still.
- [ ] Ship visibly rotates before main burn; visibly flips and brakes before the
      approach corridor; RCS puffs appear on the side that matches the correction.
- [ ] Port lights and clamps track `DockingPortState`; a queued ship's beacon blinks
      at the holding slot with a correct queue number.
- [ ] Deleting every Presentation component leaves the simulation behavior identical
      (proves rule 2).
