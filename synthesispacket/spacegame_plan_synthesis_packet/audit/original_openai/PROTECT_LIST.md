# Protect List — Structure the De-Specification Must Not Destroy

The audit is not an argument for vague code or endless prototyping. These are the real load-bearing facts and near-term seams worth preserving.

## A — Existing code facts

### 1. Inventory is the resource-quantity authority

**Evidence:** `Assets/Scripts/ColonyPrototype/Economy/InventoryComponent.cs` owns mutation through `Add` (line 142), `Remove` (172), `Reserve` (192), `WithdrawReserved` (225). Other systems use that surface.

**Protect:** no second resource-quantity cache/authority.

**Do not overextend:** this does not decide report schemas, construction material, stock thresholds, or scenario starting stock.

### 2. Converter logic is decoupled from staffing counts

**Evidence:** `ResourceConverterComponent` explicitly states that facility performance is its only operational input (line 107) and reads `performance.GetMultiplier(...)` (line 119). `StaffingComponent` publishes into `IFacilityPerformanceProvider` instead of production counting workers.

**Protect:** recipes/converters should not learn worker identity/count logic.

**Do not overextend:** this does not decide future need penalties, construction curves, power, morale, or whether every cross-cutting system must use an identical provider design.

### 3. Employment is explicit and singular

**Evidence:** `ColonistAgent.currentEmployment` (line 37); `StaffingManager.Assign` (line 207) validates and writes it.

**Protect:** new staffing automation/UI remains a client of the same explicit employment mutation path unless the human intentionally redesigns it later.

**Do not overextend:** this does not choose targets, priorities, pinning, or an allocator.

### 4. Responsible pilot is a temporary lease distinct from employment

**Evidence:** `ShipComponent.responsiblePilot` (line 46) and `ResponsiblePilot` (line 56), with employment owned elsewhere.

**Protect:** do not collapse job assignment and “who currently controls the ship” into one fact.

**Do not overextend:** this does not decide ship staffing count, long-haul crew, boarding animation, or uncrewed ship semantics.

### 5. Arrival means arrived

**Evidence:** `ColonistAgent` documents `currentLocation` as last-arrived state (line 18); `CompleteTransit` commits it at arrival (line 167+).

**Protect:** transit/presentation cannot move logical location early just to make visuals convenient.

**Do not overextend:** this does not decide Dijkstra, traversal-hour fields, corridor geometry, or walking animation.

### 6. Publish-then-commit freight is real

**Evidence:** `LogisticsManager` explicitly says a freight candidate becomes a contract only after it wins arbitration (line 225); `MaterializeAndAssign` creates the demand freight contract at line 409; `ContractManager` says reservation happens at materialization (line 167) and calls `Reserve` at line 199.

**Protect:** cheap published need/supply is distinct from committed/reserved transport work.

**Do not overextend:** this does not choose construction priorities, alert rules, future conduits, or queue metrics.

### 7. Extraction is intentionally outside ordinary freight arbitration

**Evidence:** `ExtractionMissionController` describes itself as orchestrating extraction “without ordinary logistics contracts” (line 14).

**Protect:** don't casually force extraction into ordinary freight merely for architectural uniformity.

**Do not overextend:** this does not decide future deposit selection, max range, exploration, or mission UI.

### 8. The simulation already has ordered tick ownership

Current code uses the simulation tick system and distinct bands for staffing, production, logistics and transport. Preserve deterministic simulation ownership unless an observed problem justifies changing it.

---

## A/B — Earned near-term engineering work

### 9. Split `StaffingManager` by existing responsibility, without behavior change

**Evidence:** the file is ~1,064 lines and visibly combines employment, commute batching, pilot reconciliation, colonist reconciliation, schedule formatting, discovery/lifecycle, diagnostics and tick orchestration.

**Protect:** P0-A T00–T04's intent: characterize first, extract existing responsibilities, preserve facade/public behavior.

**Revisit trigger:** if an extraction requires new semantics or creates cross-boundary duplication, stop; the refactor is not license to redesign staffing.

**Not protected:** arbitrary 250/300/400 line limits as laws.

### 10. Additive scene ownership is a useful process seam when parallel agents are editing scenes

The current tentative plan correctly notices that many agents editing one Unity scene creates merge risk.

**Protect:** split scene ownership if parallel scene work is actually underway.

**Revisit trigger:** after the first week of parallel work, confirm the split reduces conflicts rather than creating cross-scene authoring pain.

**Not protected:** future gameplay architecture merely because it matches the scene names.

### 11. Presentation must not become a second simulation authority

The existing `InventoryRackView` pattern and current ownership design support read-only presentation. This is a strong architecture guardrail.

**Protect:** presentation may interpolate/read; it should not silently write authoritative inventory, employment, transit, duty, contract, or ship state.

**Revisit trigger:** none unless the owner intentionally redesigns simulation/presentation boundaries.

### 12. Narrow validated commands are a good mutation boundary

`Assign() → AssignmentResult` is already a useful pattern.

**Protect:** UI/presentation should call explicit mutation APIs rather than reach into serialized fields.

**Not protected:** creating commands before a player intention actually exists.

### 13. One movement/voyage owner is a plausible earned refactor — after characterization

Current movement responsibilities are duplicated across transport, extraction and crew return. Centralizing ownership can be justified without choosing the final flight model.

**Protect:** eliminate multiple competing writers when the refactor is performed.

**Revisit trigger:** the first centralized voyage using **existing movement semantics**. Only then decide whether more phases/physics are needed.

### 14. “Press Play and see…” is a discovery mechanism, not merely acceptance wording

**Protect:** every committed day ends with a human-observable result.

**Strengthen:** add `Expected learning` and `Decisions fed` to every day.

**Do not overextend:** an observable is not evidence that all later design built on it is already known.

### 15. P1-X's refusal to lock before Phase 0 is the correct future-work pattern

`tickets/P1-X_Exploration/00_STUB.md` explicitly says not to lock before the Phase-0 playable exists and its seams are verified.

**Protect:** apply that same rule to construction placement, survival, housing, reports, environment, and scenario shape now.

---

# Things that look structural but are **not** on the protect list

- custom 6DOF / no PhysX;
- exact clock rate;
- exact route algorithm and traversal times;
- priority/FIFO berth queue;
- UI Toolkit;
- predeclared reporting ledgers;
- full panel/HR/report layouts;
- Regolith / Builder curves;
- SitePlane / 15° snapping / straight corridors;
- hybrid target+allocator staffing;
- needs/death/housing constants;
- full `ScenarioDefinition` schema;
- exact starting colony;
- environment palette/generator values;
- Phase-0 future-proofing for speculative Phase-1 exploration.

Those may later become good decisions. They are simply not earned decisions **today**.
