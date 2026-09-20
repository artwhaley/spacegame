# Exploration, Scans, and Long-Range Logistics — broad strokes

Not a locked design. These are the nods the Phase 0 work must make so the second act
of the game is possible without a rewrite. Everything here is Phase 1–2.

## The arc this serves

The starting ice asteroid is **finite** (`ResourceDeposit` already is). Around day 5–8
the colony notices Water trending toward zero. The mid-game question is:

> "We're running out of water. Where is more, how far is it, and how do we move that
> much of it that far?"

Answering it needs four things: a world with resources *in* it (not just next to the
base), a way to *learn* where they are, a way to *build* far away, and a way to *move*
volume over distance. Each maps onto an existing primitive.

---

## 1. The world is a volume with resources in it

**`ResourceField`** — the ground truth. A bounded region around the base (say a 20 km
cube) divided into coarse cells (~300 m; 64³ = 262k cells, or 32³ for a first pass).
Each cell holds a true density per resource (Ice, Regolith, later Ore). Generated
from a seed at scenario start (fBm + a few authored "veins" and the starting asteroid);
authored overrides in `ScenarioDefinition`. This is *content-like* state: it changes only
when deposits are depleted.

The environment generator (`ENVIRONMENT_ASSETS.md` scatter tab) should **place
asteroids from this field** — dense cells get more rocks, ice-rich cells get the icy
palette — so what the eye sees and what the scanner says agree.

**Deposits are the field made concrete.** When a cell is known well enough and dense
enough, a `ResourceDeposit` anchor is spawned there (or an authored one is *revealed*).
Extraction, voyages (Loiter), and the UI already work on deposit anchors; nothing
downstream changes.

---

## 2. Knowledge is a separate fact from truth

**`SurveyKnowledge`** — what the colony *believes*. Same cell grid; per cell, per
resource: `estimate` and `confidence 0..1`, plus a knowledge level
`Unknown → Coarse → Fine → Confirmed`. Only sensors write it. **UI and presentation
read knowledge only, never the field.** That's rule 4 (one owner per fact) applied to
fog-of-war: the truth and the belief have different owners, so the scan view cannot
cheat and the sim cannot lie.

Confidence decays slowly for unvisited cells? No — space doesn't move in this game.
Knowledge is permanent once gained. Depletion of a *confirmed* deposit updates the
estimate through normal extraction.

---

## 3. Sensors and surveys — how knowledge is gained

Three sources, cheapest first:

1. **Passive scanning.** Any ship with a `ScannerComponent` (range, resolution,
   cells/hour) reveals cells it flies through or near. Every freight run and every
   mining trip is a little bit of exploration. Zero new player actions.
2. **Sensor Mast module.** A building (`BuildingDefinition`) with a `ScannerComponent`:
   omni, long range, coarse resolution. Its rate is a facility performance channel, so
   staffing/power can gate it later. Gives the player the first "there's *something*
   over there" map.
3. **Survey missions.** A `SurveyMissionController` (sibling to
   `ExtractionMissionController`): fly a ship to a target volume, **Loiter** (the voyage
   kind already exists), scan at fine resolution, come home. Player command from the
   scan view: drag a box → `RequestSurvey(volume, priority) → SurveyRequestResult`.
   Uses a pilot, a ship, a shift — it *costs* something, which is the point.

Later: **uncrewed probes**. The intended seam: a `ShipComponent` with
`operatingRole == null` is *automated* — exactly the way a facility with no required
role is automated. Don't implement now; don't add code that assumes every ship has a
pilot beyond what `IsOperationallyCrewed` already does.

---

## 4. The scan view — seeing the volume

A camera mode, not a screen. Pull back; the base and ships stay; the surrounding
volume renders **knowledge-masked density**:

- Per resource, a colour-coded **volumetric cloud** — literally a `Texture3D` written
  from `SurveyKnowledge` every few seconds and rendered through the same HDRP Local
  Volumetric Fog path the dust uses (or a small raymarch shader later). Unknown cells
  are a dim haze; coarse cells are soft blobs; fine cells are crisp; confirmed deposits
  get an isosurface shell and a pin.
- Layer toggles per resource; a density threshold slider; hover shows
  `~1,200 Ice ± 40%` from the estimate/confidence; deposit pins show remaining,
  **distance from base, and round-trip time at the current fleet's cruise speed** —
  that last number is the whole strategic conversation in one label.
- Drag-box → survey request. Click a deposit → the Deposit panel; "Send mining ship
  here" is a stock-policy/extraction-range change, not a manual order.

Presentation reads `SurveyKnowledge`. UI issues `RequestSurvey`. Neither touches the
field. The `Texture3D` generator from the dust plan is reused as-is.

---

## 5. Long-range logistics — "how do we move that much water that far?"

Throughput of one mining ship ≈ hold ÷ (2 × distance ÷ speed + load + unload). Double
the distance and you roughly halve the water. Every answer below is a **player choice
built from existing pieces**; none needs a new simulation concept.

| Option | What it is in the architecture | Trade-off the player feels |
|---|---|---|
| **More ships / bigger ship** | Ship `BuildingDefinition`s with different `ShipFlightProfile` + hold size | Pilots are scarce; ports queue |
| **Outpost buffer** | A remote *site*: a couple of modules with a `LocationAnchor`, docking ports, an `InventoryComponent`, and stock policies — the "warehouse-like background buffer" from `CONTENT_AUTHORING.md` §7. Mining ship does short hops deposit→outpost; a heavy freighter does the long haul outpost→base on a background policy | Must be built out there (see §6); pilots sleep where? |
| **Process at the source** | Build the Water Processor at the ice, ship Water not Ice; or later a densifier | Remote staffing/automation |
| **Conduit** | Phase 2: a `ResourceConduitComponent` linking two inventories with a flow rate and a length-scaled build cost — the *resource* analogue of a corridor: build once, flow forever, vs. ship forever | Huge Regolith cost, fixed geometry, vulnerable |
| **Crew endurance** | A voyage longer than a shift hits the existing rule: shift ends → finish the accepted operation → return. Long hauls need a 2-pilot roster (crew staffing cap > 1) or ship bunks (`HabitationComponent` on a ship) | Doubles pilot demand or costs a bigger ship |

The freight matcher must stay generic for the conduit: `FreightSupply`/`FreightDemand`
records identify inventories and resources, not vehicles. A conduit becomes a
"supply path that needs no vehicle" — keep `LogisticsManager` from assuming a
`TransportVehicleComponent` is the only way to satisfy demand.

---

## 6. Long-range construction — building where nobody can walk home

A site far from the base has no corridor, so the resolver makes it **shuttle-served**
automatically — builders fly out. Then the shift ends four hours from home and nothing
gets built. Options, in the order players will meet them:

1. **Bootstrap with a habitat first.** The first module at a new site must be a
   Habitat; colonists can be *rehomed* there (`ColonistAgent.home` is just an anchor).
   An "expedition" is a temporary rehoming; the allocator/HR screen show it.
2. **Mobile habitat.** A ship with `HabitationComponent` and bunks parked at the site
   is a home. Same rule as pilots: a home is wherever your bed is.
3. **Prefab towing.** Build a module *at the base* as a construction job that produces a
   discrete `PackedModule` resource; a heavy ship delivers it; a small crew unpacks it
   (a short construction site with tiny labor). Moves labor from the frontier to the
   base, where beds already are.
4. **Multiple site planes.** Placement (`PlacementRules`) works against a `SitePlane`
   (origin + normal), not a hard-coded y = 0. A new site is a new plane. Corridors
   never cross between planes; ships (and later conduits) do.

---

## 7. Who owns the new facts

| Fact | Owner |
|---|---|
| True resource distribution | `ResourceField` (seeded/generated; scenario overrides) |
| What the colony believes | `SurveyKnowledge` (written only by scanners) |
| A sensor's capability | `ScannerComponent` (ship or module) |
| An obligation to go look | `SurveyMission` (controller + record, like extraction) |
| A place you can mine | `ResourceDeposit` anchor, spawned/revealed by a `DiscoveryManager` when knowledge crosses a threshold |
| A remote base | a `SitePlane` + its modules; nothing else is site-aware |
| Flow without a vehicle | `ResourceConduitComponent` (Phase 2) |

---

## 8. Seams Phase 0 must keep open (actionable now)

These are small edits to already-planned days; make them and the second act costs no
rewrite.

- **Day 14 (extraction):** choose deposits from the **deposit registry filtered by
  `maxRangeMeters`** rather than an authored `deposits[]` list. New deposits then work
  the moment they are discovered.
- **Day 15 (placement):** implement `PlacementRules` against a `SitePlane` component
  (origin + normal), with the starting base as plane #1. Never hard-code y = 0.
- **Day 16 (sites):** the EVA link uses the nearest module *on the same plane*; a site
  with no module in range is simply shuttle-served (already the rule).
- **P0-S voyages:** `Loiter` destinations must accept any world point, not just a
  deposit (survey targets). Already true; keep it.
- **Logistics:** keep `FreightSupply` records vehicle-agnostic (they are). Do not add a
  vehicle assumption to the demand/supply match.
- **Ships:** `operatingRole == null` is reserved to mean *automated*. Don't add code
  paths that make a null role an error.
- **Environment (E03):** scatter reads density from a field, even if v1's field is a
  trivial constant. The hook is the point.
- **Rule 11:** `SurveyKnowledge` and `ResourceField` are state → byte arrays in
  serialized fields, save-ready.
- **Scenario:** starting deposit sizes are authored so local ice depletes around day
  5–8 at the starting population. That timing *is* the mid-game.

---

## 9. Suggested staging

- **Phase 1 (growth loop):** `ResourceField` + `SurveyKnowledge` + passive ship
  scanning + Sensor Mast + scan view v1 (clouds, pins, distance/round-trip labels) +
  deposit discovery + extraction by range. The player sees the water problem coming
  and finds the next asteroid.
- **Phase 1.5:** survey missions, outposts as buffers with background hauling, ship
  variants, two-pilot rosters.
- **Phase 2:** conduits, uncrewed probes, mobile habitats / expeditions, prefab
  towing, multiple site planes, remote processing.
