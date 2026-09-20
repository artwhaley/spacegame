# Packet P0-0 — Skeleton (Days 1–2)

Small packet, run first. Every other packet assumes it is done. Constitution:
`ARCHITECTURE_CONSTITUTION.md`.

## Why
Resolves dependency flaws F1 (one scene edited by everyone), F2 (Presentation asmdef
needed by five tickets), F3 (no prefab socket convention), F11 (unwatchable clock).
See `DAY_BY_DAY_PLAN.md` § "Dependency flaws".

---

## K01 — Assemblies and folders (Day 1)
**Must create:**
- `Assets/Scripts/ColonyPrototype.Presentation/ColonyPrototype.Presentation.asmdef`
  (rootNamespace `AsteroidColony.Presentation`, references `ColonyPrototype.Runtime`)
- `Assets/Scripts/ColonyPrototype.UI/ColonyPrototype.UI.asmdef`
  (rootNamespace `AsteroidColony.UI`, references Runtime + Presentation)
- Folders `Assets/Art/`, `Assets/Prefabs/Modules/`, `Assets/Prefabs/Ships/`,
  `Assets/Prefabs/People/`, `Assets/Scenes/`
**May modify:** `Assets/Tests/ColonyPrototype.Tests.asmdef` (no new references needed;
verify it still compiles).
**Forbidden:** any reference *from* Runtime to the new assemblies.
**Observable:** project compiles; Runtime asmdef references unchanged.

## K02 — Additive scene split (Day 1)
**Must create:** `Assets/Scenes/Bootstrap.unity` (one `SceneLoader` MonoBehaviour in
Presentation that additively loads the others), `Managers.unity` (SimulationManager,
Staffing/Logistics/Contract/Population managers, SimulationLog, camera for now),
`Base.unity` (all colony objects, people root, lights that belong to the base),
`Environment.unity` (Sun, sky/fog volume, asteroids), `UI.unity` (empty for now).
**May modify:** `ProjectSettings/EditorBuildSettings.asset`; retire `SpaceSim.unity`
(keep as `SpaceSim_legacy.unity` until Day 6, then delete).
**Rule:** singletons (`*.Instance`) live only in `Managers.unity`. Cross-scene
references are not allowed; everything resolves through registries/`Instance`.
**Observable:** Press Play from `Bootstrap` → identical colony behavior to the legacy
scene over 24 game-hours.

## K03 — Clock retune (Day 1)
**May modify:** `Managers.unity` SimulationManager: `gameHoursPerRealSecond = 0.0166667`
(1 game-hour per 60 real seconds), `tickIntervalSeconds = 0.1`.
**Observable:** HUD-less check: `CurrentGameHour` advances 1.0 per real minute at 1×;
at 10× a day passes in ~2.4 real minutes.

## K04 — `ModuleSockets` convention (Day 2)
**Must create:** `Assets/Scripts/ColonyPrototype/Core/ModuleSockets.cs` (Runtime, data
only):
```csharp
public class ModuleSockets : MonoBehaviour {
    public Transform meshRoot;                 // decorative geometry; never the sim root
    public List<Transform> attachmentNodes;    // corridor/module connection points (P0-C)
    public List<Transform> dockPorts;          // berth poses; DockingPortComponent goes here (P0-S)
    public List<Transform> dockApproaches;     // one per port, on the port axis
    public List<Transform> holdingSlots;       // station-keeping poses for queued ships
    public List<Transform> workstations;       // Facilities presenter slots (P0-P)
    public List<Transform> beds;               // sleeping slots; count = HabitationComponent capacity
    public Transform evaSpawn;                 // where a body appears when arriving on foot
    // OnValidate: meshRoot != null, no socket is the sim root, beds.Count matches HabitationComponent if present
}
```
**Observable:** Inspector on any module shows validated socket lists; Scene view gizmos
draw them (Editor script in `Core/Editor/ModuleSocketsGizmos.cs`).

## K05 — Prefab conversion (Day 2)
**May modify:** `Base.unity`; **Must create:** `Prefabs/Modules/CommandPod.prefab`,
`Farm.prefab`, `WaterProcessor.prefab`; `Prefabs/Ships/Shuttle.prefab`,
`MiningShip.prefab`; `Prefabs/People/Colonist.prefab` (from `Person.prefab`, mesh under
a child root); `Prefabs/World/IceDeposit.prefab`.
Each: sim components on the root; primitives moved under `meshRoot`; `ModuleSockets`
authored with placeholder transforms (CommandPod: 2 ports, 2 holding slots, 8 beds, 4
nodes; Farm: 1 port, 2 workstations, 2 nodes; WaterProcessor: 1 port, 2 nodes).
`Base.unity` contains only prefab instances plus the `People` root.
**Observable:** identical behavior; changing a prefab changes the scene instance.

## K06 — Acceptance
- [ ] Runtime asmdef references unchanged; Presentation/UI compile empty.
- [ ] Five scenes load from Bootstrap; no cross-scene references (Unity would log them).
- [ ] 24h run matches legacy scene: same contract count, same duty events for Pilot 3.
- [ ] Every module prefab passes `ModuleSockets.OnValidate` with zero errors.
- [ ] `ARCHITECTURE.md` gains a "Scenes and assemblies" section; `CONTENT_AUTHORING.md`
      gains "Author module sockets".
