# P2-T10 — Full Phase 2 Acceptance and Regression Test

## Objective

Prove the Phase 2 economy works end-to-end and fails causally.

This ticket should contain no major new feature work.

Fix only defects discovered while running the acceptance suite.

---

## Required Final World

Phase 2 should contain:

```text
Command Post
Farm
Water Processor
Ice Asteroid 1

Shuttle 1
Mining Ship 1

8 original colonists
```

Pilot assignments:

```text
Pilot 1 → Shuttle 1
Pilot 2 → Mining Ship 1
```

Resources:

```text
Food
Ice
Water
```

---

# TEST 1 — Phase 1 Regression

Run long enough to observe the original Phase 1 behavior.

Pass only if:

- farmers leave Command Post;
- farmers physically ride Shuttle;
- farmers reach Farm;
- farmers work;
- farmers eventually return;
- farmers rest;
- cycle repeats;
- Food is produced;
- Food is physically transported;
- Food is consumed;
- Food reservations work.

No Phase 2 feature excuses a Phase 1 regression.

---

# TEST 2 — Normal Mining Trip

Initial relevant state:

```text
Processor Ice = 0
Processor Ice Target = 24
Deposit Remaining >= 24
Mining Ship Cargo = 0
```

Expected causal sequence:

```text
Mining Ship recognizes need
→ travels to Ice Asteroid
→ arrives
→ mines
→ Deposit loses Ice
→ Mining Ship gains same Ice
→ returns to Processor
→ unloads
→ Processor gains same Ice
→ Mining Ship cargo returns to zero
```

At recommended numbers:

```text
Deposit 500 → 476
Mining Ship 0 → 24 → 0
Processor 0 → 24
```

Exact quantities may differ if tuning changed, but conservation must hold.

---

# TEST 3 — Partial Deposit

Set deposit remaining to:

```text
5
```

Ensure Processor needs more than 5.

Expected:

```text
Mining Ship extracts exactly 5
Deposit becomes exactly 0
Mining Ship returns with exactly 5
Processor receives exactly 5
```

Fail if:

- deposit goes negative;
- ship invents a full load;
- Processor receives more than extracted.

---

# TEST 4 — Processor Conversion

Give Processor Ice.

Expected:

```text
Ice decreases
Water increases
```

At Phase 2's 1:1 recipe:

```text
Ice lost == Water created
```

Then set:

```text
Ice = 0
```

Expected:

```text
Water production stops
InputStarved visible
```

Fill Water storage.

Expected:

```text
Ice consumption stops
OutputBlocked visible
```

---

# TEST 5 — Farm Water Dependency

Ensure farmers are physically at Farm and working.

With Water available:

```text
Water decreases
Food increases
```

Set Farm Water to zero.

Expected:

```text
Farmers remain present
Food production stops
Blocked Reason indicates No Water
```

Deliver Water normally.

Expected:

```text
Food production resumes automatically
```

No manual Farm reset.

---

# TEST 6 — Farm Water Freight

Allow Processor to accumulate Water.

Ensure Farm is below Water reorder threshold.

Expected:

```text
Farm creates Water freight demand
→ Processor Water becomes reserved
→ Shuttle eventually accepts contract
→ Shuttle reaches Processor
→ Water moves into Shuttle
→ Shuttle travels to Farm
→ Water moves into Farm
→ contract completes
```

Fail if Farm receives Water before unload.

---

# TEST 7 — Command Post Water Consumption

Run normally.

Expected:

```text
Command Post Water steadily decreases
```

Below threshold:

```text
Water contract created
```

Eventually:

```text
Processor → Shuttle → Command Post
```

physically replenishes Water.

Food must continue functioning simultaneously.

---

# TEST 8 — Competing Water Demand

Arrange:

```text
Farm below threshold
Command Post below threshold
Processor has limited Water
```

Expected:

- both consumers may create valid demand;
- reservations prevent double-spending;
- one contract may reserve stock before the other;
- second requester sees only remaining available Water;
- Shuttle services contracts according to existing priority/creation ordering;
- no Water duplication;
- no negative inventory.

The current suggested priorities are:

```text
Command Post Water: 90
Farm Water: 60
```

Exact balance may change, but behavior must be deterministic and explainable.

---

# TEST 9 — Disable Mining Ship

First allow the system to reach normal operation.

Set:

```text
Mining Ship Operational Enabled = false
```

Expected eventual causal chain:

```text
existing Processor Ice continues being consumed
→ Processor Ice reaches zero
→ Water production stops
→ existing Water continues being distributed/consumed
→ Water supply declines
→ Farm eventually lacks Water
→ Farm Food production stops
→ Command Post may develop Water shortage
→ later Food shortage may also occur
```

Nothing may magically replenish Ice or Water.

This is a key Phase 2 test.

---

# TEST 10 — Restore Mining Ship

Re-enable the Mining Ship before manually destroying the rest of the simulation state.

Expected:

```text
Mining resumes
→ Ice delivered
→ Water production resumes
→ Water contracts can be serviced
→ Farm can resume
→ shortages clear when supplies physically arrive
```

No scene reload should be required.

---

# TEST 11 — Physical Resource Conservation

During a representative run, inspect quantities at several transfer moments.

## Mining

```text
Deposit -X
Mining Ship +X
```

## Mining unload

```text
Mining Ship -X
Processor +X
```

## Water freight load

```text
Processor -X
Shuttle +X
```

## Water freight unload

```text
Shuttle -X
Farm/Command Post +X
```

## Processing

```text
Processor Ice -X
Processor Water +X
```

No transfer may duplicate or delete resources except explicit consumption/production rules.

---

# TEST 12 — No Teleportation

Confirm all of these states are possible and valid:

```text
Ice aboard Mining Ship while Processor Ice = 0

Water aboard Shuttle while destination Water has not yet increased

Food at Farm while Command Post Food is low

Water at Processor while Farm has none because Shuttle is busy
```

If any system bypasses physical transport, fail the phase.

---

# FINAL PASS CONDITIONS

Phase 2 is complete only when:

- all tests above pass;
- project compiles cleanly;
- no recurring Console errors;
- Phase 1 remains intact;
- Ice deposit depletes finitely;
- Mining Ship physically extracts and transports Ice;
- Water Processor converts local Ice to local Water;
- Farm physically consumes Water to make Food;
- Command Post consumes Water;
- Shuttle physically transports Water;
- reservations prevent double-spending;
- failures propagate upstream/downstream causally;
- runtime state is understandable through Inspector and log.

---

# STOP CONDITION

After this ticket passes:

**STOP DEVELOPMENT.**

Do not automatically implement:

- construction;
- Water Processor staffing;
- exploration;
- additional mining sites;
- another Shuttle;
- storage buildings;
- labor allocation;
- new resources;
- money.

The next slice must be chosen after actually playing and observing Phase 2.
