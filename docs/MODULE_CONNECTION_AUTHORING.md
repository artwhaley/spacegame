# Module connection authoring

The five current room prefabs use one root `NavMeshSurface` each. Select `Colony > Navigation > Author Module Connection Prefabs` to make the setup idempotently. The command adds `ModuleConnectionPoint` to every `nodeConnect`, creates a child `WalkAnchor`, places it 0.5 world metres back along the node's local `-Z` direction, assigns the root surface, sets `Collect Objects` to `Current Object Hierarchy`, and saves a room-specific `NavMesh-<module>.asset` beside the prefab. `nodeDocking` is intentionally excluded.

Changing a prefab does not retrofit an unpacked copy that is already embedded in a scene. Replace those copies with the authored prefab assets, or run a separate scene authoring pass, before testing startup pairing. The current `Assets/Prefabs/prefabss.unity` layout contains embedded facility objects whose connection nodes are not within the 0.25 m pairing radius.

Each `nodeConnect` is a physical snap marker. The `WalkAnchor` is the NavMesh endpoint and must sit on the room's walkable floor. Keep the outward side of a node on local `+Z`; if a marker is rotated, the authoring pass follows that rotation. The default pairing radius is 0.25 m between connection markers and the default link width is 0.5 m. These are Inspector dials for the current static layout.

At Play Mode startup, enabled points register during `OnEnable`. The first `Start` call runs one global nearest-available pairing pass. A candidate must be within both endpoints' configured radii, belong to a different module surface, and use the same NavMesh agent type. Each accepted pair owns one bidirectional runtime `NavMeshLink` whose endpoints are the two `WalkAnchor` transforms. The pass warns once about a point with multiple nearby alternatives and uses deterministic hierarchy keys to break equal-distance ties.

Future construction code should disconnect a moved pair, place the new module, then call `ModuleConnectionPoint.DiscoverAndConnect()`. It should not poll every frame. Call `Disconnect()` on either endpoint to remove the link and clear both partner references. A disabled or destroyed endpoint also cleans up its connection.

Use `Colony > Navigation > Validate Module Connection Prefabs` after editing. Then validate the actual static scene with the whole-scene NavMesh surface disabled, inspect the link endpoints in Play Mode, and send a real NavMeshAgent across each intended boundary. An unmatched node or an invalid/missing surface is left disconnected and reported in the Console.

## Bob-and-Friends modular Stack 4 fixture

`Assets/bobandfriends.unity` is the legacy/reference fixture and must not be migrated in place.

Open:

`Assets/bobandfriends_modular.unity`

Then use:

```text
Colony > Navigation > Build Bob And Friends Modular Scene
Colony > Navigation > Validate Bob And Friends Modular Scene
```

The build command refuses to operate on any other scene. On its first run it uses the legacy objects inside the cloned modular scene as the source of truth for the current roles, shifts, Food resource/inventory/recipe, recovery values, and other Stack 3 tuning. It then:

- instantiates the real `CommandCenter`, `Cafeteria`, `Disco`, and `Farm` prefabs while keeping prefab linkage;
- rewires beds to `Bed01`-`Bed04`;
- rewires Command work to `Command01`, Cafeteria work to `Serve01`, Farm work to `Farmwork01`, Food to `Eat01`, and recreation to `Dance01` / `Dance02`;
- preserves the existing `Station Food Store` as the Stack 3 Food inventory;
- preserves Farm production's genuine-active-work requirement;
- snaps the modules into the unambiguous chain `Disco <-> CommandCenter <-> Cafeteria <-> Farm`;
- removes the old scene-authored `CommandPod` / `CommandModule` fixture from the modular scene;
- removes any non-module `NavMeshSurface` so a whole-scene bake cannot hide a broken modular connection.

The scene validator checks authoring and semantic references only. It does **not** replace the required human Play Mode acceptance: inspect the runtime links, confirm their endpoints land on local NavMeshes, and physically send colonists across the module boundaries.
