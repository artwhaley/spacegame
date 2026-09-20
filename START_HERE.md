# Banished in Space — Start Here

This repository is planned from the current code/content, not the other way around.
Read the active documents in this order:

1. `STATE_OF_THE_PROJECT.md` — what is actually present at the current local HEAD.
2. `ARCHITECTURE_CONSTITUTION.md` — verified invariants, working architecture policy,
   and process policy, each labeled separately.
3. `PROTECT_LIST.md` — facts and owner intent that must survive experimentation.
4. `planning/CURRENT_WINDOW.md` — the actual next three-day commitment.
5. `DAY_BY_DAY_PLAN.md` — the human-facing 28-day orientation map; only Days 1–3 are
   written at working depth.
6. `DECISION_BACKLOG.md` — unresolved questions and the evidence trigger for each.
7. `PLANNING_GOVERNANCE.md` — how to classify and revisit planning statements.
8. `GAME_DESIGN_DECISIONS.md` and `DECISION_LOG.md` — owner choices and reasoning
   history, separated from hypotheses and open questions.
9. `ARCHITECTURE_OWNERSHIP.md`, `HOW_IT_WORKS.md`, and `GLOSSARY.md` — ownership,
   intended experience, and terminology.

The near-term visible loop is deliberately concrete:

- Day 1: human avatars plus navigable Command Post/Farm blockouts;
- Day 2: inspect and port the real interactable-facility prototype at
  `Packages/com.asteroidcolony.interactions` so a Farm worker visibly works;
- Day 3: physical shuttle docking with visible boarding and disembarking.

`PACKET_INDEX.md` is a reference index, not an execution queue. Old Fable packets under
`tickets/` retain useful reasoning, but their detail is not implicitly binding. A packet
becomes active only when `planning/CURRENT_WINDOW.md` promotes it.

The project already contains the reusable interaction package and imported Synty art;
the current scene and simulation authority remain the source of truth for integration.
