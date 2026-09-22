# High Speed Recipe Audit

Audit target: C:\Users\artwh\OneDrive\Documents\space.
This report is read-only evidence for the stress-scene builder. No runtime code, editor code, scenes, prefabs, or imported assets were changed.

## Findings

The production scenes do contain usable Work, Eat, Sleep, and Dance content. The current HighSpeedStressLabBuilder.ConfigureFacilityBinding does not copy it: it creates one binding, sets entrySteps, activeSteps, and exitSteps to empty arrays, and does not assign loopStep. The generated stress scene therefore has valid reservations and anchors but no activity animation recipe.

Source naming is not uniform. In Bob.unity and bobandfriends.unity, work is Farm, recreation is play, and sleep is Sleep or Sleep01 through Sleep04. In Assets/Prefabs/prefabss.unity, the corresponding semantic activities are Farmwork01/Farmwork02 and Dance01/Dance02. A builder clone must preserve the destination semantic ID (Work, Eat, Sleep, or Dance) while copying the selected source binding payload.

## Recipe sources

All listed anchor references are local scene fileIDs. They identify source content only and must not be assigned into a generated scene.

| Semantic recipe | Source object and serialized component | Binding | Anchor fileIDs | Animation payload |
|---|---|---|---|---|
| Work in Bob scenes | Assets/Bob.unity, Farm, InteractableFacility component &1856994485; same object/component IDs in Assets/bobandfriends.unity | activityId Farm, reservationGroup Farm01, completion Sustained, fatigue override 10 | approach 1548527482, animation 1274294813, exit 582313898, targets 1380790931 | entry Stand To Sit, loop Typing, exit Sit To Stand |
| Production farm/work source | Assets/Prefabs/prefabss.unity, Farm, InteractableFacility component &1313710939 | Farmwork01 and Farmwork02, same reservation groups, fatigue override 10 | Farmwork01: approach 1610777130, animation 1407185405, exit 992398559, targets 1687453931; Farmwork02: approach 437146622, animation 1421964684, exit 218930374, targets 579252485 | no entry/active/exit steps; loop Digging |
| Eat | Assets/Bob.unity, Cafeteria, InteractableFacility component &1819463919; same object/component IDs in Assets/bobandfriends.unity | activityId Eat, reservationGroup Eat01, completion Sustained | approach 1467866810, animation 302785931, exit 1953179169, targets 1721634323 | entry Stand To Sit, loop Typing, exit Sit To Stand |
| Sleep in Bob | Assets/Bob.unity, CommandPod, InteractableFacility component &1535159106 | activityId Sleep, reservationGroup Bed01, fatigue override -10 | approach 873505497, animation 511732436, exit 1082399522, targets 105953624 | entry Lying Down Scooch Edit at +1, loop Asleep, exit the same clip at -1 |
| Sleep in Bob and Friends | Assets/bobandfriends.unity, CommandPod, InteractableFacility component &1535159106 | Sleep01/Sleep02/Sleep03/Sleep04, reservation groups Bed01 through Bed04, fatigue override -10 | Sleep01 uses the Bob IDs; Sleep02 uses approach 2111000004, animation 2111000006, exit 2111000008, targets 2111000010; Sleep03 uses 2111000014, 2111000016, 2111000018, 2111000020; Sleep04 uses 2111000024, 2111000026, 2111000028, 2111000030 | all four use entry Lying Down Scooch Edit at +1, loop Asleep, exit Lying Down Scooch Edit at -1 |
| Dance/recreation in Bob | Assets/Bob.unity, Recreation, InteractableFacility component &45583011; same object/component IDs in Assets/bobandfriends.unity | activityId play, reservationGroup Play01, fatigue override 7 | approach 195510227, animation 1154771409, exit 1342717786, targets 1685033116 | loop Robot Hip Hop Dance; entry and exit arrays empty |
| Dance source with explicit Dance IDs | Assets/Prefabs/prefabss.unity, Disco, InteractableFacility component &1052926052 | Dance01 and Dance02, same reservation groups, fatigue override 6 | Dance01: approach 1338746155, animation 1606132314, exit 1164973011, targets 2065987916; Dance02: approach 47382606, animation 848400227, exit 536683911, targets 390267805 | loop Robot Hip Hop Dance; entry and exit arrays empty |

bobandfriends.unity also has a second recreation binding on Recreation: activityId relax, reservationGroup Relax01, component &45583011, anchors 2111000070, 2111000072, 2111000074, and 2111000076, with entry Stand To Sit, loop Typing, and exit Sit To Stand. Its OffDutyComponent &45583012 maps play to stimulation recovery 60 and relax to relaxation recovery 60; both have one game-hour planned duration and a 12-hour cooldown.

## Asset references

These are project assets and their GUIDs are safe to resolve across scenes.

| Use | Project asset | GUID and clip identity | Serialized scene reference |
|---|---|---|---|
| Dance loop | Assets/Animations/Colonists/Mixamo_POLYGON_Guy_Naked@Robot Hip Hop Dance.fbx | GUID ca4e3bb4ee83d6f4bbd48578afae1be0; subclip Robot Hip Hop Dance, internal fileID -203655887218126122 | type 3 |
| Sleep loop | Assets/Animations/Colonists/Mixamo_POLYGON_Guy_Naked@Asleep.fbx | GUID 222ca5a48655460439da906fba76d64f; subclip Asleep, internal fileID -203655887218126122 | type 3 |
| Sleep entry/exit | Assets/Animations/Colonists/Lying Down  Scooch Edit.anim | GUID c599896b0ce28f04984ee5e9826a92c6; main clip fileID 7400000 | type 2; exit speed -1 |
| Sit entry | Assets/Animations/Colonists/Mixamo_POLYGON_Guy_Naked@Stand To Sit.fbx | GUID 8b2a0713f4a6a694588560ec3d70b1f1; imported FBX animation subclip | type 3 |
| Sit exit | Assets/Animations/Colonists/Mixamo_POLYGON_Guy_Naked@Sit To Stand.fbx | GUID 11545e9337b3b2c4ca9190b6c56df556; imported FBX animation subclip | type 3 |
| Typing loop | Assets/Animations/Colonists/Mixamo_POLYGON_Guy_Naked@Typing.fbx | GUID 2416b3822e1b0ee4ca6cfe7c488ab8f7; subclip Typing, internal fileID -203655887218126122 | type 3 |
| Farm/work loop | Assets/Animations/Colonists/Digging.fbx | GUID 403ca41feab007a47b3ccfa850495ad4; imported clip name mixamo.com, internal fileID -203655887218126122 | type 3 |

For FBX clips, the asset GUID plus imported clip name is the useful stable identity. The subclip fileID alone is not globally meaningful. The standalone .anim clip uses its main fileID.

## Controller, avatar, and runtime hookup

The production colonist is Assets/Prefabs/Colonists/Colonist_Synty_Male_01.prefab, GUID b6849b056c7b4474d93989bff971e110. It is a prefab variant over Assets/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Male_01.prefab, GUID 788b00144675db14ab99aaf2c6e9b1d8.

The base prefab Animator component fileID 95786743544921876 uses avatar GUID e274402e4c839934c817fc0664347c81, fileID 9000000, from Assets/PolygonSciFiWorlds/Models/Characters.fbx. The colonist prefab variant overrides m_Controller with Assets/Animations/Colonists/ColonistHumanoid.controller, GUID 907f662dea2dc7940bc7a75a3416b71d8, main fileID 9100000. The variant does not serialize a replacement avatar, so the inherited avatar must remain intact.

ColonistHumanoid.controller intentionally contains Locomotion, ActionA, and ActionB states plus ActionA Placeholder and ActionB Placeholder clips. ColonistAnimationDriver creates an AnimatorOverrideController at runtime and swaps those placeholders for each AnimationSegment, then drives ActionA/ActionB and their speed parameters. The stress scene must use the production colonist prefab/controller/avatar combination; copying only clips or assigning a scene object's Animator is insufficient.

## Activity binding structure

Colony.Interactions.FacilityActivityBinding serializes:
activityId
reservationGroup
externallyRequestable
completionMode
overridesFatigueRate and fatiguePerGameHour
approachAnchor, animationAnchor, exitAnchor, targets
entrySteps array
loopStep
activeSteps array
exitSteps array

Each AnimationSegment contains an asset clip, signed speed, blendDuration, placement reference, offsets, and contacts. The source recipes use the facility AnimationAnchor placement reference (0) and one-second blend durations. Sleep's reverse wake behavior is data in exitSteps (speed -1), not a second wake clip.

The semantic component that owns the binding must also be cloned or authored:

* WorkplaceComponent.roles maps a role asset to the facility and activity ID. In Bob scenes, Farm is linked by the farm role; in bobandfriends, the cafeteria ServeFood binding is linked to the food-service role.
* FoodServiceComponent points to the facility and sets eatActivityId Eat. Bob's cafeteria is public with hungerRecoveryPerGameHour 60; bobandfriends additionally requires staffed service and has selfServicePolicy 1.
* OffDutyComponent.activities maps recreation IDs to planned duration, cooldown, and stimulation/relaxation recovery. It must point at the same facility binding ID.
* ColonistAssignments.sleepTarget points at a generated bed and generated destination sleep ID; it must never retain a source scene facility reference.

## Safe builder cloning boundary

The safest implementation is an editor-only recipe extraction step followed by local authoring:

1. Load the source scene asset in an isolated editor scene context and locate the source InteractableFacility by object name plus component/fileID and activity ID. Treat the scene fileID as a diagnostic selector, not as a value to serialize into the generated scene.
2. Copy scalar binding fields and every segment's scalar fields. Resolve each clip from its project asset path/GUID with AssetDatabase, retaining the AnimationClip asset reference. Do not retain Transform, InteractableFacility, WorkplaceComponent, or other scene-object references from the source scene.
3. Create the generated facility and its four local anchor transforms first. Assign those local transforms to approachAnchor, animationAnchor, exitAnchor, and targets; preserve source local positions and rotations only when appropriate for the generated layout.
4. Write the cloned binding through SerializedObject so every AnimationSegment field, including negative sleep-exit speed and contacts, is copied. Then set the generated semantic activity ID and reservation group explicitly.
5. Add the matching semantic component and remap its facility/activity fields to generated objects. For multiple beds or seats, clone the recipe once per facility and give each generated binding a unique reservation group.
6. Validate that every required binding has a non-null loop or step clip, local anchors, and the expected semantic component before saving. Fail generation when a required clip is missing; do not silently create an empty recipe.

Asset GUIDs and clip references can cross scenes. Scene transforms and component fileIDs cannot. This distinction prevents broken cross-scene references.

## Blockers and risks

* The current builder is confirmed animation-empty by construction. This is the direct reason the previous high-speed run did not exercise animation.
* There is no literal activityId Work in the inspected production scenes. The builder must choose whether Work is backed by the Bob Farm recipe or Farmwork01/Farmwork02; the role binding must use the same renamed ID.
* There is no literal activityId Dance in Bob or Bob and Friends. Use the play recipe or explicit Dance01/Dance02 recipe as the source and rename it for the stress scenario.
* Assets/Prefabs/prefabss.unity is not a safe universal source for full lifecycle recipes: its bed bindings have no entry/exit steps, its cafeteria bindings have no entry/exit steps, and Command02 has a null loop clip. Use the full Bob/Bob and Friends bindings for Eat and Sleep transition tests.
* ColonistHumanoid.controller uses placeholder Action clips by design. A generated stress colonist that does not retain the production controller and inherited avatar will not exercise the same runtime override path.
* The dance FBX importer records retargeting warnings about Ball_L and Ball_R rotation curves. This is an existing import warning, not a missing recipe, but it should be recorded in high-speed animation test results.

## Audit scope and preserved state

Inspected Assets/Bob.unity, Assets/bobandfriends.unity, Assets/Prefabs/prefabss.unity, Assets/Prefabs/Colonists/Colonist_Synty_Male_01.prefab, the inherited production colonist prefab, Assets/Animations/Colonists/ColonistHumanoid.controller, the referenced animation assets and metas, and the interaction binding/runtime definitions. Existing working-tree modifications and untracked generated stress assets were preserved.

