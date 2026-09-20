# T08 — Packet Acceptance

Run after T07. This is a read-and-verify ticket; fix only what the checklist finds and
report every fix.

## Structural
- [ ] `StaffingManager.cs` ≤ 250 lines; no file in `People/Staffing/` or
      `World/Transit/` exceeds 300.
- [ ] `grep -rn "SetDutyState" Assets/Scripts` → writes only in `ColonistReconciler.cs`
      and `PilotDutyReconciler.cs`.
- [ ] `grep -rn "FindObjectsByType" Assets/Scripts` → only inside `Awake`/discovery
      methods; none in any `SimulationTick`.
- [ ] No `Update()` in any new file.
- [ ] No new `ScriptableObject`, no Presentation/UI code, no NavMesh references.
- [ ] Every public member `StaffingManager` had at baseline still exists with the same
      signature (compare against `T00_CHARACTERIZATION.md`).

## Behavioral (fresh Play Mode, `SpaceSim.unity`, 72 game-hours at 10×)
- [ ] Farm staff commute by `Walking`; zero passenger contracts CommandPod↔Farm.
- [ ] Water Processor staff commute by shuttle; passenger contracts exist.
- [ ] Pilot 3 duty event sequence matches the T04 baseline capture.
- [ ] Toggling the corridor off/on at runtime produces Blocked → shuttle → Walking
      transitions with no console errors and no colonist stuck in transit forever.
- [ ] Removing the Shuttle with the corridor off produces `Blocked` with readable
      `DutyBlocker`; restoring the Shuttle clears it within one shift.
- [ ] Fatigue is unchanged for any colonist while `Walking`.

## Documentation
- [ ] `STAFFING_ARCHITECTURE.md` and `CONTENT_AUTHORING.md` describe corridors vs
      shuttle service and how to author a link.
- [ ] `ARCHITECTURE.md` tick-order list includes `PedestrianTransitComponent` at 400.

## Report
Write `tickets/P0-A_Foundation/ACCEPTANCE_REPORT.md`: each checkbox with evidence
(line counts, grep output, history excerpts). Unchecked items name the owning ticket.
