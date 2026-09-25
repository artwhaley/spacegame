using System.Collections.Generic;
using Colony.Interactions;
using UnityEngine;
using UnityEngine.AI;

namespace AsteroidColony
{
    public enum ShuttleOperationalState
    {
        Idle,
        Repositioning,
        LoadingOrBoarding,
        InTransit,
        UnloadingOrDisembarking,
        Blocked
    }

    /// <summary>
    /// Vehicle-side physical custody and voyage adapter. It never chooses work
    /// and never writes the Shuttle root transform; voyage owns that authority.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ShuttleVoyageComponent))]
    [AddComponentMenu("Colony/Vehicles/Shuttle Service")]
    public sealed class ShuttleServiceComponent : MonoBehaviour
    {
        private sealed class PresentationComponentState
        {
            public Component Component;
            public bool Enabled;
        }

        private sealed class BoardedActorState
        {
            public ColonistIdentity Actor;
            public ShuttleTransportRequest Request;
            public bool IsPilot;
            public NavMeshAgent Agent;
            public bool AgentWasEnabled;
            public bool AgentWasStopped;
            public bool AgentUpdatedPosition;
            public bool AgentUpdatedRotation;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public PresentationComponentState[] Renderers;
            public PresentationComponentState[] Canvases;
        }

        [SerializeField] private string stableId;
        [SerializeField] private ShuttleBaseComponent homeBase;
        [SerializeField] private ShuttleVoyageComponent voyage;
        [SerializeField, Min(0)] private int passengerCapacity = 8;
        [SerializeField, Min(0)] private int freightCapacity = 10;
        [SerializeField] private Transform passengerAnchor;
        [SerializeField] private InventoryComponent cargoInventory;
        [SerializeField] private ShuttlePilotWorkService pilotService;

        private readonly List<BoardedActorState> boardedActors = new List<BoardedActorState>();
        private ShuttleTrip currentTrip;
        private ShuttleTransferEndpoint activeOrigin;
        private string lastTripDiagnostic;

        public string StableId => string.IsNullOrWhiteSpace(stableId)
            ? SceneStableIdentity.GetKey(this)
            : stableId;
        public ShuttleBaseComponent HomeBase => homeBase;
        public ShuttleVoyageComponent Voyage => voyage;
        public DockingPortComponent CurrentDock => voyage != null ? voyage.CurrentDock : null;
        public ShuttlePilotWorkService PilotService => pilotService;
        public ColonistIdentity Pilot => pilotService != null ? pilotService.Pilot : null;
        public bool PilotAboard => Pilot != null && IsActorAboard(Pilot, true);
        public bool PilotReleasePending => pilotService != null && pilotService.PilotReleasePending;
        public int PassengerCapacity => passengerCapacity;
        public int PassengerCount => CountPassengers();
        public int FreightCapacity => freightCapacity;
        public InventoryComponent CargoInventory => cargoInventory;
        public ShuttleTrip CurrentTrip => currentTrip;
        public IReadOnlyList<ColonistIdentity> Passengers
        {
            get
            {
                List<ColonistIdentity> result = new List<ColonistIdentity>();
                for (int index = 0; index < boardedActors.Count; index++)
                    if (boardedActors[index] != null && !boardedActors[index].IsPilot &&
                        boardedActors[index].Actor != null)
                        result.Add(boardedActors[index].Actor);
                return result;
            }
        }
        public ShuttleOperationalState OperationalState { get; private set; } = ShuttleOperationalState.Idle;

        public void Configure(string id, ShuttleBaseComponent baseComponent,
            ShuttleVoyageComponent acceptedVoyage, InventoryComponent inventory,
            Transform seatAnchor, int passengerLimit, int freightLimit,
            ShuttlePilotWorkService operations)
        {
            stableId = id ?? string.Empty;
            homeBase = baseComponent;
            voyage = acceptedVoyage;
            cargoInventory = inventory;
            passengerAnchor = seatAnchor;
            passengerCapacity = Mathf.Max(0, passengerLimit);
            freightCapacity = Mathf.Max(0, freightLimit);
            pilotService = operations;
        }

        private void Reset() => ResolveReferences();

        private void Awake()
        {
            ResolveReferences();
            pilotService?.BindVehicle(this);
        }

        private void OnEnable()
        {
            ResolveReferences();
            pilotService?.BindVehicle(this);
            ShuttleManager.Instance?.RegisterShuttle(this);
        }

        private void Start()
        {
            ResolveReferences();
            pilotService?.BindVehicle(this);
            ShuttleManager.Instance?.RegisterShuttle(this);
        }

        private void OnDisable()
        {
            ShuttleManager.Instance?.UnregisterShuttle(this);
            BlockTripForLostShuttle();
            DetachAllBoardedActors();
        }

        private void LateUpdate()
        {
            for (int index = 0; index < boardedActors.Count; index++)
            {
                BoardedActorState state = boardedActors[index];
                if (state?.Actor == null)
                    continue;
                state.Actor.transform.SetPositionAndRotation(
                    transform.TransformPoint(state.LocalPosition),
                    transform.rotation * state.LocalRotation);
            }
        }

        public bool CanCarry(ShuttlePayloadType type)
        {
            return type == ShuttlePayloadType.Passenger
                ? passengerCapacity > 0
                : freightCapacity > 0 && cargoInventory != null;
        }

        public bool TryAcceptTrip(ShuttleTrip trip)
        {
            ResolveReferences();
            if (trip == null || currentTrip != null || voyage == null ||
                trip.Shuttle != this || trip.Origin == null || trip.Destination == null)
                return false;

            currentTrip = trip;
            activeOrigin = trip.Origin;
            lastTripDiagnostic = null;
            pilotService?.BindVehicle(this);
            if (!TryEnsurePilot())
            {
                LogTripDiagnostic("waiting for pilot at trip acceptance; pilotService=" +
                    (pilotService != null ? pilotService.name : "missing"));
                trip.State = ShuttleTripState.Queued;
                OperationalState = ShuttleOperationalState.Idle;
                for (int index = 0; index < trip.Requests.Count; index++)
                    trip.Requests[index].SetState(ShuttleTransportRequestState.WaitingForPilot);
                return true;
            }

            return BeginOrReposition();
        }

        public void SimulationTickTransport()
        {
            ResolveReferences();
            if (currentTrip == null || voyage == null)
                return;
            if (currentTrip.State == ShuttleTripState.Blocked ||
                currentTrip.State == ShuttleTripState.Cancelled)
                return;

            if (!TryEnsurePilot())
            {
                LogTripDiagnostic("waiting for pilot; trip=" + currentTrip.Id);
                for (int index = 0; index < currentTrip.Requests.Count; index++)
                    currentTrip.Requests[index].SetState(ShuttleTransportRequestState.WaitingForPilot);
                return;
            }

            if (voyage.Phase == ShuttleVoyagePhase.Blocked)
            {
                OperationalState = ShuttleOperationalState.Blocked;
                currentTrip.State = ShuttleTripState.Blocked;
                return;
            }

            if (voyage.Phase != ShuttleVoyagePhase.Docked)
            {
                OperationalState = ShuttleOperationalState.InTransit;
                currentTrip.State = ShuttleTripState.InTransit;
                return;
            }

            if (CurrentDock != activeOrigin.DockingPort &&
                CurrentDock != currentTrip.Destination.DockingPort)
            {
                BeginOrReposition();
                return;
            }

            if (CurrentDock == activeOrigin.DockingPort)
            {
                PrepareAndDepart();
                return;
            }

            if (CurrentDock == currentTrip.Destination.DockingPort)
                CompleteTripAtDestination();
        }

        public bool TryReturnHome()
        {
            if (homeBase == null || homeBase.DockingPort == null || voyage == null ||
                voyage.Phase != ShuttleVoyagePhase.Docked || CurrentDock == homeBase.DockingPort)
                return CurrentDock == (homeBase != null ? homeBase.DockingPort : null);
            return voyage.TryRequestVoyage(homeBase.DockingPort, out _);
        }

        internal bool TryBoardPassenger(ShuttleTransportRequest request, ColonistIdentity person)
        {
            if (request == null || person == null || currentTrip == null ||
                request.AssignedTrip != currentTrip || request.PayloadType != ShuttlePayloadType.Passenger ||
                request.Origin != activeOrigin || CurrentDock != activeOrigin.DockingPort ||
                PassengerCount >= passengerCapacity || !request.IsPayloadReady)
                return false;

            if (!TryBoardActor(person, request, false))
                return false;
            request.IsPhysicallyTransferred = true;
            request.SetState(ShuttleTransportRequestState.LoadingOrBoarding);
            ShuttleDiagnosticLog.Record("passenger_physically_boarded",
                "request=" + request.Id + ", actor=" + person.name +
                ", trip=" + currentTrip.Id + ", pilotAboard=" + PilotAboard);
            return true;
        }

        internal bool TryBoardPilot(ColonistIdentity pilot)
        {
            if (pilot == null || pilotService == null || pilotService.Pilot != pilot ||
                homeBase == null || voyage == null || CurrentDock != homeBase.DockingPort ||
                voyage.Phase != ShuttleVoyagePhase.Docked)
                return false;
            if (IsActorAboard(pilot, true))
                return true;
            bool boarded = TryBoardActor(pilot, null, true);
            if (boarded)
                ShuttleDiagnosticLog.Record("pilot_physically_boarded",
                    "pilot=" + pilot.name + ", shuttle=" + StableId);
            return boarded;
        }

        internal bool TryDisembarkPilotAtHome(ColonistIdentity pilot)
        {
            if (pilot == null || homeBase == null || CurrentDock != homeBase.DockingPort ||
                voyage == null || voyage.Phase != ShuttleVoyagePhase.Docked || currentTrip != null ||
                homeBase.DutyAnchor == null)
                return false;
            for (int index = boardedActors.Count - 1; index >= 0; index--)
            {
                BoardedActorState state = boardedActors[index];
                if (state == null || !state.IsPilot || state.Actor != pilot)
                    continue;
                if (!DisembarkActor(state, homeBase.DutyAnchor))
                    return false;
                boardedActors.RemoveAt(index);
                ShuttleDiagnosticLog.Record("pilot_disembarked_home",
                    "pilot=" + pilot.name + ", shuttle=" + StableId);
                return true;
            }
            return false;
        }

        private bool BeginOrReposition()
        {
            if (CurrentDock != activeOrigin.DockingPort)
            {
                OperationalState = ShuttleOperationalState.Repositioning;
                currentTrip.State = ShuttleTripState.Repositioning;
                return voyage.TryRequestVoyage(activeOrigin.DockingPort, out _);
            }

            PrepareAndDepart();
            return true;
        }

        private void PrepareAndDepart()
        {
            if (currentTrip == null || CurrentDock != activeOrigin.DockingPort)
                return;

            OperationalState = ShuttleOperationalState.LoadingOrBoarding;
            currentTrip.State = ShuttleTripState.LoadingOrBoarding;
            bool allReady = true;
            for (int index = 0; index < currentTrip.Requests.Count; index++)
            {
                ShuttleTransportRequest request = currentTrip.Requests[index];
                if (!request.IsPayloadReady || request.IsTerminal)
                {
                    allReady = false;
                    continue;
                }

                if (request.PayloadType == ShuttlePayloadType.Passenger)
                {
                    for (int payloadIndex = 0; payloadIndex < request.Payloads.Count; payloadIndex++)
                    {
                        ColonistIdentity person = request.Payloads[payloadIndex] as ColonistIdentity;
                        if (person != null && !IsActorAboard(person, false))
                            TryBoardPassenger(request, person);
                    }
                }
                else
                {
                    IShuttleTransportPayload payload = request.Payload as IShuttleTransportPayload;
                    if (payload is ShuttleFreightExecution freightPayload)
                        freightPayload.BindShuttle(this);
                    if (payload == null || !payload.TryLoad(this, request))
                        allReady = false;
                }

                if (!request.IsPhysicallyTransferred)
                    allReady = false;
            }

            if (!allReady)
            {
                LogTripDiagnostic("loading incomplete; trip=" + currentTrip.Id +
                    ", passengers=" + PassengerCount +
                    ", requests=" + currentTrip.Requests.Count);
                return;
            }

            if (!voyage.TryRequestVoyage(currentTrip.Destination.DockingPort, out string departureReason))
            {
                LogTripDiagnostic("departure rejected; trip=" + currentTrip.Id +
                    ", reason=" + departureReason +
                    ", currentDock=" + (CurrentDock != null ? CurrentDock.name : "none") +
                    ", destinationDock=" + (currentTrip.Destination.DockingPort != null
                        ? currentTrip.Destination.DockingPort.name : "none"));
                currentTrip.State = ShuttleTripState.Blocked;
                OperationalState = ShuttleOperationalState.Blocked;
                return;
            }

            currentTrip.State = ShuttleTripState.InTransit;
            ShuttleDiagnosticLog.Record("voyage_departure_accepted",
                "trip=" + currentTrip.Id + ", pilot=" + (Pilot != null ? Pilot.name : "none"));
            OperationalState = ShuttleOperationalState.InTransit;
            for (int index = 0; index < currentTrip.Requests.Count; index++)
                currentTrip.Requests[index].SetState(ShuttleTransportRequestState.InTransit);
        }

        private void CompleteTripAtDestination()
        {
            OperationalState = ShuttleOperationalState.UnloadingOrDisembarking;
            currentTrip.State = ShuttleTripState.UnloadingOrDisembarking;
            for (int index = boardedActors.Count - 1; index >= 0; index--)
            {
                BoardedActorState actor = boardedActors[index];
                if (actor == null || actor.IsPilot || actor.Request == null ||
                    actor.Request.Destination != currentTrip.Destination)
                    continue;
                if (!DisembarkActor(actor, currentTrip.Destination.TransferAnchor))
                {
                    currentTrip.State = ShuttleTripState.Blocked;
                    OperationalState = ShuttleOperationalState.Blocked;
                    LogTripDiagnostic("disembark blocked by missing destination NavMesh; trip=" +
                        currentTrip.Id + ", actor=" + actor.Actor.name);
                    return;
                }
                boardedActors.RemoveAt(index);
            }

            for (int index = 0; index < currentTrip.Requests.Count; index++)
            {
                ShuttleTransportRequest request = currentTrip.Requests[index];
                if (request.PayloadType == ShuttlePayloadType.Freight)
                {
                    IShuttleTransportPayload payload = request.Payload as IShuttleTransportPayload;
                    payload?.OnShuttleArrived(this, request);
                }
                if (!request.IsTerminal)
                    request.Complete();
            }

            ShuttleManager.Instance?.NotifyTripCompleted(currentTrip);
            pilotService?.NotifyTripCompleted(this);
            currentTrip = null;
            activeOrigin = null;
            OperationalState = ShuttleOperationalState.Idle;
        }

        private void LogTripDiagnostic(string diagnostic)
        {
            if (diagnostic == lastTripDiagnostic)
                return;
            lastTripDiagnostic = diagnostic;
            SimulationLog.Log("[B2Shuttle] " + diagnostic);
        }

        private bool TryBoardActor(ColonistIdentity person,
            ShuttleTransportRequest request, bool isPilot)
        {
            if (person == null || IsActorAboard(person, isPilot))
                return person != null;

            Transform anchor = passengerAnchor != null ? passengerAnchor : transform;
            int aboardIndex = boardedActors.Count;
            Vector3 boardingPosition = anchor.position + anchor.up * (0.2f + aboardIndex * 0.18f);
            Quaternion boardingRotation = anchor.rotation;
            NavMeshAgent agent = person.GetComponent<NavMeshAgent>();
            BoardedActorState state = new BoardedActorState
            {
                Actor = person,
                Request = request,
                IsPilot = isPilot,
                Agent = agent,
                AgentWasEnabled = agent != null && agent.enabled,
                AgentWasStopped = agent == null || !agent.isActiveAndEnabled ||
                    !agent.isOnNavMesh || agent.isStopped,
                AgentUpdatedPosition = agent != null && agent.updatePosition,
                AgentUpdatedRotation = agent != null && agent.updateRotation,
                LocalPosition = transform.InverseTransformPoint(boardingPosition),
                LocalRotation = Quaternion.Inverse(transform.rotation) * boardingRotation,
                Renderers = CaptureEnabledStates(person.GetComponentsInChildren<Renderer>(true)),
                Canvases = CaptureEnabledStates(person.GetComponentsInChildren<Canvas>(true))
            };

            person.GetComponent<ColonistMotor>()?.Stop();
            if (agent != null && agent.enabled)
            {
                if (agent.isActiveAndEnabled && agent.isOnNavMesh)
                    agent.isStopped = true;
                agent.enabled = false;
            }
            person.transform.SetPositionAndRotation(boardingPosition, boardingRotation);
            SetPresentationVisible(state, false);
            boardedActors.Add(state);
            return true;
        }

        private bool DisembarkActor(BoardedActorState state, Transform target)
        {
            if (state == null || state.Actor == null || target == null)
                return false;

            NavMeshAgent agent = state.Agent;
            NavMeshHit hit = default;
            bool shouldRestoreAgent = agent != null && state.AgentWasEnabled;
            if (shouldRestoreAgent && !NavMesh.SamplePosition(target.position, out hit, 2f, agent.areaMask))
            {
                ShuttleDiagnosticLog.Record("actor_disembark_waiting_for_navmesh",
                    "actor=" + state.Actor.name + ", target=" + target.position);
                return false;
            }

            Vector3 destination = shouldRestoreAgent ? hit.position : target.position;
            Vector3 priorPosition = state.Actor.transform.position;
            Quaternion priorRotation = state.Actor.transform.rotation;
            state.Actor.transform.SetPositionAndRotation(destination, target.rotation);
            if (shouldRestoreAgent)
            {
                agent.enabled = true;
                if (!agent.Warp(destination))
                {
                    agent.enabled = false;
                    state.Actor.transform.SetPositionAndRotation(priorPosition, priorRotation);
                    ShuttleDiagnosticLog.Record("actor_disembark_waiting_for_navmesh",
                        "actor=" + state.Actor.name + ", reason=warp_rejected");
                    return false;
                }
                agent.updatePosition = state.AgentUpdatedPosition;
                agent.updateRotation = state.AgentUpdatedRotation;
                agent.isStopped = state.AgentWasStopped;
            }
            SetPresentationVisible(state, true);
            return true;
        }

        private void DetachAllBoardedActors()
        {
            for (int index = boardedActors.Count - 1; index >= 0; index--)
            {
                BoardedActorState state = boardedActors[index];
                if (state?.Actor == null)
                    continue;
                Vector3 lastPosition = state.Actor.transform.position;
                RestoreNavigationAt(state, lastPosition);
                SetPresentationVisible(state, true);
                ShuttleDiagnosticLog.Record("boarded_actor_released_with_shuttle_disabled",
                    "actor=" + state.Actor.name + ", pilot=" + state.IsPilot +
                    ", position=" + lastPosition);
            }
            boardedActors.Clear();
        }

        private void RestoreNavigationAt(BoardedActorState state, Vector3 worldPosition)
        {
            NavMeshAgent agent = state != null ? state.Agent : null;
            if (agent == null || !state.AgentWasEnabled)
            {
                if (agent != null)
                    agent.enabled = false;
                return;
            }

            if (!NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, 2f, agent.areaMask))
            {
                agent.enabled = false;
                ShuttleDiagnosticLog.Record("actor_navmesh_reattach_failed",
                    "actor=" + state.Actor.name + ", position=" + worldPosition);
                return;
            }

            state.Actor.transform.position = hit.position;
            agent.enabled = true;
            if (!agent.Warp(hit.position))
            {
                agent.enabled = false;
                ShuttleDiagnosticLog.Record("actor_navmesh_reattach_failed",
                    "actor=" + state.Actor.name + ", reason=warp_rejected, position=" + hit.position);
                return;
            }
            agent.updatePosition = state.AgentUpdatedPosition;
            agent.updateRotation = state.AgentUpdatedRotation;
            agent.isStopped = state.AgentWasStopped;
        }

        private static PresentationComponentState[] CaptureEnabledStates(Renderer[] components)
        {
            PresentationComponentState[] states = new PresentationComponentState[components.Length];
            for (int index = 0; index < components.Length; index++)
            {
                states[index] = new PresentationComponentState
                {
                    Component = components[index],
                    Enabled = components[index] != null && components[index].enabled
                };
            }
            return states;
        }

        private static PresentationComponentState[] CaptureEnabledStates(Canvas[] components)
        {
            PresentationComponentState[] states = new PresentationComponentState[components.Length];
            for (int index = 0; index < components.Length; index++)
            {
                states[index] = new PresentationComponentState
                {
                    Component = components[index],
                    Enabled = components[index] != null && components[index].enabled
                };
            }
            return states;
        }

        private static void SetPresentationVisible(BoardedActorState state, bool visible)
        {
            SetComponentsEnabled(state != null ? state.Renderers : null, visible);
            SetComponentsEnabled(state != null ? state.Canvases : null, visible);
        }

        private static void SetComponentsEnabled(PresentationComponentState[] states, bool visible)
        {
            if (states == null)
                return;
            for (int index = 0; index < states.Length; index++)
            {
                PresentationComponentState saved = states[index];
                if (saved?.Component is Renderer renderer)
                    renderer.enabled = visible && saved.Enabled;
                else if (saved?.Component is Canvas canvas)
                    canvas.enabled = visible && saved.Enabled;
            }
        }

        private bool IsActorAboard(ColonistIdentity person, bool pilot)
        {
            for (int index = 0; index < boardedActors.Count; index++)
                if (boardedActors[index] != null && boardedActors[index].Actor == person &&
                    boardedActors[index].IsPilot == pilot)
                    return true;
            return false;
        }

        private int CountPassengers()
        {
            int count = 0;
            for (int index = 0; index < boardedActors.Count; index++)
                if (boardedActors[index] != null && !boardedActors[index].IsPilot &&
                    boardedActors[index].Actor != null)
                    count++;
            return count;
        }

        private void BlockTripForLostShuttle()
        {
            if (currentTrip == null || currentTrip.State == ShuttleTripState.Completed ||
                currentTrip.State == ShuttleTripState.Cancelled)
                return;
            currentTrip.State = ShuttleTripState.Blocked;
            for (int index = 0; index < currentTrip.Requests.Count; index++)
            {
                ShuttleTransportRequest request = currentTrip.Requests[index];
                if (request != null && !request.IsTerminal)
                    request.SetState(ShuttleTransportRequestState.Blocked);
            }
            OperationalState = ShuttleOperationalState.Blocked;
            ShuttleDiagnosticLog.Record("active_trip_blocked_shuttle_disabled",
                "trip=" + currentTrip.Id + ", shuttle=" + StableId);
        }

        private bool TryEnsurePilot()
        {
            return pilotService != null && pilotService.TryAcquirePilot() && PilotAboard;
        }

        private void ResolveReferences()
        {
            if (voyage == null)
                voyage = GetComponent<ShuttleVoyageComponent>();
            if (cargoInventory == null)
                cargoInventory = GetComponent<InventoryComponent>();
            if (pilotService == null)
                pilotService = GetComponent<ShuttlePilotWorkService>();
            if (passengerAnchor == null)
            {
                Transform candidate = transform.Find("PassengerAnchor");
                if (candidate != null)
                    passengerAnchor = candidate;
            }
        }
    }
}
