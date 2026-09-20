# Environment Assets Plan

Goal: a dirty, dusty, faceted asteroid field around the base — **Synty-adjacent** low
poly, not full Synty. Delivered as an **Editor generator tool** (deterministic by
seed, re-runnable) rather than hand-made FBX, so the look can be iterated in minutes
and every asset follows the same conventions.

Where it runs: `Assets/Editor/Environment/` (the empty `Assets/Editor` folder already
exists and compiles into the default editor assembly; it does not touch the Runtime
asmdef). Output under `Assets/Art/Environment/`. Scene target: `Environment.unity`.

## Verified project facts
- HDRP 17.5 on Unity 6000.5.9f1. `HDRP High Fidelity.asset` has `supportVolumetrics: 1`
  (check Balanced/Performant before relying on them).
- `SkyandFogSettingsProfile.asset` has a `Fog` override with `enableVolumetricFog`,
  `meanFreePath`, `albedo` fields present; the tool must ensure `enableVolumetricFog`
  is on.
- No `.mat` assets exist yet; the tool creates the first materials.
- Local Volumetric Fog API (HDRP docs): component `UnityEngine.Rendering.HighDefinition.LocalVolumetricFog`
  with `parameters : LocalVolumetricFogArtistParameters` exposing `albedo`,
  `meanFreePath`, `size`, `volumeMask (Texture3D)`, `textureScrollingSpeed`,
  `textureTiling`, `blendingMode`, `priority`, `distanceFadeStart/End`, `falloffMode`,
  `positiveFade/negativeFade`, `invertFade`, `anisotropy`. Mask is a `Texture3D`
  (32³–64³ recommended); create with `new Texture3D(w,h,d,TextureFormat.RGBA32,false)`,
  `SetPixels32`, `Apply`, `AssetDatabase.CreateAsset`.
  Source: https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@latest/index.html?subfolder=/manual/Local-Volumetric-Fog-Component.html

## Art bible (one screen)

- **Silhouette first.** Every asteroid reads at thumbnail size: lumpy, one or two big
  facets, a crater or bite.
- **Faceting.** Hard-shaded (unwelded vertices, per-face normals). Large: ~1,280 tris
  (icosphere subdiv 3), medium ~320, small ~80, debris ~20.
- **Palette** (base colours, HDRP/Lit, metallic 0, smoothness 0.10–0.20):
  basalt `#3A3A40`, regolith grey `#7A7168`, iron rust `#6E4A3A`, carbon dark `#2A2622`,
  ice (deposits) `#B9D8E6` smoothness 0.6. Accent lights (ports/beacons): amber
  `#FFB347`, green `#7CE58A`, red `#FF5C5C`, blue `#6FB6FF`.
- **Dust.** Warm grey-tan albedo `#8E8074`; visible in light shafts from the sun; ships
  leave brief wake disturbance later (not in slice).
- **Scale.** Shuttle ≈ 12 m long. Small asteroids 2–6 m, medium 8–25 m, large 40–150 m,
  home asteroid 300+ m (authored, not generated in v1).
- **Motion.** Everything tumbles slowly (0.5–3°/s); never static, never fast.

## Tool: `Tools ▸ Asteroid Colony ▸ Environment Generator` (EditorWindow)

### Tab 1 — Asteroids
Inputs: seed, counts per size class, subdivision per class, noise amplitude/frequency
per class, crater count/depth range, anisotropic stretch range, output folder.

`AsteroidMeshGenerator` (static):
1. Icosphere at the class subdivision.
2. Displace vertices radially by 3-octave value-noise fBm (hash-based, seeded) ×
   amplitude; apply anisotropic scale (e.g. 1.0 × 0.8 × 1.3).
3. Carve 0–4 craters: spherical depressions with a raised rim.
4. **Unweld** (each triangle owns its 3 vertices), `RecalculateNormals`,
   `RecalculateBounds`; optional vertex-colour AO approximation in crater floors.
5. LOD1 = same displacement sampled on a subdiv-1 icosphere (silhouette preserved).
6. Save `Mesh` assets: `Art/Environment/Asteroids/Meshes/Asteroid_{Class}_{Seed}_{i}(_LOD1).asset`.

Materials: one per palette entry in `Art/Environment/Materials/`.

Prefabs: `Art/Environment/Asteroids/Prefabs/Asteroid_{Class}_{i}.prefab` with
`LODGroup` (LOD0/LOD1, cull at 1%), `MeshRenderer` (random palette material),
`MeshCollider` (convex for small/medium, non-convex for large), layer `Environment`,
`Presentation/Environment/SlowTumble` component (seeded axis/rate).

### Tab 2 — Dust
- `DustNoiseTextureGenerator`: 32³ (option 64³) fBm `Texture3D`, RGBA32, density in
  alpha and R, `wrapMode Repeat`; saved to `Art/Environment/Dust/DustNoise{N}.asset`.
- `DustCloud_Volumetric.prefab`: `LocalVolumetricFog` with `volumeMask` = the texture,
  `size` 300–800 m, `albedo` palette dust, `meanFreePath` 150–400, `textureTiling`
  2–4, `textureScrollingSpeed` ~0.01 m/s, `falloffMode` Exponential, positive fade
  0.3 on all axes, `distanceFadeStart/End` 600/900, `anisotropy` 0.3.
- Ensure the `Fog` override on `SkyandFogSettingsProfile` has `enabled` and
  `enableVolumetricFog` true; set global `meanFreePath` high (thin ambient haze) so
  local volumes read as clouds.
- `DustMotes.prefab`: `ParticleSystem`, 300–600 tiny quads in a 200 m box around the
  camera target, lifetime 20–40 s, near-zero velocity + noise module, alpha 0.05–0.2,
  HDRP Lit particle material with soft particles, simulation space World.
- `Debris_Small.prefab`: subdiv-0 chunks, `SlowTumble`, scattered densely near large
  asteroids.

### Tab 3 — Scatter
- Inputs: target parent (`Environment.unity` → `AsteroidField`), seed, volume shape
  (shell radius min/max, or box), counts per class, **clearance**: radius around each
  `LocationAnchor` in `Base.unity`, plus a cylinder along each dock port approach axis
  (from `ModuleSockets.dockPorts`) so nothing sits in a docking corridor.
- Places prefab instances with random rotation, scale jitter ±20%, no overlap
  (sphere test), all under one root; re-running with the same seed replaces the root.
- Places 2–4 `DustCloud_Volumetric` instances biased toward denser asteroid clusters,
  one `DustMotes` at the base centre.

## Tickets (packet `P0-E_Environment`, Day 24)
- **E01** Mesh generator + materials + prefabs + `SlowTumble` (Presentation asmdef).
  Observable: 3 size classes visible in a scratch scene, faceted, tumbling, LOD switch
  visible in stats.
- **E02** Dust texture + volumetric prefab + motes + fog override check. Observable: a
  visible dust bank the shuttle flies through; sun shafts in it.
- **E03** Scatter tool + `Environment.unity` population with base clearance.
  Observable: field around the base, nothing inside dock approach cylinders.
- **E04** Lighting pass: sun intensity/colour, exposure, sky; reviewer sign-off against
  the art bible.

Forbidden in v1: runtime generation, procedural home asteroid, destructible asteroids,
ship–asteroid collision, GPU instancing tuning beyond defaults.
