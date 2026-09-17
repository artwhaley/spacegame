# Source Audit Snapshot

Packet authored after inspecting public `main` on September 15, 2026.

Observed baseline relevant to staffing:

## ColonistAgent

Current code had:

```text
ColonistRole:
    BridgeCrew
    ShuttlePilot
    Maintenance
    Farmer

ColonistActivity:
    Idle
    WaitingForTransport
    Passenger
    Working
    Resting
    OnDutyCrew
```

and direct fields:

```text
home
currentLocation
assignedWorkplace
activity
```

## PopulationManager

Registry only; comments explicitly stated no employment allocator existed.

## FarmController

Current controller was ~300 lines and bundled:

```text
assignedFarmers
8h work / 8h rest state
passenger request creation
activity transitions
Water demand
Water consumption
Food production
```

It required all assigned farmers to move together for its group phase changes.

## ContractManager

Already supported passenger contracts containing a list of `ColonistAgent`, required all passengers to be physically at source, and supported active-passenger queries.

Current default passenger priority was legacy `100`.

## ShuttleController

Current Shuttle had persistent:

```text
assignedPilot
```

moved that pilot aboard at initialization and treated them as `OnDutyCrew`.

Passenger unloading set passengers back to Idle; a StaffingManager can safely correct that to Working/Resting on the next staffing tick based on shift/location.

## Important adaptation rule

If the repository has changed since this snapshot, do not restore old code to match the packet.

Preserve the staffing architecture and adapt the migration instructions to current equivalents.
