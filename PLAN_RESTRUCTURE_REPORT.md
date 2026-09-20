# Plan Restructure Report

## Install baseline

- Branch: `main`
- Local HEAD at the start of the install: `5986e66b0a9ba8d22036e8a3c3ec99952f0fb60f`
- The working tree was already dirty. Existing local changes were five project/settings
  files plus `.vsconfig`, the root Synty `.unitypackage`, `hab poster.png`, and the
  untracked `synthesispacket/` source directory. They were preserved.
- The install changed documentation/planning only. It did not edit `Assets/Scripts`,
  scenes, prefabs, `Assets/GameData`, or `ProjectSettings`.

## Files added

- `DECISION_BACKLOG.md`
- `PROTECT_LIST.md`
- `PLANNING_GOVERNANCE.md`
- `ARCHITECTURE_OWNERSHIP.md`
- `planning/28_DAY_AGENT_PROMPTS.md`
- `planning/CURRENT_WINDOW.md`
- `planning/CURRENT_WINDOW_TEMPLATE.md`
- `planning/audits/2026-09-20/` containing the synthesis report, original OpenAI
  deliverables, DeepSeek review, Claude review, original audit prompt, and repository
  patch map with source attribution preserved.
- `synthesispacket/` retained as the source packet supplied for this install.

## Files modified

- Active root planning docs: `START_HERE.md`, `DAY_BY_DAY_PLAN.md`,
  `GAME_DESIGN_DECISIONS.md`, `DECISION_LOG.md`, `ARCHITECTURE_CONSTITUTION.md`,
  `ROADMAP.md`, `GAPS_AND_OPEN_QUESTIONS.md`, `HOW_IT_WORKS.md`, `GLOSSARY.md`,
  `PACKET_INDEX.md`, and `STATE_OF_THE_PROJECT.md`.
- Packet status/readability banners and de-authorizing wording in the P0/P1-X README and
  design/reference files under `tickets/`.
- Existing user-authored runtime/settings/art changes were deliberately left unchanged.

## Deliberately unchanged

- No runtime C# code, Unity scene, prefab, ScriptableObject content, or ProjectSettings
  was changed by this planning install.
- The current `Packages/com.asteroidcolony.interactions` package was inspected, not
  rewritten. It is the local source dependency for Day 2.
- The existing imported Synty assets and manually authored scene work were preserved.

## Reclassification

### FACT

Current inventory, employment, logical-arrival, facility-performance, aggregate
consumption, habitation, ship, and interactable-facility seams are documented against
source. The Farm Operator `0.65` one-worker curve is shipped content.

### OWNER DECISION

Visible physical colonist activity matters; walk and shuttle are both first-class;
staffing stays separate from recipes; the hybrid target/priority/manual-control concept
is valid; employment and the pilot lease remain distinct.

### WORKING HYPOTHESIS

The first three-day visible loop, facility-local work-cycle boundary, one-voyage-owner
direction, and derive-then-round-trip scenario principle are provisional choices used to
make the next observable possible.

### OPEN

Personal needs and death, homelessness consequences, bed counts and starting content,
construction material/placement/site access, docking contention/queue policy, flight
model, UI standard, interior visibility, allocator behavior, and scenario schema now
point to `DECISION_BACKLOG.md` with triggers instead of silent answers.

## Contradictions corrected

- The Farm `0.65` curve is identified as existing shipped content.
- Aggregate shortage remains the first scarcity experiment; the speculative
  `Fed(colonist, resource, fraction)` path is de-authorized.
- The old 6-bed/8-bed conflict is left unresolved until a housing experiment.
- The duplicated Constitution summary and stale `ShipPhase` term were removed from the
  active roadmap; the canonical policy lives in `ARCHITECTURE_CONSTITUTION.md`.
- Visual bed transforms do not define habitation capacity.
- Custom Newtonian 6DOF, UI Toolkit-only, full scenario fields, death teardown order,
  modulo holding slots, and arbitrary line/test-count gates are no longer locks.
- Construction-site access is restored as an open question.
- The near-term plan carries the mesh-root/collider regression warning.
- Gap documents now record triggers/evidence rather than recommendations that answer
  their own questions.

## Unresolved interaction with newer local work

The local repository now contains the extracted interaction package and imported Synty
assets, but the main scene does not yet contain the Day-1/Day-2 integrated visible loop.
The next execution agent must inspect the package and current scene together, preserve
manual scene work, and record the actual seam instead of assuming the old packet API.

## Exact next action

**Execute Day 1 from `planning/CURRENT_WINDOW.md`.**
