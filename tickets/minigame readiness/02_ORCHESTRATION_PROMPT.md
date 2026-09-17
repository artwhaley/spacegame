# Orchestration Prompt

You are implementing a narrowly-scoped readiness remediation for this Unity repository:

- Repository: https://github.com/artwhaley/spacegame
- Starting oracle baseline: `15fb66ba52ea210d57f593acc4ce4d70ce618a31`
- Execution packet: this directory
- Primary product intent: a small playable space-colony management slice with visible workers, shuttle/mining logistics, facility management, docking, and physicalized inventory.

Read `01_SYNTHESIS.md` and `03_ACCEPTANCE_MATRIX.md` before touching code.

## Operating rules

1. Execute `tickets/T00` through `tickets/T14` **strictly in order**.
2. Treat the current MonoBehaviour architecture as intentional. Do not propose or implement a pure-C# backend, global ECS, event bus, generalized work-order abstraction, or speculative framework.
3. Make the smallest code change that satisfies each ticket.
4. Preserve serialized scene/content compatibility unless a ticket explicitly requires a migration. When serialized fields must change, provide safe defaults/migration behavior.
5. Do not conflate:
   - employment with temporary pilot responsibility,
   - logical arrival with visual interpolation,
   - pause with cancel,
   - transport obligation with carrier occupancy,
   - inventory state with inventory presentation.
6. Do not alter gameplay policy unless a ticket explicitly requires content tuning. In particular:
   - no automatic replacement staffing,
   - no call-ins,
   - no predictive commute,
   - no rerouting accepted flights,
   - no weakening pilot fatigue while completing/returning from accepted work.
7. When a ticket asks for a test, add only the behavior-protecting test(s) named or their closest minimal equivalent.
8. At the end of each ticket:
   - compile the affected assemblies;
   - run the narrowest relevant tests available;
   - record exact files changed;
   - record any deliberate deviation and the reason;
   - commit with a ticket-specific commit message.
9. Do not silently roll unresolved behavior into a later ticket. If a ticket exposes an unexpected prerequisite, fix it only if it is tightly local; otherwise document the dependency and stop that ticket.
10. Existing external review notes are evidence, not commandments. `01_SYNTHESIS.md` is the merged oracle for scope and priority.

## Required ticket report format

For every ticket return:

- Ticket ID / title
- Revision before
- Files changed
- Exact behavior changed
- Tests added/changed
- Commands/tests run + results
- Acceptance criteria: PASS/FAIL line by line
- Any remaining risk
- Commit SHA

Then continue to the next ticket.

## Completion

After T14 is green and pushed:
- stop implementation;
- provide the final SHA;
- do **not** claim independent acceptance;
- instruct the operator to run `99_FINAL_ACCEPTANCE_GATE_PROMPT.md` with a higher-capability reviewing agent against that SHA.
