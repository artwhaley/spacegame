using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    public enum ShuttleLocalSafetyState
    {
        Clear,
        Caution,
        EmergencyBraking,
        AvoidanceHold,
        Replanning
    }

    public enum ShuttleVoyagePhase
    {
        Docked,
        Undocking,
        AlignDeparture,
        CruiseAccelerating,
        CruiseCoasting,
        FlipForBraking,
        CruiseBraking,
        Approach,
        DockingTurn,
        FinalDocking,
        Captured,
        Blocked
    }

    /// <summary>
    /// Deterministic Shuttle movement owner. It advances from SimulationManager time
    /// and is the sole writer of this Shuttle root's world position and rotation.
    /// </summary>
    public sealed class ShuttleVoyageComponent : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
    {
        private const float PoseEpsilon = 0.01f;
        private const float AngularSettleSpeed = 1f;
        private const int MaxPendingRcsPulseEvents = 32;

        [Header("Reusable flight setup")]
        [SerializeField] private ShuttleFlightProfile profile;
        [SerializeField] private ShuttleNavigationProfile navigationProfile;
        [SerializeField] private ShuttleRoutePlanner routePlanner;
        [SerializeField] private ShuttleDockingProbeComponent probe;
        [SerializeField] private DockingPortComponent currentDock;

        [Header("Voyage diagnostics")]
        [SerializeField] private ShuttleVoyagePhase phase = ShuttleVoyagePhase.Docked;
        [SerializeField] private ShuttleRouteKind plannedRouteKind;
        [SerializeField] private string routeDiagnostic;
        [SerializeField] private int routeRelevantObstacleCount;
        [SerializeField] private int routeCandidateNodeCount;
        [SerializeField] private int routeEdgeSweepCount;
        [SerializeField] private int routeExpansionCount;
        [SerializeField] private float routeLength;
        [SerializeField] private float routeLengthRatio = 1f;
        [SerializeField] private float routePlanningMilliseconds;
        [SerializeField] private ShuttleLocalSafetyState localSafetyState;
        [SerializeField] private string localSafetyBlocker;
        [SerializeField] private DockingPortComponent destination;
        [SerializeField] private FlightRoute route = new FlightRoute();
        [SerializeField] private int waypointIndex;
        [SerializeField] private string blockReason;
        [SerializeField] private ShuttleFlightState flightState;
        [SerializeField] private bool mainEngineFiring;
        [SerializeField] private float segmentDistance;
        [SerializeField] private float brakingDistance;
        [SerializeField] private float flipAllowanceDistance;
        [SerializeField] private float angularErrorDegrees;
        [SerializeField] private ShuttleRcsActivity linearRcsActivity = ShuttleRcsActivity.Settled;
        [SerializeField] private ShuttleRcsActivity angularRcsActivity = ShuttleRcsActivity.Settled;

        [Header("Optional inspector debug targets")]
        [SerializeField] private DockingPortComponent debugPortA;
        [SerializeField] private DockingPortComponent debugPortB;

        private DockingPortComponent originPort;
        private Quaternion safeDepartureRotation = Quaternion.identity;
        private Vector3 probeLocalPosition;
        private Quaternion probeLocalRotation = Quaternion.identity;
        private bool hasInitialState;
        private bool hasProbeOffset;
        private readonly ShuttleRcsPulseController linearRcs = new ShuttleRcsPulseController();
        private readonly ShuttleRcsPulseController angularRcs = new ShuttleRcsPulseController();
        private readonly List<ShuttleRcsPulseEvent> pendingRcsPulseEvents = new List<ShuttleRcsPulseEvent>();
        private bool usedLinearRcsThisSubstep;
        private bool usedAngularRcsThisSubstep;
        private float safetyReplanCooldown;
        private bool wasSafetyHolding;
        private int brakingConstraintWaypointIndex = -1;
        private float brakingConstraintSpeed;

        public int SimulationTickPriority => 250;
        public ShuttleVoyagePhase Phase => phase;
        public ShuttleFlightProfile Profile { get => profile; set => profile = value; }
        public ShuttleNavigationProfile NavigationProfile { get => navigationProfile; set => navigationProfile = value; }
        public ShuttleDockingProbeComponent Probe { get => probe; set { probe = value; hasProbeOffset = false; } }
        public DockingPortComponent CurrentDock { get => currentDock; set => currentDock = value; }
        public DockingPortComponent Destination => destination;
        public FlightRoute CurrentRoute => route;
        public int CurrentWaypointIndex => waypointIndex;
        public FlightWaypoint CurrentWaypoint => route != null && waypointIndex >= 0 && waypointIndex < route.Count
            ? route[waypointIndex] : null;
        public string BlockReason => blockReason;
        public ShuttleFlightState FlightState => flightState;
        public float Speed => flightState.velocity.magnitude;
        public float AngularSpeed => flightState.angularVelocity.magnitude;
        public float SegmentDistance => segmentDistance;
        public float BrakingDistance => brakingDistance;
        public float FlipAllowanceDistance => flipAllowanceDistance;
        public float AngularErrorDegrees => angularErrorDegrees;
        public bool MainEngineFiring => mainEngineFiring;
        public ShuttleRcsActivity LinearRcsActivity => linearRcsActivity;
        public ShuttleRcsActivity AngularRcsActivity => angularRcsActivity;
        public ShuttleLocalSafetyState LocalSafetyState => localSafetyState;

        public void DrainRcsPulseEvents(List<ShuttleRcsPulseEvent> receiver)
        {
            if (receiver == null)
                return;
            receiver.AddRange(pendingRcsPulseEvents);
            pendingRcsPulseEvents.Clear();
        }

        private void Awake()
        {
            EnsureProbeReference();
            InitializeStateFromTransform();
        }

        private void OnEnable()
        {
            SimulationManager.RegisterTickable(this);
        }

        private void OnDisable()
        {
            SimulationManager.UnregisterTickable(this);
            linearRcs.Reset();
            angularRcs.Reset();
            linearRcsActivity = ShuttleRcsActivity.Settled;
            angularRcsActivity = ShuttleRcsActivity.Settled;
            pendingRcsPulseEvents.Clear();
        }

        public bool TryRequestVoyage(DockingPortComponent requestedDestination)
        {
            return TryRequestVoyage(requestedDestination, out _);
        }

        public bool TryRequestVoyage(DockingPortComponent requestedDestination, out string reason)
        {
            if (currentDock == null || requestedDestination == null || currentDock.ClearanceNode == null ||
                requestedDestination.ApproachNode == null)
            {
                reason = "current dock or requested destination is missing its required nodes";
                return false;
            }

            FlightRoute planned;
            routePlanner = routePlanner != null ? routePlanner : GetComponent<ShuttleRoutePlanner>();
            if (routePlanner != null && navigationProfile != null)
            {
                ShuttleRoutePlan plan = routePlanner.Plan(currentDock.ClearanceNode.position,
                    requestedDestination.ApproachNode.position, navigationProfile,
                    profile != null ? profile.maxCruiseSpeed : 25f, transform);
                plannedRouteKind = plan.kind;
                routeDiagnostic = plan.diagnostic;
                RecordPlanMetrics(plan);
                if (plan.failure != ShuttleRouteFailure.None)
                {
                    reason = $"{plan.failure}: {plan.diagnostic}";
                    return false;
                }
                List<FlightWaypoint> waypoints = new List<FlightWaypoint>();
                waypoints.Add(FlightWaypoint.Clearance(currentDock.ClearanceNode.position, null,
                    Mathf.Max(0.1f, profile != null ? profile.positionTolerance * 2f : 1f)));
                waypoints.AddRange(plan.cruiseWaypoints);
                waypoints.Add(FlightWaypoint.Approach(requestedDestination.ApproachNode.position,
                    DockingPoseUtility.GetMatingProbeRotation(requestedDestination.ApproachNode.rotation),
                    Mathf.Max(0.1f, profile != null ? profile.positionTolerance * 2f : 1f),
                    profile != null ? profile.approachMaxSpeed : 1f));
                planned = new FlightRoute(waypoints);
            }
            else
            {
                planned = FlightRoute.CreateDirect(currentDock.ClearanceNode.position,
                    requestedDestination.ApproachNode.position,
                    DockingPoseUtility.GetMatingProbeRotation(requestedDestination.ApproachNode.rotation),
                    Mathf.Max(0.1f, profile != null ? profile.positionTolerance * 2f : 1f),
                    Mathf.Max(0.1f, profile != null ? profile.positionTolerance * 2f : 1f));
                routeDiagnostic = "navigation planner/profile is unassigned; using Sprint A direct route";
                plannedRouteKind = ShuttleRouteKind.Direct;
            }
            planned[planned.Count - 1].requiredArrivalSpeed = profile != null ? profile.approachMaxSpeed : 1f;
            return TryRequestVoyage(requestedDestination, planned, out reason);
        }

        /// <summary>Starts one trip while consuming a caller-supplied, already-planned route.</summary>
        public bool TryRequestVoyage(DockingPortComponent requestedDestination, FlightRoute plannedRoute,
            out string reason)
        {
            if (phase != ShuttleVoyagePhase.Docked)
            {
                reason = $"Shuttle is {phase}, not safely docked";
                return false;
            }
            if (!EnsureFlightReferences(out reason))
                return false;
            if (!ValidateProfile(out reason))
                return false;
            if (requestedDestination == null || requestedDestination == currentDock)
            {
                reason = requestedDestination == null
                    ? "missing destination port"
                    : "destination must differ from the current dock";
                return false;
            }
            if (!currentDock.ValidateConfiguration(out reason) || !requestedDestination.ValidateConfiguration(out reason))
                return false;
            if (currentDock.State != DockingPortState.Occupied || currentDock.Occupant != probe)
            {
                reason = "current dock is not occupied by this Shuttle probe";
                return false;
            }

            InitializeStateFromTransform();
            GetProbePose(out Vector3 probePosition, out Quaternion probeRotation, out Vector3 probeVelocity);
            if (!currentDock.IsWithinCaptureTolerance(probePosition, probeRotation, probeVelocity,
                    flightState.angularVelocity.magnitude, out reason))
            {
                reason = $"current berth is not safely captured: {reason}";
                return false;
            }
            if (!TryCopyAndValidateRoute(plannedRoute, currentDock, requestedDestination,
                    out FlightRoute routeCopy, out reason))
                return false;

            // Reserve before changing any departure state. Failed validation above leaves both ports untouched.
            if (!requestedDestination.TryReserve(probe, out reason))
                return false;

            originPort = currentDock;
            destination = requestedDestination;
            route = routeCopy;
            waypointIndex = 0;
            brakingConstraintWaypointIndex = -1;
            blockReason = string.Empty;
            safeDepartureRotation = flightState.rotation;
            mainEngineFiring = false;
            pendingRcsPulseEvents.Clear();
            SetPhase(ShuttleVoyagePhase.Undocking);
            SimulationLog.Log($"{name} voyage requested: {originPort.name} → {destination.name}");
            reason = "voyage reserved and queued for undocking";
            return true;
        }

        public void SimulationTick(float deltaGameHours)
        {
            if (!isActiveAndEnabled)
                return;
            if (phase == ShuttleVoyagePhase.Captured)
            {
                SetPhase(ShuttleVoyagePhase.Docked);
                return;
            }
            if (!IsFlightPhase(phase) || !ShuttleFlightIntegrator.IsFinite(deltaGameHours) || deltaGameHours <= 0f)
                return;

            if (!EnsureFlightReferences(out string reason) || !ValidateProfile(out reason))
            {
                Block(reason);
                SyncRootTransform();
                return;
            }
            if (destination == null || route == null || !route.IsValid)
            {
                Block("voyage destination or route became invalid");
                SyncRootTransform();
                return;
            }
            if (!destination.ValidateConfiguration(out reason) ||
                destination.State != DockingPortState.Reserved || destination.ReservationHolder != probe)
            {
                Block(string.IsNullOrEmpty(reason) || reason == "valid"
                    ? "destination berth reservation was lost during the voyage"
                    : reason);
                SyncRootTransform();
                return;
            }
            if (originPort != null && !originPort.ValidateConfiguration(out reason))
            {
                Block($"origin berth became structurally invalid: {reason}");
                SyncRootTransform();
                return;
            }
            if ((phase == ShuttleVoyagePhase.Undocking || phase == ShuttleVoyagePhase.AlignDeparture) &&
                (originPort == null || originPort.ClearanceNode == null))
            {
                Block("origin berth clearance node disappeared before safe release");
                SyncRootTransform();
                return;
            }

            double remainingSeconds = (double)deltaGameHours * 3600d;
            float maxSubstep = Mathf.Max(0.001f, profile.integrationSubstepSeconds);
            int iterations = 0;
            while (remainingSeconds > 0.000001d && IsFlightPhase(phase))
            {
                float dt = (float)System.Math.Min(remainingSeconds, maxSubstep);
                AdvanceSubstep(dt);
                remainingSeconds -= dt;
                if (++iterations > 1000000)
                {
                    Block("simulation tick exceeded the bounded integration step count");
                    break;
                }
            }

            SyncRootTransform();
            UpdateDiagnostics();
        }

        private void AdvanceSubstep(float dt)
        {
            if (phase == ShuttleVoyagePhase.Undocking && IsClearOfOrigin())
            {
                if (originPort == null || !originPort.Release(probe))
                {
                    Block("origin berth could not be released at the clearance boundary");
                    return;
                }
                currentDock = null;
                waypointIndex = 1;
                SetPhase(ShuttleVoyagePhase.AlignDeparture);
                return;
            }

            if (phase == ShuttleVoyagePhase.FinalDocking && TryCapture())
                return;

            if (TryHandleLocalSafety(dt))
                return;

            Vector3 linearAcceleration = Vector3.zero;
            Vector3 angularAcceleration = Vector3.zero;
            mainEngineFiring = false;
            usedLinearRcsThisSubstep = false;
            usedAngularRcsThisSubstep = false;

            switch (phase)
            {
                case ShuttleVoyagePhase.Undocking:
                    linearAcceleration = RcsToProbeTarget(originPort.ClearanceNode.position,
                        profile.approachMaxSpeed, dt);
                    angularAcceleration = AngularToward(safeDepartureRotation, dt);
                    break;

                case ShuttleVoyagePhase.AlignDeparture:
                    if (!AdvanceCruiseWaypoints())
                        return;
                    if (CurrentWaypoint == null)
                    {
                        Block("route has no departure segment");
                        return;
                    }
                    Vector3 departDirection = CurrentWaypoint.worldPosition - ProbePosition();
                    if (departDirection.sqrMagnitude < 0.0001f)
                        departDirection = NextRouteDirection();
                    Quaternion departRotation = ShuttleFlightGuidance.RotationForForward(
                        departDirection, flightState.rotation * Vector3.up);
                    angularAcceleration = AngularToward(departRotation, dt);
                    break;

                case ShuttleVoyagePhase.CruiseAccelerating:
                    if (!AdvanceCruiseWaypoints())
                        return;
                    if (ShouldBeginApproachAtLowSpeed())
                    {
                        SetPhase(ShuttleVoyagePhase.Approach);
                        return;
                    }
                    if (ShouldFlipForBraking())
                    {
                        SetPhase(ShuttleVoyagePhase.FlipForBraking);
                        return;
                    }
                    if (Speed >= CurrentCruiseSpeedLimit() - 0.001f)
                    {
                        SetPhase(ShuttleVoyagePhase.CruiseCoasting);
                        return;
                    }
                    if (CurrentWaypoint == null)
                    {
                        Block("route ended before the required low-speed destination waypoint");
                        return;
                    }
                    Vector3 accelerationDirection = CurrentWaypoint.worldPosition - ProbePosition();
                    Quaternion accelerationRotation = ShuttleFlightGuidance.RotationForForward(
                        accelerationDirection, flightState.rotation * Vector3.up);
                    if (ShuttleFlightGuidance.TryGetMainEngineAcceleration(flightState,
                            accelerationDirection, profile, out Vector3 mainAcceleration))
                    {
                        linearAcceleration = mainAcceleration;
                        mainEngineFiring = true;
                    }
                    else
                    {
                        linearAcceleration = RcsToProbeTarget(CurrentWaypoint.worldPosition,
                            CurrentCruiseSpeedLimit(), dt);
                    }
                    angularAcceleration = AngularToward(accelerationRotation, dt);
                    break;

                case ShuttleVoyagePhase.CruiseCoasting:
                    if (!AdvanceCruiseWaypoints())
                        return;
                    if (brakingConstraintWaypointIndex >= 0 && waypointIndex > brakingConstraintWaypointIndex)
                        brakingConstraintWaypointIndex = -1;
                    if (ShouldBeginApproachAtLowSpeed())
                    {
                        SetPhase(ShuttleVoyagePhase.Approach);
                        return;
                    }
                    if (ShouldFlipForBraking())
                    {
                        SetPhase(ShuttleVoyagePhase.FlipForBraking);
                        return;
                    }
                    bool holdingSpeedForUpcomingCorner = brakingConstraintWaypointIndex >= waypointIndex &&
                        brakingConstraintWaypointIndex >= 0 && Speed <= brakingConstraintSpeed + 0.001f;
                    if (!holdingSpeedForUpcomingCorner && Speed < CurrentCruiseSpeedLimit() - 0.001f)
                    {
                        SetPhase(ShuttleVoyagePhase.CruiseAccelerating);
                        return;
                    }
                    if (CurrentWaypoint != null)
                    {
                        Vector3 coastDirection = CurrentWaypoint.worldPosition - ProbePosition();
                        Quaternion coastRotation = ShuttleFlightGuidance.RotationForForward(
                            coastDirection, flightState.rotation * Vector3.up);
                        angularAcceleration = AngularToward(coastRotation, dt);
                    }
                    break;

                case ShuttleVoyagePhase.FlipForBraking:
                    if (brakingConstraintWaypointIndex >= 0 && Speed <= brakingConstraintSpeed + 0.001f)
                    {
                        CompleteBrakingConstraint();
                        return;
                    }
                    Vector3 reverseDirection = -flightState.velocity.normalized;
                    Quaternion reverseRotation = ShuttleFlightGuidance.RotationForForward(
                        reverseDirection, flightState.rotation * Vector3.up);
                    angularAcceleration = AngularToward(reverseRotation, dt);
                    break;

                case ShuttleVoyagePhase.CruiseBraking:
                    if (brakingConstraintWaypointIndex >= 0 && Speed <= brakingConstraintSpeed + 0.001f)
                    {
                        CompleteBrakingConstraint();
                        return;
                    }
                    Vector3 brakeDirection = -flightState.velocity.normalized;
                    Quaternion brakeRotation = ShuttleFlightGuidance.RotationForForward(
                        brakeDirection, flightState.rotation * Vector3.up);
                    float brakeAngle = Quaternion.Angle(flightState.rotation, brakeRotation);
                    if (brakeAngle > profile.mainBurnAlignmentDegrees)
                    {
                        SetPhase(ShuttleVoyagePhase.FlipForBraking);
                        return;
                    }
                    angularAcceleration = AngularToward(brakeRotation, dt);
                    float brakeTarget = brakingConstraintWaypointIndex >= 0
                        ? brakingConstraintSpeed : profile.approachMaxSpeed;
                    float throttle = Mathf.Min(profile.mainAcceleration,
                        Mathf.Max(0f, Speed - brakeTarget) / dt);
                    linearAcceleration = (flightState.rotation * Vector3.forward).normalized * throttle;
                    mainEngineFiring = throttle > 0f;
                    break;

                case ShuttleVoyagePhase.Approach:
                    if (destination == null || destination.ApproachNode == null)
                    {
                        Block("destination approach node disappeared");
                        return;
                    }
                    Quaternion approachRootRotation = RootRotationForProbe(
                        DockingPoseUtility.GetMatingProbeRotation(destination.ApproachNode.rotation));
                    angularAcceleration = AngularToward(approachRootRotation, dt);
                    linearAcceleration = RcsToProbeTarget(destination.ApproachNode.position,
                        profile.approachMaxSpeed, dt);
                    break;

                case ShuttleVoyagePhase.DockingTurn:
                    if (destination == null || destination.DockingNode == null || destination.ApproachNode == null)
                    {
                        Block("destination docking or approach node disappeared");
                        return;
                    }
                    Quaternion dockingRootRotation = RootRotationForProbe(
                        DockingPoseUtility.GetMatingProbeRotation(destination.DockingNode.rotation));
                    angularAcceleration = AngularToward(dockingRootRotation, dt);
                    linearAcceleration = RcsToProbeTarget(destination.ApproachNode.position,
                        profile.approachMaxSpeed, dt);
                    break;

                case ShuttleVoyagePhase.FinalDocking:
                    if (destination == null || destination.DockingNode == null)
                    {
                        Block("destination docking node disappeared");
                        return;
                    }
                    Quaternion finalRootRotation = RootRotationForProbe(
                        DockingPoseUtility.GetMatingProbeRotation(destination.DockingNode.rotation));
                    angularAcceleration = AngularToward(finalRootRotation, dt);
                    linearAcceleration = RcsToProbeTarget(destination.DockingNode.position,
                        GetFinalDockingSpeedLimit(destination.DockingNode.position), dt);
                    break;
            }

            if (!usedLinearRcsThisSubstep)
            {
                linearRcs.Reset();
                linearRcsActivity = ShuttleRcsActivity.Settled;
            }
            if (!usedAngularRcsThisSubstep)
            {
                angularRcs.Reset();
                angularRcsActivity = ShuttleRcsActivity.Settled;
            }

            ShuttleFlightIntegrator.StepSubstep(ref flightState, profile,
                linearAcceleration, angularAcceleration, dt);
            EvaluatePhaseAfterIntegration();
        }

        private void EvaluatePhaseAfterIntegration()
        {
            switch (phase)
            {
                case ShuttleVoyagePhase.Undocking:
                    if (IsClearOfOrigin() && originPort != null && originPort.Release(probe))
                    {
                        currentDock = null;
                        waypointIndex = 1;
                        SetPhase(ShuttleVoyagePhase.AlignDeparture);
                    }
                    break;

                case ShuttleVoyagePhase.AlignDeparture:
                    if (Quaternion.Angle(flightState.rotation,
                            ShuttleFlightGuidance.RotationForForward(CurrentWaypoint != null
                                ? CurrentWaypoint.worldPosition - ProbePosition() : NextRouteDirection(),
                                flightState.rotation * Vector3.up)) <= profile.mainBurnAlignmentDegrees &&
                        AngularSpeed <= AngularSettleSpeed)
                        SetPhase(ShuttleVoyagePhase.CruiseAccelerating);
                    break;

                case ShuttleVoyagePhase.CruiseAccelerating:
                    if (Speed >= CurrentCruiseSpeedLimit() - 0.001f)
                        SetPhase(ShuttleVoyagePhase.CruiseCoasting);
                    break;

                case ShuttleVoyagePhase.FlipForBraking:
                    if (brakingConstraintWaypointIndex >= 0 && Speed <= brakingConstraintSpeed + 0.001f)
                        CompleteBrakingConstraint();
                    else if (Speed > (brakingConstraintWaypointIndex >= 0
                            ? brakingConstraintSpeed : profile.approachMaxSpeed) &&
                        Vector3.Angle(flightState.rotation * Vector3.forward, -flightState.velocity) <=
                            profile.mainBurnAlignmentDegrees && AngularSpeed <= AngularSettleSpeed)
                        SetPhase(ShuttleVoyagePhase.CruiseBraking);
                    break;

                case ShuttleVoyagePhase.CruiseBraking:
                    if (brakingConstraintWaypointIndex >= 0 && Speed <= brakingConstraintSpeed + 0.001f)
                        CompleteBrakingConstraint();
                    else if (brakingConstraintWaypointIndex < 0 && Speed <= profile.approachMaxSpeed + 0.001f)
                        SetPhase(ShuttleVoyagePhase.Approach);
                    break;

                case ShuttleVoyagePhase.Approach:
                    if (destination != null && destination.ApproachNode != null &&
                        IsSettledAtProbePose(destination.ApproachNode.position,
                            DockingPoseUtility.GetMatingProbeRotation(destination.ApproachNode.rotation),
                            profile.positionTolerance,
                            profile.velocityTolerance, profile.angleTolerance,
                            profile.captureAngularSpeedTolerance))
                        SetPhase(ShuttleVoyagePhase.DockingTurn);
                    break;

                case ShuttleVoyagePhase.DockingTurn:
                    if (destination != null && destination.ApproachNode != null && destination.DockingNode != null &&
                        IsSettledAtProbePose(destination.ApproachNode.position,
                            DockingPoseUtility.GetMatingProbeRotation(destination.DockingNode.rotation),
                            profile.positionTolerance,
                            profile.velocityTolerance, profile.angleTolerance,
                            profile.captureAngularSpeedTolerance))
                        SetPhase(ShuttleVoyagePhase.FinalDocking);
                    break;

                case ShuttleVoyagePhase.FinalDocking:
                    TryCapture();
                    break;
            }
        }

        private bool IsClearOfOrigin()
        {
            if (originPort == null || originPort.ClearanceNode == null || probe == null)
                return false;
            Vector3 pointVelocity = ProbePointVelocity();
            return Vector3.Distance(ProbePosition(), originPort.ClearanceNode.position) <=
                    Mathf.Max(profile.positionTolerance, originPort.capturePositionTolerance) &&
                pointVelocity.magnitude <= profile.velocityTolerance &&
                Quaternion.Angle(ProbeRotation(), safeDepartureRotation * probeLocalRotation) <=
                    originPort.captureAngleToleranceDegrees &&
                AngularSpeed <= profile.captureAngularSpeedTolerance;
        }

        private bool ShouldBeginApproachAtLowSpeed()
        {
            float distance = DistanceToNextLowSpeedWaypoint();
            return Speed <= profile.approachMaxSpeed &&
                (distance <= Mathf.Max(profile.brakingSafetyMargin, profile.approachMaxSpeed * 2f) ||
                 HasPassedFinalApproachPlane());
        }

        private void CompleteBrakingConstraint()
        {
            bool finalApproachConstraint = route != null &&
                brakingConstraintWaypointIndex == route.Count - 1 &&
                brakingConstraintWaypointIndex >= 0 &&
                route[brakingConstraintWaypointIndex].kind == FlightWaypointKind.Approach;
            SetPhase(finalApproachConstraint
                ? ShuttleVoyagePhase.Approach
                : ShuttleVoyagePhase.CruiseCoasting);
        }

        private bool ShouldFlipForBraking()
        {
            if (!TryGetUpcomingSpeedConstraint(out float distanceToConstraint, out float targetSpeed,
                    out int constraintIndex) ||
                Speed <= targetSpeed + 0.001f)
            {
                brakingDistance = 0f;
                flipAllowanceDistance = 0f;
                return false;
            }
            float acceleration = Mathf.Max(0.001f, profile.mainAcceleration);
            brakingDistance = Mathf.Max(0f, Speed * Speed - targetSpeed * targetSpeed) / (2f * acceleration);
            Vector3 antiVelocity = -flightState.velocity.normalized;
            float error = Vector3.Angle(flightState.rotation * Vector3.forward, antiVelocity);
            float turnTime = ShuttleFlightGuidance.EstimateRotationTime(error, AngularSpeed,
                profile.angularAcceleration, Mathf.Min(profile.maxAngularSpeed, profile.rcsMaxTurnSpeed));
            turnTime += profile.rcsAngularPulseSeconds + profile.rcsMinimumCoastSeconds;
            flipAllowanceDistance = Speed * turnTime;
            float cornerLookahead = navigationProfile != null ? navigationProfile.cornerLookaheadDistance : 0f;
            bool shouldBrake = distanceToConstraint <= brakingDistance + flipAllowanceDistance +
                profile.brakingSafetyMargin + cornerLookahead;
            if (shouldBrake)
            {
                brakingConstraintWaypointIndex = constraintIndex;
                brakingConstraintSpeed = targetSpeed;
            }
            return shouldBrake;
        }

        private float CurrentCruiseSpeedLimit()
        {
            float limit = profile.maxCruiseSpeed;
            FlightWaypoint waypoint = CurrentWaypoint;
            if (waypoint != null && waypoint.kind == FlightWaypointKind.Cruise && waypoint.maxPassSpeed > 0f)
                limit = Mathf.Min(limit, waypoint.maxPassSpeed);
            return Mathf.Max(0.1f, limit);
        }

        private bool TryGetUpcomingSpeedConstraint(out float distance, out float targetSpeed,
            out int constraintIndex)
        {
            distance = float.PositiveInfinity;
            targetSpeed = profile != null ? profile.approachMaxSpeed : 0f;
            constraintIndex = -1;
            if (route == null || route.Count == 0 || profile == null)
                return false;
            Vector3 cursor = ProbePosition();
            float traversed = 0f;
            for (int i = Mathf.Clamp(waypointIndex, 0, route.Count); i < route.Count; i++)
            {
                FlightWaypoint waypoint = route[i];
                traversed += Vector3.Distance(cursor, waypoint.worldPosition);
                cursor = waypoint.worldPosition;
                // Euclidean distance grows again after the shuttle passes the destination.
                // Treat the missed endpoint as an immediate braking constraint so it cannot
                // coast away while waiting for the distance based braking window to return.
                if (i == route.Count - 1 && waypoint.requiresLowArrivalSpeed &&
                    HasPassedFinalApproachPlane())
                    traversed = 0f;
                float limit = waypoint.requiresLowArrivalSpeed
                    ? waypoint.requiredArrivalSpeed
                    : waypoint.maxPassSpeed > 0f ? Mathf.Min(profile.maxCruiseSpeed, waypoint.maxPassSpeed)
                    : profile.maxCruiseSpeed;
                if (limit + 0.001f < Speed)
                {
                    distance = traversed;
                    targetSpeed = limit;
                    constraintIndex = i;
                    return true;
                }
            }
            return false;
        }

        private float DistanceToNextLowSpeedWaypoint()
        {
            if (route == null || route.Count == 0)
                return float.PositiveInfinity;
            Vector3 cursor = ProbePosition();
            float distance = 0f;
            for (int i = Mathf.Clamp(waypointIndex, 0, route.Count); i < route.Count; i++)
            {
                FlightWaypoint waypoint = route[i];
                distance += Vector3.Distance(cursor, waypoint.worldPosition);
                cursor = waypoint.worldPosition;
                if (waypoint.requiresLowArrivalSpeed)
                    return distance;
            }
            return float.PositiveInfinity;
        }

        private bool HasPassedFinalApproachPlane()
        {
            if (route == null || route.Count < 2)
                return false;
            int finalIndex = route.Count - 1;
            FlightWaypoint finalWaypoint = route[finalIndex];
            if (finalWaypoint == null || finalWaypoint.kind != FlightWaypointKind.Approach)
                return false;

            Vector3 inbound = finalWaypoint.worldPosition - route[finalIndex - 1].worldPosition;
            if (inbound.sqrMagnitude <= 0.0001f)
                return false;
            return Vector3.Dot(ProbePosition() - finalWaypoint.worldPosition, inbound.normalized) > 0f;
        }

        private bool AdvanceCruiseWaypoints()
        {
            while (route != null && waypointIndex >= 0 && waypointIndex < route.Count)
            {
                FlightWaypoint waypoint = route[waypointIndex];
                if (waypoint.kind != FlightWaypointKind.Cruise ||
                    (!FlightRoute.CanAdvancePastWaypoint(ProbePosition(), ProbePointVelocity(), waypoint) &&
                     !HasPassedCruiseWaypoint(waypoint, waypointIndex)))
                    break;
                waypointIndex++;
            }
            return route != null && waypointIndex < route.Count;
        }

        private bool HasPassedCruiseWaypoint(FlightWaypoint waypoint, int index)
        {
            if (waypoint == null || index <= 0)
                return false;
            Vector3 segment = waypoint.worldPosition - route[index - 1].worldPosition;
            if (segment.sqrMagnitude <= 0.0001f)
                return false;
            Vector3 direction = segment.normalized;
            if (Vector3.Dot(ProbePosition() - route[index - 1].worldPosition, direction) < segment.magnitude)
                return false;
            return Vector3.Distance(ProbePosition(), waypoint.worldPosition) <= waypoint.arrivalRadius;
        }

        private Vector3 NextRouteDirection()
        {
            if (route == null)
                return flightState.rotation * Vector3.forward;
            for (int i = Mathf.Max(waypointIndex, 0); i < route.Count; i++)
            {
                Vector3 direction = route[i].worldPosition - ProbePosition();
                if (direction.sqrMagnitude > 0.0001f)
                    return direction;
            }
            return flightState.rotation * Vector3.forward;
        }

        private bool TryCapture()
        {
            if (destination == null || destination.DockingNode == null || probe == null)
                return false;
            GetProbePose(out Vector3 pointPosition, out Quaternion pointRotation, out Vector3 pointVelocity);
            if (!destination.IsWithinCaptureTolerance(pointPosition, pointRotation, pointVelocity,
                    AngularSpeed, out _))
                return false;

            if (!DockingPoseUtility.TrySolveRootPose(probeLocalPosition, probeLocalRotation,
                    destination.DockingNode.position,
                    DockingPoseUtility.GetMatingProbeRotation(destination.DockingNode.rotation),
                    out Vector3 exactRootPosition, out Quaternion exactRootRotation))
            {
                Block("could not solve exact root pose for the captured probe");
                return false;
            }
            if (!destination.TryOccupy(probe, out string reason))
            {
                Block($"capture pose was valid, but destination berth could not be occupied: {reason}");
                return false;
            }

            flightState = new ShuttleFlightState(exactRootPosition, Vector3.zero,
                exactRootRotation, Vector3.zero);
            currentDock = destination;
            mainEngineFiring = false;
            SyncRootTransform();
            SetPhase(ShuttleVoyagePhase.Captured);
            SimulationLog.Log($"{name} captured at {destination.name}");
            return true;
        }

        private bool IsSettledAtProbePose(Vector3 targetPosition, Quaternion targetRotation,
            float positionTolerance, float speedTolerance, float angleTolerance, float angularSpeedTolerance)
        {
            GetProbePose(out Vector3 pointPosition, out Quaternion pointRotation, out Vector3 pointVelocity);
            return Vector3.Distance(pointPosition, targetPosition) <= Mathf.Max(0f, positionTolerance) &&
                pointVelocity.magnitude <= Mathf.Max(0f, speedTolerance) &&
                Quaternion.Angle(pointRotation, targetRotation) <= Mathf.Max(0f, angleTolerance) &&
                AngularSpeed <= Mathf.Max(0f, angularSpeedTolerance);
        }

        private Vector3 RcsToProbeTarget(Vector3 targetPosition, float maxSpeed, float dt)
        {
            usedLinearRcsThisSubstep = true;
            ShuttleRcsPulseCommand command = linearRcs.StepLinear(
                ProbePosition(), ProbePointVelocity(), targetPosition,
                Mathf.Max(0.01f, maxSpeed), profile, dt);
            linearRcsActivity = command.activity;
            if (command.started)
                QueueRcsPulse(command.acceleration, Vector3.zero);
            return command.acceleration;
        }

        private float GetFinalDockingSpeedLimit(Vector3 dockingPosition)
        {
            float distance = Vector3.Distance(ProbePosition(), dockingPosition);
            return distance > profile.finalDockingCommitDistance
                ? profile.approachMaxSpeed
                : profile.finalDockMaxSpeed;
        }

        private Vector3 AngularToward(Quaternion targetRotation, float dt)
        {
            usedAngularRcsThisSubstep = true;
            ShuttleRcsPulseCommand command = angularRcs.StepAngular(
                flightState.rotation, flightState.angularVelocity, targetRotation,
                profile, dt, out angularErrorDegrees);
            angularRcsActivity = command.activity;
            if (command.started)
                QueueRcsPulse(Vector3.zero, command.acceleration);
            return command.acceleration;
        }

        private bool TryHandleLocalSafety(float dt)
        {
            if (navigationProfile == null || routePlanner == null || destination == null ||
                (phase != ShuttleVoyagePhase.CruiseAccelerating && phase != ShuttleVoyagePhase.CruiseCoasting &&
                 phase != ShuttleVoyagePhase.FlipForBraking && phase != ShuttleVoyagePhase.CruiseBraking))
            {
                localSafetyState = ShuttleLocalSafetyState.Clear;
                localSafetyBlocker = string.Empty;
                wasSafetyHolding = false;
                return false;
            }

            safetyReplanCooldown = Mathf.Max(0f, safetyReplanCooldown - dt);
            float speed = flightState.velocity.magnitude;
            Vector3 direction = speed > 0.05f ? flightState.velocity / speed : NextRouteDirection().normalized;
            if (direction.sqrMagnitude <= 0.0001f)
                return false;
            float brakingDistance = ShuttleFlightGuidance.StoppingDistance(speed, profile.mainAcceleration);
            float lookahead = Mathf.Max(2f, brakingDistance + profile.brakingSafetyMargin +
                navigationProfile.emergencyClearance);
            Vector3 start = ProbePosition();
            Vector3 end = start + direction * lookahead;
            SpaceNavigationObstacle blocker = SpaceClearanceQuery.FindFirstBlocker(start, end,
                navigationProfile.navigationRadius + navigationProfile.preferredClearance, transform,
                navigationProfile.navigationObstacleLayers.value);

            if (blocker == null)
            {
                bool wasUnsafe = localSafetyState != ShuttleLocalSafetyState.Clear || wasSafetyHolding;
                localSafetyState = wasUnsafe ? ShuttleLocalSafetyState.Replanning : ShuttleLocalSafetyState.Clear;
                localSafetyBlocker = string.Empty;
                wasSafetyHolding = false;
                if (wasUnsafe)
                    TryReplanFromCurrentPosition();
                if (localSafetyState == ShuttleLocalSafetyState.Replanning)
                    localSafetyState = ShuttleLocalSafetyState.Clear;
                return false;
            }

            string stableId = blocker.StableId;
            if (localSafetyBlocker != stableId)
            {
                localSafetyBlocker = stableId;
                safetyReplanCooldown = 0f;
            }
            bool emergency = SpaceClearanceQuery.FindFirstBlocker(start,
                start + direction * Mathf.Max(navigationProfile.SweptRadius, brakingDistance),
                navigationProfile.navigationRadius + navigationProfile.emergencyClearance, transform,
                navigationProfile.navigationObstacleLayers.value) != null;
            localSafetyState = emergency ? ShuttleLocalSafetyState.EmergencyBraking : ShuttleLocalSafetyState.Caution;

            if (speed <= 0.1f)
            {
                localSafetyState = ShuttleLocalSafetyState.AvoidanceHold;
                wasSafetyHolding = true;
                mainEngineFiring = false;
                linearRcs.Reset();
                angularRcs.Reset();
                linearRcsActivity = ShuttleRcsActivity.Settled;
                angularRcsActivity = ShuttleRcsActivity.Settled;
                if (safetyReplanCooldown <= 0f)
                {
                    safetyReplanCooldown = navigationProfile.replanCooldownSeconds;
                    TryReplanFromCurrentPosition();
                }
                return true;
            }

            mainEngineFiring = false;
            linearRcs.Reset();
            linearRcsActivity = ShuttleRcsActivity.Settled;
            usedLinearRcsThisSubstep = false;
            usedAngularRcsThisSubstep = true;
            Vector3 antiVelocity = -flightState.velocity.normalized;
            Quaternion brakingRotation = ShuttleFlightGuidance.RotationForForward(
                antiVelocity, flightState.rotation * Vector3.up);
            Vector3 angularAcceleration = AngularToward(brakingRotation, dt);
            Vector3 linearAcceleration = Vector3.zero;
            float turnError = Quaternion.Angle(flightState.rotation, brakingRotation);
            if (turnError <= profile.mainBurnAlignmentDegrees)
            {
                float throttle = Mathf.Min(profile.mainAcceleration, speed / Mathf.Max(dt, 0.0001f));
                linearAcceleration = (flightState.rotation * Vector3.forward).normalized * throttle;
                mainEngineFiring = throttle > 0f;
            }
            ShuttleFlightIntegrator.StepSubstep(ref flightState, profile,
                linearAcceleration, angularAcceleration, dt);
            wasSafetyHolding = true;
            return true;
        }

        private bool TryReplanFromCurrentPosition()
        {
            if (routePlanner == null || navigationProfile == null || destination == null ||
                destination.ApproachNode == null || Speed > 0.1f)
                return false;
            ShuttleRoutePlan plan = routePlanner.Plan(ProbePosition(), destination.ApproachNode.position,
                navigationProfile, profile.maxCruiseSpeed, transform);
            RecordPlanMetrics(plan);
            routeDiagnostic = plan.failure == ShuttleRouteFailure.None
                ? $"safety replan: {plan.kind} ({plan.diagnostic})"
                : $"safety hold: {plan.failure} ({plan.diagnostic})";
            if (plan.failure != ShuttleRouteFailure.None)
                return false;
            List<FlightWaypoint> waypoints = new List<FlightWaypoint>(plan.cruiseWaypoints);
            waypoints.Add(FlightWaypoint.Approach(destination.ApproachNode.position,
                DockingPoseUtility.GetMatingProbeRotation(destination.ApproachNode.rotation),
                Mathf.Max(0.1f, profile.positionTolerance * 2f), profile.approachMaxSpeed));
            route = new FlightRoute(waypoints);
            waypointIndex = 0;
            brakingConstraintWaypointIndex = -1;
            plannedRouteKind = plan.kind;
            localSafetyState = ShuttleLocalSafetyState.Clear;
            localSafetyBlocker = string.Empty;
            wasSafetyHolding = false;
            SetPhase(ShuttleVoyagePhase.CruiseAccelerating);
            SimulationLog.Log($"{name} safety replan complete: {plan.kind}");
            return true;
        }

        private void RecordPlanMetrics(ShuttleRoutePlan plan)
        {
            routeRelevantObstacleCount = plan.relevantObstacleCount;
            routeCandidateNodeCount = plan.candidateNodeCount;
            routeEdgeSweepCount = plan.edgeSweepCount;
            routeExpansionCount = plan.graphExpansionCount;
            routeLength = plan.routeLength;
            routeLengthRatio = plan.routeLengthRatio;
            routePlanningMilliseconds = (float)plan.planningMilliseconds;
        }

        private void QueueRcsPulse(Vector3 linearAcceleration, Vector3 angularAcceleration)
        {
            if (pendingRcsPulseEvents.Count >= MaxPendingRcsPulseEvents)
                return;
            Quaternion inverseRotation = Quaternion.Inverse(flightState.rotation);
            float strength = linearAcceleration.sqrMagnitude > 0f
                ? Mathf.Clamp01(linearAcceleration.magnitude / Mathf.Max(0.001f, profile.rcsAcceleration))
                : Mathf.Clamp01(angularAcceleration.magnitude / Mathf.Max(0.001f, profile.angularAcceleration));
            pendingRcsPulseEvents.Add(new ShuttleRcsPulseEvent(
                inverseRotation * linearAcceleration, inverseRotation * angularAcceleration, strength));
        }

        private Quaternion RootRotationForProbe(Quaternion worldProbeRotation)
        {
            return ShuttleFlightIntegrator.Normalize(worldProbeRotation * Quaternion.Inverse(probeLocalRotation));
        }

        private Vector3 ProbePosition()
        {
            return flightState.position + flightState.rotation * probeLocalPosition;
        }

        private Quaternion ProbeRotation()
        {
            return ShuttleFlightIntegrator.Normalize(flightState.rotation * probeLocalRotation);
        }

        private Vector3 ProbePointVelocity()
        {
            Vector3 angularRadians = flightState.angularVelocity * Mathf.Deg2Rad;
            return flightState.velocity + Vector3.Cross(angularRadians, flightState.rotation * probeLocalPosition);
        }

        private void GetProbePose(out Vector3 position, out Quaternion rotation, out Vector3 velocity)
        {
            position = ProbePosition();
            rotation = ProbeRotation();
            velocity = ProbePointVelocity();
        }

        private bool EnsureFlightReferences(out string reason)
        {
            EnsureProbeReference();
            if (probe == null)
            {
                reason = "missing Shuttle docking probe component";
                return false;
            }
            if (!probe.ValidateConfiguration(out reason))
                return false;
            if (!CacheProbeOffset())
            {
                reason = "could not read the Shuttle probe offset from its root";
                return false;
            }
            if (!hasInitialState)
                InitializeStateFromTransform();
            reason = "valid";
            return true;
        }

        private bool ValidateProfile(out string reason)
        {
            if (profile == null)
            {
                reason = "missing Shuttle flight profile";
                return false;
            }
            if (!PositiveFinite(profile.mainAcceleration) || !PositiveFinite(profile.rcsAcceleration) ||
                profile.rcsAcceleration > profile.mainAcceleration * 0.5f ||
                !PositiveFinite(profile.maxCruiseSpeed) || !PositiveFinite(profile.approachMaxSpeed) ||
                !PositiveFinite(profile.finalDockMaxSpeed) || !PositiveFinite(profile.angularAcceleration) ||
                !PositiveFinite(profile.maxAngularSpeed) || !PositiveFinite(profile.integrationSubstepSeconds) ||
                !NonNegativeFinite(profile.positionTolerance) || !NonNegativeFinite(profile.velocityTolerance) ||
                !NonNegativeFinite(profile.angleTolerance) || !NonNegativeFinite(profile.captureAngularSpeedTolerance) ||
                !NonNegativeFinite(profile.brakingSafetyMargin) ||
                !NonNegativeFinite(profile.finalDockingCommitDistance) ||
                !ShuttleFlightIntegrator.IsFinite(profile.mainBurnAlignmentDegrees))
            {
                reason = "Shuttle flight profile contains invalid or inconsistent tuning values";
                return false;
            }
            reason = "valid";
            return true;
        }

        private bool TryCopyAndValidateRoute(FlightRoute plannedRoute, DockingPortComponent origin,
            DockingPortComponent requestedDestination, out FlightRoute routeCopy, out string reason)
        {
            routeCopy = null;
            if (plannedRoute == null || !plannedRoute.IsValid || plannedRoute.Count < 2)
            {
                reason = "route must contain a clearance waypoint and a destination approach waypoint";
                return false;
            }
            if (plannedRoute[0].kind != FlightWaypointKind.Clearance ||
                !plannedRoute[0].requiresLowArrivalSpeed ||
                Vector3.Distance(plannedRoute[0].worldPosition, origin.ClearanceNode.position) > PoseEpsilon)
            {
                reason = "route must begin at the current dock's authored clearance node";
                return false;
            }
            FlightWaypoint last = plannedRoute[plannedRoute.Count - 1];
            if (last.kind != FlightWaypointKind.Approach || !last.requiresLowArrivalSpeed ||
                Vector3.Distance(last.worldPosition, requestedDestination.ApproachNode.position) > PoseEpsilon ||
                (last.hasDesiredOrientation && Quaternion.Angle(last.desiredOrientation,
                    DockingPoseUtility.GetMatingProbeRotation(requestedDestination.ApproachNode.rotation)) > 0.1f))
            {
                reason = "route must end at the destination's authored approach node and orientation";
                return false;
            }
            for (int i = 1; i < plannedRoute.Count - 1; i++)
            {
                if (plannedRoute[i].kind != FlightWaypointKind.Cruise || plannedRoute[i].requiresLowArrivalSpeed)
                {
                    reason = "intermediate route waypoints must be non-stopping Cruise targets";
                    return false;
                }
            }

            List<FlightWaypoint> copy = new List<FlightWaypoint>(plannedRoute.Count);
            for (int i = 0; i < plannedRoute.Count; i++)
            {
                FlightWaypoint source = plannedRoute[i];
                copy.Add(new FlightWaypoint(source.worldPosition, source.kind, source.arrivalRadius,
                    source.requiresLowArrivalSpeed, source.requiredArrivalSpeed,
                    source.hasDesiredOrientation ? (Quaternion?)source.desiredOrientation : null));
            }
            routeCopy = new FlightRoute(copy);
            reason = "valid";
            return true;
        }

        private bool CacheProbeOffset()
        {
            if (probe == null || probe.ProbeTransform == null)
                return false;
            if (!hasProbeOffset)
            {
                if (!DockingPoseUtility.TryGetProbePoseRelativeToRoot(transform, probe.ProbeTransform,
                        out probeLocalPosition, out probeLocalRotation))
                    return false;
                hasProbeOffset = true;
            }
            return true;
        }

        private void EnsureProbeReference()
        {
            if (probe == null)
                probe = GetComponent<ShuttleDockingProbeComponent>();
        }

        private void InitializeStateFromTransform()
        {
            if (transform != null)
            {
                flightState = ShuttleFlightState.FromTransform(transform);
                hasInitialState = true;
                CacheProbeOffset();
            }
        }

        private void SyncRootTransform()
        {
            if (ShuttleFlightIntegrator.IsFinite(flightState.position) &&
                ShuttleFlightIntegrator.IsFinite(flightState.rotation))
                transform.SetPositionAndRotation(flightState.position, flightState.rotation);
        }

        private void UpdateDiagnostics()
        {
            Vector3? target = null;
            switch (phase)
            {
                case ShuttleVoyagePhase.Undocking:
                case ShuttleVoyagePhase.AlignDeparture:
                    if (originPort != null && originPort.ClearanceNode != null)
                        target = originPort.ClearanceNode.position;
                    break;
                case ShuttleVoyagePhase.Approach:
                    if (destination != null && destination.ApproachNode != null)
                        target = destination.ApproachNode.position;
                    break;
                case ShuttleVoyagePhase.DockingTurn:
                    if (destination != null && destination.ApproachNode != null)
                        target = destination.ApproachNode.position;
                    break;
                case ShuttleVoyagePhase.FinalDocking:
                    if (destination != null && destination.DockingNode != null)
                        target = destination.DockingNode.position;
                    break;
                default:
                    if (CurrentWaypoint != null)
                        target = CurrentWaypoint.worldPosition;
                    break;
            }
            segmentDistance = target.HasValue ? Vector3.Distance(ProbePosition(), target.Value) : 0f;
            if (profile != null && Speed > profile.approachMaxSpeed &&
                (phase == ShuttleVoyagePhase.CruiseAccelerating ||
                 phase == ShuttleVoyagePhase.CruiseCoasting ||
                 phase == ShuttleVoyagePhase.FlipForBraking ||
                 phase == ShuttleVoyagePhase.CruiseBraking))
                ShouldFlipForBraking();
            else
            {
                brakingDistance = 0f;
                flipAllowanceDistance = 0f;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (navigationProfile == null || routePlanner == null || !routePlanner.ShowDebugGizmos)
                return;
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.45f);
            Gizmos.DrawWireSphere(transform.position, navigationProfile.SweptRadius);
        }

        private void Block(string reason)
        {
            blockReason = string.IsNullOrEmpty(reason) ? "unspecified flight structure failure" : reason;
            mainEngineFiring = false;
            if (destination != null && probe != null && destination.State == DockingPortState.Reserved &&
                destination.ReservationHolder == probe)
                destination.Release(probe);
            SetPhase(ShuttleVoyagePhase.Blocked);
            SimulationLog.Log($"{name} voyage blocked: {blockReason}");
        }

        private void SetPhase(ShuttleVoyagePhase value)
        {
            if (phase == value)
                return;
            ShuttleVoyagePhase previous = phase;
            phase = value;
            if (value == ShuttleVoyagePhase.Approach)
                brakingConstraintWaypointIndex = -1;
            linearRcs.Reset();
            angularRcs.Reset();
            linearRcsActivity = ShuttleRcsActivity.Settled;
            angularRcsActivity = ShuttleRcsActivity.Settled;
            if (Application.isPlaying)
                SimulationLog.Log($"{name} shuttle phase: {previous} → {phase}");
        }

        private static bool IsFlightPhase(ShuttleVoyagePhase value)
        {
            return value != ShuttleVoyagePhase.Docked && value != ShuttleVoyagePhase.Captured &&
                value != ShuttleVoyagePhase.Blocked;
        }

        private static bool PositiveFinite(float value)
        {
            return ShuttleFlightIntegrator.IsFinite(value) && value > 0f;
        }

        private static bool NonNegativeFinite(float value)
        {
            return ShuttleFlightIntegrator.IsFinite(value) && value >= 0f;
        }

        [ContextMenu("Debug Flight to Port A")]
        private void DebugFlyToPortA()
        {
            if (!TryRequestVoyage(debugPortA, out string reason))
                Debug.LogWarning($"{name}: debug flight to Port A rejected: {reason}", this);
        }

        [ContextMenu("Debug Flight to Port B")]
        private void DebugFlyToPortB()
        {
            if (!TryRequestVoyage(debugPortB, out string reason))
                Debug.LogWarning($"{name}: debug flight to Port B rejected: {reason}", this);
        }
    }
}
