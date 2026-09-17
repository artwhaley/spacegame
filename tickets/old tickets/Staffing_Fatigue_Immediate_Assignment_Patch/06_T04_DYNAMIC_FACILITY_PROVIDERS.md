# T04 — Dynamic Facility Performance Providers

Depends on T03.

## Goal

Make facility performance reflect the live enabled provider network, including components added, disabled, re-enabled, or destroyed at runtime.

## Required production changes

In `FacilityPerformanceComponent`:

- remove the permanent provider cache and its `cached` boolean;
- rebuild a local provider buffer on each `EvaluateAt(float gameHour)`;
- gather sibling `MonoBehaviour` instances that implement the provider interface;
- gather `additionalProviders` entries;
- ignore null, destroyed, inactive, or disabled behaviours;
- ignore `this` if it happens to implement the interface;
- deduplicate providers by Unity object identity before evaluation;
- preserve deterministic order: sibling component order first, then additional-provider list order;
- keep the existing multiplication/operational-block semantics.

Do not use a global scene scan. Evaluation is local to the facility and its explicitly linked providers.

## Converter pause/resume contract

Audit `ResourceConverterComponent`:

- disabling it must stop ticking and stop contributing provider effects, if any;
- retain reserved inputs, current batch, elapsed batch progress, and outputs already committed;
- re-enabling resumes the same batch on the next simulation tick;
- do not restart, refund, duplicate, or complete a batch merely because of disable/re-enable;
- destroying a converter follows its existing cancellation/cleanup policy; document that policy in the test name and architecture docs.

If facility performance is disabled, consumers should receive the existing safe/default behavior already defined by the component; do not invent a second cache.

## Essential tests

Add tests proving:

1. a provider added after the first evaluation participates on the next evaluation;
2. disabling a provider removes its multiplier and operational block;
3. re-enabling restores it;
4. destroying it removes it;
5. the same provider referenced as sibling and additional is evaluated once;
6. two providers still multiply effects in deterministic order;
7. a provider on an inactive GameObject is ignored;
8. a converter disabled mid-batch makes no progress while disabled and resumes from the retained progress after re-enable;
9. re-enable does not duplicate input consumption or output.

Lifecycle-sensitive cases belong in PlayMode. Pure effect aggregation can remain EditMode.

## Acceptance gate

- Focused tests pass.
- No permanent provider-cache flag remains.
- Adding or toggling a provider changes the next evaluation without manual refresh calls.
- Converter state survives a disable/re-enable cycle exactly once.

