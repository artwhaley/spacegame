# SPACEGAME — High-Speed Population Stress Lab

## Revised execution packet: speed-independent behavior and manual readiness

Reviewed against local `main @ 553831e2548ed195fb347e60552bc74d9ddab583` on 2026-09-22. Repository: https://github.com/artwhaley/spacegame.

This document **replaces** the supplied HSS-0 through HSS-23 stack. Execute this document in order, respecting the evidence gates. The implementation report records what is now built and what still requires Unity-side manual evidence.

The goal is that a colony started from the same state and given the same simulation-time inputs produces the same ordered gameplay events and state at 10x and 1000x. Presentation may skip frames. Losing time, changing reservation winners, changing activity duration, or completing a different meal is a gameplay divergence even when no exception occurs.

Build the laboratory and make bounded, evidence-backed corrections. Do not equate preparing the laboratory with proving the final goal. An unresolved engine-navigation dependency must be reported as a determinism blocker, with a reproducible case, rather than concealed by a green stress summary.

## What changed from the original stack

- Add an explicit comparison oracle, stable identities, fresh-run reset, repeated baseline runs, and first-divergence reports before the large fixture.
- Replace the blanket prohibition on simulation substeps with a narrowly scoped, speed-independent logical clock ticket. The present tick size already proves this is a correctness issue.
- Separate logical activity timing from animation observation. Recovering after a real-time watchdog is insufficient to make gameplay speed-independent.
- Add a navigation feasibility gate. Faster agents and repeated brain ticks do not establish equal navigation progress or arrival order.
- Preserve the 200-person laboratory, real production systems, contention scenarios, invariant checks, export, and human-run campaign.
- Fix the logging-disabled assumption, bound all instrumentation storage, and measure observer overhead.
- Distinguish exact gameplay equivalence, lifecycle health, achieved throughput, and presentation quality. These are separate results.

## Boundaries and operating rules

The local working tree is authoritative. Inspect branch, HEAD, status, and relevant diffs before edits; preserve unrelated edits and imported content. The 1:1 base clock, 1–1000 speed range, default 10x, and animation watchdog changes are now merged into the inspected main. Recheck at execution time; do not revert them to the old remote reference in the original packet.

Use the real `ColonistBrain`, stats, assignments, resolver, runner, motor, animation driver, `NavMeshAgent`, facilities, and Food/OffDuty/Workforce managers. Do not substitute a toy colony to claim an end-to-end pass. Controlled-input component tests are useful but must be labeled as such.

Keep the current gameplay policies: critical hunger lock, staffed/self-service food rules, work priorities, sleep, recreation durations, and cooldowns. Do not change need tuning to make results pass. Do not add bid/offer arbitration, queues, ECS, workers, simulation LOD, or a new brain. Deterministic ordering and clock/lifecycle corrections are in scope; broad movement replacement needs a separately described architectural follow-up.

The human runs Unity. Do not operate, close, or kill their editor, fight licensing, or start a competing Unity process on this project. Supply editor menu actions and a one-action build/validate/test workflow. If a native Unity step cannot be performed in the authorized environment, continue independent code and documentation work, list the exact pending step, and do not claim that an unbuilt or unvalidated scene is ready.

Keep runtime and Editor assemblies correctly separated. Development instrumentation must not introduce a dependency from the interactions package onto `Assets/Dev/Stress`. Do not ship lab scenes in ordinary player builds by default.

## Findings already established by source inspection

Paths below are repository-relative. These are code findings, not measured stress results.

| Source and method | Finding | Consequence for this packet |
|---|---|---|
| `Assets/Scripts/ColonyPrototype/Core/SimulationManager.cs`: `Update`, `AdvanceTick` | Accumulates real time in 0.1-second chunks, then multiplies each chunk by speed. A tick is 1 simulated second at 10x and 100 simulated seconds at 1000x. | Identical simulated intervals execute different numbers of decisions. Measuring this without correcting it cannot meet the stated goal. |
| Same: `CompareTickables`, `RegisterTickable`, `FlushPending` | Priority then registration ordinal determines execution. Stats priority is 50; brains priority is 100. Registration ordinals are static and depend on registration order. `ToArray()` allocates each tick, and flushing sorts the list. | Preserve/document phase order, provide stable ties, test reload/reset order; measure scheduler allocation when increasing tick throughput. |
| `People/ColonistStatsComponent.cs`: `SimulationTick`, effective-rate properties | Needs use the runner's currently active binding for the whole tick. Hunger is capped at 100. | Active/exit boundary placement changes the amount eaten, slept, or recreated. Fatigue has no equivalent maximum. |
| `Packages/com.asteroidcolony.interactions/Runtime/ColonistActivityRunner.cs`: `HandleActiveStarted`, `HandleActivityBodyCompleted`, `WaitForSequenceDwell`, `WaitForSequenceLoops`, `CompleteExitWhenReady` | Activity activation/body completion depend on driver events; dwell waits use presentation delta and optionally observed loop counts; release waits for exit animation plus motor placement. | Advancing brains alone cannot make the colony deterministic. Loop-count and finite activities also need coverage. |
| `ColonistAnimationDriver.cs`: segment/loop/locomotion routines, `Fail` | Four watchdog loops now use unscaled real delta, and failure requests Locomotion. No explicit pause exclusion exists in those elapsed-time loops. | Preserve the watchdog correction, add pause tests, and distinguish visual recovery from logical success. |
| `ColonistActivityRunner.cs`: `HandleAnimationStatus`, `Fail` | Parses `Action failed:` status text, fails the activity, attempts motor abort, and releases the reservation. The return value of `AbortActivityMotion()` is not checked here. | A presentation miss still changes simulation outcomes; failed reattachment can be hidden by successful reservation cleanup. |
| `ColonistMotor.cs`: `Update`, `ApplyPresentationSpeed`, `LateUpdate`, `OnAnimatorMove` | Agent speed scales by factor, acceleration by factor squared; arrival is frame-observed. Facing, placement, and root-motion integration occur outside simulation ticks. | A scaled agent is not a demonstrated equivalent of many small movement updates. Visual transforms also feed nearest-service queries. |
| `ContactRigDriver.cs`: `Update`, `CalculateLerp` | IK progress comes from the animation driver; smoothing uses `Time.deltaTime`. | Audit as presentation cost/behavior. Do not mechanically change every delta-time call into simulation time. |
| `Core/FoodManager.cs`, `Core/OffDutyManager.cs`: searches and tie breaks | Selection uses current approach/seeker positions, strict distance comparisons plus `Mathf.Approximately`, and names; initial discovery scans Unity objects. | Capture stable selection inputs/order. Name ties and near-equal distances need tests; approximate equality is not a reliable total ordering rule. |
| `People/ColonistIdentity.cs` | Contains a display name, not a persistent simulation identity. | Identical names or new Unity object IDs cannot identify entities across runs. |
| `Core/SimulationLogEntry.cs` | Entries contain UTC time and subjects identified with `GetEntityId()`. Field values use `ToString()`. | Raw JSONL is not a cross-run oracle: wall timestamps, runtime IDs, and culture-dependent formatting differ. |
| `Core/SimulationLogManager.cs`: `Append` | Per-event `File.AppendAllText`, optional Console output, and front removal from a bounded List. | Avoid the normal formatted event stream in benchmarks. |
| `Core/SimulationLog.cs`: `Log`; brain and food diagnostic methods | Legacy logging still calls `Debug.Log` without a logger instance. Food signatures and some brain field arrays/strings are constructed before the manager's null check. | Omitting `SimulationLogManager` does not produce allocation-free, Console-free instrumentation. Preserve brain inspector decision state when gating formatting. |
| `Assets/Tests/EditMode/PresentationTimeTests.cs`: high-speed failure test | Invokes private `Fail`, with an Animator not first placed in an action state. It does not exercise a watchdog or runner reservation lifecycle. | Keep useful assertions, but replace this weak recovery evidence with a test that demonstrably fails without recovery and exercises an actual action. |

Installed versions at review: Unity `6000.5.9f1`, AI Navigation `2.0.14`, Test Framework `1.7.0`. Check installed APIs rather than copying examples from other releases. Unity documents separate Animator update modes; choosing one does not automatically connect it to this project's custom simulation clock. See [AnimatorUpdateMode](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AnimatorUpdateMode.html). The [NavMeshAgent API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshAgent.html) describes navigation controls; do not infer a supported manual crowd-simulation step or determinism guarantee from speed controls. Verify any proposed engine stepping technique against the installed version.

## HSS-0 — Record the execution baseline and clock ownership

Record status, branch, full HEAD, diff summary, and relevant diffs in `HIGH_SPEED_STRESS_AUDIT.md`. Inspect current local versions of all sources listed above, plus Workforce, FoodService, OffDuty, target resolver, facility reservation token, and activity/sequence definitions.

For each meaningful timer, state transition, or wait, record file/method, time source, owner, high-speed risk, and intended action. Classify as simulation duration, visual presentation duration, engine watchdog, or engine/frame observation dependency. Audit root-motion position and nearest-target selection as inputs, not just timers.

Inventory `Time.*`, `PresentationTime.*`, Animator normalized-time/transition reads, coroutines, NavMesh arrival/path state, registration/discovery order, RNG use, object IDs, float clock accumulation, and logging. Identify every path that can release a reservation or change an active need rate outside a logical tick. Include disable/destroy, scene reload, pause, culling, and loss of a facility.

Acceptance: an ownership map identifies who advances simulation time, movement, logical activity progress, visual progress, and reservation changes. Unproven claims are labeled hypotheses. No remote reset is used to simplify the audit.

## HSS-1 — Define what “the same log” means

Create a versioned run manifest and comparison schema before adding broad telemetry.

### Comparison contract

For the same executable/content/configuration, same initial authoritative state, seed, navigation data, and tick-indexed external inputs, compare runs through the same final logical tick. Initially scope strict reproducibility to the same platform and Unity build; do not claim cross-platform floating-point determinism.

The canonical gameplay event stream includes tick (and substep if introduced), execution phase, within-phase sequence, stable actor ID, event kind, activity request generation, stable facility ID, reservation group/generation, activity/sequence member, reason code, and necessary numeric payload. Cover decisions/target selections, accepted/denied reservations or their exact per-tick digest, lifecycle changes, need-threshold transitions, work obligation/active work changes, meal release, and recreation completion/cooldown changes. Route all equivalent event sites to this sink whether verbose logging is enabled or absent.

Compare causal order, entity identities, targets, reasons, and logical event times exactly. Do not sort the resulting event log to hide ordering differences, drop failures, combine actors, or accept matching aggregate totals as equivalence. Retry counts/order that affect arbitration are gameplay data. If a redundant diagnostic is excluded, list it in the schema and explain why it cannot affect behavior.

Exclude UTC timestamps, real elapsed time, frame indices, engine object IDs, output paths, diagnostic wording, and purely visual fallback events from gameplay equality. Keep those in a separate diagnostic stream with simulation-tick correlation. This is a canonical view of the game's events, not literal byte equality of the existing JSONL wrapper.

Use invariant numeric serialization. Compare exact state representations on the initial same-build scope; report small numeric drift separately for diagnosis. Any relaxed tolerance must be explicit in the report and cannot conceal a different threshold crossing, reservation owner, target, or event tick.

### Stable identities and state

Assign serialized stable simulation keys to colonists, facilities, and other ordering participants, with stable component-kind keys where needed. Lab names may be human-readable, but name alone and Unity entity ID are not the identity contract. Resolve keys once at startup; validate uniqueness. Do not use `.GetHashCode()` or runtime dictionary enumeration as a persistent digest/order.

At fixed simulation checkpoints capture an ordered digest of needs, brain state, target/pending requests, authoritative location/route state, runner phase/subphase, active duration/sequence progress, reservations/generations, workforce obligations, cooldown history, RNG state if used, and pending logical events. Include state that influences future decisions, not only what is currently visible. Keep clock accumulation/backlog in scheduler diagnostics; requested speed and wall-time debt are not gameplay-state equality fields.

Manifest: commit and dirty-state fingerprint, schema/build version, scenario and seed, exact resource counts, initial-state fingerprint, tuning/recipe/layout/NavMesh fingerprints, logical quantum, ordering/phase policy, speed schedule, input script, presentation/navigation modes, observation mode, Unity/package versions, platform, and termination condition. Performance metadata also records hardware, Editor/player, quality, resolution, frame cap/vsync, focus/background settings, and profiler attachment.

Acceptance: comparator rejects incompatible manifests; known added/deleted/reordered/retargeted events and altered state cause failure. A matching hash is compact evidence, not a mathematical proof; retain event counts and support an exact bounded trace rerun around a differing interval.

## HSS-2 — Build a small paired-run harness first

Before the large scene, use a generated 4-colonist fixture with real components, four dedicated beds, explicitly configured jobs, one contested food seat, and a recreation slot. Add a two-colonist/one-seat case for arbitration. Keep real animation recipes and a small valid navigation area.

Provide Start, Pause, Stop/Export, Recreate/Reset, and Run Comparison controls. Apply initial conditions while paused and before brains can tick. Inputs are scheduled by logical tick, including service availability changes and speed changes. No reflection or field hacks during runs. Narrow development setup APIs are acceptable; normal gameplay must not call them.

Each run starts fresh. Prefer recreating the owned scene/population/managers to an incomplete in-place reset. Clear reservations, generation state, coroutines, subscriptions, target caches, selection/decision deduplication, cooldowns, duty history, tick registration ordinals, logger sequence, telemetry, and RNG state. Audit static registries with domain reload enabled and disabled. Do not clear unrelated additive scenes or global application state.

Implement only the minimal manifest/event sink/comparator needed here, then extend that same implementation in HSS-6. Do not build a disposable logging framework. Include HSS-5's single-trip and two-agent navigation probes in this baseline so a native movement limitation is discovered before substantial lifecycle work; HSS-5 later reruns them after bounded corrections.

Warm up assets/rendering outside measurement, then recreate/reset logical state and verify the initial-state fingerprint. No arbitrary number of real seconds of gameplay may act as warm-up. A scene that starts a chef early must record that pre-roll and its resulting starting state. Setup readiness and measurement start are distinct.

Minimum comparisons, with identical simulated endpoints:

1. 10x versus 10x from fresh setup: detect baseline nonrepeatability first.
2. 10x versus 1000x; use a short fixed horizon initially, such as 600 simulated seconds.
3. Same fixed-speed runs with alternate frame caps, plus a deterministic hitch/input schedule in the scheduler test harness.
4. A tick-indexed 10x → 1000x → 10x schedule versus a constant-speed reference.
5. Pause in navigation, entry, active, and exit; hold longer than a watchdog timeout, then resume. Freeze logical state/event progress while paused.

Initially run these against the existing production behavior and save the first mismatch. A red baseline is expected evidence, not grounds to weaken comparison. Add short controlled-motion/input tests only to isolate a subsystem; label them clearly as non-end-to-end.

Acceptance: run pairs export the first differing tick/event or checkpoint, expected/actual records, preceding bounded context, and relevant actor/facility states. Missing, overflowed, aborted, or incompatible data yields INCOMPLETE, never PASS.

## HSS-3 — Make logical clock advancement independent of speed

This is a correction explicitly permitted by this revised stack. The original “do not introduce global substepping” instruction is superseded for the logical clock. Do not implement a broad engine/physics rewrite under this ticket.

Use a fixed quantum in **simulated seconds**, not real seconds multiplied inside each tick. Begin the experiment with 1 simulated second, which matches the current 10x tick size. Validate adequacy with the small fixture; a finer quantum may be chosen from evidence, but must be identical across all compared speeds. Fingerprint this value and document serialization migration of the old real-second interval.

Accumulate requested simulation time using double precision from real unscaled delta times speed. Execute whole logical quanta; give all tickables the same quantum converted to game hours. Use a long logical tick and derive absolute simulation time from a starting epoch plus ticks, rather than repeatedly accumulating a float hour. Preserve existing hour-based APIs as a projection where practical. Do not change need rates or shift definitions.

Define the within-tick phase contract, including which interval stats integrate and when lifecycle boundary events become effective. Preserve the current stats-before-brains ordering unless the boundary correction requires an explicitly tested change. Stable simulation keys break ties within each priority/phase. Registration and removal during a tick take effect at documented boundaries. Test reversed creation/registration order, missing/duplicate keys, and enable/disable behavior. Do not let diagnostic listener attachment determine gameplay ordering.

Bound work per rendered frame so the editor stays responsive. Retain outstanding simulation debt; never discard it, enlarge the quantum, or advance `CurrentGameHour` for work not performed. Export requested speed, achieved simulation-seconds/real-second, pending debt, oldest debt, steps/frame, and catch-up limit hits. A safety debt cap may pause/abort with an explicit incomplete result; it cannot silently skip time. Pause must prevent both new debt and execution of existing debt; resume and speed changes must have tested semantics.

Advancing the brain repeatedly while NavMesh/Animator have not advanced is **not** an end-to-end fix. Engine-facing simulation owners must progress coherently with logical time, or be reported as unresolved at HSS-5. Presentation should follow executed simulation progress under overload; it must not race ahead on requested speed while logical ticks lag.

Expected cost: with a 1-second quantum, requested 1000x means 1000 logical ticks per real second. For 200 brains plus 200 stats components that is approximately 400,000 component tick calls per real second before other work. This is a workload estimate, not measured capacity. Do not preserve the old cheap 100-second jumps to report an artificial 1000x win.

Retain the old-build 10x trace as characterization, but create fresh paired 10x/1000x baselines after changing logical timing. A manifest mismatch across builds is not a determinism comparison. Explicitly review behavioral differences from the old low-speed run; do not silently bless a regression merely because both new speeds reproduce it.

Measure and, if required, remove scheduler-owned per-tick snapshot allocation/sorting using safe reusable storage and dirty-on-membership-change ordering. Preserve mutation-during-tick semantics. Avoid unrelated manager redesign.

Acceptance: scheduler tests inject different real-frame partitions yielding the same accumulated simulated time and obtain identical executed ticks/state after debt drains. Include hitch, speed change, pause, multi-midnight, schedule boundary, and large starting-time precision cases. A wall-time/CPU budget cannot change the sequence of logical results, only when they are computed.

## HSS-4 — Make logical activity timing survive presentation loss

Write and test the logical lifecycle contract before changing driver callbacks. Entry, active, finite completion, dwell, loop-count completion, exit, placement handoff, and release need one authoritative timing owner. Keep this narrow to existing runner/driver/recipe contracts; no new intent framework.

Both 10x and 1000x must use the same logical timing rules. A high-speed-only skip that shortens entry or frees a chair earlier is a divergence. Derive logical clip durations from authored clip length and absolute segment speed where appropriate; specify how blends overlap, how facing/placement time contributes, and how reverse playback completes. Validate against known-good low-speed recipes rather than assuming every blend adds serial duration. Specify tick quantization or deterministic substep boundaries and consistent need-rate integration across them. Carry elapsed remainder through multiple segment boundaries; do not demand one rendered frame per logical segment or round each duration differently by speed.

Preserve authored finite versus sustained behavior, dwell's current first-loop minimum, sequence loop/cycle counts, pending requests, shared-group reservation retention between sequence members, meal policies, and recreation cooldown semantics. Make differences from the current contract explicit and tested. A sustained Sleep/Work/Eat loop is not complete just because a watchdog expires or its clip plays once.

Animation/IK/root motion may render or approximate the authoritative activity. They must not decide when food recovery starts, shift work becomes active, cooldown begins, or reservation release occurs merely because an Animator state was observed. Motor placement still needs a valid authoritative navigation handoff. Visual and logical events must be distinguishable; avoid duplicate completion from both clocks.

Maintain unscaled real-time engine watchdogs, but suspend timeout accumulation during intentional pause. Their outcome is a presentation fault/fallback signal. Do not wait ten real seconds to advance a logical activity: that would be 100 simulated seconds at 10x versus 10,000 at 1000x. Recovery must not silently shorten or lengthen gameplay duration. Test pause before and after the watchdog budget starts.

Replace string-parsed visual failure as a gameplay control signal with a narrow typed distinction where necessary. Missing logical bindings/anchors, invalid recipes, destroyed targets, and failed navigation reattachment remain genuine failures. Missing transient rendering is not proof that an activity failed. Do not swallow structural failures to obtain matching logs.

Guard callbacks with request/playback generation and phase so stale, duplicate, or reentrant callbacks cannot complete a replacement action. Abort/reset must be idempotent, release at most once, cancel old completion paths, clear contacts, and verify motor recovery. A failed `AbortActivityMotion()` must remain visible as unrecovered navigation state.

Keep full/auto/forced-coarse visual modes for tests at all speeds. Forced coarse changes presentation only. Off-screen/culled/disabled visual Animator cases should follow the same logical trace when the logical actor is still enabled. Disabling the actor itself is a separate lifecycle case.

Acceptance: start the Animator in ActionA/ActionB, demonstrate failure recovery to Locomotion, and exercise runner ownership through entry and exit. Test real watchdog semantics, reverse clips of more than one second, empty steps, finite and sustained bodies, dwell/loop sequences, pause, stop/replacement, and failed reattachment. Controlled missing/delayed/duplicate visual callbacks must not alter the logical trace. Native Animator tests belong in PlayMode; do not replace them with a private-method reflection test that begins in Locomotion.

## HSS-5 — Establish whether navigation can meet the contract

This is an explicit feasibility gate before declaring determinism. Keep real navigation in the end-to-end fixture.

Run a single unobstructed trip, two competing colonists, open-floor groups, then a choke lane. Record request tick, path result, authoritative position/progress at simulation checkpoints, arrival tick/order, selected subsequent facility, and failure reason. Compare 10x repeatability, then 10x/1000x under different frame caps. Report requested agent speed, acceleration, angular speed, stopping distance, radius, avoidance settings, and native path readiness behavior.

The current motor's speed/acceleration scaling and frame-observed arrival are concrete hypotheses to test. Unity callbacks or asynchronous results must not mutate reservations halfway through a logical phase. Queue their observations at a defined boundary, with stable ordering, but acknowledge that queuing alone does not remove variability in *which tick* a result becomes available.

Investigate the smallest supported route to authoritative speed-independent movement. Verify any engine manual-stepping proposal against installed public APIs and a working experiment. Repeated `SimulationTick`, `Physics.Simulate`, changing `fixedDeltaTime`, `Animator.Update`, or setting `agent.speed` is not evidence that NavMesh crowd simulation was stepped equivalently. Do not introduce undocumented player-loop calls as a shortcut.

Nearest-service decisions must read authoritative positions, not a visual interpolated or root-motion-only transform. A path-length/speed timer that ignores avoidance, blockage, and reachability is a new movement policy; it cannot be inserted only at 1000x and called equivalent. Disabling avoidance, teleporting travelers, or replaying recorded 10x arrivals can isolate faults but cannot pass the real navigation acceptance test.

If bounded changes cannot produce equal navigation outcomes, produce a minimal failing fixture and an architectural decision note specifying the required change, expected behavioral differences, and cost. Keep the strict goal marked BLOCKED and continue independent lab/export work. Do not silently replace NavMesh or describe a statistically similar run as deterministic.

Acceptance: a real-navigation comparison pass with evidence, or an explicit failed gate. The whole-project determinism status cannot be stronger than this result.

## HSS-6 — Low-cost telemetry and a canonical digest sink

Build one lab-owned monitor holding direct population/facility references. Reuse runner lifecycle events and add only generic typed diagnostics needed by the interactions package. Count at mutation sites where feasible; never find every object each frame or perform a second manager search just for telemetry.

Use fixed indexed counters, preallocated buffers, stable numeric codes, and cached ID mappings. No LINQ, formatted strings, JSON serialization, per-tick dictionaries, or per-colonist-per-tick records in instrumentation hot paths. Existing production event allocation should be measured separately; attaching the monitor must not add an object per tick/event.

Counters: logical ticks, requests, reservations, denials, navigation start/arrival/failure, entry/active/exit/release/failure, visual fallback/recovery, food/off-duty queries and candidates, workforce lookups/scans, invariant failures, and callback suppressions by generation. Count actual operations consistently, not inspector polling. Repeated contention denial is workload, not a warning to print.

Use a specified, portable rolling digest over canonical binary event fields plus exact event counts and fixed-simulation-time checkpoint digests. Choose a fixed algorithm with test vectors, explicit byte order, and a version; never use runtime hash APIs. No disk write per event. Keep a bounded recent event ring and a separately bounded first-failure capture. A short trace rerun retains exact records for the differing interval; digest mode does not require retaining a whole multi-day event log.

Initial storage budgets, configurable and reported: 256 distinct failure records, 4096 recent compact events, and a 64 MiB ceiling for total lab buffers. Allocate at setup, export schema/ID tables once, and store counters for overwritten/dropped records. Once the first critical failure is captured, preserve its context instead of allowing repetitive errors to overwrite it. Semantic digest input must never be sampled/dropped; inability to process it invalidates comparison.

For long runs, bounded checkpoint/time-series storage may thin diagnostic samples deterministically or stop measurement explicitly. Do not claim full resolution after thinning. Export happens after measurement or through bounded buffered I/O with cost/overflow reported. No unbounded queues.

Acceptance: observed telemetry-owned steady-state allocations are zero or a precisely documented bounded exception; memory does not grow with run length; unsubscribe/reset works across repeated runs. Typed failure counters survive with all verbose logging disabled.

## HSS-7 — Actually silence verbose logging in performance runs

Support three modes:

| Mode | Gameplay comparison | Observability | Use |
|---|---|---|---|
| Performance | Optional compact digest, declared in manifest | Aggregate counters, bounded failures, cheap invariants | Throughput and frame cost |
| Determinism | Required compact event/checkpoint digests | Bounded event context and comparison | Default for paired runs |
| Focused Trace | Required digest plus exact scoped trace | Selected actors/tick window; no default Console echo | Diagnose divergence; not performance-comparable |

Audit `SimulationLogManager`, legacy `SimulationLog`, `ReadinessHistory`, direct warnings, and eager call-site allocations. Null logger checks inside `RecordEvent` occur too late to prevent construction of `params` fields. Disable formatting/verbose emission before allocation, while preserving decision fields used by inspector UI and independent typed gameplay events. Do not parse Console text to reconstruct the canonical trace.

Apply suppression through a scoped lab configuration restored on stop, destroy, error, and domain reload. Do not globally disable all Unity errors or mutate normal scene settings. Deduplicate repeated diagnostic warnings by actor/request/reason and retain a count plus first occurrence; serious exceptions remain visible. If a logger is present, benchmark mode must prevent in-memory formatted-entry accumulation as well as JSONL and Console output.

Acceptance: a run with normal loggers absent and a run with logging disabled have equal canonical results. Profiler/measurement evidence accounts for legacy output and allocation at call sites. The ordinary game's logging still works after leaving the lab.

## HSS-8 — State monitoring and invariants without polluting results

Maintain event-driven reservation/lifecycle ledgers for cheap immediate checks and periodically reconcile them against actual facility/runner state. Inspection APIs are read-only; no mutable reservation dictionaries. Include current facility, request generation, reservation owner/token generation, runner subphase, and recovery state where necessary. The monitor never repairs the world.

Check ownership in both directions: each facility/group has at most one valid token/holder; each claiming runner has that token; no disabled/destroyed owner persists; no stale release frees a newer reservation. Reconcile at a stable end-of-tick boundary. Snapshot intervals alone can miss a leak that occurs and clears between real-second samples, so retain transition counts and peak violations.

Check brain/runner coherence using actual current subphases. `Busy` includes entry/active/exit and is not itself a stuck condition. A sustained worker or sleeper may legitimately stay active for hours. Track progress and expected deadlines; pending work that waits behind an exiting request is not a second acquired reservation. Use documented grace in logical ticks for logical coherence and real active time only for native engine liveness. Do not let transient checks fire mid-callback.

Navigation: distinguish crowd stall, unavailable service, path pending, incomplete/invalid path, lost agent attachment, and unresolved lifecycle. `remainingDistance` can be unavailable while a path is pending; do not indiscriminately classify that as numeric corruption. Flag the ignored motor-abort result identified in the audit. Endpoints must be validated before declaring success.

Numeric checks: finite authoritative time/needs/positions/deadlines/speeds, nonnegative absolute time/needs, Hunger in [0,100]. Fatigue has no arbitrary maximum. Validate numbers before a clamp hides NaN input where practical. Invalid timing inputs should produce a clear rejection/diagnostic, not an accidental infinite catch-up loop.

Record progress ages in simulation time and active real time, plus target, phase, reason, request generation, and last progress. Do not declare an actor broken just because a staffed cafeteria is legitimately closed. Report per-colonist hunger/wait maximums, wait-age histograms, time without eligible service, and starvation/fairness outliers without adding queues or tuning changes.

Performance sampling: aggregate population states, runner subphases, active versus reserved facilities, min/mean/max needs, and liveness about once per real second, after a completed logical batch. Determinism sampling: ordered state checkpoints at a fixed simulation cadence, initially every 60 simulated seconds and at the final tick. These are different clocks for different purposes. Report monitoring cadence and missed/coalesced samples.

When stopping measurement, capture pre-cleanup state first. Live reservations at an arbitrary endpoint can be valid. Then cancel/tear down in a separate cleanup phase and assert no leaked ownership/subscriptions. Do not count cleanup events in the measured gameplay stream or hide leaks by clearing dictionaries before inspection.

## HSS-9 — Measure throughput, frame cost, and observer overhead

Record real active seconds, executed simulated seconds, requested and achieved multiplier, logical ticks, quantum, debt, catch-up hits, and CPU time spent in clock/tick dispatch versus navigation/presentation versus monitoring. Display achieved speed prominently: a slider at 1000 with 200 simulated seconds executed per real second is 200x throughput.

Collect frame count, total frame duration, average FPS from elapsed/count, p50/p95/p99/worst frame time. Use a bounded histogram with stated resolution/overflow or a bounded reservoir with explicitly approximate quantiles; do not sort growing history every frame. Use `ProfilerRecorder` only where available and low cost; record GC allocation/collections, managed memory, and CPU markers as available. Label missing metrics unavailable, not zero.

Use wall time as well as engine delta in hitch tests to reveal clamped/lost pacing time. Log Editor focus/pauses distinctly from stalls. Measurement ends at the configured logical tick for equivalence pairs; partial final-frame work and export cost must not inflate throughput.

Run instrumentation-off, counters-only, and full monitor/digest variants on the same short deterministic workload, with warm-up, repeat runs, and identical endpoint. Confirm that adding observers does not change the gameplay digest. Use a provisional target of <=5% median wall/CPU overhead and <=10% p95 frame-time overhead for normal instrumentation; report raw values, noise, and repeatability rather than treating these as pre-proven guarantees. If overhead is material, reduce polling and formatting, not gameplay event coverage or tick count. Keep overhead violations visible.

Do not profile every query with a stopwatch by default. Aggregate counters and coarse profiler markers come first; enable fine timing only for focused diagnosis. Disable deep profiling for performance evidence and separate Editor from development-player results.

## HSS-10 — Deterministic facility and population builder

Create `Assets/Dev/Stress/Editor/PopulationStressLabBuilder.cs`, versioned config, simple fixture prefabs, and `Assets/Dev/Stress/PopulationStressLab.unity`. Reuse `Assets/Prefabs/Colonists/Colonist_Synty_Male_01.prefab` and known-good production recipe assets. Use serialization/editor APIs, never invented GUIDs or manually assembled scene YAML.

Canonical counts:

| Resource | Count |
|---|---:|
| Real colonists / dedicated beds | 200 / 200 |
| Workforce assignments / unassigned colonists | 150 / 50 |
| Generic workstations | 149 |
| Staffed cafeteria counter/job | 1, included in the 150 |
| Independently reservable Eat seats | 40 |
| Independently reservable Dance opportunities | 50 |

Create StressBed, StressWorkstation, StressCafeteriaCounter, StressCafeteriaSeat, and StressDanceSlot templates with real bindings, local reservation groups, and separate approach/animation/exit anchors. Each Eat seat points to the shared counter/role with minimum one genuinely active worker. Dance slots share the semantic cooldown key `dance`. Use actual OffDuty authoring so need recovery, planned duration, cooldown, and recipe completion agree.

Names/IDs and assignments derive from config indices. Use a local seeded RNG with versioned algorithm, separate from presentation/global Unity random consumption. If a fixture needs no randomness, do not introduce it. All setup state must exist before component registration/activation; validate missing references before OnEnable can register partial entities.

Canonical shift is 08:00–16:00 for all 150 workers, including the cafeteria worker. This intentionally closes staffed public food outside that worker's actual service. A long mixed run may expose policy problems; do not call every unmet meal an engine failure. Separate scenario variants below may configure staffing, but never silently give the canonical fixture permanent service.

Support explicit resource configurations for 4, 50, 100, 200, and 500 colonists. Reject inconsistent sums or assignments exceeding capacity. Do not assume ratios without saved config. Rebuilding the same config must give the same logical layout/IDs/assignments/fingerprint; exact Unity serialization file IDs need not match.

The builder owns only the lab output folder. Validate paths and protect non-generated edits. Rebuild into temporary owned output before replacement where practical; use Undo/editor-safe scene operations and preserve the user's open scene state. Assets outside the lab are references, not build targets.

## HSS-11 — Navigation layout and structural validation

Use a flat, plain laboratory with spawn, beds, work, cafeteria, and recreation zones. Wide lanes and unique approach/exit positions avoid an unintended shared destination bottleneck. Spawn spacing respects agent radii. Build a separate deliberate choke lane with staging/target areas; regular activity routes must not all pass through it.

Bake/generate NavMesh reproducibly using the project's existing AI Navigation technology. Record data/settings fingerprint. Check start positions, approach and exit anchors against the correct agent type/mask, complete paths, island connectivity, and separation from geometry. Activity animation anchors need not themselves be walkable; return anchors do. NavMesh changes or rebakes make paired manifests incompatible unless equivalence of their input data is explicitly established.

Validator checks counts, unique IDs, complete components, assignments, staffed service wiring, roles/shifts, reservation groups, recipe/clip/anchor references, manager registrations, and no duplicate singleton managers. It checks actual capacity, not just GameObject counts. Validate the canonical prefab instead of automatically trusting its name.

Only emit `STRESS LAB VALID` after all required checks, including navigation checks, pass. Report structural validity separately from determinism and measured runtime health. Store the validator output and hash with each run.

## HSS-12 — Scenario harness and reliable reset

The scene starts paused. The Inspector offers config/scenario, observation mode, presentation mode, speed, target simulated duration, Start, Pause, Stop/Export, Reset/Recreate, and Compare Baseline. Reject mid-run fixture edits; speed changes are recorded tick-indexed inputs. Setup through public APIs or narrow development initialization APIs; never spoof an active worker flag or write private state every frame.

Implement these scenarios with explicit start state, eligibility expectations, measurement horizon, and success/liveness bounds:

| Scenario | Setup and expected exercise |
|---|---|
| Idle baseline | Off-shift, low needs, no discretionary drive; choose a short horizon before normal accumulation changes eligibility. Production brains/stats remain enabled. |
| Work stampede | 150 healthy assigned workers cross a common obligation boundary and seek 149 stations plus the counter. Account for the existing work-preparation window; choose starting time based on actual policy. Count obligations, reservations, arrivals, and genuinely active workers separately. |
| Sleep stampede | Off-shift, all sleepy, low hunger, 200 dedicated beds. No scarcity. Cover entry, sustained recovery, reverse wake/exit, release, and another request. |
| Food stampede | 199 critically hungry consumers, 40 seats, and a healthy chef who becomes genuinely active through the normal work lifecycle. A saved scenario-specific shift keeps only the chef obligated during this off-duty measurement. Use a deterministic pre-roll/setup phase and record resulting initial state. Up to 40 seat reservations/active meals; this is a capacity ceiling, not a promise of simultaneous activation. |
| Recreation stampede | Off-shift, high stimulation need, low hunger/fatigue, 50 Dance opportunities. Ensure the actual duration/rest/work budget permits a selection. Test duration, recovery, interruption, and cooldown rather than merely activity entry. |
| Mixed colony | Seeded needs, 150 employed/50 unassigned, ordinary staffing schedule. Include near-threshold values and boundaries. Distinguish supply/policy starvation from reservation or lifecycle corruption. |
| Navigation crush | Real motors/agents through a dedicated choke; brains may be disabled by this explicitly labeled subsystem scenario. Validate cleanup before re-enabling them. |
| Service loss / lifecycle interruption | Disable/re-enable chef/service, cancel during approach/entry/active/exit, destroy/disable a selected owned test actor/facility, and reacquire the same reservation group with a new generation. All inputs occur at specified ticks. |

Provide both a staffed-contention case and an explicitly unstaffed control using the real FoodService configuration to separate access denial from slot contention. Compare each config to itself across speeds; do not mix them as one baseline. Mixed-case initial states should include exact/below/above hunger/sleep/work thresholds, midnight, shift start/end, cooldown expiry, and exhausted-but-critically-hungry combinations. Do not retune these thresholds.

Reset must pass HSS-2's initial-state comparison after every scenario, including failed runs and play-mode reload. Setup changes to global frame limits, profiler recorders, log settings, and random state are restored on every exit path.

## HSS-13 — Exports and first-divergence diagnostics

Use `StressResults/<unique-run-id>/` under the project for Editor output and an explicitly displayed persistent-data path for players. Add generated result directories to ignore rules; never overwrite a prior run. Provide an Inspector “Open results folder” action. The implementation/manual report must print the resolved absolute path.

Export manifest, Markdown summary, machine-readable JSON, CSV aggregates, checkpoint/event digests, and bounded failure context. A comparison creates its own report referencing both run IDs and includes the first mismatching tick/phase/event or checkpoint, stable actor/facility identities, expected/actual values, and a recipe for a scoped exact-trace rerun. All formats share versioned identifiers and invariant numeric formatting.

Include requested/achieved speed and debt, per-simulation-time lifecycle/query/denial rates, frame statistics, GC availability, monitoring costs/capacities, overflow counts, population/needs distributions, waits/fairness outliers, active versus reserved capacity, presentation failures/fallbacks, and pre-cleanup versus post-cleanup ownership. Record the exact end reason: target tick reached, manual stop, first divergence, invariant failure, engine exception, debt cap, or recorder overflow.

Incomplete runs are useful evidence but cannot pass full-horizon equivalence. Export/serialization/I/O work after the run is excluded from performance timing and measured separately. Handle disk errors visibly while retaining bounded in-memory results.

## HSS-14 — Focused automated coverage and compilation gate

Compile touched runtime, Editor, and test assemblies with the installed Unity APIs before claiming readiness. A successful runtime-only `.csproj` build does not compile/execute the Unity tests, validate imported references, or prove scene readiness. Distinguish unavailable generated build inputs from a source error; do not report unrun tests as passed.

Prioritize behavioral tests over trivial counter/property tests:

- Comparator detects event deletion/reordering/retargeting, first differing checkpoint, incompatible manifests, missing tail, culture changes, and overflow/incomplete capture. Stable digest test vectors and repeat-run ID mapping pass.
- Fixed logical stepping produces equal results under alternate wall-frame partitions, pause/speed changes, catch-up limits, large-time precision, multiple midnights, and deterministic membership changes. Include a case that fails with the old speed-dependent tick size.
- Genuine contention: same stable actor wins with reversed setup registration order; stale token release cannot release a new owner's group. Near-equal/equal-distance target selection has a defined stable tie policy.
- Logical lifecycle: full versus coarse, delayed/missing/duplicate visual callbacks, reverse entry/exit, finite/sustained/loop/dwell sequences, and stop/replacement produce correct durations and exactly-once release. Needs integrate over the documented active interval; cooldown starts once after actual planned completion.
- Pause longer than an engine watchdog budget causes no logical completion/failure or ownership change. Resume recovers; real structural failure still reports failure.
- Native PlayMode: begin in an action, exercise watchdog/failure, assert actual locomotion recovery, valid agent attachment, reservation release, and a successful subsequent request. Include a short real-navigation pair; retain the full coupled test even if isolated tests pass.
- Monitor detects injected orphan/duplicate ownership, persistent phase mismatch, failed reattachment, and invalid numeric state while accepting ordinary sustained activity, path pending, and transient phase handoffs. Monitor observation does not repair or change state.
- Fresh reset/repeated setup, including domain-reload-disabled mode where supported, has no duplicate subscribers, stale static registrations, or inherited cooldowns. Deterministic config fingerprint and structural builder validation are covered.
- Quantiles/histogram boundaries and telemetry capacity/overflow semantics are correct on known input. Measure allocation/overhead in the dedicated run, not through brittle hardware-specific unit assertions.

Run each appropriate suite after the related code is complete; avoid repeatedly running all tests per ticket. If Unity access or licensing prevents execution, record the exact limitation and human command/menu path. Stop repeated Unity troubleshooting after a second unsuccessful attempt and request specific help, following the developer's session preference.

## HSS-15 — Human campaign, ordered to find causes cheaply

Write `HIGH_SPEED_STRESS_MANUAL.md` with exact menu actions, scene/config paths, expected checkpoints, how to compare, and where to retrieve reports. The developer must not wire 200 components by hand.

1. **Build and validate:** generate the lab, check required assets/NavMesh, compile tests, verify paused startup. Inspect the four-colonist fixture visually at 1x/10x.
2. **Small repeatability gate:** two 10x runs, then 10x versus 1000x for identical 600-simulation-second endpoints. Extend horizons where an action requires more time. At 10x, 600 simulated seconds costs approximately 60 real seconds; at achieved 1000x it costs 0.6 seconds, so use longer workloads for reliable performance timing.
3. **Boundary/lifecycle gate:** food contention, reverse wake, finite/dwell loops, service loss, pause/resume, speed changes, and full/coarse presentation. Repeat at distinct render frame caps, then controlled hitches. Inspect the earliest mismatch before raising population.
4. **50-person fixture proof:** idle/work/sleep/food/recreation, fixed endpoints at 10x and 1000x. Use 1x for visual checks. One subsystem failing does not justify redesigning all managers.
5. **Canonical 200:** same scenarios at 10x, 100x, 250x, 500x, 1000x; optional 1x transition checks. Compare each against the same 10x start and endpoint. Repeat baseline and high-speed runs at least twice before calling repeatable.
6. **Mixed full cycle:** compare fixed 24-hour or 72-hour simulated horizons to capture shift changes, food, sleep, recreation, and cooldowns. Budget honestly: 24 simulated hours at 10x is 2.4 real hours; 72 hours is 7.2 real hours. Reuse a recorded baseline only with matching fingerprints. A short run positioned near 08:00 or midnight is a boundary test, not a multi-day substitute.
7. **Population versus speed:** 50/100/200/500 with saved explicit resource configs at 10x and 1000x, including work and mixed cases. Measure achieved speed and backlog independently of log equality. Include the separate choke-lane test.
8. **Observer calibration:** instrumentation-off/counters/full variants and fixed simulated workloads. Performance-only runs may use a minimum real duration (for example 30–60 seconds after warm-up), but label them throughput runs, not equal-endpoint determinism pairs.

An early mismatch should produce a small reproducible case and report without forcing the human to finish the entire matrix. Keep wider scalability runs available for independent questions. Never infer same behavior from matching final Hunger averages or a quiet Console.

## HSS-16 — Result classification and completion gates

Report four independent axes for every run/pair:

| Axis | Result |
|---|---|
| Gameplay equivalence | PASS / DIVERGED / INCOMPATIBLE / INCOMPLETE / NOT TESTED, with first mismatch |
| Lifecycle/invariants | PASS / FAILURE / NOT TESTED, with unresolved recovery and ownership details |
| Throughput | Requested multiplier, achieved multiplier, backlog trend, frame/CPU cost; sustained target met or not |
| Presentation | Full / degraded / failed, with counts; degradation alone is not a gameplay failure |

Equal logs can describe two identically broken worlds; invariant health must also pass. A coherent colony with different reservation winners fails equivalence. A deterministic colony too slow to sustain 1000x is a throughput limitation. Growing backlog is not sustained 1000x. A navigation-control/replay-only test is not the real-world comparison.

Produce `HIGH_SPEED_STRESS_AUDIT.md`, `HIGH_SPEED_STRESS_MANUAL.md`, and `HIGH_SPEED_STRESS_IMPLEMENTATION_REPORT.md`. The closeout includes starting/final HEAD and dirty state, preserved changes, modified files, clock/phase policy, IDs and comparator schema, actual hardening, builder/config/scene paths, canonical counts, exact tests executed/results, Unity steps pending, measured observer cost or unmeasured status, navigation-gate result, limitations, and resolved output paths. Do not invent stress results.

### Repository implementation status

The current implementation pass has delivered the fixed simulated-second clock/debt path, stable identities, bounded summary/detailed telemetry, deterministic digest and first-retained-event comparison, lifecycle/reservation inspection surfaces, manager query counters, a production-component 200-colonist builder (149 workstations plus one counter, 150 assignments, 50 unassigned), structural validation, focused source/test coverage, and export/manual-run documentation. Unity Test Runner execution, NavMesh bake validation, and the paired 10x/1000x behavioral evidence remain deliberately open until the generated scene is run in the Unity editor.

### Laboratory-ready acceptance

- [ ] Current merged speed/time work and unrelated local changes are preserved.
- [ ] Clock/authority audit and stable comparison contract exist.
- [ ] Fresh repeatable reset, tick-indexed inputs, paired runs, digests, and first-divergence export work.
- [ ] Fixed logical-time stepping and boundary policy have focused regression coverage.
- [ ] Animation recovery/pause and logical-duration behavior have actual behavioral coverage.
- [ ] Real navigation feasibility result is explicit; controlled tests are labeled.
- [ ] Canonical 200 real colonists, 200 beds, 149 workstations plus counter, 150 assignments, 50 unassigned, 40 Eat seats, and 50 Dance opportunities validate.
- [ ] All scenarios and independent population configs are available with reproducible NavMesh/layout.
- [ ] Telemetry, semantic comparison, failures, and time series have bounded memory/overflow policies.
- [ ] Verbose logging is suppressed without losing authoritative events or mutating gameplay.
- [ ] Invariants distinguish legitimate waiting/sustained activity from corruption; cleanup is verified separately.
- [ ] Requested/achieved speed, debt, simulation tick size, frame quantiles, and observer overhead are visible.
- [ ] Runtime, Editor, and test compilation status is accurate; native tests/scene validation are actually run or explicitly pending.
- [ ] Manual guide and exact report paths permit the developer to run the campaign without manual fixture wiring.

### Deterministic 1000x acceptance — a separate, stronger claim

- [ ] Same-build 10x repeats match canonical events and state through the same endpoint.
- [ ] Real coupled 10x/1000x runs match ordered gameplay events and state through the same endpoint, including contention and activity boundaries.
- [ ] Frame-cap, hitch, pause/resume, and speed-change cases pass the defined comparison contract.
- [ ] Full/coarse presentation cannot change the gameplay trace; real navigation satisfies the same contract.
- [ ] All required lifecycle/numeric/reservation invariants pass independently of equality.
- [ ] The declared target population sustains requested 1000x without increasing debt, skipping time, or dropping comparison data, on the reported machine/configuration.

If the laboratory is delivered but one of the latter checks fails, report exactly that. Preparation is valuable; it is not evidence that the final goal has been met.

## Mapping from the original ticket numbers

| Original | Revised location |
|---|---|
| HSS-0/1: baseline and audit | Findings table, revised HSS-0 |
| HSS-2/3/12: counters, recorder, manager metrics | HSS-6, HSS-8, HSS-9 |
| HSS-4: invariants | HSS-8 |
| HSS-5/15: logging and modes | HSS-7 |
| HSS-6/17: presentation hardening and tests | HSS-4, HSS-14 |
| HSS-7/8/9/18/19: prefabs, builder, layout, validation, scale | HSS-10/11 |
| HSS-10/11: scenarios and setup | HSS-2, HSS-12 |
| HSS-13: tick-size observation only | Superseded by HSS-3 clock correction plus telemetry |
| HSS-14: exports | HSS-13 |
| HSS-16: harness tests | HSS-14 |
| HSS-20/21: no premature redesign/bid-offer | Operating rules, bounded changes, navigation gate |
| HSS-22/23: campaign and closeout | HSS-15/16 |
| New: exact meaning of determinism and navigation limitation | HSS-1/2/5 and final acceptance |

The renderer may omit moments. The simulator must execute the same logical moments, in the same order, with the same consequences. This packet makes that claim measurable before scaling the laboratory.
