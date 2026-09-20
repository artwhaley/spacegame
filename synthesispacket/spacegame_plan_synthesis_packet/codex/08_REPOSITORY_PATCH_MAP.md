# Repository Patch Map

This is the intended **documentation/planning** mutation map for the local Codex install task. It is not a request to modify runtime code.

## Files to add

Recommended canonical planning additions:

- `DECISION_BACKLOG.md` — from this packet's `planning/04_DECISION_BACKLOG.md`
- `PROTECT_LIST.md` — from `planning/05_PROTECT_LIST.md`
- `PLANNING_GOVERNANCE.md` — from `planning/03_PLANNING_GOVERNANCE.md`
- `planning/28_DAY_AGENT_PROMPTS.md` — from `planning/07_AGENT_28_DAY_PROMPTS.md`
- `planning/CURRENT_WINDOW.md` — initialize from `planning/06_CURRENT_WINDOW_TEMPLATE.md` with Days 1–3 from the active plan
- `planning/audits/2026-09-20/` — preserve the audit/synthesis/reference material from this packet
- optional `ARCHITECTURE_OWNERSHIP.md` — extract the code-grounded ownership/layer material currently mixed into `HOW_IT_WORKS.md`

## Files to replace or substantially rewrite

### `DAY_BY_DAY_PLAN.md`

Replace the old frozen 26-day plan with the packet's human-facing 28-day discovery plan.

Keep the root filename so existing links continue to work.

Important framing:
- Days 1–3 committed at working depth.
- Days 4–7 near horizon.
- Days 8–28 provisional daily shapes.
- Every day carries observable + learning + decisions fed + explicit non-decisions.
- The first three days are human avatars/NavMesh/interactable Farm/docking+boarding.

### `GAME_DESIGN_DECISIONS.md`

Replace `Locked` vs `Open` with clearly different registers:

1. `Verified / Owner Decisions`
2. `Working Hypotheses`
3. `Open Questions` → point to `DECISION_BACKLOG.md`

Specific treatment:
- Keep Banished-like colony-builder intent if still owner-stated.
- Keep walk-vs-shuttle as owner decision.
- Keep hybrid staffing concept as owner decision.
- Do **not** lock allocator algorithm.
- Do not lock custom 6DOF.
- Do not lock UI Toolkit.
- Do not lock construction material identity.
- Do not lock starting scenario counts/stock/beds.
- Do not lock personal needs/death thresholds.
- Interior work being visible is now owner intent; exact cutaway method remains open.

### `DECISION_LOG.md`

Preserve every reasoning trail, but add fields/columns:

- `Status`: FACT / OWNER DECISION / WORKING HYPOTHESIS / OPEN / SUPERSEDED
- `Basis`: code path, owner statement, experiment, or planner proposal
- `Revisit trigger`

Do not delete historical rows merely because they are demoted.

Move the active question into `DECISION_BACKLOG.md` and leave the old row as historical reasoning with a pointer.

### `START_HERE.md`

Make active planning authority unambiguous:

1. current code/content is truth;
2. `ARCHITECTURE_CONSTITUTION.md` for verified architectural invariants/policies, with its status labels;
3. `PROTECT_LIST.md`;
4. `DAY_BY_DAY_PLAN.md`;
5. `planning/CURRENT_WINDOW.md` — actual three-day commitment;
6. `DECISION_BACKLOG.md`;
7. old packets are reference until promoted by the active window.

### `ROADMAP.md`

- Delete the duplicated Constitution rule list; link to the canonical file.
- Phase 0 points to the active 28-day discovery plan.
- Phase 1–3 become **horizons/questions**, not committed shapes.
- No Phase-0 ticket may be justified solely by a future-phase feature.
- Preserve high-level themes, not speculative schemas.

### `GAPS_AND_OPEN_QUESTIONS.md`

- Remove `Recommendation` and calendar `Decide by` columns for unresolved questions.
- Use `Trigger / evidence needed`.
- Deduplicate with `DECISION_BACKLOG.md`; this file may become a short gap index linking there.
- Interior visibility: visible humans/interiors are owner intent; exact camera/cutaway technique remains open.
- Do not pre-answer module kit, colonist identity, immigration gating, etc.

### `HOW_IT_WORKS.md`

Do not let a predicted walkthrough masquerade as observed behavior.

Preferred patch:
- extract verified ownership table/layer direction/invariants into `ARCHITECTURE_OWNERSHIP.md`;
- retitle/reframe the remaining narrative as `Intended Experience — Working Draft`;
- for each predicted behavior, make it clear it is an intended observable/hypothesis, not a fact.

If renaming the file would break too many links, retain the path and change title/front matter, plus create the ownership doc.

### `ARCHITECTURE_CONSTITUTION.md`

Keep the filename as the canonical architecture policy location, but distinguish:
- **verified/project invariant**;
- **working architecture policy**;
- **process preference**.

Do not claim that all 12 rules are code-forced facts.

At minimum:
- keep dependency direction, read-only view intent, narrow commands, one authority, tick-based runtime, performance-provider seam, registry discipline as protected principles;
- treat reusable-by-two rule, line-count ceiling, save/load serialization policy, and mandatory test-per-ticket language as policy/revisit items rather than immutable facts where appropriate.

### `GLOSSARY.md`

Split or tag terms:
- `Exists in current code/content`
- `Proposed / working term`

Verify existence by source search; do not let planned classes read like shipped APIs.

### `PACKET_INDEX.md`

Make clear that the old Fable packets are **reference material, not an execution queue**.

Suggested statuses:
- P0-0: superseded by Days 1–4; reference.
- P0-A: Epic G staffing split remains useful for Days 5–6; walk design is reference, not exact contract.
- P0-S: one-voyage-authority intent retained; 6DOF/queue specifics provisional.
- P0-B: inactive draft/reference until management questions fire.
- P0-P: superseded/reframed by the owner's interactable-facility prototype integration.
- P0-C/D: draft/reference only; execute only after relevant triggers.
- P0-E: horizon/reference.
- P1-X: stub/horizon only.

Do not mass-delete these packets; they contain useful reasoning.

### `STATE_OF_THE_PROJECT.md`

Add a planning/status note:
- current remote planning baseline;
- blank-space audit outcome;
- near-term Days 1–3 visible-work-loop focus;
- external interactable-facility prototype is an input to Day 2 and must be inspected rather than reconstructed from planning prose.

## Packet-local status banners

Where active packet files still say `This design is fixed`, `LOCKED`, or equivalent:

- do not trust the filename alone;
- prepend a status banner saying whether the packet is:
  - protected behavior-preserving refactor,
  - working design/reference,
  - draft,
  - superseded/horizon.
- update internal headings that contradict that banner.

Avoid mass renames unless all repository references are updated safely.

## Specific contradictions/defects to fix during the doc pass

1. **Farm `0.65`** is existing shipped content; do not call it an invented constant.
2. **Beds 6 vs 8**: do not choose one merely to satisfy the old Day-21 demo. Mark starting bed count unresolved/current-content until housing experiment.
3. **`ROADMAP` Constitution copy** differs from canonical and names nonexistent `ShipPhase`; remove the duplicate.
4. **Camera y=0 vs placement never-y=0**: remove hard-coded camera-plane planning claim.
5. **Bed transform authority**: `ModuleSockets.beds.Count` must not silently own `HabitationComponent.capacity`.
6. **Personal needs API**: remove planned `Fed(colonist, resource, fraction)` from active plan; aggregate shortage first.
7. **Death thresholds**: move to backlog.
8. **Custom 6DOF**: move from lock to working hypothesis/backlog; one voyage authority remains.
9. **UI Toolkit-only**: remove as global lock.
10. **Scenario field list**: remove from active plan; derive minimum schema on scenario day.
11. **Construction material `Regolith`**: do not make it canon from the plan; first construction uses explicitly temporary material unless owner has separately ratified Regolith.
12. **Holding slot modulo**: do not preserve a rule that can overlap queued ships.
13. **Collider regression**: Day 1/4 plan must explicitly verify colliders after moving meshes under child roots.
14. **P0-C site-access open question**: restore it to backlog rather than silently using the recommendation.
15. **Open-question recommendations**: convert to triggers/evidence, not pre-filled answers.

## No-runtime-code rule for the install task

The Codex session installing this packet should change **documentation/planning only**.

Do not implement Day 1.
Do not edit `Assets/Scripts`, Unity scenes, prefabs, ScriptableObject content, or ProjectSettings as part of the planning install.

The whole purpose of the install is to leave the repository in a state where the next execution session can start Day 1 from an unambiguous active plan.
