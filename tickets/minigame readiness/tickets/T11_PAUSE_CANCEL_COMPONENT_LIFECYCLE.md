# T11 — Explicit Pause/Cancel/Suspend Semantics Across Active Operations

## Goal

Make live component toggling coherent for the current production/logistics loop.

## Establish explicit semantics

For each:
- `ResourceConverterComponent`
- `ExtractionMissionController`
- `TransportExecutorComponent` / ship crew staffing
- `ResourceStockPolicyComponent`
- relevant manager replacement paths

document and implement whether `OnDisable` means:
- pause/suspend;
- cancel/unwind;
- or unregister/recreate derived demand state.

## Required behaviors

### Converter
- disable pauses active work;
- serialize/preserve `batchActive` and `batchProgress`;
- batch inputs already consumed cannot simply vanish across reconstruction;
- recipe switch during active batch is rejected with a reason (smallest honest policy).

### Extraction / transport
- disabled operation cannot advance;
- it also cannot pretend progress is being made;
- stalled/paused inbound freight must not indefinitely mark a demand "Satisfied";
- movement lease remains coherent and recoverable;
- re-enable resumes if pause semantics apply.

### Stock policy
- disabling in either manager/component order cannot leave orphan active demands/supplies;
- local registration IDs are cleared/reconciled safely.

### Cancellation
- explicit cancel is separate from pause and must unwind reservations/ownership.

## Tests

One focused lifecycle test per subsystem:
- converter disable/re-enable mid-batch;
- extraction disable mid-trip;
- transport/crew disable mid-freight with planning state visible as stalled;
- stock policy manager/component disable in both orders.

## Acceptance

- [ ] Every current subsystem has documented disable semantics.
- [ ] Pause never silently becomes success.
- [ ] Pause never silently becomes permanent "satisfied" inbound.
- [ ] Explicit cancellation unwinds resources/leases.
