# Module connection authoring

The five current room prefabs use one root `NavMeshSurface` each. Select `Colony > Navigation > Author Module Connection Prefabs` to make the setup idempotently. The command adds `ModuleConnectionPoint` to every `nodeConnect`, creates a child `WalkAnchor`, places it 0.5 world metres back along the node's local `-Z` direction, assigns the root surface, sets `Collect Objects` to `Current Object Hierarchy`, and bakes the room into its own NavMesh data. `nodeDocking` is intentionally excluded.

Each `nodeConnect` is a physical snap marker. The `WalkAnchor` is the NavMesh endpoint and must sit on the room's walkable floor. Keep the outward side of a node on local `+Z`; if a marker is rotated, the authoring pass follows that rotation. The default pairing radius is 0.25 m between connection markers and the default link width is 0.5 m. These are Inspector dials for the current static layout.

At Play Mode startup, enabled points register during `OnEnable`. The first `Start` call runs one global nearest-available pairing pass. A candidate must be within both endpoints' configured radii, belong to a different module surface, and use the same NavMesh agent type. Each accepted pair owns one bidirectional runtime `NavMeshLink` whose endpoints are the two `WalkAnchor` transforms. The pass warns once about a point with multiple nearby alternatives and uses deterministic hierarchy keys to break equal-distance ties.

Future construction code should disconnect a moved pair, place the new module, then call `ModuleConnectionPoint.DiscoverAndConnect()`. It should not poll every frame. Call `Disconnect()` on either endpoint to remove the link and clear both partner references. A disabled or destroyed endpoint also cleans up its connection.

Use `Colony > Navigation > Validate Module Connection Prefabs` after editing. Then validate the actual static scene with the whole-scene NavMesh surface disabled, inspect the link endpoints in Play Mode, and send a real NavMeshAgent across each intended boundary. An unmatched node or an invalid/missing surface is left disconnected and reported in the Console.
