# Implementing-agent prompt

Implement this stack in the shared Unity repository. Read `00_START_HERE.md`, `01_LOCKED_RULES.md`, `03_ACCEPTANCE_MATRIX.md`, project agent instructions, and the exploration testing policy before code changes. Then execute T00–T11 sequentially.

## Working method

1. At each ticket, read the current owner and its callers. The audit identifies suspected defects; verify the current implementation rather than patching by line number.
2. Write a short concrete implementation note in `EXECUTION_LOG.md` before a consequential seam change. Use the locked rules to resolve routine choices; do not reopen the owner's final-leg completion decision.
3. Keep every ticket compiling. When a seam signature changes, update its production callers in the same ticket. Small internal value types/helpers are allowed where they serve this behavior; no general framework.
4. Ticket surfaces identify expected ownership, not an approval trap. Necessary adjacent caller/test/migration edits are allowed and must be recorded. Unrelated systems/art/settings remain out of scope.
5. Preserve the dirty baseline. Do not reset, stash away, revert, or overwrite pre-existing work. Do not stage the whole repository. If committing, inspect and stage only the intended task changes; report any inseparable baseline changes honestly. Follow current Git authorization, with no force push.
6. Read the ticket's Verification section before adding tests. Prefer a narrow owner-level test. Do not generate test families, source-text architecture tests, YAML scene tests, or a fake colony. Inspect old assertions before treating failures as requirements.
7. Compile affected projects and run relevant existing Core/Regression tests where available. A successful build or `dotnet test` without actual discovered/executed Unity tests is not a test pass. Generated projects may omit newly added scripts; verify inclusion/Unity compilation where possible.
8. Use actual Play Mode for physical acceptance if access permits. Preserve the user's editor session and existing scene work. If unavailable, complete independent code/checks and record specific pending manual steps; do not falsely mark physical acceptance passed or spend hours fighting licensing.
9. Update the execution log after each ticket, then continue. Do not stop for a routine implementation preference or because the task spans several tickets/context windows. If a genuine product conflict appears beyond the locked rules, state it narrowly and continue unaffected work.
10. Do not spawn agents or create another task merely because this is a handoff packet. This stack is designed for sequential ownership.

## Required ticket evidence

- Status: NOT STARTED / IN PROGRESS / IMPLEMENTED, ACCEPTANCE PENDING / VERIFIED / BLOCKED.
- Starting revision and relevant dirty files.
- Concrete behavior and owner changed; files changed.
- Which acceptance IDs are covered and their observed results.
- Commands, result paths, executed test counts when available; failed checks and their disposition.
- Human Unity results or precise pending steps.
- Remaining limitation, migration, follow-up dependency, and commit SHA if created.

Never use VERIFIED for a physical claim supported only by compilation. Do not record a future planned test as already passing.

## Completion

Use T11 and `99_REVIEW_PROMPT.md` to assess the final tree. Deliver the implementation summary, preserved gameplay rules, verification evidence, and any pending human acceptance. Do not claim the game is physically accepted without the observations. Do not build the UI as an unrequested final flourish.
