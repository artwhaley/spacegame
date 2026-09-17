# Acceptance Correlation Matrix

This matrix links the consolidated synthesis to implementation tickets and the end-goal effect that must be demonstrated.

| Synthesis issue | Primary ticket(s) | End-goal effect |
|---|---|---|
| 1. Real Unity suites | T00, T14 | We know the baseline/final state from executed tests, not static confidence. |
| 2. Disabled staffing ≠ automation | T01 | Live component toggling remains trustworthy. |
| 3. No transform-owned people | T03, T05 | Replacing/disabling ships/facilities cannot remove occupants from simulation. |
| 4. Arrival means arrived | T03, T04 | Walking/docking visuals can become real gameplay state without lies. |
| 5. Missing mover blocks | T04 | Removing movement during experimentation cannot teleport cargo/ships. |
| 6. Transport teardown recovery | T05 | No stranded contracts/reservations/passengers after destroy/disable. |
| 7. Disabled worker still owns slot | T02 | Employment caps remain invariant. |
| 8. Duty phase single owner/truthful | T06 | UI can trust worker/pilot phase labels. |
| 9. Reconcile before fatigue | T07 | No phantom post-shift work; pilot return-work rule preserved. |
| 10. Availability + reason unified | T08 | UI/log diagnostics cannot contradict dispatch truth. |
| 11. Validated UI commands/queries | T10 | Management UI does not become a second authority. |
| 12. Runtime registries | T09 | Colony-wide UI and simulation avoid per-tick scene scans. |
| 13. Pause/cancel semantics | T11 | Component toggles pause/block coherently rather than deadlock/lie. |
| 14. Durable observability | T12 | A multi-day run can be reconstructed after Play Mode. |
| 15. Scene/economy contradictions | T13 | The slice visibly works long enough to evaluate. |
| 16. Inventory racks are views | T10, T13 | Physicalized inventory cannot desync simulation stock. |
| 17. Cheap latent hardening | T02, T11, T12 | Nearby known sharp edges are closed while seams are open. |
| 18. Preserve architecture | ALL | No framework rewrite; development continues directly into the game. |

## Cross-ticket invariants

These must remain true after every ticket:

1. `ResourceConverterComponent` does not count/query workers directly.
2. `StaffingComponent` does not know recipes/resources.
3. `EmploymentAssignment` remains persistent identity of employment.
4. `ShipComponent.ResponsiblePilot` remains temporary operational responsibility.
5. Accepted flights/operations are not rerouted merely because employment changes.
6. Inventory quantity authority stays in `InventoryComponent`.
7. Disabling a component never silently means "operation succeeded."
8. No UI/presentation object owns authoritative simulation quantities.
