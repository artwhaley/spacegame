# Gaps and Open Questions

"What else are we missing" — everything not covered by a written or planned packet,
plus process risks. Dependency flaws already resolved in the day plan are listed in
`DAY_BY_DAY_PLAN.md` and not repeated here.

## Gaps in the plan (things nobody owns yet)

### Design
- **Interior visibility.** Colonists walk corridors and work at stations — do we see
  inside modules (cutaway roof, x-ray on selection, windows) or only exteriors? This
  decides the module art kit, the Facilities presenter's camera needs, and how death or
  exhaustion is *noticed*. Must be decided before Day 13.
- **The module kit itself.** Asteroids and dust are planned; the actual base pieces
  (command pod, habitat, farm, processor, corridor segment, port module, construction
  scaffold) are placeholder primitives with sockets. This is the largest art item in
  the slice and has no owner. Recommendation: a second generator pass (kit-bash from
  primitives: cylinders, trusses, panel greebles, faceted) in `ENVIRONMENT_ASSETS.md`
  style, or a purchased Synty-adjacent sci-fi kit adapted to `ModuleSockets`.
- **Colonist identity.** Name pool, portrait/colour, one-line trait. Without it death is
  a number. Cheap; add to `ScenarioDefinition` + colonist panel (Day 22–23 or buffer).
- **Pilot boarding presentation.** Boarding is instant at the dock; with ports the
  pilot should visibly walk to the port and the ship should not undock until they are
  aboard. Small P0-P follow-up.
- **Why-not-working explanations for facilities.** Facility panel shows blockers, but
  "Farm produced nothing this shift because Water was empty for 6h" needs the flow
  ledger and blocked ledger joined. Day 23 report covers part; a per-facility
  "shift summary" sentence would help.
- **Difficulty / balance numbers.** None exist; the Day 19 tuning is the first pass.

### Presentation
- **Lighting and sky.** HDRP physically based sky vs. a space HDRI; sun direction and
  exposure; how the "dirty space" reads against a black sky. One pass on Day 24.
- **Audio.** Nothing until Phase 2 in the roadmap. For slickness, thruster hum, RCS
  pops, clamp thunk, ambient drone are cheap and belong in buffer days.
- **Camera feel.** Follow-ship mode, smooth focus, cinematic dock cam — not in the slice;
  note for Phase 1.
- **Selection outlines under HDRP.** Needs a custom pass or a rim material swap;
  Day 7 will discover which.

### Systems
- **Save/load readiness.** Rule 11 is being followed but never verified. Add a
  "serialize every runtime component to JSON and back" smoke test in Phase 1 week 1.
- **Performance budget.** 10 Hz sim with substepped flight for N ships and Dijkstra per
  commute is fine at slice scale; nobody has set a budget for 200 colonists / 10 ships.
- **Input rebinding, settings, main menu depth, localization** — Phase 3.
- **Off-map anchor** for immigration/trade arrivals — Phase 1, but ports/voyages should
  not assume every destination is a station (Loiter already covers this).

## Process risks

| Risk | Mitigation |
|---|---|
| **Scene merge conflicts** from parallel agents | Additive scene split Day 1; scene ownership per packet (`PACKET_INDEX.md`). |
| **Review bandwidth** — one human, up to 3 agents/day | Every ticket ends with an observable "Press Play and see"; review that first, diff second. |
| **Designs locked against unbuilt seams** | Lock P0-C after Day 6, P0-D after Day 13. |
| **Unity Test Runner never runs** | Accepted. Compile + Play observable is the gate; tests are design artifacts. |
| **Commit hygiene** — last packet landed as one squash | Commit per ticket with ticket ID. |
| **Agent scope creep** | Forbidden lists by name in every ticket; "stop and report" rule for unlisted files. |
| **Clock retune surprises** | Everything is per game-hour; the only visible change is pace. Verify Day 1. |

## Open questions (copy of `GAME_DESIGN_DECISIONS.md` § Open, with recommendations)

| Question | Recommendation | Decide by |
|---|---|---|
| Interior visibility | Cutaway roofs on selected/hovered module + always-visible corridor tubes with windows | Day 12 |
| Module kit source | Generator kit-bash first; buy later if it isn't good enough | Day 13 |
| Resource chains beyond Regolith | Hold until after first playable | Phase 1 |
| Immigration gating | Surplus food *and* free bed | Phase 1 |
| Corridor pressurization/damage | Phase 2; the "closed link = blocker" seam already supports it | Phase 2 |
| Colonist identity depth | Names + colour + one trait line in the slice | Day 22 |
