# T09 — Make the system inspectable without accumulating old work forever

Dependency: T08. Primary surfaces: freight and shuttle live collections, `SupplyChainDebugLog`, structured event producers, narrow read models and existing inspectors; no player UI.

## Work

1. Separate active obligations from a bounded recent terminal history. Remove terminal work from dispatch/projection scans only after final custody/lease cleanup. Never prune blocked, paused, awaiting-provider, or recovery-required work as if terminal.
2. Clean correlation maps, event subscriptions, and completed execution references consistently. A terminal request still observed by a route runner must remain safely observable; pruning cannot cause a missed arrival or recreate the same logical request. Keep idempotency valid after archival.
3. Expose read-only facts for parent requested/delivered/committed/uncovered; child original/remaining quantity; actual origin/carried/staged custody; current leg/provider; accepted worker remainder and pending release; shuttle manifest/load/unload state; blocker and recovery/retry action.
4. Include waiting-for-capacity/worker/pilot/berth reasons. Avoid a single vague `Blocked` label that forces the UI to infer internal behavior.
5. Emit each economic transfer exactly once. Remove duplicate shuttle transfer events. Replace food-specific event names for generic recipe outputs, and passenger-specific names for generic freight/request transitions. Update actual diagnostic consumers rather than silently breaking them.
6. Report retired/cancelled/partially delivered orders honestly. Do not show all committed stock as assured immediate delivery. Expose final-leg incoming separately if useful, derived from live state.
7. Measure active counts/path-query work during a longer run if practical. Fix history scans now; introduce caching only if measured cost justifies a small, correctly invalidated cache. No speculative scheduler optimization.

## Verification

Automated: focused terminal-history pruning check only if needed to protect live obligations and delayed correlation acknowledgment. Log spelling/count snapshots are generally not durable tests; inspect a transfer trace instead. A05, A20.

Human Unity: inspect one demand through walking→shuttle→walking, a blocked unload, and pending worker release. A player-facing view could explain each state from the queries without private field reads. Run several game days and inspect active/history sizes.

Optional diagnostics: timing/path-query counters may be temporary; record findings and remove instrumentation without an ongoing consumer.

## Done

Long runs do not scan the entire completed economic history on every tick. Diagnostics describe the single runtime authority and do not become a second ledger.
