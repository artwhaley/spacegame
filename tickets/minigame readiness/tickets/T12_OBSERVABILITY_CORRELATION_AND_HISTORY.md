# T12 — Durable Transition Evidence and Correlation

## Goal

Be able to explain a 3–6 day run after Play Mode ends without tick-spam logging.

## Add correlation

1. Preserve stable contract IDs across manager reconstruction:
   - serialize counters or derive `max(existing)+1`.
2. Do the same for demand/supply IDs where history can coexist with manager reconstruction.
3. Add a duty/operation correlation ID or equivalent.
4. Record current/most-relevant contract/demand/mission identity on pilot duty history when appropriate.

## Durable transition output

Implement the smallest dev-only durable artifact:
- append-only JSONL, or
- explicit "dump simulation history" file.

Record state transitions only:
- assignment applied/rejected;
- duty start;
- release requested;
- duty end + reasons/fatigue;
- contract created/assigned/completed/cancelled;
- movement lease acquire/release;
- depart/dock;
- colonist transit start/arrival;
- facility operational<->blocked;
- demand status changes including stalled;
- shortage start/resolution;
- reservation creation/release if needed to explain conservation;
- exhaustion latch set/clear.

Do not log every tick.

## Hot history

If completed contract history is scanned in hot paths, maintain a separate active-contract collection/index while preserving history for post-mortem inspection.

## Acceptance

Run 3 game days, exit Play Mode, and use only the exported artifact to answer:
- why Pilot 1 stopped work;
- which contract/mission they were on;
- why Shuttle 1 was unavailable at a chosen timestamp;
- when Farm became blocked/unblocked;
- whether any freight reservation was stranded.

All answers must be reconstructable without relying on the live Inspector.
