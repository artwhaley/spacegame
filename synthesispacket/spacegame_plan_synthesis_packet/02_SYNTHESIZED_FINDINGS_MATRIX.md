# Synthesized Findings Matrix

This matrix is the final disposition after comparing the OpenAI, DeepSeek, and Claude audits plus the owner's new direction.

| Topic | Final disposition | What changed / why |
|---|---|---|
| Fable planning authority / "locked" language | **Demote speculative locks** | All audits agree. Keep locks only for behavior-preserving refactors or explicit owner decisions. |
| 26-day frozen working-depth schedule | **Replace** | Human 28-day roadmap survives as orientation, but only next 3 days are working-depth commitments. |
| Daily "Press Play and see" | **Protect and strengthen** | Add "what we expect to learn" and decisions fed. |
| Farm one-worker production curve `0.65` | **FACT / keep** | DeepSeek caught the audit-prompt false positive: value already exists in shipped Farm role content. |
| `HabitationComponent.restfulnessMultiplier` seam | **FACT / keep** | Existing code seam is real; proposed homeless `0.5` is not. |
| Homeless restfulness `0.5` | **OPEN** | Needs a housing experiment. Do not choose a number merely to create pressure. |
| Per-colonist needs / `Fed(colonist,...)` | **Remove from active plan** | Other audits caught the hidden aggregate→personal scarcity-model rewrite. First surface existing aggregate shortage. |
| Nutrition/hydration work penalties | **OPEN** | Depends on personal-needs decision and aggregation semantics. |
| Death command | **Needed seam** | One explicit cleanup command is useful. |
| Fixed eight-step death unwind | **Do not lock** | Discover cleanup order by manually killing difficult states; some lifecycle cleanup already exists. |
| Automatic starvation/dehydration timers | **OPEN** | No thresholds until shortage/death meaning has been played. |
| Hybrid staffing targets/priority/manual pin concept | **OWNER DECISION / keep** | Recorded as user choice. |
| `WorkforceAllocator` exact algorithm | **OPEN** | First expose targets manually; automate only observed tedium. |
| Allocator priority-before-staffing ordering | **Likely needed when allocator exists** | Keep as local ordering constraint then; do not prebuild allocator now. |
| `StaffingManager` responsibility split | **Protect** | 1,000+ line hotspot verified; behavior-preserving split remains earned work. |
| Hard file-line-count acceptance | **Reject** | Responsibility/behavior are acceptance; line count is diagnostic only. |
| Walk / Ship / Blocked strategic rule | **OWNER DECISION / protect** | Core game intent survives. |
| Exact Dijkstra/traversal implementation | **Working hypothesis only** | Start with the smallest graph required by the first corridor; NavMesh presentation now enters early. |
| Human avatars + visible work | **OWNER DECISION / move to Days 1–2** | New input overrides old infrastructure-first ordering. |
| Facility work-cycle prototype | **Integrate immediately** | Inspect/port real prototype; do not reinvent from prose. Initially presentation/local activity only. |
| NavMesh facility surfaces | **Move to Day 1** | Required to watch human work and learn movement/interaction ownership. |
| Full `ModuleSockets` eight-field schema | **Reject as Day-2 contract** | Grow only immediate consumers. New first days earn mesh/dock/interaction anchors; beds/corridor/EVA later. |
| `beds.Count` owns habitation capacity | **Reject** | Presentation transforms must not silently own runtime economy capacity. |
| One voyage authority | **Protect** | Existing duplicate ship movement writers justify consolidation. |
| Custom Newtonian 6DOF | **Open / later experiment** | Determinism does not uniquely force this solution. First use existing motion with real docking. |
| Physical docking berths | **Move to Day 3** | New owner priority and excellent discovery mechanism. |
| Visible boarding/disembarking | **Move to Day 3** | Must preserve contract/carrier/pilot authorities while making handoff visible. |
| Dock reservation/queue | **Trigger-gated** | Force two ships to contend first. |
| Priority/FIFO queue formula | **Open** | No policy until contention creates a real question. |
| `queuePosition % holdingSlots` | **Reject** | Can physically overlap queued ships. |
| Loading/unloading takes time | **Working design goal** | Watchability is plausible; exact durations stay tunable. |
| UI Toolkit-only | **Reject as global lock** | Choose first panel's fastest working stack; standardize only after repeated evidence. |
| HR candidate rejection reasons | **Protect** | Existing assignment results are useful simulation truth. |
| HR two-pane layout / filters / badges | **Open** | Use first real shift-management experience to learn the mental model. |
| Ledger existence / in-memory history feed | **Keep seam** | Current code has raw events but no in-memory read path; need facts for UI. |
| Ledger named aggregates / 24h buckets / hours-until-empty | **Open** | Store raw facts first; derive metrics when a real question needs them. |
| Colony report tables | **Open** | Build only the explanation/alert wished for during actual runs. |
| Alert thresholds | **Open / data-tuned** | Measure normal/failure distributions first. |
| `Regolith` as canonical construction material | **Open** | Construction needs a temporary material to prove the loop, not a canon chain. |
| Full `BuildingDefinition` schema | **Open** | Derive fields from first real buildable(s). |
| `SitePlane` / plane IDs / 15° snap | **Open** | Drag one ghost with real camera first. |
| Scenario field list | **Reject as pre-spec** | Derive the minimum representation from the working slice and round-trip it. |
| Scenario capture Editor command | **Possible tactic, not contract** | Adopt derive/round-trip principle, not one prescribed implementation. |
| Starting crew / food / beds | **Open** | Tune from integrated run; 6-vs-8 bed drift demonstrates premature rot. |
| Water Processor automated | **Working scenario experiment** | Run staffed vs automated when scenario becomes real; not architecture. |
| Ice depletion Day 5–8 | **Open** | Requires actual measured economy and an agreed exploration arc. |
| Interior visibility | **Partly decided** | Visible humans working inside facilities is now owner intent; exact cutaway/roof/x-ray technique stays open. |
| Full environment generator | **Defer** | Presentation around the real slice first; procedural system only when earned. |
| Duplicated Constitution in ROADMAP | **Fix defect** | Remove duplicate; one canonical source. |
| Constitution rules all mechanically forced | **Reject** | Several are policies/preferences, not code facts; label them accordingly. |
| `HOW_IT_WORKS` ownership table/layers | **Protect** | Code-grounded material is valuable. |
| `HOW_IT_WORKS` minute-by-minute future narrative | **Reframe** | Intended experience / hypotheses, not observed truth. |
| Open-question recommendations/deadlines | **Remove** | Replace with triggers/evidence needed. |
| Phase-1 future designs shaping Phase 0 | **Stop unless independently justified** | Future horizons may warn, not force current abstractions. |
| Test runner not a gate | **Owner/process fact** | Preserve existing tests; add new tests where they protect logic/refactors, not arbitrary counts. |
| Mesh moved to child root / collider sizing | **Fix near-term hazard** | Claude caught a concrete regression risk; Day 1/4 observable must verify colliders. |

## One-line rule

**Preserve facts and owner decisions; isolate placeholders; trigger questions with play; never let a future schema become authority merely because it was written first.**
