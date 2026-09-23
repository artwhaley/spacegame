# Space scene setup

`Assets/Art/Environment/Space/SpaceEnvironment.prefab` is a visual-only HDRP space backdrop assembled from the installed Synty POLYGON Sci-Fi Space assets. It includes a six-face starfield shell and a static scatter of asteroid and pebble meshes. The sky faces use HDRP Unlit materials; the asteroid colliders are omitted. The prefab adds no gameplay scripts, movement, or collision behavior.

## Create a new space scene

1. In Unity, choose **File > New Scene** and use an HDRP scene template. Save it under `Assets/Scenes/`, for example as `Space_Prototype.unity`.
2. Add an HDRP camera. If you create a plain Camera GameObject, add **HD Additional Camera Data**. Set the camera's **Far** clipping plane to at least `500`.
3. Drag `SpaceEnvironment.prefab` into the Hierarchy and leave its root at `(0, 0, 0)` to use the authored asteroid layout. The six starfield faces enclose an 800-unit-wide volume centered on the prefab.
4. Keep the camera and playable area inside that shell. The default camera view at the origin sees the field; move or scale the `Synty Asteroid Clutter (Visual Only)` child if the base needs more clearance. If the scene spans beyond the shell, enlarge the six sky faces together and raise the camera far clip plane.
5. Add a Directional Light if the scene's structures need direct sunlight. The starfield is unlit and supplies the background; the light is separate from it.

The project already uses HDRP 17.5. A Built-in or URP scene will not render the prefab's HDRP Unlit materials correctly. The sky is geometry inside the prefab, so it does not replace `RenderSettings.skybox` or change other scenes.

## Contents

- Six `SpaceSky_01_*.mat` assets: HDRP Unlit materials using the six Synty Skybox 01 textures.
- `SpaceEnvironment.prefab`: the six-face starfield shell and 26 Synty asteroid/rubble props.

The backdrop props are static so they remain presentation-only. Arrange or remove them in the prefab to fit a particular scene's framing and clearance.
