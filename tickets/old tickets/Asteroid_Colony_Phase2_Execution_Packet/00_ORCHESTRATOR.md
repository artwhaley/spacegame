# Asteroid Colony Prototype — Phase 2 Execution Packet

**Unity:** 6000.5.9f1  
**Preset:** HD URP  
**Prerequisite:** Phase 1 is complete and manually tested.  
**Purpose:** Extend the working Phase 1 colony loop upstream with finite Ice extraction, a dedicated Mining Ship, Ice→Water processing, and Water consumption by both the Farm and Command Post.

---

## 1. Phase 2 Target Loop

Phase 1 already proves:

```text
Farmers
  ↓ passenger transport
Farm
  ↓ Food
Shuttle
  ↓
Command Post
```

Phase 2 extends that to:

```text
Finite Ice Asteroid
        ↓
Mining Ship + Pilot 2
        ↓ Ice
Water Processor
        ↓ Water
      ↙         ↘
   Farm       Command Post
     ↓             ↓
    Food       Consumption
     ↓
Command Post
```

The physical-location rule remains authoritative:

> Resources and people do not teleport. If a resource moves between facilities, it must be physically present in the source, then in the carrying vehicle, then in the destination.

---

## 2. Existing Phase 1 World

Current scene concept:

```text
Managers

Command Post
Farm
Shuttle

Bridge Crew 1
Bridge Crew 2
Bridge Crew 3
Pilot 1
Pilot 2
Maintenance
Farmer 1
Farmer 2
```

Current known Phase 1 behavior:

- 8 colonists live at the Command Post.
- Pilot 1 operates the Shuttle.
- Pilot 2 currently has no useful task.
- Farmers commute physically between Command Post and Farm.
- Farm creates Food while farmers are physically present and working.
- Command Post consumes Food.
- Command Post generates Food freight demand.
- Shuttle physically carries Food.
- Resource reservations prevent double-spending.
- Disabling the Shuttle causes causal downstream failure rather than teleportation.

The actual implementation is authoritative. Ticket text uses expected class names from the Phase 1 spec, but the executor must inspect the project and modify the existing equivalent instead of duplicating systems.

---

## 3. Phase 2 New Objects

Add:

```text
Ice Asteroid 1
Water Processor
Mining Ship 1
```

Add resources:

```text
Ice
Water
```

Pilot allocation for this phase:

```text
Pilot 1 → Shuttle 1
Pilot 2 → Mining Ship 1
```

No pilot shifts or crew scheduling in Phase 2.

---

## 4. Hard Global Guardrails

Do **not** add any of the following during this packet:

- construction
- repairs
- power
- oxygen
- fuel
- money
- markets
- trade
- immigration
- population growth
- new colonists
- job allocation UI
- pilot fatigue
- pilot shifts
- crew relief
- generalized crew scheduler
- orbital mechanics
- asteroid movement
- exploration
- scanning
- fog of war
- procedural generation
- generalized WorkOrder framework
- mining contracts
- generic task graphs
- generalized production-building hierarchy
- ScriptableObject recipe database
- multi-stop routing
- route optimization
- cargo batching
- mixed-contract cargo
- DOTS/ECS
- custom production UI
- custom logistics UI

Do not refactor working Phase 1 code simply because a different architecture is theoretically cleaner.

Do not convert the project into a framework.

Implement only abstractions earned by the current gameplay requirements.

---

## 5. Intentional Temporary Cheats

These are deliberate and must **not** be “fixed” during Phase 2:

### Command Post consumption
All 8 colonists continue consuming Command Post Food and Water regardless of temporary physical location.

### Water Processor labor
Water Processor is automated for now and needs no worker.

### Pilot assignment
Pilot 1 remains assigned to Shuttle 1.
Pilot 2 remains assigned to Mining Ship 1.

### Mining source knowledge
Mining Ship already knows where Ice Asteroid 1 is.

### Mining dispatch
Mining Ship responds directly to Water Processor Ice stock need.
Do not route mining through ContractManager yet.

### Shuttle scheduling
One Shuttle executes one existing transport contract at a time with simple priority ordering.

---

## 6. Ticket Execution Order

Execute exactly in this order:

1. `01_P2-T00_BASELINE.md`
2. `02_P2-T01_RESOURCES.md`
3. `03_P2-T02_ICE_DEPOSIT.md`
4. `04_P2-T03_MINING_SHIP.md`
5. `05_P2-T04_WATER_PROCESSOR.md`
6. `06_P2-T05_GENERIC_FREIGHT.md`
7. `07_P2-T06_FARM_WATER.md`
8. `08_P2-T07_COMMAND_POST_WATER.md`
9. `09_P2-T08_SCENE_WIRING.md`
10. `10_P2-T09_OBSERVABILITY.md`
11. `11_P2-T10_ACCEPTANCE.md`

Do not skip ahead because a later ticket appears easy.

---

## 7. Executor Operating Rules

For every ticket:

1. Read this file.
2. Read the individual ticket completely.
3. Inspect the actual Phase 1 implementation before editing.
4. Reuse existing systems where they already fit.
5. Keep file count minimal.
6. Compile after the ticket.
7. Enter Play mode where the ticket has runtime acceptance criteria.
8. Verify Phase 1 behavior still works after every ticket that touches shared systems.
9. Do not implement features from future tickets early unless a minimal compile dependency requires it.
10. If the actual Phase 1 architecture differs from the expected names, adapt the ticket to the real architecture instead of creating duplicate managers/components.

---

## 8. Regression Rule

The following Phase 1 behaviors are protected throughout Phase 2:

- farmer passenger transport
- farmer work/rest loop
- Food production
- Food consumption
- Food freight
- inventory reservation
- Shuttle physical cargo movement
- Shuttle disable/re-enable behavior
- event logging
- simulation time

A Phase 2 ticket is not complete if it breaks any of these.

---

## 9. Core Economic Invariants

### Extraction conservation

```text
Ice Asteroid -X
Mining Ship +X
```

### Freight conservation

```text
Source -X
Vehicle +X

then

Vehicle -X
Destination +X
```

### Processing conservation

Initial Phase 2 recipe is 1:1:

```text
Water Processor Ice -X
Water Processor Water +X
```

### Reservation safety

Reserved Water cannot simultaneously satisfy Farm and Command Post demand.

### Physical stranding is valid

These states are intentionally possible:

- Food exists at Farm while Command Post is hungry.
- Water exists at Processor while Farm is dry.
- Ice exists aboard Mining Ship while Processor has none.
- Ice remains in the asteroid while Processor is empty because Mining Ship is unavailable.

Location is part of the economy.

---

## 10. Phase 2 Stop Condition

When `11_P2-T10_ACCEPTANCE.md` passes, STOP.

Do not automatically continue into construction, staffing the processor, exploration, more resource chains, storage buildings, second Shuttle, or job allocation.

The next slice must be chosen from actual playtest evidence.
