# Module connections — executable ticket stack

Status: M01-M03 IMPLEMENTED; M04 STATIC-SCENE TRAVERSAL AWAITING AN ALIGNED VALIDATION LAYOUT; M05 PARTIAL. See `tickets/MODULE_CONNECTION_POINTS_IMPLEMENTATION_REPORT.md` for evidence.
Execute M01–M05 in order after the current phase. Keep implementation within this small static-scene slice.

## Outcome and decisions

OWNER DECISION: Airlock, Cafeteria, CommandCenter, Disco, and Farm each carry their own baked navigation. Every connection marker receives ModuleConnectionPoint and a WalkAnchor half a meter inside the room. At scene startup, modules discover partners by distance and create traversable links automatically. The same discovery operation must be callable by future snap construction.

OWNER DECISION: Partner discovery is distance-only. Do not add facing, socket type, room type, or docking compatibility filters. Same-module exclusion, active-state checks, and one-partner occupancy are structural constraints, not compatibility rules.

WORKING HYPOTHESES / Inspector dials: maximum connection distance defaults to 0.25 m, measured between connection nodes in world-space 3D; anchor inset is 0.5 world meters; initial link width is 0.5 m, adjusted to actual doorway clearance. Revisit the distance after inspecting the static layout, and width/inset if actual baked endpoints or agent traversal fail. Do not silently increase the pairing radius to bridge unrelated rooms.

FACT at inspection: Unity 6000.5.9f1 and com.unity.ai.navigation 2.0.14. The four facility prefabs have NavMeshSurface components on Navigation children configured to collect all objects. Airlock has no directly saved surface. Saved nodeConnect counts: Airlock 1, Cafeteria 2, CommandCenter 2, Disco 1, Farm 2. Airlock also contains nodeDocking; docking is outside this stack. Reinspect before edits because the user is actively authoring these assets.

## Execution boundaries

- Preserve existing local edits, untracked prefabs, interaction recipes, activity anchors, and scene overrides. Inspect git status/diffs before editing. Do not stage unrelated work.
- Implement navigation in the project runtime layer, preferably Assets/Scripts/ColonyPrototype/Navigation; editor authoring utilities belong in an editor assembly. Inspect assembly definitions and add the required Unity.AI.Navigation reference. Keep the reusable interactions package content-agnostic.
- This system owns physical module connectivity only. Do not change employment, production, reservations, logical ColonistAgent.currentLocation, transport, or simulation timing.
- No construction UI, snapping controller, continuous proximity polling, runtime rebaking, or door/airlock simulation in this slice.
- Use Unity APIs to edit/save prefabs and bake navigation. Preserve GUIDs and include generated asset metadata. Do not claim YAML changes constitute a successful bake.
- Compilation alone is not end-to-end acceptance. Record unavailable Unity validation as INCOMPLETE and complete independent work.

## M01 — Connection point component and lifecycle

Dependency: none.

Implement ModuleConnectionPoint as an Inspector-authorable component with serialized owner NavMeshSurface, WalkAnchor Transform, connection distance, and link width. Expose read-only current partner/connected state for inspection and focused selected-object gizmos for node radius, anchor, and current link.

Derive module ownership from the assigned surface rather than transform.root, since several modules may share a scene parent. Resolve a parent surface when unambiguous; report missing/ambiguous ownership without connecting. Validate missing anchors and nonpositive settings with actionable diagnostics.

Define a reusable discovery entry point and disconnect operation. Finalize their minimal API alongside M02; do not create speculative construction interfaces. Register active points on enable, unregister on disable/destroy, and clear static state for Play Mode with domain reload disabled. Enabling a point registers it but does not schedule continuous discovery.

Acceptance: component compiles in the appropriate assembly; assigned owner/anchor survive prefab saving; disabled or destroyed points cannot remain usable candidates; repeated play sessions leave no stale registry entries. No simulation authority changes.

## M02 — One startup discovery pass and one link per pair

Dependency: M01.

Run one coordinated pass from an early Start phase after scene objects have registered in OnEnable and baked surfaces have enabled. Finish before the first navigation consumer requests a path; inspect actual consumer execution order and set explicit ordering where necessary. No yield-to-next-frame startup workaround. Multiple component Start calls must not repeat the global pass. No manually placed manager is required: use the point registration mechanism or a minimal automatically managed coordinator.

Build candidate pairs among active, unpaired points belonging to different module surfaces. Eligibility is node-to-node distance within both endpoints' configured limits. Sort unordered pairs by ascending squared distance; break exact ties with a documented deterministic hierarchy ordering, then greedily claim still-free endpoints. Do not depend on registration/FindObjects iteration order. This is nearest-available pairing, not a maximum-matching solver. Warn once for ambiguous nearby alternatives so mistaken layouts are diagnosable.

Use the same discover-and-connect operation for startup and explicit later calls. Repeated calls are idempotent and preserve existing valid pairs. A later explicitly requested pass can connect newly instantiated modules; there is no per-frame search or automatic late construction feature.

For each accepted pair create exactly one runtime NavMeshLink, with Start Transform and End Transform referencing the two WalkAnchors, bidirectional traversal, Walkable area, matching agent type, and width constrained by both endpoints. Validate that both surfaces use the same agent type. If configuration or endpoint attachment is invalid, diagnose that pair and leave it disconnected; do not choose a more distant partner based on geometry or orientation. Do not silently project anchors onto arbitrary nearby floors.

A single connection record owns the link and mutual references. Disable/destroy/disconnect of either endpoint immediately deactivates the link, clears both partners, and safely destroys the owned runtime object. Cleanup is idempotent. An explicit new discovery call may reconnect surviving/re-enabled points. Moving connected modules and riding moving NavMeshes are outside this slice; future snapping should disconnect, place, then rediscover.

Acceptance: two nearby rooms connect during initial startup; distant nodes and same-room nodes do not connect; every node has at most one partner; one link exists per unordered pair; repeated discovery creates no duplicates; ties yield repeatable results; disabling either endpoint removes the traversable connection and stale references. Verify a complete cross-room NavMesh path after initial link registration.

## M03 — Author all eight nodes and bake each prefab separately

Dependencies: M01, M02.

Create a narrowly scoped, rerunnable editor authoring command for the five named prefabs. Add exactly one ModuleConnectionPoint to every nodeConnect and exactly one child WalkAnchor, assigning both owner and anchor. Preserve deliberate existing authoring on reruns; use Undo where applicable and save through Unity prefab APIs. Exclude nodeDocking and shuttle prefabs.

Inspect each node against room geometry. Establish outward local +Z for connection markers only where doing so will not disturb existing consumers; otherwise preserve the marker and author the equivalent inward offset explicitly. Place each WalkAnchor 0.5 world meters toward the room interior, at the walkable floor height. Do not assume every existing node has correct facing or floor-level Y; Airlock markers currently have local Y=1. Account for parent scale and rotation. Keep snap markers at their authored positions. Document any correction needed to place an anchor on the baked surface; do not silently change the requested inset.

Move each existing NavMeshSurface to the corresponding prefab root while preserving its relevant settings, then remove the superseded surface; add one for Airlock. Set Collect Objects to Current Object Hierarchy, retain consistent agent type, and use the existing Physics Colliders geometry mode after checking floor/wall/furniture collider coverage. Exclude inappropriate decorative geometry through layers/modifiers where needed. Bake and save each prefab independently in Prefab Mode or equivalent supported editor workflow. Remove obsolete data only when no longer referenced by other assets or scene overrides.

Acceptance: 8 points and 8 assigned WalkAnchors across the five prefabs, with counts 1/2/2/1/2 respectively; anchors visibly inside at the correct height; one surface and a valid room-specific bake per prefab; baking one room cannot collect its neighbors. A translated/yaw-rotated instance carries correctly placed baked navigation and anchors. No lost activity bindings, geometry, or GUIDs.

## M04 — Static-scene integration and actual avatar traversal

Dependency: M03.

Inspect Assets/Prefabs/prefabss.unity and its prefab overrides before editing. Preserve the author's layout. Apply the new prefab setup and remove/reconcile obsolete surface overrides only where they conflict. Disable any whole-scene navigation surface in the validation setup so it cannot conceal missing module links. Do not delete unrelated navigation assets.

If the authoring scene intentionally separates rooms, create a dedicated small validation scene using real instances of the five source prefabs rather than rearranging the user's layout. Align selected node pairs within the configured radius, retain an intentionally unmatched node, and use the real colonist/NavMeshAgent movement path. The available node counts allow a connected chain; arrange it only if geometry permits, without overlapping rooms.

Press Play and verify links are created from node proximity without manual partner assignment. Request travel across at least two module boundaries in each direction; observe the avatar crossing doorways and completing movement. Inspect the existing motor's off-mesh-link traversal policy and make the smallest compatible change only if needed. Check agent area mask, agent type, door clearance, and endpoint attachment before changing movement code.

Acceptance: intended pairs exist on initial startup, unmatched nodes remain disconnected, path status is complete between connected rooms, and actual avatar traversal succeeds both ways. A disabled connection breaks the only route across its boundary. No whole-scene bake or duplicate links mask failures. Record which scene was exercised.

## M05 — Focused regression checks and authoring handoff

Dependencies: M02–M04.

Add focused algorithm tests for threshold boundaries, same-module exclusion, one-to-one claims in a three-node cluster, deterministic ties, and idempotent rediscovery. Add lifecycle/Play Mode coverage for startup registration timing, one link per pair, complete paths across independently baked surfaces, disconnect cleanup, and explicit discovery after late module instantiation. Repeated Play Mode with domain reload disabled must not retain stale state; use a manual check if the automated harness cannot exercise that editor setting.

Exercise real prefab content in M04; generated planar fixtures can test lifecycle mechanics but cannot replace real-room validation. Run affected existing navigation/movement checks and compile project/runtime/editor assemblies. Do not expand into an unrelated stress-system rewrite.

Write a short authoring guide covering: per-prefab bake workflow; node orientation; 0.5 m inset and floor height; pairing-radius and width dials; startup-only discovery; unmatched/ambiguous/invalid-node diagnostics; and the explicit discover/disconnect calls future construction should invoke.

Deliver an implementation report with changed files, final node/pair counts, scene tested, automated/manual results, and unresolved limitations. Mark each ticket DONE only with its acceptance evidence; otherwise state INCOMPLETE and the exact missing check.

## Execution prompt

Implement tickets M01–M05 in tickets/MODULE_CONNECTION_POINTS_STACK.md in order. Reinspect current prefabs and local changes first. Complete the component, one-time startup distance pairing, all eight prefab WalkAnchors, independent room bakes, static-scene traversal validation, focused checks, and authoring guide. Preserve unrelated ongoing work and report actual Unity validation evidence separately from compilation.
