# Decision Backlog — Trigger-Gated Unknowns

Every item in this file is deliberately **not decided**. The decider is the **human owner** in every row. Agents may run the experiment, collect evidence, and propose options; they do not ratify.

**States:** `BLANK` = intentionally unresolved; `SPIKE` = experiment authorized, still unresolved; `RATIFIED` = human chose (only then may it leave this backlog for a binding design doc).

| ID | Open question | Class | Trigger | Current blank | Decider |
|---|---|---:|---|---|---|
| DB-001 | What is the default game-time pace? | D | First walking commute + first watchable ship voyage exist | No canonical `gameHoursPerRealSecond` | Human |
| DB-002 | Which prefab socket concepts need a shared component? | C | Two accepted systems need the same authored transform data | No all-purpose `ModuleSockets` schema | Human |
| DB-003 | How many ports/beds/nodes/workstations does each starting module have? | D | That module's first playable mechanic requires the count | Content counts are disposable | Human |
| DB-004 | Does walking require a general route graph now? | C | One-link walking spike is accepted and a second/multi-hop route is needed | No pathfinding architecture | Human |
| DB-005 | If a walk graph exists, what determines path cost? | C | Multiple plausible walk paths exist in real content | No Dijkstra/traversal-hours rule | Human |
| DB-006 | What is a readable walking duration? | D | First walking spike can be watched at chosen pace | No canonical `0.25h` | Human |
| DB-007 | Are finite docking ports part of the first playable? | C | Two ships actually contend at a station | No required port-count mechanic | Human |
| DB-008 | How should berth contention resolve? | C | Contention spike produces at least two plausible behaviors | No priority/FIFO policy | Human |
| DB-009 | Do berth reservations expire or get reclaimed? | C | Real contention demonstrates stale reservation risk | No timeout/reclaim policy | Human |
| DB-010 | What ship movement model should production use? | C | Current movement shortcomings can be named in a watchable voyage | No custom-integrator/PhysX/kinematic lock | Human |
| DB-011 | What should ship acceleration/rotation/braking feel like? | C | Flight-feel spike can be watched | No burn-flip-burn requirement | Human |
| DB-012 | What physical/tuning values drive ship motion? | D | Movement model is ratified | No mass/thrust/speed/substep constants | Human |
| DB-013 | Which voyage phases are real simulation states? | C | One centralized voyage owner exists and presentation/logistics need distinct states | No final phase enum | Human |
| DB-014 | What cancellations/aborts are allowed mid-voyage? | C | A real cancel/reassignment interaction exists | No abort-to-nearest-berth policy | Human |
| DB-015 | Should loading/unloading take simulated time, and how much? | C/D | Transfer is visible and instant transfer is judged inadequate | No transfer duration constants | Human |
| DB-016 | Which ship states need visual affordances? | C | Accepted voyage states are visible through presentation | No clamp/light/beacon spec | Human |
| DB-017 | Which Unity UI workflow will be used? | C | First real management surface is ready | No UI Toolkit/uGUI lock | Human |
| DB-018 | What are the default camera controls? | C | First selectable/navigable scene is used for a session | Temporary bindings only | Human |
| DB-019 | What belongs permanently on the HUD? | D | Playtest repeatedly asks for the same always-visible fact/control | HUD content blank | Human |
| DB-020 | What time window should flow reporting use? | D | A real trend question exists and its useful horizon is observed | No 24h rule | Human |
| DB-021 | What resource-flow read model is actually needed? | C | Existing inventory/history cannot answer a concrete question | No `ResourceFlowLedger` schema | Human |
| DB-022 | What blocked-time read model is actually needed? | C | A real blocked-state question cannot be answered from current history | No `BlockedTimeLedger` schema | Human |
| DB-023 | Do duty summaries need generated prose? | C | Human repeatedly reconstructs duty history manually | No `DutyReportBuilder` sentence format | Human |
| DB-024 | What information belongs in a facility management surface? | C | First facility is managed through UI | No predetermined table/column set | Human |
| DB-025 | What information belongs in a ship surface? | C | First ship is managed/diagnosed through UI | No predetermined panel | Human |
| DB-026 | What information belongs in a colonist surface? | C | First colonist-management task exists | No predetermined panel | Human |
| DB-027 | What is the staffing-management interaction model? | C | Manual assignment is usable and has been exercised | No HR tree/candidate/filter layout | Human |
| DB-028 | What belongs in a colony report? | D | Two real sessions reveal unanswered end-to-end questions | Report sections blank | Human |
| DB-029 | Which conditions deserve alerts? | D | A playtest misses a problem until too late | No alert catalogue | Human |
| DB-030 | What thresholds make those alerts useful? | D | A specific alert exists and can be tuned from observed lead time | No 12h/2h/1h/8h thresholds | Human |
| DB-031 | Are module interiors visible, and how? | C | First people/facility presentation spike can be viewed | Cutaway/x-ray/window choice blank | Human |
| DB-032 | Where do working/sleeping/idle bodies appear? | C | Interior visibility + module geometry exist | No workstation/bed/idle-slot policy | Human |
| DB-033 | Should pilot boarding be visibly simulated? | C | Ship/pilot presentation exists and instant boarding is judged unclear | Boarding choreography blank | Human |
| DB-034 | What construction material(s) exist first? | D | First construction experiment needs hauled material | `Regolith` not canonical | Human |
| DB-035 | What staffing/content drives construction speed? | D | First staffed construction can be watched | No Builder min/cap/exertion/curve | Human |
| DB-036 | What fields does a building definition require? | C | One building can be manually placed/completed and consumed fields are known | `BuildingDefinition` schema blank | Human |
| DB-037 | What is the placement domain? | C | 2–3 representative placeholder modules exist | No SitePlane/grid/freeform lock | Human |
| DB-038 | How does rotation snapping work, if at all? | D | Placement spike demonstrates a need for snapping | No 15° constant | Human |
| DB-039 | How are corridors represented geometrically? | C | Placement spike includes two connected modules | Straight/node-to-node rule blank | Human |
| DB-040 | Can construction progress before every material is delivered? | C | First material-delivery/building spike exists | Material/progress gating blank | Human |
| DB-041 | How do builders reach a newly placed site? | C | A site is placed somewhere not already walkable | EVA/shuttle/nearest-module behavior blank | Human |
| DB-042 | What state transfers from site to completed building? | C | First completion occurs | Employment/anchor/socket transfer policy blank | Human |
| DB-043 | How is a built object safely removed? | C | First constructed object exists and removal is actually needed | Demolish lifecycle blank | Human |
| DB-044 | Are construction costs refunded on removal? | D | Removal loop exists and economy consequences can be judged | Refund policy blank | Human |
| DB-045 | Does staffing need automation at all? | C | Human has manually staffed multiple real shifts/facilities | No allocator assumption | Human |
| DB-046 | If automation is needed, what model fits: targets, templates, suggestions, allocator, something else? | C | DB-045 trigger fires and pain is described | No hybrid-control lock | Human |
| DB-047 | If an allocator exists, what ordering/reassignment rules does it use? | D | Automation model is ratified and real conflicts exist | No priority-90/hourly/4h-hysteresis algorithm | Human |
| DB-048 | What balance target should the first economy have? | D | A playable loop exists and owner states intended pressure | No “8 colonists + 2 farm techs sustainable” target | Human |
| DB-049 | What individual survival needs exist? | C | Shortage can be created and observed | No nutrition/hydration schema beyond current consumption facts | Human |
| DB-050 | What nonlethal consequences does unmet consumption have? | D | DB-049 experiment shows how shortage should matter | No 0.4→1.0 curve | Human |
| DB-051 | When, if ever, does starvation/dehydration kill? | D | Nonlethal shortage loop has been played and warning/recovery are understood | No 24h/72h death timer | Human |
| DB-052 | What is the lifecycle contract for removing a colonist? | C | First actual removal/death mechanic is accepted | One entry point is desired; exact unwind order blank | Human |
| DB-053 | What makes a colonist “housed”? | C | Sleeping/home is visible and housing pressure is intentionally tested | No bed-capacity model | Human |
| DB-054 | What penalty, if any, does homelessness cause? | D | Housing-pressure playtest exists | No `0.5` restfulness | Human |
| DB-055 | How are homes assigned/reassigned? | C | Housing system exists and manual behavior is understood | No auto-home allocator | Human |
| DB-056 | What fields belong in a starting-scenario asset? | C | Stable start can be assembled manually twice via real paths | No `ScenarioDefinition` schema | Human |
| DB-057 | What is the first curated starting colony? | D | Core loop is understood and external playtest needs a curated start | No 3 Pilots/2 Techs/3 Builders/48h Food composition | Human |
| DB-058 | Is the Water Processor staffed or automated in that scenario? | D | Starting scenario is being curated after staffing/logistics play | Blank | Human |
| DB-059 | What constitutes game over? | C | A real failure state occurs in the playable loop | No population-zero lock | Human |
| DB-060 | What session/menu frame is required? | C | External playtest can no longer reasonably launch from editor/bootstrap | No menu/session-controller shape | Human |
| DB-061 | What hotkeys become defaults? | D | UI/camera/time controls are stable and repeatedly used | No Space/1–4/F/Esc lock | Human |
| DB-062 | What is the environment art direction in-engine? | C | Camera/module scale/core composition are stable enough to judge a look spike | Reference intent only; no production bible | Human |
| DB-063 | Should environment art be generated, hand-authored, kit-bashed, purchased, or mixed? | C | One bounded look-dev comparison exists | Workflow blank | Human |
| DB-064 | What palette/mesh/fog/particle/scatter values are used? | D | Environment direction is ratified and performance measured | No canonical values | Human |
| DB-065 | Does Phase 0 need any resource-field hook for future exploration? | C | Phase 1 exploration is actively being designed against completed Phase 0 | No field hook now | Human |
| DB-066 | What semantics does an uncrewed/automated ship use? | C | Uncrewed ships are a real Phase-1/2 feature | `operatingRole == null` remains unassigned, not reserved future meaning | Human |
| DB-067 | When should starting ice deplete? | D | Exploration exists and campaign pacing can be playtested | No day-5–8 target | Human |
| DB-068 | What is Phase 1's actual growth loop architecture? | C | Phase 0 playable has been reviewed and its surviving seams are known | Roadmap retains themes, not committed implementation | Human |

## Backlog hygiene

- If a trigger has not fired, the item cannot appear in `01_LOCKED_DESIGN.md`.
- If a spike runs but the human has not chosen, the state becomes `SPIKE`, not `RATIFIED`.
- Rejected options and reasoning stay attached to the item so later choices are informed.
- A decision may remain blank indefinitely. That is a successful outcome when nothing currently needs it.
