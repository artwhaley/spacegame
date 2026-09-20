# Decision Backlog — Trigger-Gated Questions

This file is intentionally a register of **questions**, not recommendations disguised as questions.

The human owner decides. Agents may propose options only when the trigger fires.

| ID | Question | Current blank | Trigger / last responsible moment |
|---|---|---|---|
| DB-001 | Is scarcity a colony/place fact or a per-colonist fact? | Existing aggregate shortage remains authoritative. No personal needs component. | Run a real shortage and ask whether "the colony is hungry" is insufficient; decide before writing personal-needs code. |
| DB-002 | If personal needs exist, how is scarce food/water allocated among colonists? | No rationing model. | DB-001 resolves in favor of personal needs. |
| DB-003 | What does low food/water do to a colonist? | No work/recovery penalty. | After personal needs exist and a low-need state has been watched. |
| DB-004 | What automatically causes death? | Manual/debug death command only. | After a shortage/failure run produces a clear desired emotional/gameplay consequence. |
| DB-005 | What is the consequence of homelessness? | Visible "unhoused" fact only; no penalty. | Create a housing shortage and watch it. |
| DB-006 | Should completed housing auto-home people? | No automation. | Manually re-home people enough times to find it tedious. |
| DB-007 | Does the interactable facility remain presentation-only, or can task steps ever affect simulation outcomes? | Presentation client only. | A real design need arises for task-specific gameplay rather than merely visible work. |
| DB-008 | What is the reusable contract for facility work cycles? | Port only what the existing prototype actually needs. | After Farm plus a second qualitatively different facility have both used it. |
| DB-009 | How does an avatar's local NavMesh motion synchronize with logical transit arrival? | Days 1–2 only use local movement within an already-arrived facility. | First inter-facility walking commute on Day 7/8. |
| DB-010 | Should inter-facility walking use authored links + NavMesh presentation, full NavMesh route truth, or another model? | Strategic mode decision only: Walk / Ship / Blocked. | Watch the first real corridor commute. |
| DB-011 | What walking/traversal duration feels right? | Placeholder dial. | Watch one human perform the commute at normal play speeds. |
| DB-012 | What is the minimum docking state machine? | One berth + actual arrival/boarding. | Day-3 boarding/docking experiment exposes missing states. |
| DB-013 | Do docking ports need reservations? | Not until contention. | Force two ships to want one berth. |
| DB-014 | What queue ordering should docking use? | No final policy. | Multiple simultaneous berth requests create a player-visible fairness/priority problem. |
| DB-015 | How should extra queued ships hold physically? | No modulo-overlap rule. | Queue depth exceeds authored safe hold positions. |
| DB-016 | Is current kinematic ship movement good enough? | Keep current movement behind one voyage owner. | After docking/boarding has been watched repeatedly. |
| DB-017 | If not kinematic, should flight be custom 6DOF, PhysX, guided spline, or another approach? | Blank. | DB-016 says current motion is inadequate; run focused motion spikes. |
| DB-018 | How long should loading/unloading take? | Preserve correctness; timing is a dial. | Watch actual dock operations and decide whether instant transfer is unreadable. |
| DB-019 | Which UI technology should the project standardize on? | No project-wide lock. | After at least two real management surfaces exist and one stack has demonstrated clear advantage. |
| DB-020 | Is HR job-first, person-first, or target-first? | Minimal selected job/shift + candidates. | Run one full shift change through the first HR UI. |
| DB-021 | Should HR/management be modal or visible while watching the colony? | First implementation may use simplest workable form. | Use it while diagnosing live activity and notice whether losing the world view is painful. |
| DB-022 | What staffing automation is actually wanted? | Targets/priority/pin are data; no autonomous algorithm. | Manually fill targets and notice specific tedium. |
| DB-023 | May the allocator move an already-employed worker automatically? | No. | Automation exists and under-target gaps cannot be solved without reassignment; owner decides. |
| DB-024 | How should an allocator avoid thrash? | No hysteresis number. | Actual thrash is observed. |
| DB-025 | What should release order be when over target? | No rule. | First over-target state occurs under automation. |
| DB-026 | What exact resource-flow aggregates matter? | Raw event/delta history only. | A real UI question needs an aggregate. |
| DB-027 | What alert conditions deserve interrupting the player? | No thresholds. | First long run produces "I wish it told me X." |
| DB-028 | What should a colony report contain? | No fixed sections/tables. | Player cannot explain a run's failure from direct UI/history. |
| DB-029 | What is the canonical construction material or chain? | Temporary explicitly non-canonical test resource. | Construction loop exists and needs economy identity rather than just proof of transport/labor. |
| DB-030 | What buildings are player-buildable in the first real slice? | Only entries demanded by current observables. | A new strategic action requires another buildable type. |
| DB-031 | Should placement be free, snapped, node-based, planar, or something else? | Free pose + overlap for first ghost. | Drag a real ghost with the real camera. |
| DB-032 | What rotation snapping, if any, feels good? | Continuous/default. | First placement session. |
| DB-033 | What corridor placement interaction is right? | Smallest two-endpoint interaction that proves a walk link can be built. | Build and remove first corridor. |
| DB-034 | How do builders reach an isolated construction site? | Blank. | Place a site that is not walk-connected and observe desired behavior. |
| DB-035 | What does demolish refund/cancel/do? | No full rule. | First time demolition becomes necessary for play, not merely cleanup. |
| DB-036 | What is the final `BuildingDefinition` schema? | Only fields required by the first one/two real buildables. | A second/third building exposes common data. |
| DB-037 | What is the final scenario data schema? | Derive minimum from the working slice. | New Game/bootstrap day. |
| DB-038 | What is the canonical starting crew composition? | Current scene/content until economy observation. | Long run after staffing/construction/shortage exist. |
| DB-039 | What starting stock is appropriate? | Current content/placeholder. | Same long run provides measured burn/production. |
| DB-040 | How many starting beds should the Command Post have? | Do not resolve 6-vs-8 from old plan prose. | Housing experiment + desired opening pressure. |
| DB-041 | Is the Water Processor staffed or automated in the starting slice? | Experiment both if easy. | Scenario/new-game configuration after commute/freight is visible. |
| DB-042 | Should local ice deplete on a designed day range? | No target. | Exploration actually enters the roadmap and measured consumption/mining exists. |
| DB-043 | How should cramped interiors be exposed to the camera? | People/interiors are visible; exact cutaway/x-ray/roof rule is open. | After real humans walk/work/sleep in actual module shells. |
| DB-044 | What is the module art kit source/style workflow? | Blockout first. | Day-26 presentation pass reveals the repeated forms that need a kit. |
| DB-045 | What environment generation system is worth building? | Focused environment around the active slice only. | Repeated manual environment work becomes bottleneck or exploration needs procedural space. |
| DB-046 | What should "game over" mean? | No automatic session end except explicit owner decision. | Failure pressure/death/population loop exists and playtest shows a meaningful terminal condition. |
| DB-047 | What rules belong in the Architecture Constitution versus planning policy? | Preserve verified runtime invariants; classify process preferences separately. | Documentation patch; revisit after first seven days of new cadence. |
| DB-048 | Should every runtime-authoritative field be serialized for future save/load? | Keep existing serialization discipline; do not add speculative state just for future save. | Actual save/load work begins. |
| DB-049 | What line-count limits are useful? | None as hard acceptance. | A refactor produces a class that is hard to reason about despite clear responsibility. |
| DB-050 | Which tests are mandatory? | Existing tests preserved; new focused tests only where they add verifiable value. | After five execution days, inspect which tests were actually run/read and useful. |

## Owner-stated decisions that are not backlog items

These are decisions, not questions, unless the owner changes them:

- The player should physically see people moving through the colony and doing work.
- Corridor walking and shuttle transport are both first-class strategic modes.
- Staffing is a facility-level concern separate from recipe definitions.
- Production consumes facility-performance output; it does not count workers itself.
- Explicit employment remains the mutation authority for who works where.
- A ship's responsible pilot is a temporary operating lease distinct from employment.
- The staffing control concept includes human-set targets/priority with manual control/pinning; automation details remain open.
- The near-term work should integrate the existing interactable-facility prototype rather than invent a replacement from prose.
