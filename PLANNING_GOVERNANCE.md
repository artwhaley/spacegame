# Planning Governance — How this repository should make decisions

## 1. Four statuses, visible everywhere

Every material planning statement should be legible as one of these:

### FACT
Verified in current code or current content. Cite the file/type when useful.

### OWNER DECISION
A deliberate product choice made by the owner. This may be binding even if no code exists yet.

### WORKING HYPOTHESIS
A proposed implementation or design chosen only to make the next experiment possible. It must name its revisit trigger.

### OPEN QUESTION
No answer has been earned. State what experiment or observation will answer it.

Do not put FACT and WORKING HYPOTHESIS in the same declarative register.

## 2. Reversal cost determines how much planning certainty is allowed

- **Free:** Inspector/content values, temporary timing values. Fine to guess locally; label them dials.
- **Cheap:** one component or UI file with no consumers. Can be provisional inside the ticket that creates it.
- **Expensive:** schemas, authorities, shared interfaces, data contracts consumed by multiple later systems. Require direct evidence or owner approval.
- **Question-deleting:** a choice that silently decides a major game-design fork. Keep blank until the owner explicitly decides after an informing experiment.

## 3. Rolling commitment

Maintain a 28-day human roadmap for orientation.

Only the next **three days** are allowed to contain working-depth implementation detail.

At the end of each day:
1. record what was observed;
2. mark hypotheses confirmed/falsified/unresolved;
3. adjust the next three days;
4. never continue into new scope if today's Press Play observable is broken.

## 4. Lock a refactor, not a first draft

A locked design is appropriate when it protects known existing behavior during a behavior-preserving change.

Examples:
- splitting an existing large class behind an unchanged facade;
- consolidating multiple existing writers into one authority while preserving externally visible behavior.

A locked design is **not** appropriate for:
- first UI,
- first construction placement,
- first personal-needs system,
- first report,
- first flight-feel model,
- first scenario schema.

Those start as working designs with triggers.

## 5. Preserve one owner per fact

The revised plan specifically guards against these authority mistakes:

- Presentation bed transforms do not silently own `HabitationComponent.capacity`.
- Facility work-cycle presentation does not own production quantity or staffing eligibility.
- NavMesh/avatar position does not replace `ColonistAgent.currentLocation` as the logical arrived-location fact.
- A report/ledger does not become a second resource authority.
- Passenger visual containment does not replace transport-contract/passenger-carrier truth.
- Scenario data does not duplicate runtime authority; it initializes it.

## 6. Future documents cannot justify current complexity by themselves

A Phase-1/2 idea may warn Phase 0 not to paint itself into an obvious corner, but it cannot force a Phase-0 abstraction unless that abstraction is independently justified by a Phase-0 need.

"Maybe probes later" is not enough reason to choose today's null-role semantics.
"Maybe multiple site planes later" is not enough reason to build plane IDs today.
"Maybe scan fields later" is not enough reason to make today's environment tool read a fake density hook.

## 7. Placeholders are allowed and must say they are placeholders

Good:

- `ConstructionMaterial_Test` — temporary content used to prove freight → site → builder.
- `walkTraversalHours` — placeholder tuned by watching the avatar.
- `ShuttleFlightProfile_Test` — a visual tuning asset, not a game-design law.

Bad:

- putting the number in a design document as though measured;
- building downstream schemas around the placeholder;
- letting a placeholder silently become canon because nobody revisited it.

## 8. UI is question-first

Before adding a table, graph, HUD value, or alert, state the question it answers.

Examples:
- "Why is the Farm not producing?"
- "Why is this colonist not eligible for this shift?"
- "Why is the shuttle waiting?"
- "What went wrong in the run I just lost?"

If no human has wanted the answer yet, the UI is probably early.

## 9. Tests

Preserve existing tests.

Add focused tests when:
- a pure algorithm is easier to verify mechanically than visually;
- a bug has regressed before;
- a behavior-preserving refactor needs a regression characterization;
- a critical invariant cannot be proven by the day's Play observable.

Do not make arbitrary test-file counts or line counts acceptance gates.

## 10. End-of-day learning record

Each execution day should produce a small note:

```md
# Day N Learnings

## Observable
What actually happened in Play Mode.

## Confirmed
Facts/hypotheses that survived.

## Falsified
What turned out not to work.

## New questions
What became visible only after implementation.

## Decision-backlog updates
Questions whose triggers fired.

## Next three days
Only the immediate window, revised from today's evidence.
```
