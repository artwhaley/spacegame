# TESTING IN EXPLORATION MODE

## Status

This document defines the project's active testing philosophy while core gameplay and architecture are still being discovered.

It is written for human developers and coding agents.

When this project enters a later stabilization/release phase, this policy should be revisited deliberately rather than silently accumulating production-grade testing requirements during exploration.

---

# The principle

> **Tests should protect truths we intend to keep, not freeze every implementation we happen to have today.**

Spacegame is currently discovering:

- colonist decision policy;
- facility/activity contracts;
- staffing/service behavior;
- time acceleration;
- physical travel;
- transport;
- economy;
- UI;
- eventual ship movement.

Many of these rules are intentionally changing.

A large test suite that mirrors today's implementation can make healthy design changes harder than actual bug fixes.

During exploration, automated testing exists to make iteration safer — not to make iteration expensive.

---

# What deserves a permanent automated test

A permanent test should normally satisfy at least one of these conditions.

## 1. Pure, stable rule

The behavior is deterministic and likely to remain meaningful even if implementation changes.

Good examples:

- an overnight shift contains 02:00;
- `[08:00, 14:00)` does not overlap `[14:00, 20:00)`;
- inventory cannot exceed capacity;
- discrete resources remain integral;
- an OffDuty cooldown expires after its authored duration;
- one reservation group cannot have two simultaneous owners;
- a 1× simulation clock converts one real second into one simulation second.

These tests are cheap, trustworthy, and useful.

## 2. Specific regression we actually experienced

If a bug cost real debugging time or produced a severe silent failure, a narrow regression test is valuable.

Examples from project history include behaviors such as:

- a critically hungry colonist failing to get food and immediately returning to Work;
- a critically hungry colonist waking, failing to get food, and going straight back to Sleep;
- one colonist's personal cooldown affecting another colonist;
- a reservation being released by the wrong actor.

A regression test should reproduce the actual failure as narrowly as possible.

Do not create a family of speculative variants merely because one regression existed.

## 3. Critical invariant whose failure would be hard to notice

Examples:

- authoritative inventory quantity must have one owner;
- requester identity must not leak across colonists;
- current/next shift calculations must remain monotonic across midnight;
- bid/offer state must not survive beyond the lifecycle contract if stale state would silently poison simulation.

If a human would likely notice the break within seconds of pressing Play, automated protection is less valuable.

---

# What usually should NOT be a permanent automated test

## Scene composition

Do not parse `.unity` YAML to prove:

- Bob exists;
- a chair exists;
- there are four beds;
- Alice has a scene override;
- a fixture contains an activity.

Open the scene.

If the setup is important, maintain a human acceptance checklist.

## Prefab serialization

Do not write tests around private serialized file structure, file IDs, YAML ordering, or exact component serialization unless the serialization format itself is a product contract.

The product contract is usually behavior.

## NavMesh movement

Do not pretend an EditMode object graph proves that Bob can physically navigate the authored scene.

For navigation:

- open Unity;
- run the scene;
- observe the route;
- use diagnostic telemetry if needed.

Create automated navigation tests only for a specific reproduced engine/lifecycle regression that has proven worth the maintenance cost.

## Animation and physical choreography

Do not make permanent tests for:

- exact Animator state progression;
- exact frame timing;
- "Bob visibly sits correctly";
- "Alice walks to this anchor and faces this direction";
- visual blend quality.

These are human acceptance concerns unless a stable low-level contract can be tested independently.

## Test-fixture correctness

Do not build a large automated system to prove that the manual test fixture itself is authored according to the manual test plan.

That is testing the test.

## Temporary architecture

If a gameplay policy is explicitly experimental, prefer:

- structured logs;
- debug inspectors;
- temporary diagnostics;
- human observation.

Do not immediately fossilize it into dozens of permanent assertions.

---

# Test tiers

The permanent suite should use these conceptual tiers.

## CORE

Fast, deterministic, stable logic.

Examples:

- schedules;
- numeric rules;
- inventory;
- cooldown arithmetic;
- stable manager selection logic;
- time conversion.

A Core failure is presumed serious.

Agents should run relevant Core tests frequently.

## REGRESSION

Narrow reproduction of an actual historical bug.

A Regression failure is also presumed serious.

Do not broaden one regression test into a speculative architecture test suite.

## UNITY INTEGRATION

Small tests that genuinely require Unity runtime lifecycle behavior.

Examples might include:

- component registers correctly when created before its manager;
- disabling a connection endpoint tears down the runtime link.

Keep this category small.

These tests are useful around subsystem/milestone changes, but should not become the default burden attached to every exploratory ticket.

## HUMAN ACCEPTANCE

The primary verification method for:

- scenes;
- prefab composition;
- NavMesh;
- animation;
- physical interactions;
- presentation;
- "does this actually look/feel right?";
- high-speed visual behavior;
- multi-colonist emergent behavior.

A ticket may legitimately have **no new permanent automated test** if its acceptance is fundamentally physical/visual and obvious in Unity.

## TEMPORARY DIAGNOSTIC

Temporary code/tests/telemetry used to answer one question.

Examples:

- measure how long Bob spends between reservation and activity start;
- trace one suspect lifecycle;
- compare high-speed timing.

Once the investigation is complete:

- delete it; or
- promote only the narrow durable regression/invariant into permanent coverage.

Do not let diagnostics quietly become permanent harness infrastructure.

---

# The permanence test

Before adding a permanent automated test, ask:

1. What exact truth does this protect?
2. Do we expect that truth to survive a reasonable architecture refactor?
3. Is this the class that actually owns the truth?
4. Would a human notice failure immediately in Play Mode?
5. Has this bug actually happened?
6. Is the test simpler than the behavior it protects?
7. Will another agent understand why this test must remain six months from now?

If those answers are weak, do not add the test.

---

# Test the owner, not the whole game

Prefer:

`DailyShiftWindow -> overlap rule`

over:

`ColonistBrain + WorkforceManager + scene objects -> infer overlap behavior`

Prefer:

`OffDutyCompletionHistory -> cooldown rule`

over:

`Brain + OffDutyManager + InteractableFacility + private state -> prove cooldown arithmetic`

Prefer:

`FoodServiceComponent -> employee self-service authorization`

over:

`Alice brain + Workforce + cafeteria + runner + reflection -> prove authorization`

Integration tests should exist only where the **integration itself** is the invariant.

---

# Reflection and private-state mutation

Private reflection is a warning that the test may be testing implementation rather than behavior.

Default rule:

> Do not manipulate private fields or invoke private methods merely to force the object into an internal state.

Allowed exception:

A narrow, high-value regression cannot reasonably be reproduced through public behavior and the reflection-based setup is significantly cheaper and more stable than creating a permanent test-only production seam.

Even then:

- document why reflection is necessary;
- keep the test narrow;
- do not use the technique as the default harness style.

Never make production internals public solely to satisfy a test unless the public seam is independently valuable to the architecture.

---

# Mocks, fakes, and test worlds

Use the smallest collaborator set that proves the owner under test.

Avoid creating a parallel fake colony.

A test harness is suspect when it must recreate:

- several managers;
- a colonist prefab;
- private runner phases;
- facility internals;
- schedule state;
- reservation state;
- simulated animation state;

just to assert one rule.

At that point either:

- test the owning class directly; or
- make it a human integration acceptance test.

---

# Scene and prefab acceptance

For authored Unity content, write a short checklist instead of a YAML parser.

A good manual checklist says:

1. Open `Assets/bobandfriends.unity`.
2. Press Play.
3. Confirm Alice reports to Cafeteria.
4. Make Bob Hungry.
5. Confirm he physically reaches Eat.
6. Confirm only one diner occupies the seat.
7. Confirm the structured log explains the transition.

A bad automated substitute reads scene text and asserts file IDs, serialized field ordering, or exact object counts merely because those happen to implement the checklist today.

---

# PlayMode tests

Use PlayMode tests sparingly.

Good PlayMode candidate:

> Unity component lifecycle ordering itself caused a real regression and cannot be tested outside runtime lifecycle.

Poor PlayMode candidate:

> We want to automate watching Bob walk across the room so nobody has to press Play.

Do not write frame-perfect tests around:

- `WaitForSeconds`;
- Animator transitions;
- visual arrival timing;
- NavMesh movement;

unless protecting a specific recurring bug justifies the maintenance.

A flaky PlayMode test is not useful protection.

Do not normalize rerunning until green.

---

# How future tickets should specify testing

Every ticket should contain a **Verification** section.

It should use this format.

## Automated verification

List only permanent tests justified by stable rules/regressions.

For each test say why it deserves permanence.

Example:

- Add `CriticalHungerDoesNotFallThroughToWork`.
  - Regression: this failure occurred in a multi-day run and produced Work/Eat thrashing.
  - Owner: `ColonistBrain`.
  - Deterministic and independent of scene authoring.

Or state:

> No new permanent automated test is justified for this ticket.

That is acceptable.

## Human Unity acceptance

For physical/visual/gameplay changes, give explicit steps.

Example:

- Open `bobandfriends.unity`.
- Set speed to 1×.
- Make Charlie Hungry.
- Verify he walks to the Cafeteria in human-scale seconds.
- Repeat at 10× and verify the same trip consumes approximately the same simulation duration.

Human acceptance is not a lesser form of testing for these systems. It is the correct form.

## Optional diagnostics

If the ticket needs temporary instrumentation, label it temporary and state whether it should be removed before closeout.

---

# Do not impose test quotas

Forbidden requirements include:

- "one EditMode test per ticket";
- "at least five tests";
- "test every branch";
- "increase coverage";
- "add tests for every changed method."

Test count is not a quality metric during exploration.

One excellent regression test is better than twenty implementation-mirroring tests.

Zero new automated tests can be correct.

---

# When an existing test fails after an intentional change

Do not begin by changing production.

Ask in this order:

1. Did intended behavior change?
2. Is the old assertion still a product/architecture truth?
3. Is the test coupled to implementation rather than behavior?
4. Is this a real regression?

Then:

- real regression -> fix production;
- intended behavior changed -> update/delete the test;
- implementation-only coupling -> rewrite/delete the test;
- unclear -> investigate before touching production.

The test suite is evidence, not scripture.

---

# When to run tests

During ordinary exploratory work:

1. compile;
2. run directly relevant Core/Regression tests;
3. perform the specified human Unity acceptance.

Before a significant merge/milestone:

1. compile;
2. Core;
3. Regression;
4. relevant UnityIntegration;
5. broader suite if practical;
6. human acceptance.

Do not spend hours fighting Unity licensing/test-runner infrastructure merely to satisfy ritual if the environment is unavailable. Record the limitation and perform the evidence you can actually obtain.

---

# Observability is often better than more tests

This project already values structured simulation logs and live debug inspectors.

During exploration, high-quality observability often provides more value than another brittle integration test.

Prefer adding a truthful observable when the recurring problem is:

> We cannot tell why Bob did this.

Examples:

- decision reason;
- current need;
- current target;
- reservation owner;
- activity phase;
- structured lifecycle timestamps.

Do not turn observability into a second gameplay authority.

Logs describe truth; they do not own it.

---

# Deleting tests is allowed

A test should be removed when:

- it protects obsolete behavior;
- it duplicates a better owner-level test;
- it validates a fixture rather than gameplay;
- it repeatedly breaks under harmless refactors;
- its setup is more complex than the rule it protects;
- nobody can explain what durable truth it enforces;
- the behavior is better validated manually in Unity during exploration.

Deleting a bad test is maintenance, not loss of quality.

---

# Exploration-mode exit

This policy is deliberately optimized for architectural exploration.

Revisit it when the project reaches a phase where:

- core gameplay rules are stable;
- save compatibility matters;
- content volume makes manual regression impractical;
- releases are distributed externally;
- platform/device matrices matter;
- many contributors modify the same stable systems;
- regressions cost more than test maintenance.

At that point, expanding integration/automation coverage may be appropriate.

Do not prematurely apply that stabilization burden to the current project.

---

# Short version for agents

Before adding a test:

> Is this a stable rule, an actual regression, or a critical invariant that humans would miss?

If no:

> Write a human Unity acceptance step instead.

Before fixing code because a test failed:

> Is the test still right?

If no:

> Fix or delete the test.

And above all:

> Do not build a second fake game inside `Assets/Tests`.