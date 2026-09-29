# C05 — Protect intermediate inventory bindings during live work

Priority: P2. Corrects T08. Status: implemented in source; Unity acceptance pending.

## Reproduction and source trace

Use Farm → Dock A → Dock B → Cafeteria. After cargo is reserved/staged at Dock B, call its public `TrySetBindings` with another otherwise-valid inventory.

The binding command asks `HasLiveJobsForStock` before changing inventory (stock lines 188–210). The manager checks only `job.Source` and `job.Destination` (1253–1264); Dock B is neither. The command succeeds. The reservation still belongs to Dock B's old inventory, but pickup resolves the origin through its newly bound inventory (manager lines 701–707). Final pickup fails and blocks the retained obligation. The endpoint's staging inventory can also disagree with the new stock binding.

Primary sources: [LogisticsStockComponent.cs](../../Assets/Scripts/ColonyPrototype/Logistics/P4b/LogisticsStockComponent.cs), [FreightLogisticsManager.cs](../../Assets/Scripts/ColonyPrototype/Logistics/P4b/FreightLogisticsManager.cs), [ShuttleTransferEndpoint.cs](../../Assets/Scripts/ColonyPrototype/Vehicles/ShuttleTransferEndpoint.cs).

## Correction

- Include intermediate origins/destinations and live custody dependencies when deciding whether a stock participates in accepted work. A conservative guard across the live route is sufficient; avoid a new migration mechanism.
- Reject incompatible inventory rebinding atomically while those obligations are live. Leave inventory, anchor, policies, claims and route references unchanged on rejection.
- Audit the immediately related endpoint `Configure` command so changing depot/staging bindings cannot bypass the same live-route rule. Keep stock and endpoint physical inventory identity coherent.
- Permit normal idle authoring/rebinding and no-op configuration. Do not require approval flows for valid commands or scan all scene objects every tick.

## Acceptance

Use a narrow manager/stock command regression with a route containing an intermediate dock and a real owned claim. Rebinding that dock must fail without changing either owner; repeat while cargo is traveling toward it, before it has stock. Rebinding an unrelated idle stock remains allowed. Cover the endpoint bypass if its public configuration remains callable at runtime.

In Unity, attempt the guarded command while a dock has accepted cargo, then allow the original route to finish. Every assigned unit must still reach the consumer. Once no live route depends on the dock, a valid consistent rebind should succeed. No cargo migration, teleporting, or release of the worker's remaining assignment.
