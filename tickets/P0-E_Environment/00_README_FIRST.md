# Packet P0-E — Environment (Day 24)

> **STATUS: HORIZON / REFERENCE.** Use this packet only after the active visible slice
> demonstrates a repeated environment bottleneck. It does not authorize a generator
> or art-pipeline commitment now.

Full spec and art bible: `ENVIRONMENT_ASSETS.md` (root). This packet owns
`Assets/Editor/Environment/`, `Assets/Art/Environment/`, `Environment.unity`, and a
`Presentation/Environment/SlowTumble` component. It touches no Runtime code.

Tickets (details in the spec §"Tickets"):
- **E01** Asteroid mesh generator, palette materials, LOD prefabs, `SlowTumble`.
- **E02** Dust `Texture3D` generator, `LocalVolumetricFog` prefab, dust motes, fog
  override check on `SkyandFogSettingsProfile`.
- **E03** Scatter tool with base and dock-approach clearance; reads density from a
  field hook (constant in v1 — see `EXPLORATION_AND_LONG_RANGE.md` §8).
- **E04** Lighting/exposure pass and art-bible sign-off.

Verified facts and API references are in the spec. HDRP Local Volumetric Fog docs:
https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@latest/index.html?subfolder=/manual/Local-Volumetric-Fog-Component.html
