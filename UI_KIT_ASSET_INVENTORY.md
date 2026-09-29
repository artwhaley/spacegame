# UI Asset Inventory — "SciFi Modern Flat Themed GUI" kit

**Audience:** anyone (agent or human) doing UI specification or implementation for this project.
**Status:** reference document. The kit it describes is **present on disk but deliberately not tracked in Git.**

---

## 0. Why this document exists

The web/UI agent works against a fresh clone, and the art kit is intentionally **excluded from the
repository**. That means the agent cannot see the assets — so this file records what is available,
at what sizes, and in what visual language, in enough detail to write a UI specification without
opening the folder.

If you need to *look* at the art, it is on the machine that has the working copy, at:

```
Assets/SciFi - Modern Flat Themed GUI - UI Kit - 6 Themes + PSD, AI Sources/
```

---

## 1. Why it is excluded from Git

| Fact | Value |
|---|---|
| Total size on disk | **453 MB** |
| Files (including `.meta`) | 3,765 |
| Files excluding `.meta` | 1,846 |
| Git history size before adding it | ~428 MB packed |
| Largest single file | 58 MB (`flat_scifi_gui_blue.psd`) |
| Asset GUIDs in the kit | 1,919 — **0 referenced by any tracked file** |

Committing it would roughly **double the clone** for every agent and CI job, and the bulk of the
payload is aimed at a different engine. Excluding it costs nothing: no tracked scene, prefab,
asset, or script references any GUID from this folder (verified with `git grep` across all 1,919
kit GUIDs).

The exclusion is enforced by two rules at the end of [.gitignore](.gitignore). The folder still
exists locally and Unity still imports it; it simply never enters a commit.

> If this ever needs to ship with the project, use **Git LFS** following the existing
> `Assets/PolygonSciFiWorlds/**` pattern in [.gitattributes](.gitattributes) rather than committing
> the blobs directly. Twelve PSDs are 10–58 MB each and would warn on every push.

---

## 2. Executive summary — what is actually usable

The kit is an **Unreal Engine** asset pack (1050 `.uasset` / `.umap` files, 22 MB of Unreal
textures, Unreal Widget Blueprints). Unreal-specific content is **not directly usable** in this
Unity project. What *is* usable:

| Usable in Unity | Count | Size | Where |
|---|---|---|---|
| PNG sprites (RGBA, white stencils) | **774** | ~11 MB | `.../sources/png/` |
| TTF fonts (Aileron family) | **4** | ~156 KB | `.../sources/fonts/` |
| PSD master sheets (6 theme artboards) | **6** (duplicated ×2) | ~389 MB | `SourcesFiles/` |
| AI vector icons | 1 | 343 KB | `SourcesFiles/monochrome_icons_items.ai` |

**Only ~11 MB of the 453 MB is production-usable raster art.** The rest is Unreal content,
duplicated PSD masters, and Unity/Unreal import sidecars.

### The single most important property: the art is tintable

Every source sprite is a **white / greyscale stencil with an alpha channel**, not pre-coloured art.
Sampled results:

```
Buttons/Button.png      avg_rgb=(255,255,255)  distinct opaque colours = 1
Bars/BarBgLeft.png      avg_rgb=(255,255,255)  distinct opaque colours = 17
Icons/128/Heart.png     avg_rgb=(255,255,255)  distinct opaque colours = 42   (mostly alpha ramp)
Misc/Avatar.png         avg_rgb=(255,255,255)  distinct opaque colours = 55
Panels/Window.png       avg_rgb=(236,236,236)  distinct opaque colours = 39   (white + #D9D9D9 fills)
```

Colour comes entirely from **tinting at draw time**. This maps cleanly onto Unity UI Toolkit:
one sprite + `-unity-background-image-tint-color` (or a `Tint` on an `Image`) yields every theme,
and the "6 themes" are simply **6 palettes over identical geometry**. Do not author six copies of
any icon.

---

## 3. Directory layout

```
Assets/SciFi - Modern Flat Themed GUI - UI Kit - 6 Themes + PSD, AI Sources/
├── SourcesFiles/                      390 MB   ← PSD + AI masters
│   ├── flat_scifi_gui_{blue,green,orange,pink,purple,red}.psd   (3840×2160 artboards)
│   ├── monochrome_icons_items.ai
│   └── psd_ai/                        195 MB   ← byte-identical duplicate of the six PSDs + the .ai
└── Content/scifi_modern_GUI/           63 MB   ← Unreal content tree
    ├── sources/png/                    17 MB   ← ★ THE USABLE PNG ART
    │   ├── Icons/{32,64,128,256,512}/         138 icons at 5 resolutions
    │   ├── Panels/                             34 pieces
    │   ├── Bars/                               21 pieces
    │   ├── Misc/                               24 pieces
    │   ├── Buttons/                             5 pieces
    │   └── Fonts/                              4 Aileron .ttf  ← ★ USABLE FONTS
    ├── fonts/                         120 KB   (same 4 Aileron faces, as .uasset — Unreal)
    ├── textures/png/                   22 MB   (the same art as .uasset — Unreal; 1,559 files)
    ├── materials/                     765 KB   (Unreal materials: progress bars, strokes, round masks)
    ├── styles/                        150 KB   (Unreal enums + structs describing button styles/icons)
    ├── widgets/                        24 MB   ← Unreal Widget Blueprints (see §7)
    ├── blueprints/                     78 KB   (Unreal GUI functions + gamemode)
    └── demo_scene/Demo.umap            13 KB   (Unreal demo level)
```

Note: `SourcesFiles/` and `SourcesFiles/psd_ai/` hold the **same six PSDs** — ~195 MB of the folder
is pure duplication.

---

## 4. PNG art inventory (the usable set)

All RGBA, all white stencils, no `.meta` needed if imported into Unity from scratch.

### 4.1 Icons — 138 names × 5 sizes = 690 files (~2.8 MB)

Sizes are exact and uniform: `32×32`, `64×64`, `128×128`, `256×256`, `512×512` (138 files each).

Full name list (filename without extension; identical across all five folders):

```
Aid Air Amulet Apple ArrowDown ArrowDown02 ArrowLeft ArrowLeft02 ArrowRight
ArrowRight02 ArrowUp ArrowUp02 Arrows Ax Ax02 Backpack Basket Belt Bin Bolt Bones
Boots Bow Candy01 Candy02 Chat Checkmark Checkmark02 Chest ChestArmor Clock Coins
Cold Crosshair Crown Cup DogTag Dots Dynamite EmojiSad EmojiSerious EmojiSmile
EmojiWink EmojiWow Envelope EnvelopeAid EnvelopeClosed EnvelopeCoins EnvelopeGem
EnvelopeHeart EnvelopeSecret EnvelopeStar EnvelopeSwords Fire Flag Gem Gloves Gogles
Gun GunAmmo Hammer HandLeft HandRight HealthKit Heart Helmet Help Home Info Key
KneeArmor Leaf Location Lock Lock02 Magnet Mars Medal Menu Minus Pants PaperPlane
Pause Pill01 Pill02 Play Plus Potion01 Potion02 Potion03 Potion04 Potion05 Potion06
PotionAir PotionBolt PotionCold PotionFire PotionHeart PotionShield PotionStrength
PotionWater Quiver Radio Repeat Ring Scroll Search Settings Share Shield Shield02
ShieldAir ShieldBolt ShieldCold ShieldFire ShieldLeaf ShieldWater ShinArmor
ShoulderArmor Shovel Skull Slingshot SlingshotAmmo SoundHigh SoundLow SoundMute
Star Stats Strength Sword Swords Tiles User Venus Wand Warning Water X
```

**Relevance to this project (asteroid-colony sim):** the set is RPG-flavoured, but a useful subset
maps directly onto colony concerns — `Gem`, `Bin`/`Backpack` (stockpiles), `Bolt`, `Fire`, `Cold`,
`Water`, `Air`, `Potion*`/`HealthKit` (life support & meds), `Warning`, `Info`, `Help`, `Clock`,
`Settings`, `Search`, `Lock`, `Menu`, `Play`/`Pause`, `Plus`/`Minus`, `Checkmark`, `X`,
`Arrow*`, `Stats`, `MapGrid`, `Location`, `User`, `Chat`, `Envelope*`, `Star`/`Medal`/`Crown`
(progression & ranks), `Sword`/`Shield`/`Gun`/`Bolt` (security), `Hammer`/`Shovel`/`Gogles`
(labour & EVA). Fantasy-only items (`Candy`, `Emoji*`, `Potion01`…) are ignorable.

### 4.2 Panels & windows — 34 pieces (~115 KB)

Sizes vary (28 distinct; most none-square). Frames are built from **9-sliceable corner/edge pieces**
plus separate title bars.

```
Badge6Sided BadgeActionBar BadgeArrow BottomBar BubbleHolderLeft BubbleHolderRight
DropdownMask DropdownOpen Ellipse400px Ellipse60pxStroke3px Ellipse90px GameBarBgLeft
GameBarBgRight GameBarsBg GameBarsBgRight InputFiledHighlighted PanelOpacity30
RankingPlace Separator SeparatorVertical SlantedBar SlantedBarBigLeft SlantedBarBigRight
SlantedBarBigRight2 SlantedBarMediumRight SlantedBarSmall SlantedBarSmallRight
SlantedBarSmallRight2 Stroke3px Stroke5px TitleBar TitleBarMedium TitleBarSmall Window
```

`Window` (286×286), `TitleBar` (146×155), `TitleBarMedium`, `TitleBarSmall` are the window
chrome; `Stroke3px`/`Stroke5px` are reusable outlines; `Separator*` are list dividers.

### 4.3 Bars & progress — 21 pieces (~104 KB)

`BarBackground` / `BarFill` pairs in straight, slanted, and left/right-anchored variants — i.e. a
fill can grow from either edge, which is what you want for a resource bar that can drain.

```
BarBackLight BarBackLightSlanted BarBgBigLeft BarBgBigRight BarBgBigSlanted BarBgLeft
BarBgSlanted BarFillLeft BarFillParts BarFillPartsSlanted BarFillRight BarFillSlanted 1
BarFillSlanted BarFillSlanted2 BubbleGradient BubbleHandle BubbleInnerGlow BubbleShadow
BubbleStroke Cooldown fill_glow
```

`Cooldown` + `fill_glow` + `BubbleHandle` indicate the kit also ships a radial/cooldown meter
vocabulary. `Bubble*` is a tooltip/bubble system.

### 4.4 Buttons & controls — 5 pieces (~7 KB, 44×44 typical)

```
Button ButtonStroke Checkbox RadioBg RadioOn
```

Small but complete: neutral button plate, outline, checkbox, and radio on/off. `ButtonStroke` sits
over `Button` for focus/hover emphasis — a two-layer button is the intended construction.

### 4.5 Misc & screens — 24 pieces (~8.3 MB; one file is most of it)

```
ActionBarLineLeft ActionBarLineRight Avatar AvatarBg AvatarFrame AvatarGradient AvatarUser
Background01 BarBgRight BubbleHolderLineLeft BubbleHolderLineRight ChatStatus ChatStatusAway
ChatStatusBg ChatStatusInvisible LevelFailed LevelsStarBig LevelsStarBigOff LevelsStarSmall
LineSlanted LineSlantedLeft LineSlantedRight MapGrid MenuPoint
```

- `Background01` is **3840×2160** (4K) and is the only large file in this set.
- `Avatar*` / `AvatarUser` form a portrait frame (268×292 typical).
- `MapGrid` (340×340) is a ready-made map/tactical overlay grid — directly relevant to a
  colony/asteroid layout view.
- `LevelsStarBig`/`LevelsStarBigOff`/`LevelsStarSmall` give a filled/empty star rating pair.
- `ChatStatus*` gives presence dots (online / away / invisible).

---

## 5. Typography

**Aileron** — four faces, real `.ttf`, ~39 KB each, free to embed:

| File | Weight |
|---|---|
| `Aileron-Regular.ttf` | 400 |
| `Aileron-Bold.ttf` | 700 |
| `Aileron-Black.ttf` | 900 |
| `Aileron-Italic.ttf` | italic |

A geometric humanist sans with a squarish, technical character — a good fit for the "instrument,
don't decorate" direction in [docs/UI_PASS_1_SPECIFICATION.md](docs/UI_PASS_1_SPECIFICATION.md).
There is no monospace face in the kit; the existing spec's call for **tabular monospace numerals**
still needs a separate font.

---

## 6. Themes — six palettes over identical geometry

Six theme artboards ship as PSDs, named `flat_scifi_gui_{blue,green,orange,pink,purple,red}.psd`,
each a **3840×2160** presentation sheet.

Accent colours below are **heuristic samples** (dominant saturated colours from a downscaled,
flattened render of each sheet) — indicative, *not* authoritative palette values. Re-derive exact
values from a PSD if a theme is adopted.

| Theme | Dominant accent | Secondary / support | Notes |
|---|---|---|---|
| Blue | `#83DDFF` light cyan | `#163667` deep navy | Cool, classic sci-fi HUD |
| Green | `#B8FF49` lime | `#1B8380` teal | High-energy / "good state" |
| Orange | `#FFA800` amber | `#FF6161` coral | Warm warning-forward |
| Pink | `#7A2055` deep magenta | — | Darkest of the six |
| Purple | `#00CCFF` cyan accent | `#2A2473` violet ground | Deep violet field + cyan pop |
| Red | `#FF4949` red | `#FFDE58` yellow, `#982B2B` dark red | Alert / danger-forward |

Every theme is a dark translucent panel field with a bright saturated accent — consistent with
"windows feel like hardware" (glass panels, hairline borders). `PanelOpacity30` exists specifically
to build translucent panels, so the kit anticipates being laid over live 3D.

---

## 7. Unreal-only content — reference value, not usable assets

These are recorded because their *structure* is a free specification of the widget vocabulary, even
though the files cannot be loaded by Unity.

- **`widgets/templates/`** — 22 categories of Widget Blueprint templates:
  `achievement`, `achievements`, `actionbar`, `bubble`, `buttons`, `chat`, `checkbox`, `cooldown`,
  `counter`, `dropdown`, `input`, `inventory`, `level`, `messages`, `progressbar`, `quests`,
  `ranking`, `shop`, `slider` (19 files), `stars`, `toggle`, `user`.
- **`widgets/sample_windows/`** — 146 files: complete themed screens per theme, each with popups.
  Screens include `login`, `register`, `settings`, `inventory`, `shop`, `quests`,
  `quest_description`, `quest_summary`, `achievements`, `levels`, `level_completed`, `level_failed`,
  `ranking`, `messages`, `chat`, `game_chat`, `pause`; popups include `achievement_unlocked`,
  `chat_status`, `delete_item`, `exit`, `item_info`, `pause`.
- **`widgets/demo/`** — 26 demo widgets (`w_hud_demo`, `w_windows_demo`, `w_buttons_demo`, …).
- **`materials/`** — 34 Unreal materials: progress bars, round/radial progress, button strokes.
- **`styles/`** — Unreal enums/structs describing button styles and per-button icon styles; these
  encode the intended style taxonomy (a useful checklist of button variants).
- **`blueprints/`** — `bc_gui_functions`, `bc_scifi_modern_gui_gamemode`.
- **`demo_scene/Demo.umap`**, **`Content/scifi_modern_GUI/textures/png/**`** (1,559 files),
  **`Content/scifi_modern_GUI/fonts/*.uasset`** — Unreal-native duplicates of content already
  listed above in usable form.

**Worth mining regardless:** the sample-window list is effectively a free IA (information
architecture) checklist — the kit already decided that a game UI of this genre wants login, settings,
inventory, shop, quests, achievements, levels, ranking, messages, chat and pause as first-class
screens. Compare against Pass 1 scope (HUD, Colonist Dossier, Facility Panel, Colony Command, Crew
Office) when deciding what to adopt.

---

## 8. Notes for the UI specification

1. **Tint, never recolour.** Treat the PNGs as masks. One sprite per icon; theme via tint colour.
   This keeps a six-theme capability at ~11 MB and mirrors the kit's own design.
2. **The kit's look ≠ the project's stated look, but they are compatible.** The kit is a dark,
   rounded-corner, game-HUD language; [docs/UI_PASS_1_SPECIFICATION.md](docs/UI_PASS_1_SPECIFICATION.md)
   asks for instrument-grade, tabular, hardware-feeling panels. The white-stencil construction means
   the kit's *geometry* can be adopted without inheriting its colour or its fantasy/RPG iconography.
3. **9-slice the chrome.** `Panels/*`, `Stroke3px`, `Stroke5px`, `Separator*` and `Bars/*` are
   authored as stretchable pieces and should be imported with Unity sprite borders rather than scaled.
4. **Gaps to fill elsewhere:** no monospace/tabular numeral font; no colony-specific iconography
   (asteroid, refinery, oxygen, power, habitat, drone); no world-space/diegetic panel art.
5. **Do not add this folder to Git** to make a spec work. Spec against this document; prototype by
   copying the few sprites actually chosen into a tracked folder under `Assets/`.

---

## 9. Provenance

Vendor UI kit, file timestamps 2024-12-22; imported into the working copy 2026-09-27. No licence
file ships inside the folder — **confirm licensing before shipping any sprite or font** in a build.
