# FINAL ACCEPTANCE GATE PROMPT — RUN ONLY AFTER IMPLEMENTATION

**Operator note: run this prompt only after T00–T14 have been implemented, tested, committed, and pushed. Use a higher-capability reviewing agent than the implementation executor. Do not give the reviewer only summaries; give it repository access.**

---

Perform an adversarial, code-grounded final acceptance audit of the completed Spacegame readiness remediation.

Repository: https://github.com/artwhaley/spacegame

Final revision to review:
`<PASTE FINAL IMPLEMENTATION SHA HERE>`

Original review baseline:
`15fb66ba52ea210d57f593acc4ce4d70ce618a31`

You must inspect the actual final source and compare it against the execution packet contents, not merely against commit messages or the implementer's final report.

Read these packet files first:
- `01_SYNTHESIS.md`
- `03_ACCEPTANCE_MATRIX.md`
- every file in `tickets/`
- `docs/readiness/BASELINE_TEST_RESULTS.md`
- `docs/readiness/FINAL_IMPLEMENTATION_EVIDENCE.md`

## Review stance

This is a final gate, not an architecture brainstorming exercise.

The project deliberately uses a network of independent Unity MonoBehaviours because live add/disable/adjust/re-enable experimentation is a design requirement. Do not recommend replacing that with a separate pure-C# simulation backend, ECS, generalized event bus, scheduler framework, universal work-order system, or speculative save architecture.

The question is:

**Did the implementation close the identified readiness deficiencies while preserving the intended architecture and producing the stated gameplay/end-goal effects?**

## Required audit method

1. Confirm exactly which final revision you can inspect.
2. Diff the final revision against the original baseline and map every remediation commit/ticket.
3. Re-read the affected runtime code rather than trusting the diff summary.
4. Inspect tests and the actual recorded Unity test evidence.
5. Inspect `SpaceSim.unity`, relevant prefabs, GameData, and the durable smoke artifact.
6. Distinguish:
   - verified fixed;
   - partially fixed;
   - regression;
   - unverifiable because required evidence is missing.
7. For every failed/partial item give:
   - ticket ID;
   - file/path and line;
   - exact mechanism;
   - concrete reproduction scenario;
   - smallest corrective action.
8. Do not assign points or a numerical score. Give a factual gate result:
   - **Acceptance gate clear**
   - or **Acceptance gate blocked by the following concrete items**.

## Correlated acceptance checks

### A. Live component experimentation

Verify:
- disabling a staffed facility does **not** make it a default automated 1.0 facility;
- workers stop/release appropriately;
- re-enable recovers;
- disabled workers still consume persistent employment capacity;
- stock policy, converter, transport and extraction disable/re-enable follow their documented pause/suspend semantics;
- a paused operation does not claim fake progress or fake satisfied inbound demand.

### B. Colonist physical state

Verify:
- colonist root objects are not simulation-contained by transform parenting to ships/facilities;
- disabling/destroying a ship cannot remove occupants from the population merely through hierarchy inheritance;
- `currentLocation` means arrived location;
- transit has explicit origin/destination/state;
- physical transition completion commits arrival rather than arrival being announced early.

### C. Ship movement/docking

Verify:
- ship state distinguishes Docked/Undocking/InFlight/Docking or an equivalent explicit model;
- logical dock identity is separate from docking pose;
- movement lease still prevents competing transport/extraction/crew-return movement;
- missing/disabled movement cannot be interpreted as arrival;
- only the movement owner can commit hard arrival;
- unloading/boarding cannot occur before docking completion.

### D. Transport recovery

Verify:
- contract manifest and carrier occupancy have documented/non-conflicting authority;
- null/destroyed passengers are pruned/reconciled;
- vehicle destruction releases movement ownership and resolves/cancels/reopens work;
- freight reservations cannot remain stranded on dead contracts;
- surviving passengers cannot remain Passenger forever with no live obligation;
- open commute reassignment may be rebuilt, but accepted/in-flight trips still finish unless the vehicle itself is lost.

### E. Duty/fatigue truth

Verify:
- duty phase has one authoritative writer;
- phases do not contradict active contract destination or operation;
- blocked workers/pilots have an inspectable blocked reason;
- committed work is not mislabeled `AcceptingNewWork`;
- no extra post-shift facility work/fatigue tick exists;
- pilots finishing accepted work/returning to base still accumulate duty time/fatigue;
- suspended disabled operations do not accrue impossible work forever;
- duty end/release reason evidence remains understandable.

### F. Availability/UI authority

Verify:
- vehicle availability boolean and explanation derive from one rule;
- "unavailable [available]" style contradictions are structurally impossible;
- staffing candidate UI can use the exact assignment validator non-mutatingly;
- runtime-authoritative employment/location/passenger/dock/contract state is not intended to be directly mutated by UI;
- no generalized command framework was introduced unnecessarily.

### G. Registries/performance seams

Verify:
- no per-tick/per-colonist ship `FindObjectsByType` hot path remains;
- startup discovery fallback still exists where useful;
- colony-wide UI can enumerate inspectable runtime objects from read-only registries/collections;
- the implementation did not prematurely add stable-ID/save architecture merely to solve in-session selection.

### H. Inventory presentation

Verify:
- `InventoryComponent` remains the sole quantity/reservation authority;
- rack/box visuals are rebuildable read-only projections;
- deleting a visual box cannot change stock;
- event notification, if added, is notification only and not a second authority.

### I. Observability

Using the durable final smoke artifact alone, demonstrate that you can reconstruct:
- one pilot's duty from start through release/end;
- the contract/mission they were servicing;
- one vehicle-unavailable interval and blocker;
- one facility operational→blocked→operational transition;
- one reservation lifecycle;
- one shortage start/resolution if any occurred.

Verify IDs remain unambiguous across manager reconstruction in the scenarios the packet requires.

### J. Playable slice content/scene

Verify from final scene/content plus live evidence:
- Mining Ship logical/visual initial dock agrees;
- intended facilities/ships/asteroid are selectable;
- placeholder scale/anchor conventions are coherent enough for the slice;
- Food/Water loop survives the required 72 game-hours without unavoidable permanent failure;
- at least one passenger commute, one freight trip and one extraction cycle complete;
- current architecture remains content-driven rather than hardcoding these exact scene objects.

### K. Test/acceptance evidence

Verify:
- licensed EditMode totals exist;
- licensed PlayMode totals exist;
- new focused lifecycle/integration tests actually exercise real component chains rather than only manufactured reflection state;
- the 72h smoke is fresh for the final SHA;
- evidence names the final SHA and is not an old run accidentally reused.

## Architecture regression guard

Explicitly report if implementation introduced any of these prohibited regressions:
- separate pure-C# simulation backend;
- universal work-order/action framework;
- automatic vacancy filler/call-ins;
- predictive/early commute logic;
- rerouting of accepted flights merely due to reassignment;
- staffing logic embedded back into recipes;
- rack visuals with authoritative inventory;
- full save/load project;
- broad framework introduced only to satisfy tests.

## Final response format

Start with one of:

`ACCEPTANCE GATE CLEAR`

or

`ACCEPTANCE GATE BLOCKED`

Then provide:

1. **Revision/evidence inspected**
2. **Blocking findings** — only if any
3. **Correlated matrix** — each synthesis item 1–18 marked Verified Fixed / Partial / Regression / Missing Evidence, with direct code/evidence references
4. **End-goal effects check** — explicitly state whether live component experimentation, physical movement, docking, management UI seams, observable logistics, inventory racks, and the 72h colony loop now behave as intended
5. **Non-blocking follow-ups** — only genuine deferrable items
6. **Architecture preservation check**

Do not accept the work because tests pass alone. Do not reject it because implementation differs cosmetically from suggested code shapes. Judge both:
- the concrete specification/invariants;
- and the stated end-goal effect the repair was supposed to produce.
