# STACK 1 — STAFFED SERVICES, PERSONAL ACCESS, AND LEISURE-DRIVE GROUNDWORK

Automated report for the Stack 1 packet. Code changes only; no Unity Editor session was opened or
closed by the agent.

**PLAY MODE HAS NOT BEEN CLAIMED AS VERIFIED.** No human and no automated run performed a play-mode
scenario for this packet.

## Environment and SHAs

| Item | Value |
| --- | --- |
| Repository | `https://github.com/artwhaley/spacegame` |
| Starting SHA | `c77f7aee966b7e930e315a0890d5bd9cfa495140` (`main`, 2026-09-21, "Add imported space environment assets") |
| Ending SHA (HEAD) | `c77f7aee966b7e930e315a0890d5bd9cfa495140` — all work is uncommitted working-tree change |
| `origin/main` at closeout | `c77f7aee966b7e930e315a0890d5bd9cfa495140` (in sync with local HEAD) |
| Unity version in project | 6000.5.9f1, HDRP 17.5.0 |
| Unrelated working-tree noise | The same checkout also contains uncommitted editor/agent changes to imported Synty materials, `Assets/Prefabs/SM_Ship_*` prefabs, `Assets/PluginMaster/`, `ProjectSettings/PWBSettings.txt` — **not touched by this packet** |

### Compilation feedback from the human's open Editor

The Editor compiled the packet and reported exactly one error, in the new test file:
`FoodServiceAccessTests.cs(307,33): error CS0121` — an ambiguous `out _` between
`FoodManager.TryFindFoodService(FoodQuery, out FoodServiceOpportunity)` and
`TryFindFoodService(FoodQuery, out FoodServiceComponent)`. It was fixed by binding an explicitly typed
local (`out FoodServiceComponent closedService`) and asserting it is null. Because Unity reports every
compile error in every assembly it builds, the fact that this was the sole error indicates the runtime
(`ColonyPrototype.Runtime`) and editor (`ColonyPrototype.Editor`) assemblies containing the packet's
production code compiled cleanly. Every other `out _` in the packet was audited for the same overload
hazard and is unambiguous.

### Environment limitation (recorded once)

The Unity Editor was deliberately left running for the human user working in a different scene, and
the packet + the user's instruction explicitly forbade headless runs. Consequently:

- **No EditMode or PlayMode test could be executed.** Test execution requires the Unity Test Runner.
- No `Unity -batchmode` import, compile, or validation run was performed.
- Verification for this packet is therefore: code inspection, cross-reference greps, brace/paren
  balance checking of every edited file, and `git diff --check`. All static checks pass; compilation
  and test results must be confirmed by the human in the Unity Editor.

## Files changed

Modified (11):

```text
Assets/Scripts/ColonyPrototype/People/ColonistStatsComponent.cs      (+/- 195 /  ~)
Assets/Scripts/ColonyPrototype/People/ColonistBrain.cs               (+236 /  ~)
Assets/Scripts/ColonyPrototype/People/ColonistTargetResolver.cs      (+18  /  ~)
Assets/Scripts/ColonyPrototype/Core/OffDutyComponent.cs              (+45)
Assets/Scripts/ColonyPrototype/Core/OffDutyManager.cs                (+118)
Assets/Scripts/ColonyPrototype/Core/FoodServiceComponent.cs          (+163)
Assets/Scripts/ColonyPrototype/Core/FoodManager.cs                   (rewritten: +294)
Assets/Scripts/ColonyPrototype/Core/Editor/ColonistBrainEditor.cs    (+133)
Assets/Tests/EditMode/ColonistStatsTests.cs                          (+307)
Assets/Tests/EditMode/ColonistBrainTests.cs                          (+291)
Assets/Tests/EditMode/OffDutyManagerTests.cs                         (+161)
```

New (9 files, of which 4 are scripts/assets and the rest metas):

```text
Assets/Scripts/ColonyPrototype/Core/OffDutyCompletionHistory.cs       (new: plain C# history)
Assets/Scripts/ColonyPrototype/Core/OffDutyCompletionHistory.cs.meta
Assets/GameData/Jobs/CafeteriaWorker.asset                           (stableId cafeteria_worker)
Assets/GameData/Jobs/CafeteriaWorker.asset.meta
Assets/Tests/EditMode/OffDutyCompletionHistoryTests.cs                (new: 9 tests)
Assets/Tests/EditMode/OffDutyCompletionHistoryTests.cs.meta
Assets/Tests/EditMode/FoodServiceAccessTests.cs                       (new: 18 tests)
Assets/Tests/EditMode/FoodServiceAccessTests.cs.meta
LEISURE_AND_FOOD_ACCESS.md                                            (architecture documentation)
```

No prefab, scene, or material file was modified.

## Implementation summary

### S1-1 — Stimulation and Relaxation in `ColonistStatsComponent`

- New serialized state: `stimulationNeed`, `baselineStimulationNeedPerGameHour` (4),
  `stimulationNeedThreshold` (50), `relaxationNeed`, `baselineRelaxationNeedPerGameHour` (4),
  `relaxationNeedThreshold` (50).
- New API: `StimulationNeed`, `BaselineStimulationNeedPerGameHour`, `StimulationNeedThreshold`,
  `NeedsStimulation`, the Relaxation equivalents, `EffectiveStimulationPerGameHour`,
  `EffectiveRelaxationPerGameHour`, `AdjustStimulationNeed(float)`, `AdjustRelaxationNeed(float)`.
- Clamped at zero only, allowed above threshold, no upper cap, no health consequence.
- Effective leisure rate is `baseline - active recovery`; recovery is resolved from
  `ColonistActivityRunner.ActiveFacility` / `ActiveActivityId` plus the sibling `OffDutyComponent`.
  The brain state enum is never consulted.
- Threshold transitions log `colonist.need.stimulation` / `colonist.need.relaxation` with
  `state=entered|exited`, `value`, `threshold` — transitions only, never per-tick.
  `RecordTransition` was refactored to take the current value explicitly (it previously guessed
  Hunger vs Fatigue from the event-key string).

### S1-2 — Leisure effects, per-activity cooldown, completion history

- `OffDutyActivityBinding` gained `cooldownGameHours` (default 12), `cooldownKey`,
  `stimulationRecoveryPerGameHour`, `relaxationRecoveryPerGameHour`, plus `CooldownKey` (falls back
  to `activityId`), `HasCooldown`, `CooldownGameHours`, the two recovery properties, and
  `Satisfies(OffDutyDrive)`.
- New plain C# `OffDutyCompletionHistory` / `OffDutyCompletionRecord` with `RecordCompletion`,
  `TryGetLastCompletion`, `IsOnCooldown`, `TryGetRemainingCooldown`, `TryGetCooldownUntil`, `Clear`.
  Owned by `ColonistBrain` (exposed as `OffDutyCompletionHistory`); no new MonoBehaviour.
- Cooldown is recorded **only** when `actualActiveOffDutyGameHours >= plannedDurationGameHours`,
  inside the `planned_duration_complete` branch of `RequestOffDutyStop`. Discovery, reservation,
  walking, entry, becoming active, and early interruption do not record anything.
- The completion/interruption event now also carries `drive`, `cooldownKey`, and `cooldownUntil`.

### S1-3 — Drive-aware OffDuty discovery and brain policy

- New `OffDutyDrive { Stimulation, Relaxation }` (deliberately no Active/Passive).
- `OffDutyQuery` extended with `Seeker`, `DesiredDrive` (nullable), `CompletionHistory`,
  `CurrentAbsoluteGameHour`; the 2-argument constructor is retained for inspection/legacy callers and
  means "no drive constraint".
- `OffDutyManager.TryFindOpportunity(query, out opportunity, out OffDutySearchReport)` filters by
  `enabled`, `configured`, `externally requestable`, capacity/reservation, duration, staff (unchanged
  semantics, still requiring coverage for the planned duration), **not on cooldown**, and
  **satisfies the requested drive**. Ranking stays nearest-first with the existing deterministic
  tie-break.
- `OffDutySearchReport.NoTargetReason` reports `cooldown` only when *every* drive-matching activity
  was excluded by cooldown; otherwise `no_fitting_opportunity`. No false precision is invented.
- `ColonistBrain.TryStartOffDuty` now: returns without seeking when neither drive is above threshold
  (logged `colonist.decision.blocked` / `discretionary_needs_satisfied`), selects the drive with the
  larger normalized pressure `need / threshold` (deterministic tie break: Stimulation), and falls back
  to the other drive only when it is itself above its own threshold.
- Selection and completion logs carry `drive`, `need`, `threshold`, `activityId`, `cooldownKey`,
  `plannedDuration`, `activeDuration`, `cooldownUntil`.

### S1-4 — Food service staffing policy

- `FoodSelfServicePolicy { None, AssignedWorkers, Everyone }` and
  `FoodServiceAccessMode { Unavailable, Public, PublicStaffed, SelfService }`.
- `FoodServiceComponent` gained `requiresStaff`, `requiredWorkplace`, `requiredRole`,
  `minimumActiveWorkers` (min 1), `selfServicePolicy`, with properties of the same names.
- `IsConfigured` is **structural only** and validates the staffed-authoring requirements without
  consulting live worker presence. Runtime availability is `HasActivePublicStaff` /
  `EvaluateAccess(requester, gameHour)`.
- Public staffing uses the existing `WorkforceManager.HasEnoughActiveWorkers(...)` with
  `minimumRemainingHours = 0` — a food worker must be physically Working *now*, but does not need a
  full meal-length shift remaining. Recreation keeps its coverage-for-planned-duration semantics.
- Self-service `AssignedWorkers` compares the requester's workforce assignment workplace **and** role
  and deliberately does not require a current shift. `Everyone` supports future communal kitchens.
- No simultaneous magical Work + Eat: the existing lifecycle is untouched (critical hunger requests a
  physical Work exit; the worker then eats and returns).

### S1-5 — Requester-aware food discovery

- New `FoodQuery { Seeker, SeekerPosition, CurrentGameHour }` and
  `FoodServiceOpportunity { Target, Service, AccessMode }`.
- Canonical API: `TryFindFoodService(FoodQuery, out FoodServiceOpportunity)`,
  `TryFindFoodTarget(FoodQuery, out ActivityTarget)`, `TryFindFoodService(FoodQuery, out
  FoodServiceComponent)`. Compatibility overloads taking a bare `Vector3` remain and now delegate to
  an anonymous query (seeker `null`, which can only ever match public/`PublicStaffed` access).
- `ColonistTargetResolver` Eat routing now passes identity, position, and sim time. No personal Eat
  assignment class was introduced.
- Discovery excludes disabled/misconfigured services, a reserved `Eat` reservation group, and services
  the requester cannot currently access; it still never reserves.
- Logging is per seeker: signatures are keyed by seeker + service + access mode, so two colonists
  cannot suppress each other's `food.target_selected` / `food.no_target` records, while a repeated
  identical request from the same seeker is still deduplicated. The requester is the primary log
  subject; `accessMode` is logged.
- Lifecycle split: `EatSeeking` re-checks eligibility every tick and stops with
  `food_access_lost`/`food_target_invalid`; `Eating` checks only structural validity.
  `EffectiveHungerPerGameHour` is unchanged and does not consult staffing, so a served meal continues.

### S1-6 — Cafeteria authoring

- Created `Assets/GameData/Jobs/CafeteriaWorker.asset` (`stableId = cafeteria_worker`,
  `displayName = Cafeteria Worker`) following the existing `Farmer.asset` pattern.
- The colonist inspector debug surface (`ColonistBrainEditor`, extended rather than replaced) now
  shows Stimulation/Relaxation need, baseline and effective rates, threshold, Needs flags, Preferred
  OffDuty Drive, Active OffDuty Drive, per-key cooldown remaining, and the resolved Eat target with its
  current `AccessMode`, `Requires Staff` and `Self Service Policy`. It reads the canonical authorities
  and keeps no serialized debug mirrors.
- Scene/prefab wiring (ServeFood activity, workplace role, FoodService staffing fields, `play`
  recovery values) was **not** performed on disk because the human is editing a scene in the same open
  project; see the checklist below.
- A minimal Alice fixture was **not** added: the canonical colonist housing/sleep architecture would
  have needed kludging, which the packet forbids here. Stack 2 owns the multi-colonist fixture.

### S1-7 — Documentation

- `LEISURE_AND_FOOD_ACCESS.md` documents the leisure model, the food-access separation
  (configuration / availability / requester access / meal continuation), the Cafeteria worked
  examples (including critical hunger during food-service work and the un-ejected diner), and the
  component-ownership rules.

## Tests added / changed (not executed)

`ColonistStatsTests` (+16): baseline accumulation of both drives; clamp at zero; may exceed
threshold; authored baselines and thresholds; NaN/Infinity rejection for adjustments and effective
rates; threshold-transition logging entries/exits only; active play reduces Stimulation; active
relaxing activity reduces Relaxation only; both-drive activity affects both; walking, exiting,
disabled activity and non-OffDuty activities all use baseline rates.

`ColonistBrainTests` (+9): no unmet drive means no OffDuty request; stimulation drive selects a
stimulating activity; relaxation drive excludes a stimulating-only activity; higher normalized
pressure is tried first; a cooling primary drive falls back to the secondary drive; completed activity
starts the cooldown and is not immediately re-selected (reason `cooldown`); interrupted activity does
not start a cooldown; `EatSeeking` stops when public access is lost (reason `food_access_lost`);
`Eating` continues when public staffing is unavailable while the counter is closed to new diners.

`OffDutyManagerTests` (+6): stimulating-only activity does not satisfy the relaxation drive;
both-drive activity is eligible for either; cooling activity excluded then eligible after expiry;
different cooldown key remains eligible; search report explains a cooldown-blocked search; search
report falls back to the aggregate reason under mixed exclusions.

`OffDutyCompletionHistoryTests` (new, 9): completion starts the cooldown; expiry after the authored
duration; different keys do not share; zero/invalid cooldown never blocks; repeated completion
replaces the timestamp; invalid keys/hours ignored; keys trimmed; `cooldownUntil` reported; `Clear`.

`FoodServiceAccessTests` (new, 18): unstaffed service is public; assigned-but-not-working worker does
not open the service; walking worker does not; physically working worker does (`public_staffed`);
worker active at the wrong workplace does not; worker active at the wrong activity does not;
`minimumActiveWorkers` respected; assigned employee off shift may self-serve; assignment to another
workplace cannot self-serve; assignment to another role cannot self-serve; unassigned colonist cannot;
`Everyone` allows an outsider; `None` denies; structural configuration survives staffing changes; two
seekers get different access from the same cafeteria at the same time; a worker becoming active makes
it discoverable; requester-aware logging is not globally suppressed.

Totals: **58 new tests** (16 + 9 + 6 + 9 + 18). Pass/fail counts: **not available — the suite could
not be run** (see environment limitation).

## Static verification actually performed

```text
brace/paren/bracket balance check on all 14 changed or new .cs files             -> ALL BALANCED
tab-character check on the same files                                           -> none
git diff --check -- <all packet files>                                          -> clean
grep cross-reference of changed public APIs (Assets + Packages)                  -> only intended callers
grep cross-reference of new OffDutyQuery/TryFindOpportunity callers              -> only intended callers
consumer check for FoodManager/FoodServiceComponent/OffDutyComponent/Stats users  -> no other consumers broken
```

Deliberate compatibility checks: `ColonistTargetResolverTests` builds a resolver on a colonist with no
`ColonistIdentity`, so the Eat path must tolerate a null seeker — it does (anonymous query, public
service still resolves). `FoodManagerTests` uses the retained `Vector3` overloads. `OffDutyManagerTests`
keeps its 2-argument query path through the retained constructor.

## Architecture audit (S1-8)

| Regression searched for | Result |
| --- | --- |
| New leisure MonoBehaviour/component on colonist | Not introduced — drives live in `ColonistStatsComponent`, history is plain C# |
| Brain enum used as authority for stat effects | No — stats read `ColonistActivityRunner` + `OffDutyComponent`; `ColonistBrain.State` is never read by stats |
| `FoodManager` commanding movement | No — discovery only; no motor/nav calls |
| `FoodManager` reserving `Eat` | No — discovery still does not reserve (existing test still asserts this) |
| `FoodService` moving colonists | No |
| `WorkforceManager` moving colonists | No — it only inspects runner activity |
| Availability folded into `IsConfigured` | No — structural config, public staffing and requester access are separate (`IsConfigured`, `HasActivePublicStaff`, `EvaluateAccess`) |
| Eating interrupted merely because the worker left | No — `TickEating` never checks staffing access; covered by a new test |
| Employee self-service requiring a current shift | No — assignment-based, shift-independent; covered by a new test |
| Activity cooldown beginning on reservation/start | No — recorded on completed planned active duration only |
| Cooldown keyed to physical facility instance only | No — keyed by authored semantic `cooldownKey` (falling back to `activityId`) |
| `OffDuty` ignoring Stimulation/Relaxation | No — drive is required by the brain and enforced by `OffDutyManager` |
| `play` still immediately repeating forever | Addressed — drive satisfaction plus 12-hour per-key cooldown; covered by a new test |
| Generic `OpportunityManager` introduced | No |
| Food inventory/resource consumption added prematurely | No — no inventory, economy, recipes, diet, or pricing touched |

No `ColonistWellbeingComponent`, `ColonistLeisureComponent`, `NeedsComponent`, `ServiceManager`,
`OpportunityManager`, Utility AI, or generic intent framework was introduced.

## Scene authoring performed

None on disk (no `.unity`, `.prefab`, or `.mat` file was modified by this packet). One data asset was
authored: `Assets/GameData/Jobs/CafeteriaWorker.asset`.

## Remaining manual verification steps (Unity Editor)

### Wiring checklist

`Bob.unity`

1. Open `Bob.unity` and let Unity import; confirm the new script compiles
   (`OffDutyCompletionHistory.cs`, edited stats/brain/food/off-duty files) with no console errors.
2. **Recreation** object, `OffDutyComponent` → activity `play`:
   - `Cooldown Game Hours = 12`
   - `Cooldown Key = play`
   - `Stimulation Recovery Per Game Hour = 60`
   - `Relaxation Recovery Per Game Hour = 0`
   - leave `Requires Staff` as authored.
3. **Cafeteria** object, `InteractableFacility`: add a worker-facing activity `ServeFood` with its own
   reservation group (`CafeteriaWorker01`) and its own approach anchor, entry/working placement,
   active animation and exit (reuse the least-wrong existing standing/work animation; do not build new
   animation production).
4. **Cafeteria**, `WorkplaceComponent` → add role binding: `Role = Cafeteria Worker`,
   `Activity = ServeFood`, `Maximum Concurrent Scheduled Workers = 1`.
5. **Cafeteria**, `FoodServiceComponent`:
   - `Eat Activity = Eat`, `Hunger Recovery = 60` (preserve the current authored value)
   - `Requires Staff = true`
   - `Required Workplace = Cafeteria` (its own WorkplaceComponent)
   - `Required Role = Cafeteria Worker`
   - `Minimum Active Workers = 1`
   - `Self Service Policy = AssignedWorkers`
6. **WorkforceManager**: assign the colonist (or a future Alice) to the Cafeteria with role
   `Cafeteria Worker` and a valid shift. Note that a worker who becomes critically hungry will leave
   `ServeFood` and the public counter will close while they eat — that is intended.
7. Optional scaffolding for the leisure tests: temporarily author a second OffDuty activity on a
   spare facility with `Cooldown Key = relax`, `Relaxation Recovery = 60`,
   `Stimulation Recovery = 0` (HUMAN TEST C). Do not keep a permanent new facility just for this.
8. Confirm `Colonist_Synty_Male_01.prefab`'s `ColonistStatsComponent` shows the new Stimulation and
   Relaxation values (defaults 0 / 4 / 50 each) and that the prefab edit is intended to be shared.

### Behaviour-change note before testing

With the shipped defaults both leisure drives start at `0` and accumulate at `4 / game-hour` against a
threshold of `50`, so a fresh colonist needs roughly **12.5 game hours** before it wants to seek
recreation at all. Bob will therefore stay Idle (logging
`colonist.decision.blocked` / `discretionary_needs_satisfied`) until a drive crosses its threshold.
This is the intended new gate, not a regression. To observe leisure quickly, raise
`StimulationNeed` (or `RelaxationNeed`) above 50 in the colonist inspector while playing, or raise the
baseline accumulation rate. The default rates are initial tuning values, not final balance.

### Human test script

- **A — Play does not loop forever.** Set `StimulationNeed` above 50, `RelaxationNeed` below 50, no
  hunger/sleep/work interference. Expect: Bob selects `play`, walks, enters, genuinely plays,
  completes the planned duration, physically exits; log shows `drive = stimulation`,
  `offduty.completed`, `cooldownKey = play`, `cooldownUntil`; `play` is not immediately re-selected.
- **B — Drive satisfied.** After Play both needs are below threshold and Bob does not endlessly hunt
  for another recreation activity; Idle is acceptable.
- **C — Competing drive.** With both needs high and a temporary relaxing activity authored, one drive
  is chosen, then the other may dominate afterwards; Bob does not simply re-pick the nearest arbitrary
  recreation forever.
- **D — Staffed cafeteria public access.** With a real second colonist as Cafeteria Worker: before
  they physically reach `ServeFood` the cafeteria is unavailable to Bob; while genuinely Working it
  becomes available and Bob can discover `Eat`; when they physically leave Work it closes to new
  diners. (Until a second colonist exists, `FoodServiceAccessTests` is the acceptance authority for
  this.)
- **E — Alice makes herself a sandwich.** An assigned Cafeteria Worker who is not currently working and
  is Hungry must still discover `Eat` through self-service and physically eat, with no second waiter.
- **F — Critical hunger during food-service work.** A working Cafeteria Worker rising above Critical
  Hunger physically leaves `ServeFood`, the public counter closes, they remain assigned, self-serve
  `Eat`, then return to `ServeFood` if the shift continues, and the counter reopens. The structured
  log must make this sequence readable.
- **G — Existing diner is not ejected.** Bob Eating when the sole worker leaves keeps Eating; the
  cafeteria becomes unavailable to new public diners; Bob finishes normally.

Also confirm after opening the Editor: no `MissingReferenceException`/`NullReferenceException` in the
console, the colonist inspector shows the new debug lines, and `SimulationLogManager` writes
`colonist.need.stimulation` / `colonist.need.relaxation` transitions to the session JSONL.

## Not implemented (explicit non-goals respected)

No food inventory or consumption, no Farm → Cafeteria economy, no recipes, food types, diet,
allergies, preferences, quality or prices, no personality traits or recreation preference scoring, no
addiction, no random leisure choice, no Utility AI, no 4–6 colonist scenario, no reservation queues,
no station prefab/NavMesh modularization, no shuttle travel, no management UI, no save/load. Seams for
those remain (semantic cooldown keys, recovery-rate authoring, `FoodQuery`, access modes).
