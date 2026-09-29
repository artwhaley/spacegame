# T11 — Verify the full stack and leave an honest handoff

Dependency: T10. Scope: final diff/source review, appropriate tests, actual Unity acceptance, documentation/evidence fixes; no new gameplay features.

## Work

1. Review the final tree against every locked rule and acceptance row. Specifically search for any remaining one-load release of an accepted larger final-leg assignment, concrete vehicle assumptions in demand accounting, unconditional transport completion, and terminal paths retaining cargo/worker claims.
2. Recheck current source APIs/callers after all tickets. A passing architecture/property-name test is not proof of the execution seam working. Remove/update obsolete assertions only when the new intended behavior is established.
3. Compile affected runtime/editor/test assemblies with new files included. Run relevant Core and Regression tests and applicable existing UnityIntegration tests. Broaden once for a milestone if practical; do not repeat unchanged successful checks for ritual.
4. Complete the available human scenarios in `03_ACCEPTANCE_MATRIX.md`. At minimum provide precise setup and expected results for any unavailable Unity runs. Record which scenario exercises actual current providers versus provider-only mixed-capacity proof.
5. Check the final diff for accidental unrelated edits, GUID changes, altered production/need rates, debug bypasses, duplicate authorities, unconditional retries, leftover temporary instrumentation, and invented future vehicle implementations.
6. Use `99_REVIEW_PROMPT.md` as a final adversarial review checklist. Fix concrete unresolved failures within this stack, then revalidate the affected evidence.
7. Finish the execution log with per-ticket status and a clear list of unresolved conditions. Summarize current commands/queries the first UI can use, without implementing the UI.

## Verification

Automated: run existing relevant tests plus the small new invariants/regressions justified by the implementation tickets. Record actual execution counts/result paths. Compile-only evidence must be called compile-only.

Human Unity: all physical acceptance rows need observed results before declaring physical acceptance complete. If tools cannot run Unity, implementation can be delivered as IMPLEMENTED, ACCEPTANCE PENDING with the explicit checklist; never fabricate a PASS.

## Final response must include

- What changed and how the two owner corrections are preserved.
- Demand 40 split/repeated-trip and mixed-provider evidence.
- Final-leg 20/5 finish-all evidence, including pending release.
- Inventory/cargo conservation, failed-unload recovery, and cancellation evidence.
- Test/build results, human results or remaining steps, known physical limits, and final revision if committed.
- No claim of physical freighter/drone support unless separately implemented and observed under new authorization.
