# Logistics completion execution stack

Status: T01–T11 and C01–C08 source work implemented; generated assemblies compile; Unity acceptance pending. See the [September 26 corrective review](../LOGISTICS_CORRECTIVE_REVIEW_2026-09-26/00_START_HERE.md) and `EXECUTION_LOG.md` before treating this stack as complete.

Purpose: make the current vertical slice's inventory, walking labor, and shuttle logistics reliable before the first player UI. Intended for one implementing agent working sequentially, with small, independently reviewable tickets.

## Start

1. Read `01_LOCKED_RULES.md` and `02_EXECUTION_PROMPT.md`.
2. Read the repository `agents.md`, `ARCHITECTURE_CONSTITUTION.md`, and `TESTING_IN_EXPLORATION_MODE.md`.
3. Execute T00 through T11 in the order below. Do not start by rewriting the whole logistics system.
4. Maintain `EXECUTION_LOG.md`. Use `03_ACCEPTANCE_MATRIX.md` throughout.
5. Finish using `99_REVIEW_PROMPT.md`; report implementation evidence separately from human Unity acceptance.

The owner's correction in this packet supersedes the audit's suggestions to release workers between loads or stop emergency hauling once a small stock buffer has been restored. Playtesting established that those policies leave reserved cargo behind at docks. Assigned final-leg work must finish in full.

## Files and sequence

| Ticket | Deliverable | Depends on |
|---|---|---|
| [T00](T00_BASELINE.md) | Current baseline and evidence plan | Rules |
| [T01](T01_INVENTORY_AUTHORITY.md) | Inventory authority and owned meal consumption | T00 |
| [T02](T02_DEMAND_AND_EXECUTION_CONTRACTS.md) | Demand accounting and a real provider execution seam | T01 |
| [T03](T03_SPLIT_DISPATCH_AND_ROUTES.md) | Multiple shipments/providers and endpoint-aware routes | T02 |
| [T04](T04_WALKING_AND_EMERGENCY_LABOR.md) | Complete assigned walking jobs, emergency pickup, valid retries | T03 |
| [T05](T05_SHUTTLE_PAYLOAD_HANDOFF.md) | Acknowledged, retryable, exactly-once loading/unloading | T04 |
| [T06](T06_SHUTTLE_SCHEDULING.md) | Pilot/berth waits, cancellation, acceptance rollback | T05 |
| [T07](T07_SHARED_CAPACITY_AND_PRODUCTION.md) | Mixed stock targets and correct recipe capacity math | T06 |
| [T08](T08_POLICY_AND_LIFECYCLE.md) | Policy changes, pause, recovery, and release semantics | T07 |
| [T09](T09_INSPECTION_AND_RETENTION.md) | Truthful diagnostics, bounded history, narrow queries | T08 |
| [T10](T10_CONTENT_AND_DOCUMENTATION.md) | Existing scene/content integration and current ownership docs | T09 |
| [T11](T11_FINAL_VERIFICATION.md) | Final verification and outstanding physical acceptance | T10 |

Supporting files: `01_LOCKED_RULES.md`, `02_EXECUTION_PROMPT.md`, `03_ACCEPTANCE_MATRIX.md`, `99_REVIEW_PROMPT.md`, `EXECUTION_LOG.md`.

## Scope boundaries

- Implement the existing walking and shuttle providers. Freighter/drone quantities must be representable through the same capacity/accounting seam; new physical freighter/drone vehicles, flight models, art, and scenes are outside this corrective stack.
- Demonstrate 20 + 10 + 5 + 5 aggregation through a small provider-level accounting/dispatch test with capacity-only collaborators, plus real walking/shuttle acceptance. Do not claim physical drones exist.
- No player UI construction, save/load, ECS, event bus, universal work-order framework, new biological policy, or speculative warehouse optimization.
- Preserve Unity GUIDs, authored scene data, the interaction package's game-agnostic boundary, and the modern/legacy transport boundary.
- Source snapshot: HEAD `ccc329f4` plus pre-existing uncommitted changes and the audit. Re-read current source in T00; never reset to the snapshot.

## Handoff prompt

Paste this into the implementing task:

> Implement `tickets/LOGISTICS_COMPLETION_STACK/02_EXECUTION_PROMPT.md` in this repository. Read its locked rules first. Execute the tickets sequentially, preserve existing work, and maintain the execution log. Finish all available implementation and verification without asking for routine confirmations. In particular, workers finish their entire accepted final-leg quantity across multiple trips, and a demand can be fulfilled by multiple shipments and heterogeneous providers. Do not reintroduce between-load abandonment. Distinguish compilation, tests actually run, and human Unity acceptance still pending.
