# Stack 3 — Bid / Offer / Accept + Physical Food Economy

## Status

Implemented in the working tree. No commit was created.

Starting revision: `d1bf6935935c092856029473268655e781e6ceca`.

The unrelated stress/telemetry, prefab, NavMesh, and module-connection changes already present in the working tree were preserved.

## Implemented

- Added round-based `FoodBid`/`FoodOffer` and `OffDutyBid`/`OffDutyOffer` contracts.
- `ColonistBrain` submits bids, consumes next-tick offers, and leaves physical reservation ownership to `ColonistActivityRunner`.
- `FoodManager` and `OffDutyManager` resolve after the Brain phase at priority 150. Offers are ephemeral and valid only for the intended next simulation tick.
- Food contention is resolved by critical hunger, hunger pressure, need age, preference rank, and stable name tie-breaks. A reservation group is offered once per round.
- Food inventory is reserved only after a real Eat activity request succeeds, consumed on `ActiveStarted`, and released exactly once on pre-active failure/cancellation.
- Cached static recreation/food binding metadata is used by candidate scans. Dynamic access, staffing, reservation, and inventory facts remain evaluated per candidate.
- Inspectors now read bids/offers and rejection state. Migrated tests use the bid/offer path; no `TryFindFood` or `TryFindOpportunity` calls remain in the test or inspector scopes.
- Added `WorkforcePerformanceProvider` and scene wiring so Farm production requires genuinely active assigned work.
- Added the `SimpleFarmFood` recipe, Station Food Store inventory, cafeteria Food accounting, and structured production/food offer logging.
- The aggregate population Food consumer is not present in `Assets/bobandfriends.unity`; individual meals are the authoritative consumption path.

## Validation

Sequential generated C# builds completed successfully:

- `dotnet build Assembly-CSharp.csproj --no-restore` — 0 errors, 15 warnings.
- `dotnet build ColonyPrototype.Tests.csproj --no-restore` — 0 errors, 2 warnings.

Focused Unity EditMode results from the isolated copy:

- `FoodManagerTests` — 7 passed.
- `OffDutyManagerTests` — 5 passed.
- `ResourceConverterTests` — 7 passed.
- `FoodServiceAccessTests` — 17 passed.
- `FoodContentionTests` — 6 passed, 1 skipped.

The skipped contention case is explicitly marked in `FoodContentionTests.cs` for the requested test corrective pass. Its current fixture injects private runner state rather than exercising a scene-backed Brain/runner lifecycle, so production code was not changed to satisfy that synthetic setup.

The full Unity EditMode suite and `MultiColonistIsolationTests` were not run in this minimum validation pass. They remain part of the later test corrective stack.

## Real-scene test handoff

Use `Assets/bobandfriends.unity` in Unity and verify:

1. A hungry colonist submits a food bid, receives an offer on the next tick, requests Eat, and reserves the physical Eat group.
2. Inventory moves from on-hand to reserved while walking, then reserved to consumed when Eat becomes genuinely active.
3. A pre-active cancellation releases the reservation without consuming Food.
4. Farm output increases only while Bob is genuinely active at the assigned Farm job; stopping that work blocks production.
5. The inspector shows submitted bids, chosen offer, and rejection reason without calling legacy query methods.
