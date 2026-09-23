# Space environment

`SpaceEnvironment.prefab` is the drop-in environment for HDRP scenes. It provides the HDRI sky, a restrained bloom and ACES tone mapping, plus a distant field of Synty asteroid meshes. The nebula panorama is converted by Unity's texture importer into a cubemap for HDRP's HDRI Sky volume. The old inward-facing Synty sky panels remain in the prefab as a disabled fallback; they do not cover the HDRI.

## Set up a scene

1. Open the scene and drag `SpaceEnvironment.prefab` into the Hierarchy.
2. Place its root near the center of the playable area so the asteroid field surrounds the colony.
3. On the active HDRP camera, include the **Default** layer in **Volume Layer Mask**. The prefab's global volume is on that layer.
4. Press Play. Do not assign a Built-in/URP skybox material in Lighting; HDRP gets the sky from the prefab's global volume.

`bobandfriends_modular` already has an instance of this prefab. Keep that instance and let Unity refresh it after importing the updated prefab. Adding a second copy creates a second global volume and doubles the asteroid field.
