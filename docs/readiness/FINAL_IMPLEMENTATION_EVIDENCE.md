# Minigame Readiness Remediation — Implementation Evidence

This document records the implementation-side handoff. It does not certify the independent acceptance gate.

## Revision and scope

- Baseline: `15fb66ba52ea210d57f593acc4ce4d70ce618a31`
- Unity: `6000.5.9f1`
- Packet executed: T00 through T14 implementation work
- Independent gate: deliberately not run; operator will run `tickets/minigame readiness/99_FINAL_ACCEPTANCE_GATE_PROMPT.md` with Astra.
- Handoff revision: working tree based on `15fb66ba52ea210d57f593acc4ce4d70ce618a31`; no commit was created because this session has read-only `.git` access and Git could not create `.git/index.lock`.

## Implementation coverage

| Ticket | Implementation evidence |
|---|---|
| T00 | Baseline runner report and explicit Unity licensing blockage in `BASELINE_TEST_RESULTS.md`. |
| T01 | Disabled authored staffing publishes a blocker; reconciliation treats the workplace as unavailable; focused disabled-provider test added. |
| T02 | Disabled employment remains in capacity counts; null ship crew base is rejected; startup authored-employment validation emits a durable diagnostic. |
| T03 | Colonists have explicit transit origin/destination/state; arrival commits separately; roots are no longer parented to vehicles. |
| T04 | Ships use Docked/Undocking/InFlight/Docking phases, movement ownership, docking-port seam, authoritative arrival commit, and fail-closed movers. |
| T05 | Passenger carrier occupancy is serialized/read-only, destroyed passengers are pruned, vehicle loss recovers occupants and contracts/reservations. |
| T06 | `StaffingManager` is the sole duty-phase writer; phase storage/helpers and crew-duty no longer manufacture phases. |
| T07 | Ship work/fatigue is reconciled before colonist work consequences; disabled committed operations stop charging fatigue; exhaustion reason is retained. |
| T08 | Vehicle dispatch/UI explanations share `TryGetAvailability` and a stable blocker reason. |
| T09 | Runtime registries cover ships, facilities, inventories, colonists, and vehicles; scene scans are startup/recovery-only. |
| T10 | Assignment candidate validation, narrow commands, pause/speed commands, policy mode commands, and rebuildable inventory rack view are present. |
| T11 | Converter pause/recipe-switch rejection, extraction/transport blocked states, stock-policy unregister/reconcile, and explicit cancellation are present. |
| T12 | Session-scoped append-only JSONL history records duty, assignment, demand, contract, ship, transit, extraction, and blocked transitions. |
| T13 | Scene start position, selectable anchor/deposit collider seams, and food/water content tuning were corrected; rack presentation is a pure inventory projection. |
| T14 | Implementation-side compile/static checks completed; Unity suite and 72-hour smoke are blocked by licensing and are not claimed green. |

## Verification performed

Successful checks:

- `dotnet restore ColonyPrototype.Runtime.csproj --ignore-failed-sources`
- `dotnet build ColonyPrototype.Runtime.csproj --no-restore` with the two new runtime source files included temporarily: 0 errors, 2 existing unresolved Unity UI reference warnings.
- `git diff --check`: no whitespace errors.
- Source audit: no `Transform.SetParent` calls; no per-tick `FindObjectsByType<ShipComponent>` calls; remaining scene discovery is startup/recovery-only.
- Scene audit: Mining Ship 1 transform is `{x: 0, y: 0, z: 0}` and its authored initial dock is the Command Post; Water Processor remains at `{x: 0, y: 0, z: 10}`.

Unavailable checks:

- Full Unity EditMode result totals: no result XML because `LicenseClient-artwh` IPC refused connection.
- Full Unity PlayMode result totals: same licensing failure.
- Fresh accelerated 72-hour PlayMode smoke and exported trace: not run because the same licensing failure prevents Unity startup.

## Focused tests added or updated

- `Assets/Tests/EditMode/ReadinessRemediationTests.cs`
- `Assets/Tests/EditMode/PilotClassEligibilityTests.cs`
- `Assets/Tests/EditMode/PilotDutyTests.cs`
- `Assets/Tests/EditMode/ColonistStatusTests.cs`
- `Assets/Tests/EditMode/UnifiedDispatchTests.cs`
- `Assets/Tests/EditMode/TransportExecutorTests.cs`

The focused tests are source-complete but could not be executed in this environment. The Unity logs above are the authoritative reason.

## Known follow-up

Run the full licensed Unity suites, the 72-hour smoke, and then the independent Astra gate. Any failure there reopens the owning ticket; this document intentionally does not self-certify those gates.

The packet requested ticket-specific commits, but staging failed with `Permission denied` while creating `.git/index.lock`. The implementation files remain in the working tree for the operator to stage/commit in a writable checkout.
