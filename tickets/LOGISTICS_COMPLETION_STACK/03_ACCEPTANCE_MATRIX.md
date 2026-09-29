# Acceptance matrix

Start every row as NOT RUN. Record results and evidence in `EXECUTION_LOG.md`. Source reasoning, compilation, executed tests, and observed Unity behavior are distinct evidence types. The exact numeric cases below specify outcomes; they do not require a new permanent test per row.

## Accounting and dispatch

| ID | Setup / action | Required result | Primary evidence |
|---|---|---|---|
| A01 | Stock with multiple owned claims; transfer one; consume/release a meal; try invalid mutations | Quantities conserved except explicit consumption; no owner consumes/releases another claim; reads do not grow capacity | Inventory/food owner tests; dining observation |
| A02 | Demand 40, providers offering 20/10/5/5, same resource/final requester | All four children contribute to one parent; committed=40; intermediate staging contributes zero delivered; final deliveries total exactly 40 | Small provider/accounting test, without fake colony or physical drone claims |
| A03 | Demand 40, one shuttle capacity 10; stock/docks have room; let carrier repeatedly become available | Four shipments of 10 complete over trips; parent stays open until delivered=40; more source stock does not remove route | Numeric dispatch test plus real shuttle observation |
| A04 | Cargo already at departure depot; requester at arrival depot or reachable facility | Route starts with shuttle; ends at actual requester; unnecessary walking legs omitted | Existing route code review and physical depot shipment |
| A05 | Repeat stale/duplicate load, transfer, arrival, completion callbacks; archive completed work | No duplicate pickup, deposit, delivered credit, new request, or lease release; stale route/execution ignored | Focused owner regression and correlated trace |
| A06 | One provider accept fails after provisional reservation; discrete resource has 5.5 room; demand tail=2 with normal minimum=5 | Failed accept restores all provisional claims; feasible discrete shipment=5; final 2 can complete demand without being silently discarded | Small rollback/quantity tests |

## Labor

| ID | Setup / action | Required result | Primary evidence |
|---|---|---|---|
| A07 | No eligible porter/producer assistance; consumer below emergency threshold; depot has reachable stock; eligible consumer employee | Employee can initiate pickup, exits work physically, completes entire accepted quantity, and returns/resumes appropriately | Unity modular scene |
| A08 | 20 staged units assigned to a worker with carry capacity 5; shift ends after first 5 delivered | Worker performs four loads total, finishes assigned 20, clears its dock claim, then releases; no new work appended | Owner completion regression + Unity commute scene |
| A09 | Repeat A08 with critical hunger arising after first load; contrast already-critical worker at admission | Accepted work still completes in full with release pending; already-ineligible worker is not newly admitted; no tuning of hunger to pass | Owner policy check + Unity observation |
| A10 | Final pickup path fails after prior staging, then becomes available; separately make assigned executor unusable | Same owned remainder retries legally; exceptional replacement must accept full outstanding responsibility atomically; no ownerless claim | State regression + physical recovery observation |
| A11 | Producer assists with visible exit animation; routine capacity=10, assist/emergency=5 | Movement begins after physical exit; routine porter carries configured 10; distinct limits respected | Unity observation; optional pure capacity case |

## Shuttle

| ID | Setup / action | Required result | Primary evidence |
|---|---|---|---|
| A12 | Mixed payload manifest; one payload delayed; cargo destination fills before arrival, then room is freed | Loaded items are not loaded twice; passengers/cargo acknowledge separately; unload retries; vehicle remains responsible until manifest resolves | Owner acknowledgment regression + Unity |
| A13 | Destination berth occupied/reserved then freed; include sufficient spare berth to avoid an impossible swap | Wait remains visible/retryable; accepted work eventually departs; no port overbooking | Unity with actual docking |
| A14 | Compatible shuttle A unstaffed, B ready; later delay pilot at a shuttle initially docked at the requested destination | Ready B can serve; delayed-pilot case still visits origin before completion | Scheduling regression + Unity |
| A15 | Cancel one untransferred passenger of an assigned manifest; reject initial reposition; attempt cancelling already-boarded passenger | Remaining manifest proceeds; rejected accept rolls back both owners; transferred passenger retains safe completion responsibility | State regression + Unity |

## Storage, lifecycle, and long-run behavior

| ID | Setup / action | Required result | Primary evidence |
|---|---|---|---|
| A16 | Mixed store capacity 100, targets A=40/B=40; attempt two full targets; full 10-input store runs 10→5 recipe | Valid inputs coexist; invalid proposal leaves policy unchanged; conversion succeeds because resulting inventory fits | Policy/converter tests + physical production observation |
| A17 | Batch consumes inputs; another writer fills output room before completion; later free room | Completed batch waits, emits once, and never consumes inputs twice; observers never see half-applied recipe transaction | Converter/inventory tests + Unity |
| A18 | Lower/disable demand after children accepted; cancel untouched work explicitly; pause/resume provider empty and loaded | No new unwanted children; accepted children retain completion duty; rollback restores uncovered only where appropriate; resumed work transfers once | Accounting/lifecycle tests + Unity |
| A19 | Pause just as a shuttle completes, resume at 1×/10× and current authored fast speed; disable/re-enable service | No new gameplay route/activity progression while globally paused; actual ownership resumes correctly; animation/navigation remain physically valid | Human Unity acceptance |
| A20 | Several game days with recurring demand; inspect blocked and completed work | Live collections exclude cleaned-up terminal history; bounded history does not erase live waits/correlation; one transfer produces one economic event; no unexplained stranded leases/claims | Runtime trace/counts + code review |

## Physical setup guidance

Use `Assets/bobandfriends_modular.unity` for local labor and `Assets/bobandfriends_commute.unity` for the modern shuttle loop when those remain the current fixtures. T00 must verify current scene authoring before using them. Prefer Inspector/public authoring commands and reversible setup; do not script a replacement simulation.

For each physical run record: scene, relevant stock/capacity/role/shift settings, simulation speed, actions, observed result, and supporting request/allocation IDs or log path. Do not require a screenshot for a fact a structured trace proves; do not use a trace to claim an animation looked correct.

## Audit coverage

| Audit concern | Owning tickets | Acceptance |
|---|---|---|
| Unacknowledged shuttle unloading | T05 | A05, A12 |
| Shuttle waits, cancellation, pilot choice, false arrival, rollback | T06 | A13–A15 |
| Missing initial emergency employee pickup | T04 | A07 |
| Worker release recommendation corrected by owner; commitment recovery still needed | T04, T08 | A08–A10, A18 |
| Blocked walking pickup cannot resume | T04 | A10 |
| Oversized freight and forced walking legs | T02, T03 | A02–A06 |
| Mixed shared capacity and conversion feasibility | T07 | A06, A16, A17 |
| Wrong routine carry capacity / producer exit gate | T04 | A11 |
| Policy, pause, disabled provider lifecycle | T08 | A18, A19 |
| Public inventory mutations / getter repair / meal claims | T01 | A01, A17 |
| Concrete executor coupling / duplicate progress | T02 | A02, A05 |
| Unbounded live history / misleading diagnostics | T09 | A20 |
| Legacy boundary, stale documents, content compatibility | T10 | Source/content review plus applicable physical rows |

Physical freighter/drone implementation, mining migration, new UI, and a traffic-control solver remain explicitly out of scope. Their exclusion does not excuse hardcoding demand accounting to a single vehicle or load.
