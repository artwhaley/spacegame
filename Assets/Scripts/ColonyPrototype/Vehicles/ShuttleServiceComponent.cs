using System.Collections.Generic;
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
        private sealed class PassengerRecord
        {
            public ShuttleTransportRequest Request;
            public ColonistIdentity Person;
        }

        [SerializeField] private string stableId;
        [SerializeField] private ShuttleBaseComponent homeBase;
        [SerializeField] private ShuttleVoyageComponent voyage;
        [SerializeField, Min(0)] private int passengerCapacity = 8;
        [SerializeField, Min(0)] private int freightCapacity = 10;
        [SerializeField] private Transform passengerAnchor;
        [SerializeField] private InventoryComponent cargoInventory;
        [SerializeField] private ShuttlePilotWorkService pilotService;

        private readonly List<PassengerRecord> passengers = new List<PassengerRecord>();
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
        public bool PilotReleasePending => pilotService != null && pilotService.PilotReleasePending;
        public int PassengerCapacity => passengerCapacity;
        public int FreightCapacity => freightCapacity;
        public InventoryComponent CargoInventory => cargoInventory;
        public ShuttleTrip CurrentTrip => currentTrip;
        public IReadOnlyList<ColonistIdentity> Passengers
        {
            get
            {
                List<ColonistIdentity> result = new List<ColonistIdentity>();
                for (int index = 0; index < passengers.Count; index++)
                    if (passengers[index]?.Person != null)
                        result.Add(passengers[index].Person);
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

        private void Awake() => ResolveReferences();

        private void OnEnable()
        {
            ResolveReferences();
            ShuttleManager.Instance?.RegisterShuttle(this);
        }

        private void Start() => ShuttleManager.Instance?.RegisterShuttle(this);

        private void OnDisable() => ShuttleManager.Instance?.UnregisterShuttle(this);

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
                passengers.Count >= passengerCapacity || !request.IsPayloadReady)
                return false;

            NavMeshAgent agent = person.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled)
            {
                agent.isStopped = true;
                agent.enabled = false;
            }
            Transform personTransform = person.transform;
            personTransform.SetParent(transform, true);
            Transform anchor = passengerAnchor != null ? passengerAnchor : transform;
            personTransform.position = anchor.position + Vector3.up * (0.25f + passengers.Count * 0.35f);
            passengers.Add(new PassengerRecord { Request = request, Person = person });
            SimulationLog.Log("[B2Shuttle] boarded passenger=" + person.name +
                ", pilot=" + (Pilot != null ? Pilot.name : "none") +
                ", trip=" + currentTrip.Id);
            request.IsPhysicallyTransferred = true;
            request.SetState(ShuttleTransportRequestState.LoadingOrBoarding);
            return true;
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
                        if (person != null && !ContainsPassenger(person))
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
                    ", passengers=" + passengers.Count +
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
            SimulationLog.Log("[B2Shuttle] departed; trip=" + currentTrip.Id +
                ", pilot=" + (Pilot != null ? Pilot.name : "none"));
            OperationalState = ShuttleOperationalState.InTransit;
            for (int index = 0; index < currentTrip.Requests.Count; index++)
                currentTrip.Requests[index].SetState(ShuttleTransportRequestState.InTransit);
        }

        private void CompleteTripAtDestination()
        {
            OperationalState = ShuttleOperationalState.UnloadingOrDisembarking;
            currentTrip.State = ShuttleTripState.UnloadingOrDisembarking;
            for (int index = passengers.Count - 1; index >= 0; index--)
            {
                PassengerRecord passenger = passengers[index];
                if (passenger == null || passenger.Request == null ||
                    passenger.Request.Destination != currentTrip.Destination)
                    continue;
                Disembark(passenger);
                passengers.RemoveAt(index);
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

        private void Disembark(PassengerRecord passenger)
        {
            if (passenger.Person == null)
                return;
            Transform target = currentTrip.Destination.TransferAnchor;
            Transform personTransform = passenger.Person.transform;
            personTransform.SetParent(null, true);
            personTransform.position = target.position;
            NavMeshAgent agent = passenger.Person.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = true;
                if (NavMesh.SamplePosition(target.position, out NavMeshHit hit, 2f, agent.areaMask))
                    agent.Warp(hit.position);
                agent.isStopped = true;
            }
        }

        private bool ContainsPassenger(ColonistIdentity person)
        {
            for (int index = 0; index < passengers.Count; index++)
                if (passengers[index] != null && passengers[index].Person == person)
                    return true;
            return false;
        }

        private bool TryEnsurePilot()
        {
            return pilotService == null || pilotService.TryAcquirePilot();
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
