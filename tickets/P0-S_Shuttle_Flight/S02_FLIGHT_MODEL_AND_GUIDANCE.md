# S02 — `ShipFlightProfile`, `ShipFlightState`, `FlightIntegrator`, `FlightGuidance`

Independent of S01. Read `01_LOCKED_DESIGN.md` §2. Pure math; nothing here is a
`MonoBehaviour` except the ScriptableObject.

## Must create
- `Assets/Scripts/ColonyPrototype/Content/ShipFlightProfile.cs`
- `Assets/Scripts/ColonyPrototype/Vehicles/Flight/ShipFlightState.cs`
- `Assets/Scripts/ColonyPrototype/Vehicles/Flight/FlightIntegrator.cs`
- `Assets/Scripts/ColonyPrototype/Vehicles/Flight/FlightGuidance.cs`
- `Assets/GameData/Flight/Shuttle.asset`, `Assets/GameData/Flight/MiningShip.asset`
- `Assets/Tests/EditMode/FlightIntegratorTests.cs`, `Assets/Tests/EditMode/FlightGuidanceTests.cs`

## May modify
- nothing else

## Tests
Integrator:
1. Zero force/torque → velocity and angular velocity unchanged, position advances
   linearly, rotation stays normalized over 10,000 steps.
2. Constant body +Z force with identity rotation → `v = F/m · t` within 1%.
3. Force above `mainThrustN` is clamped.
4. Rotation integrates: constant torque about body Y for `t` → angle ≈ ½·(τ/I)·t² (small
   angles).

Guidance (drive integrator with guidance in a loop, `dt = profile.substepSeconds`):
5. `Cruise` from rest to a point 2,000 m away arrives within 5 m with |v| < 0.5 m/s in
   finite steps, never exceeds `cruiseSpeedMax`, and **never overshoots** the target
   along the approach axis by more than 5 m.
6. `Cruise` with an initial 30 m/s velocity *away* from the target: the ship rotates
   toward the required burn direction before |thrust| exceeds RCS capability (assert the
   angle between +Z body and desired accel < 15° whenever main engine is commanded).
7. `PoseHold` from 40 m off-pose with 20° misalignment reaches position tolerance 0.3 m
   and angle 5° with speed never exceeding the cap.
8. `WithinCapture` true only when distance, speed and angle are *all* inside the port
   tolerances.

## Forbidden
- Any Unity physics, `Time.*`, or transforms other than reading `berth` pose in `WithinCapture`.
- PD constants that are not derived from the profile.

## Acceptance
- [ ] All four runtime files ≤ 350 lines total.
- [ ] Report the placeholder numbers you settled on for `Shuttle.asset` and how long a
      2,000 m hop takes in game-seconds (target: 60–120 game-s so it reads as a
      2–4 real-second glide at 1× with a 60-s game-hour).
