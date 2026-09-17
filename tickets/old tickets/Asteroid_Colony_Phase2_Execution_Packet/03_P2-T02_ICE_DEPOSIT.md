# P2-T02 — Finite Ice Deposit

## Objective

Add one finite, inspectable raw-resource deposit representing Ice on an asteroid.

This ticket establishes that raw resources can physically exist in the world and be depleted.

---

## Required Context

Phase 2 adds one known Ice asteroid.

There is no exploration or scanning yet.

The future Mining Ship will extract Ice from this deposit.

The deposit itself does not create contracts and does not replenish.

---

## New Component

Create one small component, preferably:

```text
ResourceDeposit.cs
```

Place it in the existing world/economy folder convention.

Do not create a manager for deposits.

---

## Inspector Fields

Expose at minimum:

```text
Display Name
Resource Type
Starting Quantity
Remaining Quantity
Extraction Enabled
```

For the Phase 2 scene it will be configured:

```text
Display Name: Ice Asteroid 1
Resource Type: Ice
Starting Quantity: 500
Remaining Quantity: 500
Extraction Enabled: true
```

---

## Required Behavior

Provide an API conceptually equivalent to:

```csharp
float/int Extract(requestedAmount)
```

Use the project's existing resource numeric type.

The method must:

1. return zero if extraction is disabled;
2. return zero if Remaining Quantity is zero;
3. extract no more than requested;
4. extract no more than remains;
5. reduce Remaining Quantity by exactly the amount returned;
6. never permit Remaining Quantity below zero.

Example:

```text
Remaining = 5
Requested = 20

Returned = 5
Remaining = 0
```

The deposit does not automatically regenerate.

---

## Logging

Use the existing simulation log for meaningful transitions.

At minimum, emit a depletion message once:

```text
Ice Asteroid 1 depleted
```

Do not log every `Extract()` call if the Mining Ship later extracts continuously each tick.

The Mining Ship ticket will own "mining began" and trip-level logging.

---

## Scene Work

Create a primitive GameObject representing the asteroid, preferably a Sphere.

Attach:

```text
ResourceDeposit
```

A `LocationAnchor` is optional only if the existing logical-location/movement implementation needs one. Reuse Phase 1 conventions.

No Rigidbody required.
No collision simulation required.
No orbit required.

Positioning can be finalized in P2-T08.

---

## Files

Create:

```text
ResourceDeposit.cs
```

Modify no other systems unless a minimal compile dependency requires it.

---

## Guardrails

Do not:

- add multiple deposit support;
- build a ResourceDepositManager;
- add scanning;
- hide deposits;
- add quality grades;
- add mining efficiency;
- add regeneration;
- add randomization;
- add asteroid orbital motion;
- add mining contracts;
- add ownership/claims.

This is one finite number on one world object with a safe extraction API.

---

## Acceptance Criteria

- project compiles;
- Ice Asteroid object can hold 500 Ice;
- Remaining Quantity is visible in Inspector;
- extracting 10 reduces 500→490 and returns 10;
- requesting more than remains returns only what remains;
- deposit reaches exactly zero;
- deposit never goes negative;
- extraction disabled returns zero;
- deposit does not regenerate;
- Phase 1 still runs unchanged.
