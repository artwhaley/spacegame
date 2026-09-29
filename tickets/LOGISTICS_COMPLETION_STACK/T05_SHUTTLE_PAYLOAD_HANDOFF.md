# T05 — Make shuttle payload transfer acknowledged and retryable

Dependency: T04. Primary surfaces: `IShuttleTransportPayload` and request/trip state in `Vehicles/ShuttleTransportContracts.cs`, `ShuttleServiceComponent`, `ShuttleFreightExecution`, manager report transitions.

## Work

1. Replace the void unloading callback with an explicit acknowledged outcome appropriate to the existing seam. Distinguish completed transfer, retryable wait, and recovery-required failure. Update all callers together.
2. Arrival at a dock is not transfer completion. If the inventory refuses cargo, preserve its reservation aboard, keep the relevant request/trip active, report the reason, and retry unloading from a legal freight state when space becomes available.
3. A successful destination transfer acknowledges exactly once. Repeated arrival/tick callbacks cannot move stock, count delivered, advance the leg, or release execution twice.
4. During manifest loading, payloads already physically transferred count as loaded. Do not call pickup again while waiting for another payload. Failed untransferred items must not undo valid loaded custody.
5. Track per-request completion so a mixed manifest can unload successful items once while another waits. Do not make the shuttle available for unrelated work while it still owns undelivered cargo or passengers awaiting disembarkation from that trip.
6. Physical passenger arrival remains acknowledged after successful disembark/navigation restoration, not just vessel dock arrival. Keep the existing boarding custody/restoration behavior and pilot-home release rule.
7. Prevent partial final inventory transfers from leaving accounting stale. Either preflight an all-or-nothing load transfer, or account for the accepted portion exactly and retry only the remainder. Use inventory-owned operations; do not compensate through ad hoc create/remove calls.

## Verification

Automated: a narrow acknowledgment regression: rejected unload leaves transport active and cargo unchanged; retry after capacity returns deposits once; duplicate acknowledgment is inert. Another small manifest case may share the same fixture to cover an already-loaded payload and one pending item. These protect hard-to-see custody invariants. A05, A12.

Human Unity: fill staging after dispatch; observe loaded shuttle waiting at destination; free room; verify exact deposit and onward final leg. Repeat with passenger plus freight. Confirm release only after all obligations in the trip resolve.

Optional diagnostics: structured transfer events containing requested/accepted/remaining quantities and request/allocation/leg IDs; no duplicate economic events.

## Done

No request reports Completed merely because its arrival callback ran. Freight retry and transport retry agree on who owns the remaining cargo.
