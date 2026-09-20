# Packet P1-X — Exploration and Scans (Phase 1) — STUB

Design nods: `EXPLORATION_AND_LONG_RANGE.md`. Do not lock before the Phase 0 playable
exists and the Phase 0 seams in that document's §8 are verified on disk.

Expected tickets when locked:
- X01 `ResourceField` (seeded grid, scenario overrides, byte-array state) + scatter
  reads it.
- X02 `SurveyKnowledge` (estimate/confidence/level per cell; sensors the only writers).
- X03 `ScannerComponent` passive scanning on ships; Sensor Mast `BuildingDefinition`.
- X04 `DiscoveryManager`: knowledge threshold → spawn/reveal `ResourceDeposit`.
- X05 Scan view: knowledge → `Texture3D` → volumetric clouds per resource; deposit pins
  with distance and round-trip time; layer toggles; hover estimates.
- X06 `SurveyMissionController` + `RequestSurvey` drag-box command.
- X07 Scenario: starting ice sized to deplete around day 5–8.
