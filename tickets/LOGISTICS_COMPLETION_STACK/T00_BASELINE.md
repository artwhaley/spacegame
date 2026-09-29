# T00 — Establish the real baseline

Dependency: locked rules. Scope: read-only investigation plus this packet's execution log; no production changes.

## Read

- `docs/VERTICAL_SLICE_AUDIT_2026-09-25.md`, including its owner correction notice.
- `Assets/Scripts/ColonyPrototype/Logistics/P4b/` and `Vehicles/ShuttleTransportContracts.cs`, `ShuttleServiceComponent.cs`, `ShuttlePilotWorkService.cs`.
- `Economy/InventoryComponent.cs`, `Production/ResourceConverterComponent.cs`, `Workforce/WorkExecutionLease.cs`, and the brain's lease handling, under the same runtime root.
- Existing freight/inventory tests and current scene authoring for `bobandfriends_modular` / `bobandfriends_commute`.

## Work

1. Record HEAD, branch, dirty/untracked paths, and existing user changes. Audit snapshot `ccc329f4` is orientation, not a reset target.
2. Map live owners and current signatures to the packet. Identify which audit defects remain in current source; if already fixed, record evidence instead of duplicating a fix.
3. Confirm where the modern playable scenes and authoring commands live. Identify legacy transport components that must remain inactive there. Do not modify scene YAML to manufacture acceptance.
4. Attempt builds of Runtime, Editor, EditMode Tests, and PlayMode Tests generated projects. Record source-inclusion limitations and compile failures, including baseline failures.
5. Identify an available Unity Test Runner path and Play Mode access. Do not launch a competing editor against an already open project. Record what can actually be executed.
6. Record the validation path for acceptance A01–A20. Copy no historical PASS into this run.

## Verification

Automated: no new permanent test. Baseline builds and existing directly relevant test execution, if available, with exact results.

Human Unity: identify scene and setup steps; no need to run every scenario before fixing source-confirmed defects.

Optional diagnostics: short baseline log only; no new telemetry framework.

## Done

The next ticket has current source evidence, a preserved dirty baseline, and honest verification capabilities. Missing Unity automation is recorded, not treated as a reason to abandon independent implementation.
