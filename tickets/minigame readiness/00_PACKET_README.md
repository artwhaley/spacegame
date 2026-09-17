# Spacegame Readiness Remediation — Execution Packet

Repository: https://github.com/artwhaley/spacegame  
Baseline reviewed: `15fb66ba52ea210d57f593acc4ce4d70ce618a31`  
Purpose: close the concrete readiness deficiencies identified by the consolidated adversarial review **without replacing the MonoBehaviour architecture or widening scope into a framework rewrite**.

## How to use this packet

1. Read `01_SYNTHESIS.md` first. It is the product/architecture oracle for this remediation.
2. Start implementation with `02_ORCHESTRATION_PROMPT.md`.
3. Execute `tickets/T00_...` through `tickets/T14_...` **in order**.
4. Use `03_ACCEPTANCE_MATRIX.md` continuously to ensure every finding remains covered.
5. After all implementation tickets are complete and pushed, **do not self-certify the work as finished**. Run the separate prompt in `99_FINAL_ACCEPTANCE_GATE_PROMPT.md` with a higher-capability reviewing agent against the final repository revision.
6. The final gate must compare:
   - the actual implementation,
   - this packet,
   - the original stated gameplay/end-goal effects,
   - and real Unity test / smoke evidence.

## Non-negotiable architecture constraints

- Keep the network of independent Unity `MonoBehaviour`s.
- Preserve the ability to add, disable, adjust, and re-enable components during Play Mode.
- Do **not** introduce a separate pure-C# simulation backend.
- Keep staffing independent from recipes/converters.
- Keep persistent employment separate from the temporary ship responsible-pilot lease.
- Keep explicit person + workplace + role + shift employment.
- Keep no automatic replacement staffing/call-ins.
- Keep unstaffed shifts as intentional downtime.
- Keep flights committed once underway; no predictive acceptance, early commuting, or mid-flight rerouting in this packet.
- Keep extraction outside the normal freight contract model.
- Do not build a general work-order/event-bus/framework layer.
- Do not build full save/load as part of this packet.
- Tests serve the game; do not inflate the test suite with exhaustive permutations or tick-spam assertions.

## Completion definition

Implementation is complete only when:
- all T00–T14 acceptance criteria pass,
- licensed Unity EditMode and PlayMode results are recorded,
- a fresh multi-day smoke artifact exists,
- the implementation-side verification in T14 is green,
- and the **independent final acceptance gate** in `99_FINAL_ACCEPTANCE_GATE_PROMPT.md` finds no unresolved blocker or unaccounted regression.
