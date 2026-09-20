# Banished in Space — Start Here

Planning documents, in reading order. Each is self-contained; together they are the
whole plan from today's codebase to 1.0.

| # | File | What it answers |
|---|---|---|
| 0 | `HOW_IT_WORKS.md` | Plain-English walkthrough of the minigame: what happens hour by hour, who owns which fact, and every change made to the plan and why |
| 1 | `STATE_OF_THE_PROJECT.md` | What exists, what doesn't, why progress *feels* slow, where the hotspots are |
| 2 | `GAME_DESIGN_DECISIONS.md` | Every gameplay decision that is locked, and the ones deliberately still open |
| 3 | `ARCHITECTURE_CONSTITUTION.md` | The 12 binding rules every ticket is checked against (canonical copy) |
| 4 | `ROADMAP.md` | Phases 0–3 to 1.0, epics per phase, definition of done per phase |
| 5 | `DAY_BY_DAY_PLAN.md` | **The 26-day path to a playable minigame**, one observable per day, with the dependency flaws it exposes and where each is resolved |
| 6 | `PACKET_INDEX.md` | Every agent packet (written or planned), status, directories owned, dispatch order |
| 7 | `GAPS_AND_OPEN_QUESTIONS.md` | What the roadmap still misses, plus process risks (scene contention, review bandwidth) |
| 8 | `ENVIRONMENT_ASSETS.md` | Plan for procedural low-poly asteroids, volumetric dust, scatter tooling, and the art bible |
| 10 | `DECISION_LOG.md` | Every decision from the planning session, dated, with rationale and the vetoes still owed |
| 11 | `GLOSSARY.md` | Terms used across the docs and code |
| 9 | `EXPLORATION_AND_LONG_RANGE.md` | Second-act nods: resource field vs. survey knowledge, sensors and survey missions, the volumetric scan view, long-range logistics options, remote construction, and the Phase 0 seams that must stay open |

Packets under `tickets/`:
- `P0-0_Skeleton/` — scenes, asmdefs, sockets, prefabs, clock (Days 1–2) — written
- `P0-A_Foundation/` — staffing split + corridor/shuttle routing — written
- `P0-S_Shuttle_Flight/` — ports, queueing, 6DOF voyages, ship presentation — written
- `P0-B_Interaction_UI/` — camera, selection, panels, reporting, HR, colony report — written
- `P0-P_Presentation_People/` — colonist bodies, Facilities presenter port — written
- `P0-C_Build_And_Staff/` — construction + allocator — draft, lock after Day 6
- `P0-D_Live_And_Die/` — needs, death, housing, scenario, session — draft, lock after Day 13
- `P0-E_Environment/` — asteroids, dust, scatter — written (spec in root)
- `P1-X_Exploration/` — Phase 1 stub

Existing runtime documentation (already accurate, keep maintaining):
`ARCHITECTURE.md`, `STAFFING_ARCHITECTURE.md`, `STAFFING_AUTHORING.md`, `CONTENT_AUTHORING.md`.

Agent packets live under `tickets/P0-*/`. Each packet has `00_README_FIRST.md`,
`01_LOCKED_DESIGN.md`, and one file per ticket.

## How to use this

1. Read 1–3 once.
2. Pick the next packet from `PACKET_INDEX.md`.
3. Paste the packet's README + locked design + one ticket into an agent.
4. When the packet's acceptance ticket passes, update `PACKET_INDEX.md` status and
   tick the epic in `ROADMAP.md`.
5. When a decision in `GAME_DESIGN_DECISIONS.md` or a question in
   `GAPS_AND_OPEN_QUESTIONS.md` gets resolved, move it to the locked section and, if it
   changes a design, reopen the owning packet's `01_LOCKED_DESIGN.md`.
