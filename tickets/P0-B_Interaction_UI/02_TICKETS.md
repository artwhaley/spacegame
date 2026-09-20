# P0-B Tickets

Each ticket: read `00_README_FIRST.md` and the referenced `01_LOCKED_DESIGN.md` section.

---

## U00 — Camera and selection (§1, §2)
**Must create:** `Presentation/Camera/OrbitPanZoomCamera.cs`, `Presentation/Selection/SelectableMarker.cs`,
`Presentation/Selection/SelectionHighlight.cs`, `UI/Selection/SelectionModel.cs`, `UI/Selection/SelectionRaycaster.cs`,
`Camera` action map in `InputSystem_Actions.inputactions`.
**May modify:** ship prefabs (add root collider + marker), module prefabs (marker), `Managers.unity` camera.
**Forbidden:** any Runtime change.
**Observable:** fly the camera; click module/colonist/ship/asteroid → highlight and `SelectionModel.Kind` correct; press F → camera eases to it; a moving ship stays selectable.

## U01 — UI shell, time controls, alerts (§3)
**Must create:** `UI.unity` content (`UiRoot`, `PanelSettings`, `Theme.uss`, `Hud.uxml`, `Alerts.uxml`), `UI/UiRoot.cs`, `UI/HudView.cs`, `UI/AlertsView.cs`, `UI/UiText.cs`.
**May modify:** `Core/ReadinessHistory.cs` — add `HistoryEvent` and `OnRecorded` only.
**Observable:** pause/speed from HUD changes sim pace; `Day 2 · 08:00` matches `SimulationTime`; a toast appears on `ship.docked`; clicking it selects the ship.

## U02 — Reporting read models (§4)
**Must create:** `Reporting/ResourceFlowLedger.cs`, `Reporting/BlockedTimeLedger.cs`, `Reporting/DutyReportBuilder.cs`, `Reporting/ReportTypes.cs`, `Assets/Tests/EditMode/ReportingTests.cs`.
**May modify:** `Managers.unity` (add components).
**Tests:** flow ledger sums Add/Remove into the right hour bucket and rolls off after 24h; blocked ledger opens/closes intervals and reports open interval hours up to "now"; duty builder produces the sentence format exactly for a fixture record.
**Forbidden:** any write to a sim component; per-tick allocation beyond bucket rotation.
**Observable:** Inspector on the ledgers shows Farm Food out/hour and Shuttle Holding hours after a 24h run.

## U03 — Facility panel (§5)
**Must create:** `UI/Panels/FacilityPanel.cs` + UXML. **May modify:** `UiRoot` routing.
**Observable:** select Farm → inventory with flows, recipe progress and blocked reason, staffing rows, policy toggles that change `ResourceStockPolicyComponent` mode and show the result.

## U04 — Ship panel (§5)
**Must create:** `UI/Panels/ShipPanel.cs` + UXML.
**Observable:** phase updates through a voyage; queue position while Holding; cancel contract shows result text and the contract reopens in `ContractManager.Contracts`.

## U05 — Colonist, deposit, port panels (§5)
**Must create:** `UI/Panels/ColonistPanel.cs`, `DepositPanel.cs`, `PortPanel.cs` + UXML.
**Observable:** select a worker → duty state, fatigue, employment, five plain-English duty lines; select the ice asteroid → remaining Ice.

## U06 — HR screen v1 (§6)
**Must create:** `UI/HR/HrScreen.cs`, `HrColonistTable.cs`, `HrWorkplaceTree.cs`, `HrCandidates.cs` + UXML.
**Forbidden:** targets/priority/pin columns (P0-C adds them); any assignment logic outside `StaffingManager.Assign/Unassign`.
**Observable:** pick Farm › Farm Operator › Shift B → candidates list shows every colonist with a reason (`RejectedMissingClass`, `RejectedAtCapacity`…); Assign an eligible one → status line `Applied`; they commute at 08:00.

## U07 — Colony report and alert rules (§7)
**Must create:** `UI/Report/ColonyReportScreen.cs` + section views + UXML, `UI/Alerts/AlertRules.cs`.
**Observable:** after a 48h run with the Water Processor pilot unassigned, the report reads `"Water Processor — no eligible pilot for Shuttle shift B — 6.5h blocked (last Day 2 15:40)"` and an alert `Water: 9h until empty` is shown.

## U08 — Acceptance
- [ ] `UI` asmdef referenced by nothing; Runtime has no `using` of UI/Presentation.
- [ ] `grep -rn "= " Assets/Scripts/ColonyPrototype.UI | grep -v "local\|var\|const"` reviewed: no Runtime field writes.
- [ ] No panel computes an aggregate; all numbers trace to a ledger or a component accessor.
- [ ] Panels rebuild ≤ 4 Hz; no per-frame allocation spikes in the Profiler.
- [ ] Every button shows its result enum text.
- [ ] Observable: a fresh 72h run can be explained entirely from the Colony Report without opening the Unity Inspector.
