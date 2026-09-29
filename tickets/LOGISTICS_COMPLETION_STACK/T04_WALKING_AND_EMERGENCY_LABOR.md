# T04 — Make walking labor complete its accepted obligation

Dependency: T03. Primary surfaces: `WalkingFreightWorkService`, `WalkingFreightExecution`, manager report transitions, `WorkExecutionLease`, and only necessary generic brain/runner callers.

## Locked behavior

If an employee accepts 20 staged units with carry capacity 5, they deliver all 20 in four loads. Pending end-of-shift/critical-need release does not abandon the remaining 15 after the first delivery. This comes from owner playtesting and supersedes the audit recommendation to yield after each load.

## Work

1. Fix routine porter carry capacity to read its own setting. Keep producer-assist and emergency capacity independent. Apply legal discrete load sizing.
2. Wait for physical activity release for every borrowed facility worker, including producer assistance. Do not check only `IsEmergency`. Preserve normal exit animation/reservation ownership.
3. Include consumer emergency pickup in initial local demand discovery as well as staged final-leg assignment. Preserve existing eligibility, minimum staff remaining, critical-need exclusion at admission, and dedicated porter preference. Do not modify employment or call in off-duty workers.
4. Record a finite accepted quantity for each execution. Complete it across trips, tracking remaining origin/staged/carried stock consistently. Never enlarge accepted work after a release becomes pending.
5. Normal release requests are deferred until all accepted work is delivered, then honored promptly. Do not resume facility work or accept new freight when release is pending. Without pending release, resume the correct facility activity/mobile duty through the existing paths.
6. Implement legal blocked-to-pickup and blocked-to-delivery retries, preserving route correlation. Temporary failure keeps cargo and obligation owned. Restored reachability resumes without replacing the demand or duplicating claims.
7. If a provider is permanently unusable, expose the explicit recovery state required by T08. Do not implement silent cancellation, cargo deletion, or a timer that releases people while leaving dock reservations behind.

## Verification

Automated: a small execution-owner regression for accepted quantity surviving multiple load reports and a pending release, plus legal retry/stale-report handling. Do not construct an animated fake colonist colony. Routine capacity selection is a small pure rule if worth coverage; no test quota.

Human Unity: A07–A11. No porter, reachable depot, eligible cafeteria employee: employee exits, retrieves all assigned stock, delivers, resumes. Repeat with 20 staged/5 capacity and shift end or critical hunger after first deposit: all 20 must finish; no assigned residual dock reservation remains. Producer assistance visibly completes exit before routing.

## Done

Both porter and emergency paths honor the same complete-obligation rule. Staged cargo is not forgotten to make a worker appear idle. No hunger/fatigue tuning is used to conceal commitment bugs.
