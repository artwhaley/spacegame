# T06 — Recover from ordinary pilot, berth, and manifest changes

Dependency: T05. Primary surfaces: shuttle manager scheduling, service trip progression, pilot availability query, necessary voyage/port request-result handling and personnel request cancellation.

## Work

1. Distinguish service capability from current readiness. Planning may discover a route served by a temporarily busy vehicle; dispatch prefers a compatible currently serviceable shuttle. Read-only availability queries must not acquire a pilot lease, move a person, reserve a berth, or claim cargo.
2. Do not let an unstaffed lexically preferred shuttle monopolize work another ready shuttle can serve. When none is ready, retain the request with a truthful wait reason and allow reevaluation.
3. Treat temporary berth occupancy/reservation as retryable waiting. Do not set a permanent Blocked trip for an ordinary rejected departure. Bound retries to logical ticks/meaningful changes, preserve loaded custody, and avoid log spam.
4. Make trip acceptance/reposition rollback symmetric. A failure before commitment leaves neither service.currentTrip nor request.AssignedTrip pointing to a rejected trip. After physical transfer, retain responsibility and use recovery rather than pretending acceptance never happened.
5. Progress by explicit trip/payload state, not dock location alone. If the shuttle starts at the requested destination and waits for a pilot, it must still visit the origin and pick up before destination completion is permitted.
6. Remove or ignore cancelled UNTRANSFERRED requests in a manifest and recompute counts/readiness. Do not cancel physically transferred cargo/passengers; they must reach a safe accepted endpoint. If all untransferred items cancel, release the empty trip safely.
7. Respect separate passenger seats and shared cargo capacity during batching. Validate actual payload counts/amounts, not a request count assumption that could hide multiple payloads. Finish existing pilot responsibility before normal release at home.
8. Keep two-shuttle occupied-berth stalemates truthful. Do not teleport, overbook a dock, or invent a traffic system. A missing spare berth/operational exit is a visible authoring/gameplay blocker; document the necessary spare capacity in acceptance setup.

## Verification

Automated: focused scheduling/state regressions for ready versus unstaffed provider choice, rejected acceptance rollback, cancellation of one unboarded manifest member, and destination-before-pickup. Use small state collaborators where possible; do not simulate flight physics. A12–A15.

Human Unity: delay pilot arrival; occupy then free destination berth; cancel one queued/assigned passenger; use two compatible shuttles with only one staffed. Confirm eventual service, exact custody, and no phantom departure/completion.

## Done

Temporary scarcity remains a wait with a valid future transition. Failed and cancelled requests leave no conflicting service/request ownership.
