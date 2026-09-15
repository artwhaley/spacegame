# P2-T00 — Verify Phase 1 Baseline

## Objective

Establish a known-good Phase 1 baseline before adding Phase 2 functionality.

This ticket should introduce **no intentional gameplay change**.

---

## Required Context

Phase 1 already implements the smallest working colony economy:

```text
Farmers at Command Post
        ↓
Passenger contract
        ↓
Shuttle carries Farmers to Farm
        ↓
Farm produces Food
        ↓
Command Post consumes Food
        ↓
Food freight contract
        ↓
Shuttle carries Food to Command Post
        ↓
Farmers return home
        ↓
rest
        ↓
repeat
```

Pilot 1 operates the Shuttle.
Pilot 2 currently has no useful task.

Phase 2 must extend this without regressing it.

---

## Expected Existing Systems

Inspect the actual project for equivalents of:

```text
SimulationManager
SimulationLog
LocationAnchor
ResourceType
InventoryComponent
ColonistAgent
PopulationManager
TransportContract
ContractManager
LogisticsManager
CommandPostController
FarmController
ShuttleController
```

Do not create duplicates if names differ.

---

## Tasks

### 1. Run the existing scene

Verify the full Phase 1 loop through at least one farmer work/rest cycle.

### 2. Confirm passenger logistics

Observe:

```text
Command Post
→ Farmers board Shuttle
→ Shuttle travels
→ Farmers unload at Farm
```

Later:

```text
Farm
→ Farmers board Shuttle
→ Shuttle travels
→ Farmers unload at Command Post
```

### 3. Confirm Food production

With farmers physically present and working:

```text
Farm Food increases.
```

Without farmers present:

```text
Farm Food does not increase.
```

### 4. Confirm Food consumption

Command Post Food decreases according to the current Phase 1 rule.

### 5. Confirm freight

When Command Post reaches its Food reorder condition:

```text
Food contract is created
→ Farm Food is reserved
→ Shuttle travels to Farm
→ Food moves into Shuttle inventory
→ Shuttle returns
→ Food moves into Command Post inventory
```

### 6. Confirm failure behavior

Disable the Shuttle through the existing runtime switch.

Expected:

- open contracts remain unresolved;
- nothing teleports;
- Farm Food may accumulate;
- Command Post Food declines;
- shortage eventually occurs.

Re-enable Shuttle.

Expected:

- logistics resumes;
- queued work can recover;
- no scene reload required.

### 7. Inspect APIs needed by Phase 2

Record or understand the actual:

- simulation tick registration pattern
- simulation delta-time units
- inventory quantity numeric type
- inventory `Add`/`Remove` behavior
- inventory reservation API
- freight contract creation API
- contract priority behavior
- Shuttle freight loading/unloading behavior
- logical location representation
- simulation logging entry point

---

## Files To Inspect

Likely:

```text
Assets/Scripts/**/SimulationManager.cs
Assets/Scripts/**/SimulationLog.cs
Assets/Scripts/**/InventoryComponent.cs
Assets/Scripts/**/ResourceType.cs
Assets/Scripts/**/TransportContract.cs
Assets/Scripts/**/ContractManager.cs
Assets/Scripts/**/LogisticsManager.cs
Assets/Scripts/**/ShuttleController.cs
Assets/Scripts/**/FarmController.cs
Assets/Scripts/**/CommandPostController.cs
Assets/Scripts/**/ColonistAgent.cs
```

Actual project paths are authoritative.

---

## Guardrails

- Do not implement Ice.
- Do not implement Water.
- Do not implement Mining Ship.
- Do not implement Water Processor.
- Do not refactor Phase 1 for aesthetics.
- Do not rename working public types unless required to fix a real defect.
- If a baseline bug exists, fix only the smallest issue necessary to restore intended Phase 1 behavior.

---

## Acceptance Criteria

Pass only when:

- project compiles;
- no new Console errors appear;
- farmers commute both directions;
- farmers work/rest correctly;
- Food production works;
- Food consumption works;
- Food reservations work;
- Food physically travels through Shuttle inventory;
- Shuttle disable causes causal shortage;
- Shuttle re-enable allows recovery;
- executor understands the actual APIs listed above.

---

## Stop

Do not begin Phase 2 implementation in this ticket.
