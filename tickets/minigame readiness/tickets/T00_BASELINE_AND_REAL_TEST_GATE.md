# T00 — Establish the Real Baseline

## Goal

Replace static confidence with executed evidence before modifying behavior.

## Scope

- Confirm checkout/revision.
- Confirm Unity version/project opens.
- Confirm EditMode test assembly is discovered.
- Run current EditMode and PlayMode suites in a licensed Unity environment.
- Capture exact totals and failures.
- Resolve only test-infrastructure/discovery issues that prevent execution.
- For behavior failures, classify them; do **not** broadly fix production code in this ticket.

## Specific checks

1. Confirm starting revision is `15fb66ba52ea210d57f593acc4ce4d70ce618a31` or document why implementation begins from a descendant.
2. Open Test Runner and verify the EditMode suite is actually listed.
3. If the EditMode asmdef is not recognized as a test assembly, make the smallest asmdef correction needed and rerun discovery.
4. Run all EditMode tests.
5. Run all PlayMode tests.
6. Specifically inspect the two pilot-duty expectations around release with no active ship operation:
   - if runtime produces `ReturningHome` and this matches the documented semantics, update stale tests;
   - do not change runtime to manufacture `CompletingCommittedWork` when no committed work exists.
7. Capture a baseline report under `docs/readiness/BASELINE_TEST_RESULTS.md`.

## Do not

- Do not fix findings from later tickets.
- Do not change duty architecture beyond a clearly stale assertion.
- Do not tune content.

## Acceptance

- [ ] Test Runner discovers the intended EditMode suite.
- [ ] EditMode totals recorded.
- [ ] PlayMode totals recorded.
- [ ] Every failing test is classified as infrastructure, stale expectation, or real production defect.
- [ ] No unexplained failure remains hidden.
- [ ] Baseline evidence file committed.
