# T14 — Implementation-Side Verification and Handoff

## Goal

Prove the executor finished the packet, then hand the final SHA to an independent higher-capability reviewer.

## Required automated gates

1. Full EditMode suite.
2. Full PlayMode suite.
3. Any newly added focused integration tests.
4. Compile all relevant Unity assemblies with zero errors.
5. Record exact totals and failures/skips.

## Required live smoke

Run a fresh accelerated `SpaceSim.unity` smoke for at least 72 game-hours and capture durable evidence.

During the smoke deliberately exercise:
- facility staffing disable/re-enable;
- colonist GameObject disable/re-enable while employed;
- converter disable/re-enable;
- stock policy disable/re-enable;
- transport movement component disable/re-enable;
- extraction operation disable/re-enable;
- one reassignment while a commute is open;
- one reassignment while a flight is already committed;
- shift crossing at midnight;
- pilot shift ending during committed work and returning home;
- at least one vehicle unavailable state with UI/log blocker;
- inventory rack rebuild;
- scene selection for each facility/ship/asteroid;
- destroy/recover test in automated PlayMode if destructive live smoke is inconvenient.

## Required invariants

- no orphan reservations;
- no active contract assigned to destroyed vehicle;
- no passenger stuck with no active obligation;
- no capacity overrun after re-enable;
- no staffed facility becoming automatic because its staffing component is disabled;
- no ship teleport due to absent movement;
- no duty phase contradicting active destination/operation;
- no contradictory availability explanation;
- no extra post-shift facility work tick;
- pilot completing/returning from accepted work still accumulates fatigue;
- Food/Water loop survives 72h under tuned content;
- exported trace can explain key transitions.

## Deliverables

Create `docs/readiness/FINAL_IMPLEMENTATION_EVIDENCE.md` containing:
- final SHA;
- test totals;
- smoke configuration;
- ticket-by-ticket commit SHAs;
- evidence artifact paths;
- any known non-blocking follow-ups.

## Stop condition

If any invariant fails, reopen the owning ticket and fix it. Do not mark the packet complete with a known blocker.

When green:
- push final SHA;
- stop implementation;
- tell the operator to run `99_FINAL_ACCEPTANCE_GATE_PROMPT.md` with a higher-capability agent.
