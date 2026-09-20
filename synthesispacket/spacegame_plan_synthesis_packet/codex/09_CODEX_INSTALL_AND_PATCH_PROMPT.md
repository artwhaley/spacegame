# Prompt for Local Codex — Install the Blank-Space Synthesis and Reframe the Spacegame Plan

You are operating locally inside the `artwhaley/spacegame` repository.

Your job in this session is **documentation/planning surgery only**. Do not implement gameplay. Do not edit runtime C# code, Unity scenes, prefabs, ScriptableObject assets, or project settings.

A synthesis packet has been provided. Its directory will be referred to below as:

`<PACKET_DIR>`

It contains at least:

- `01_SYNTHESIS_REPORT.md`
- `planning/02_HUMAN_28_DAY_PLAN.md`
- `planning/03_PLANNING_GOVERNANCE.md`
- `planning/04_DECISION_BACKLOG.md`
- `planning/05_PROTECT_LIST.md`
- `planning/06_CURRENT_WINDOW_TEMPLATE.md`
- `planning/07_AGENT_28_DAY_PROMPTS.md`
- `codex/08_REPOSITORY_PATCH_MAP.md`
- prior OpenAI audit files under `audit/original_openai/`
- DeepSeek and Claude source audits under `reference/`

If `<PACKET_DIR>` is not obvious, stop and report the missing packet path rather than guessing its contents.

## Mission

Install the packet's planning artifacts into this repository and patch every relevant active planning document so the repo has **one coherent planning authority** going forward.

The new shape is:

1. **Next 3 days at working depth, 28-day roadmap at human-readable orientation depth.**
2. **Day 1:** human avatars + navigable facility blockouts.
3. **Day 2:** port the real parallel interactable-facility prototype so a Farm worker visibly performs a local work cycle.
4. **Day 3:** physical shuttle berths + visible boarding/disembarking.
5. After that, stabilize/refactor the seams the visible loop actually exposed.
6. Facts, owner decisions, working hypotheses, and open questions must be visibly different.
7. Old Fable packets remain useful historical/reference material, but speculative packet details are no longer implicitly binding.

## First: protect the working tree

Run and record:

```bash
git status --short
git branch --show-current
git rev-parse HEAD
git log -5 --oneline
```

The audited remote baseline was:

`75e5784440f6f800c2e16241a4b59d5e500ba613`

Do **not** assume the local repo is still exactly there. The local working tree is authoritative for this install.

Rules:

- Never reset, checkout over, delete, or discard unrelated local changes.
- If planning files have local modifications, merge/reconcile them; do not overwrite blindly.
- Do not create a branch or commit unless the user has already asked the local session to do so. Leave a reviewable diff by default.

## Read before editing

Read:

1. `<PACKET_DIR>/01_SYNTHESIS_REPORT.md`
2. `<PACKET_DIR>/planning/02_HUMAN_28_DAY_PLAN.md`
3. `<PACKET_DIR>/planning/03_PLANNING_GOVERNANCE.md`
4. `<PACKET_DIR>/planning/04_DECISION_BACKLOG.md`
5. `<PACKET_DIR>/planning/05_PROTECT_LIST.md`
6. `<PACKET_DIR>/codex/08_REPOSITORY_PATCH_MAP.md`

Then read the repo's current:

- `START_HERE.md`
- `DAY_BY_DAY_PLAN.md`
- `GAME_DESIGN_DECISIONS.md`
- `DECISION_LOG.md`
- `ARCHITECTURE_CONSTITUTION.md`
- `ROADMAP.md`
- `GAPS_AND_OPEN_QUESTIONS.md`
- `HOW_IT_WORKS.md`
- `GLOSSARY.md`
- `PACKET_INDEX.md`
- `STATE_OF_THE_PROJECT.md`
- all current `tickets/P0-*` and `tickets/P1-X*` README/design files referenced by `PACKET_INDEX.md`

Also inspect enough source to verify any statement you are about to label FACT. Do not re-audit the whole codebase; targeted verification is enough.

## Install the new planning artifacts

Use repository-native names/paths:

- replace root `DAY_BY_DAY_PLAN.md` with the **human-facing 28-day discovery plan**, adapted only where the local repo has materially changed;
- create/update root `DECISION_BACKLOG.md`;
- create/update root `PROTECT_LIST.md`;
- create/update root `PLANNING_GOVERNANCE.md`;
- create `planning/28_DAY_AGENT_PROMPTS.md`;
- create `planning/CURRENT_WINDOW.md` initialized with Days 1–3 at working depth;
- create `planning/audits/2026-09-20/` and copy in:
  - synthesis report,
  - prior OpenAI audit deliverables,
  - DeepSeek review,
  - Claude review,
  - original audit prompt,
  - repository patch map.

Preserve source-review attribution in filenames; do not rewrite the source audits.

## Patch the active planning/docs

Follow `<PACKET_DIR>/codex/08_REPOSITORY_PATCH_MAP.md` as the mutation checklist.

Key outcomes required:

### `START_HERE.md`
Make it obvious that:
- code/content at HEAD is truth;
- `planning/CURRENT_WINDOW.md` is the actual near-term commitment;
- `DAY_BY_DAY_PLAN.md` is the 28-day orientation map;
- `DECISION_BACKLOG.md` holds unresolved design choices;
- old packets are reference unless the active window explicitly promotes one.

### `GAME_DESIGN_DECISIONS.md`
Eliminate blanket "Locked decisions are binding" framing.

Use separate sections/registers:
- verified / owner decisions;
- working hypotheses;
- open questions (link to backlog).

Owner decisions to preserve include:
- visible physical colonist activity is important;
- walk vs shuttle are both first-class;
- facility-level staffing remains separate from recipes;
- hybrid target/priority/manual-control concept is valid.

Do **not** lock:
- allocator algorithm,
- custom 6DOF,
- UI Toolkit,
- personal needs,
- death thresholds,
- homelessness penalty,
- construction material identity,
- placement model,
- starting crew/stock/bed counts,
- report/alert shape,
- scenario schema,
- ice-depletion pacing.

### `DECISION_LOG.md`
Keep the reasoning trail, but add status/basis/revisit-trigger semantics. Do not erase history.

### `ROADMAP.md`
Remove the duplicated Constitution text and link to the canonical file. Convert Phase 1–3 detail into horizon/themes/questions where the current text reads like committed implementation.

No current-phase abstraction may be justified *only* by a future-phase idea.

### `ARCHITECTURE_CONSTITUTION.md`
Do not gut the useful architecture. Distinguish verified/project invariants from architecture/process policies.

Do not claim arbitrary line counts, "reusable by at least two facility types," future-save serialization strategy, or per-ticket test count are mechanically forced facts.

### `GAPS_AND_OPEN_QUESTIONS.md`
Remove recommendation-as-answer behavior. Use trigger/evidence-needed language and point to the backlog.

### `HOW_IT_WORKS.md`
Do not leave a minute-by-minute predicted game loop framed as observed truth.

Prefer:
- an `ARCHITECTURE_OWNERSHIP.md` containing code-grounded ownership/layer material;
- a clearly labeled intended-experience/working-draft narrative for predictions.

If renaming would cause unnecessary link churn, keep `HOW_IT_WORKS.md` but change its title/front matter and add clear FACT vs INTENDED/HYPOTHESIS labels.

### `GLOSSARY.md`
Distinguish terms/types that exist today from proposed terms.

### `PACKET_INDEX.md` and packet READMEs/design docs
De-authorize speculative packets without deleting useful reasoning.

Do not mass-rename files unless you can update every reference safely.

At minimum, status-banner each active packet:
- P0-0: superseded/reference for old skeleton ordering;
- P0-A: staffing split useful; old walk specifics are reference;
- P0-S: voyage-authority idea useful; 6DOF/queue specifics provisional;
- P0-B: inactive draft/reference;
- P0-P: superseded by the owner's interactable-facility prototype integration direction;
- P0-C/P0-D: draft/reference, trigger-gated;
- P0-E: horizon/reference;
- P1-X: horizon/stub.

If a file is literally named `01_LOCKED_DESIGN.md`, it may keep the historical filename, but its heading/banner must not falsely claim current authority.

## Incorporate the audit corrections

These are mandatory:

1. The Farm `0.65` one-worker curve is shipped content. Do not call it an invented constant.
2. The homelessness/restfulness seam exists; `0.5` is an unearned behavior change.
3. Do not resolve the old 6-bed/8-bed contradiction by choosing a number to make an old demo work. Mark the canonical starting count unresolved/current-content until the housing experiment.
4. Remove the duplicated/divergent Constitution summary in `ROADMAP.md`, including the stale/nonexistent `ShipPhase` term.
5. Do not make visual bed transforms the authority for habitation capacity.
6. Remove/deactivate the proposed per-colonist `Fed(colonist, resource, fraction)` path from active planning. First experiment uses existing aggregate shortage.
7. Do not lock the eight-step death teardown order; discover it with a manual death test.
8. One voyage authority is retained; custom Newtonian 6DOF is not locked.
9. UI Toolkit-only is not a global rule.
10. Scenario schema is derived on the day the current slice is round-tripped; do not preserve the speculative full field list as a contract.
11. Restore genuinely open construction-site access questions to the backlog.
12. Do not preserve holding-slot modulo behavior that can place multiple ships at one pose.
13. Add the mesh-root/collider regression warning to the near-term plan.
14. Remove "recommendations" that silently answer open questions.
15. Do not replace one arbitrary class line-count gate with another.

## Incorporate the owner's new near-term direction

The docs must explicitly say:

- Human avatars replace capsule visuals immediately.
- Command Post/Farm become navigable/NavMesh-ready blockouts immediately.
- Day 2 ports the **real existing interactable-facility prototype**; agents must inspect the source rather than reinvent it from prose.
- The initial work-cycle layer is a presentation/local-activity client of active staffing state. It does not become a second production/economy authority.
- Day 3 makes docking physical and synchronizes boarding/disembarking.
- Exact cutaway/interior visibility technique remains open, but visible people working inside facilities is no longer an open question.
- The old infrastructure-first Day 1 ordering is superseded.

If the local repository does not contain the parallel prototype source, the planning docs should refer to it as an external source dependency and require the Day-2 execution agent to be given/find its local path. Do not invent its API.

## Build `planning/CURRENT_WINDOW.md`

Populate it with Days 1–3 from the new plan, including:

- goal;
- allowed surface;
- work;
- Press Play observable;
- expected learning;
- decisions fed;
- explicit non-decisions.

This file is the execution authority for the next session.

## Preserve the good spine

Do not accidentally de-specify these away:

- inventory authority;
- converter/staffing separation through facility performance;
- explicit employment;
- pilot lease distinct from employment;
- publish-then-commit freight/reservation;
- extraction outside ordinary freight arbitration;
- logical arrived location distinct from transit;
- narrow validated commands;
- one authority per fact;
- runtime/presentation dependency direction;
- daily Press Play observable;
- behavior-preserving StaffingManager split;
- eventual one-voyage authority;
- walk / ship / visibly blocked strategic rule.

## Validation

After editing:

1. Run `git diff --check`.
2. Run a repository search in **active planning files** (exclude `planning/audits/` and historical source reviews) for these phrases/ideas and inspect every hit:
   - `Locked on the spot`
   - `Locked decisions are binding`
   - `This design is fixed`
   - `UI Toolkit only`
   - `hydration == 0`
   - `nutrition == 0`
   - `15°`
   - `48 hours of Food`
   - `3 Pilots`
   - `2 Farm Technicians`
   - `3 Builders`
   - `day 5–8`
   - `Fed(colonist`
   - `planeId`
   - `maxSubstepsPerTick`
   - `WorkforceAllocator`
   - `ScenarioDefinition`
3. Hits are allowed only when:
   - describing history/reference;
   - explicitly labeled a working hypothesis;
   - or the local code/content now proves them.
4. Verify every link/reference to renamed/added planning docs.
5. Ensure no files under `Assets/Scripts`, scenes, prefabs, GameData, or ProjectSettings changed in this planning-install task.

## Final deliverable from this Codex session

Create `PLAN_RESTRUCTURE_REPORT.md` containing:

- local HEAD/branch and whether the working tree was dirty;
- files added;
- files modified;
- any file deliberately left unchanged and why;
- decisions reclassified as FACT / OWNER DECISION / WORKING HYPOTHESIS / OPEN;
- contradictions resolved;
- unresolved conflicts with newer local work;
- exact next action: **execute Day 1 from `planning/CURRENT_WINDOW.md`**.

Then show:

```bash
git status --short
git diff --stat
```

Do not implement Day 1 in this session.
Do not commit unless explicitly asked.
