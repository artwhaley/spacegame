# Colony Interactions

This package contains the reusable interactable-facility runtime and editor tools.

The runtime dependency is Unity Animation Rigging 1.4.1 on Unity 6000.5.9f1. A character needs an Animator using a controller with a `Speed` locomotion parameter, `ActionA`, and `ActionB` states whose clips are replaceable at runtime. Animation Rigging is optional per character: a character without a `ContactRigDriver` can still execute activities and simply skips optional contacts.

## Authoring model

`InteractableFacility` is the complete authoring unit. Its `Activities` array contains the activity ID, reservation group, anchors, animation segments, placement corrections, and optional per-segment contacts. Activity data is not split into a ScriptableObject. Facility prefabs can therefore be duplicated with their furniture, anchors, targets, and activity recipes together.

Sequences remain facility-owned lists of activity IDs. Callers use `RequestActivity(facility, activityId)` or `RequestSequence(facility, sequenceId)` on `ColonistActivityRunner`.

Each animation segment can select a placement anchor and an optional list of semantic contacts. A facility supplies a target Transform for a channel such as `RightHand`; the character maps that channel to its own IK constraint and proxy target. This keeps furniture placement, animation timing, and character anatomy independent.

Use the **Create Missing Anchors & Targets** and **Open Activity Placement Preview** buttons on each activity. The preview edits the embedded facility data, includes entry, loop, active, and exit segments, and preserves Undo.

Use **Colony → Interactions → Validate Facilities in Open Scene** before making a facility prefab.

This package intentionally contains no sample scene, HUD, animation clips, or character assets. Those remain game-specific content owned by the consuming project.
