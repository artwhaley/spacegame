# Packet P0-D — Live & Die (Days 20–26) — DRAFT, lock after Day 13

## Needs (Day 20)
- `ColonistNeedsComponent` on the colonist prefab: `nutrition`, `hydration` 0..1,
  serialized. `PopulationResourceConsumer` reports per-colonist satisfaction each hour
  (`Fed(colonist, resource, fraction)`); unmet fraction drains the need at an authored
  rate (`needDrainPerHour`), satisfaction restores toward 1.
- Consequences via existing seams only:
  - fatigue recovery multiplier = `lerp(0.4, 1.0, min(nutrition, hydration))` applied in
    `ColonistReconciler` sleep recovery;
  - colonist work multiplier = same curve, exposed as `ColonistStatusComponent.
    WorkEffectiveness`; `StaffingComponent` multiplies its per-worker contribution by
    it (counts stay integer; contribution scales).
  - `hydration == 0` for 24h or `nutrition == 0` for 72h → `PopulationManager.Kill`.
- Colonist panel shows the two bars; alerts at < 0.3.

## Death (Day 20) — `PopulationManager.Kill(colonist, reason) → KillResult`
Order matters: end active duty (`Died`) → `StaffingManager.Unassign` → remove from any
`TransportContract.passengers` and `PassengerCarrierComponent` → `Pedestrian.Cancel()`
→ clear ship `ResponsiblePilot` if held (ship requests release; voyage aborts to nearest
berth) → `ReadinessHistory("colonist.death")` → unregister → destroy. Any step failing
is logged and the rest continue; the result names what could not be unwound.

## Housing (Day 21)
`HabitationComponent.capacity` = `ModuleSockets.beds.Count`. `home` assignment is a
command: `PopulationManager.SetHome(colonist, anchor) → HomeResult` (rejects full
housing). Homeless colonists (home null or over capacity) sleep at their current
location at restfulness 0.5 and show `Homeless` in HR. Habitat completion auto-homes
the homeless (allocator-style, once per hour, respecting pins).

## Scenario (Day 22)
`ScenarioDefinition : ScriptableObject`: `sitePlanes[]`, `modules[] {BuildingDefinition,
pose, planeId, initialStock[], staffingTargets[]}`, `corridors[] {moduleA, nodeA,
moduleB, nodeB}`, `ships[] {prefab, dockModule, portIndex, flightProfile}`,
`deposits[] {prefab, position, amount}`, `colonists[] {name, classes, skills, home,
employment?}`, `startHour`, `clockDefaults`. `ScenarioBootstrap` in `Managers.unity`
builds it through `ConstructionManager` completion paths (never by hand-placing
components) so scenario and construction cannot diverge. Default:
`Assets/GameData/Scenarios/Minigame.asset` per `GAME_DESIGN_DECISIONS.md`.

## Session frame (Day 25)
`UI/Session/MainMenu` (New Game, Quit), `GameOverScreen` (embeds Colony Report),
`SessionController` (New Game → load scenes → bootstrap; game over on population 0 →
pause + screen). Hotkeys: Space pause, 1–4 speeds, F focus, Esc menu.

## Playtest (Day 26)
Two sessions; write `PLAYTEST_NOTES.md`: top issues, tuning changes, "what did the
tester think the game was".

## Forbidden
Morale, health, immigration, events, save/load, tutorial.
