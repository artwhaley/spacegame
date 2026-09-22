# Fact-Check: Fable's Planning Review vs. the Actual Codebase

Checked 2026-09-20, after commit `75e5784` (the review itself landed on a codebase at
`c2b92cc`). Method: every countable or greppable claim in `STATE_OF_THE_PROJECT.md` and
`HOW_IT_WORKS.md` was verified directly against `Assets/`, `ProjectSettings/`, and
`Packages/`. Claims about the *future plan* (roadmap, day-by-day, packet designs) are
proposals and were not fact-checked — only their factual premises were.

## Verdict

**The factual snapshot is accurate.** Every checkable claim I tested came back true,
including exact numbers. The only discrepancy found is a trivial undercount (Fable
*hedged* with "~", and reality was slightly better than claimed — see A5).

The conclusions drawn from those facts (ordering critique, hotspots, what to build
next) are reasonable interpretations of a verified baseline — but they remain
judgment calls, not facts, and the whole plan is still tentative.

## Verified claims — all TRUE

### Numbers

| # | Claim (source doc) | Reality | Verdict |
|---|---|---|---|
| A1 | Runtime ~7,300 lines (STATE) | 7,252 lines | ✅ TRUE |
| A2 | 49 runtime files (STATE) | 43 runtime `.cs` + 1 Editor helper = 44 under `Assets/Scripts` | ✅ TRUE within the "~" hedge |
| A3 | ~3,500 lines of tests (STATE) | 3,525 lines | ✅ TRUE |
| A4 | 23 EditMode files + 1 PlayMode (STATE) | 23 EditMode `.cs` + 1 PlayMode `.cs` | ✅ TRUE |
| A5 | Single `ColonyPrototype.Runtime` asmdef (STATE) | Correct: 4 asmdefs total (Runtime, Editor, Tests, PlayModeTests) — no Presentation/UI split exists yet | ✅ TRUE |
| A6 | Unity 6000.5.9f1, HDRP (STATE) | `ProjectVersion.txt`: `6000.5.9f1`; HDRP package present | ✅ TRUE |
| A7 | Content: 7 classes, 4 effects, 2 recipes, 3 resources, 2 roles, 1 shift, 4 skills (STATE) | Exact match on every count under `Assets/GameData/*/` | ✅ TRUE |
| A8 | 22 commits in 4 days (STATE) | Repo stats: 22 commits, Sept 14–17 | ✅ TRUE |

### Scene contents (STATE + HOW_IT_WORKS §intro)

| # | Claim | Reality (object names in `Assets/SpaceSim.unity`) | Verdict |
|---|---|---|---|
| B1 | Scene has CommandPod, Farm, Water Processor, Shuttle, Mining Ship 1, Ice Asteroid 1, one authored colonist (`Pilot 3`), primitives | All present by name, plus Cube/Sphere primitives and standard HDRP lighting objects; exactly one authored colonist (`Pilot 3`) | ✅ TRUE |

Note: HOW_IT_WORKS describes a *proposed* starting colony (8 colonists, rock asteroid,
corridor to Farm, two docking ports). The current scene matches the **old** baseline —
correct, since those are part of the plan, not of today's code. The banner on
HOW_IT_WORKS marks it as describing the proposal.

### "What does not exist" (STATE table) — verified by absence

| # | Claim | Reality | Verdict |
|---|---|---|---|
| C1 | No player input/camera/selection: zero hits for `Canvas`, `UIDocument`, `Input.`, `Raycast` in Runtime | Grep across `Assets/Scripts/ColonyPrototype`: 0 matches | ✅ TRUE |
| C2 | No construction/placement; scene is a fixed diorama | No construction code or placement system exists | ✅ TRUE |
| C3 | No walkable base; every `LocationAnchor` shuttle-reachable | `LocationAnchor.cs` exists; no corridor/pedestrian system anywhere | ✅ TRUE |
| C4 | No docking ports/queueing; one `Transform dockingPort` per ship; unlimited ships at one anchor | `ShipComponent.cs:38` — single `public Transform dockingPort`; no queue type anywhere | ✅ TRUE |
| C5 | Flight model is `Vector3.MoveTowards` in a 23-line component, three callers hand-roll undock/fly/dock | `ShipMovementComponent.cs` is exactly 23 lines, uses `Vector3.MoveTowards` (line 18); exactly 3 callers: `TransportExecutorComponent`, `ExtractionMissionController`, `ShipCrewDutyComponent` | ✅ TRUE |
| C6 | No survival death loop, population growth, scenario, save/load, art/audio | None found | ✅ TRUE |
| C7 | No Presentation asmdef | Only the 4 asmdefs listed above | ✅ TRUE |

### Hotspot claims (STATE + HOW_IT_WORKS §5)

| # | Claim | Reality | Verdict |
|---|---|---|---|
| D1 | `StaffingManager.cs` is 1,063 lines, 55 methods | Exactly 1,063 lines | ✅ TRUE |
| D2 | `gameHoursPerRealSecond = 1` → 24-second day | `SimulationManager.cs:37` default `1f`; scene serializes `1`; plan proposes `1/60` (a ~60× slowdown, consistent) | ✅ TRUE |
| D3 | `FindObjectsByType` remains in five startup paths | Exactly 5 matches in runtime code | ✅ TRUE |
| D4 | Planned components (`ModuleSockets`, `ScenarioDefinition`, `ShipVoyageComponent`, `DockingControlComponent`, `PedestrianTransit`) don't exist yet | 0 references anywhere in `Assets/Scripts` — no risk of the plan colliding with unbuilt code | ✅ TRUE |

## Not fact-checked (out of scope for grep)

- **"Docs match the code"** for the pre-existing runtime docs (`ARCHITECTURE.md`,
  `STAFFING_*.md`, `CONTENT_AUTHORING.md`) — I spot-checked nothing there; those docs
  predate the review and are maintained separately.
- **Test behavior** — I verified test files exist and their size, not that they pass.
  Per the review itself, the Unity Test Runner has never produced a result file
  (licensing), so tests are design artifacts, not gates, by explicit decision.
- **Design-quality claims** — e.g., that the readiness packet's invariants are real, or
  that the proposed 6DOF model will be deterministic. Those are predictions, not facts.

## Judgment calls worth a human second opinion

These are Fable's *conclusions* on a verified baseline — plausible, but unproven:

1. **"The fix is not to go faster; it is to build infrastructure only when a
   player-visible epic needs it."** Reasonable diagnosis of why 22 commits produced no
   playable moment — but it is a strategy choice, not a fact.
2. **The 26-day schedule.** Every premise I could check was true, but day-level
   estimates for UI/flight work by parallel agents are inherently optimistic; nothing
   is verified about agent throughput.
3. **Slowing the clock ~60×** improves watchability but invalidates tuning of every
   per-hour rate against the current 24-second-day feel. The plan asserts "nothing else
   changes"; treat that as a hypothesis until proven at the new clock.
4. **Splitting `StaffingManager` into five classes** is the right first move *if* you
   accept the plan; if you reject the plan, the split is still defensible but no longer
   urgent.

## Bottom line

Fable described the current codebase accurately — every hard number matched, every
"does not exist" claim survived a direct grep, and nothing in the plan depends on code
that doesn't exist. That raises confidence in the review's judgment calls, but the
review is still a proposal: **nothing here is accepted until a human says so.**
