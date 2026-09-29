using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace AsteroidColony
{
    internal static class ShuttleDiagnosticLog
    {
        public static void Record(string transition, string details)
        {
            SimulationManager simulation = SimulationManager.Instance;
            string timestamp = "[game time unavailable]";
            if (simulation != null)
            {
                float absoluteHour = Mathf.Max(0f, simulation.CurrentGameHour);
                int day = Mathf.FloorToInt(absoluteHour / 24f) + 1;
                int minuteOfDay = Mathf.FloorToInt((absoluteHour % 24f) * 60f);
                timestamp = string.Format(CultureInfo.InvariantCulture,
                    "[Day {0} {1:00}:{2:00}]", day, minuteOfDay / 60, minuteOfDay % 60);
            }
            SimulationLog.Log(timestamp + " [B2Transit] " + transition + " " + details);
        }
    }

    public enum ShuttlePayloadType
    {
        Passenger,
        Freight
    }

    public enum ShuttleTransportRequestState
    {
        Queued,
        WaitingForPayload,
        ReadyForPickup,
        WaitingForPilot,
        Assigned,
        LoadingOrBoarding,
        InTransit,
        UnloadingOrDisembarking,
        Paused,
        Completed,
        Cancelled,
        Blocked
    }

    public enum ShuttleTripState
    {
        Queued,
        Repositioning,
        LoadingOrBoarding,
        InTransit,
        UnloadingOrDisembarking,
        Paused,
        Completed,
        Blocked,
        Cancelled
    }

    /// <summary>Transportation owed by the Shuttle system, independent of a vehicle.</summary>
    public sealed class ShuttleTransportRequest
    {
        private readonly List<object> payloads = new List<object>();

        internal ShuttleTransportRequest(string id, string correlationKey,
            ShuttlePayloadType payloadType, ShuttleTransferEndpoint origin,
            ShuttleTransferEndpoint destination, int priority, long createdTick)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A Shuttle request requires an ID.", nameof(id));
            if (origin == null || destination == null || origin == destination)
                throw new ArgumentException("A Shuttle request requires two distinct endpoints.");

            Id = id;
            CorrelationKey = correlationKey ?? string.Empty;
            PayloadType = payloadType;
            Origin = origin;
            Destination = destination;
            Priority = priority;
            CreatedTick = createdTick;
            State = ShuttleTransportRequestState.Queued;
        }

        public string Id { get; }
        public string CorrelationKey { get; }
        public ShuttlePayloadType PayloadType { get; }
        public ShuttleTransferEndpoint Origin { get; }
        public ShuttleTransferEndpoint Destination { get; }
        public int Priority { get; internal set; }
        public long CreatedTick { get; }
        public long Age => ShuttleManager.CurrentTick - CreatedTick;
        public ShuttleTransportRequestState State { get; internal set; }
        public ShuttleTrip AssignedTrip { get; internal set; }
        public string WaitReason { get; internal set; } = string.Empty;
        public object Payload => payloads.Count == 0 ? null : payloads[0];
        public IReadOnlyList<object> Payloads => payloads;
        public bool IsPayloadReady { get; internal set; }
        public bool IsPhysicallyTransferred { get; internal set; }
        public bool IsTerminal => State == ShuttleTransportRequestState.Completed ||
            State == ShuttleTransportRequestState.Cancelled;

        public event Action<ShuttleTransportRequest> StateChanged;
        public event Action<ShuttleTransportRequest> Completed;

        internal void SetPayload(object payload)
        {
            if (payload != null && !payloads.Contains(payload))
                payloads.Add(payload);
        }

        internal void AddPayload(object payload)
        {
            if (payload != null && !payloads.Contains(payload))
                payloads.Add(payload);
        }

        internal void SetState(ShuttleTransportRequestState state)
        {
            if (IsTerminal || State == state)
                return;
            State = state;
            StateChanged?.Invoke(this);
            if (IsTerminal && state == ShuttleTransportRequestState.Completed)
                Completed?.Invoke(this);
        }

        internal void Complete()
        {
            if (IsTerminal)
                return;
            IsPhysicallyTransferred = true;
            SetState(ShuttleTransportRequestState.Completed);
        }

        public bool TryCancel()
        {
            if (IsPhysicallyTransferred || IsTerminal || State == ShuttleTransportRequestState.InTransit ||
                State == ShuttleTransportRequestState.UnloadingOrDisembarking)
                return false;
            SetState(ShuttleTransportRequestState.Cancelled);
            return true;
        }
    }

    /// <summary>One actual voyage assigned to one Shuttle.</summary>
    public sealed class ShuttleTrip
    {
        private readonly List<ShuttleTransportRequest> requests =
            new List<ShuttleTransportRequest>();

        internal ShuttleTrip(string id, ShuttleServiceComponent shuttle,
            ShuttleTransferEndpoint origin, ShuttleTransferEndpoint destination,
            long createdTick)
        {
            Id = id;
            Shuttle = shuttle;
            Origin = origin;
            Destination = destination;
            CreatedTick = createdTick;
            State = ShuttleTripState.Queued;
        }

        public string Id { get; }
        public ShuttleServiceComponent Shuttle { get; }
        public ShuttleTransferEndpoint Origin { get; }
        public ShuttleTransferEndpoint Destination { get; }
        public long CreatedTick { get; }
        public ShuttleTripState State { get; internal set; }
        public string WaitReason { get; internal set; } = string.Empty;
        public IReadOnlyList<ShuttleTransportRequest> Requests => requests;
        public int PassengerCount { get; internal set; }
        public int FreightCount { get; internal set; }

        internal void Add(ShuttleTransportRequest request)
        {
            if (request != null && !requests.Contains(request))
                requests.Add(request);
        }
    }

    /// <summary>
    /// Optional payload callback used by modern freight execution. The request
    /// and trip still remain owned by ShuttleManager.
    /// </summary>
    public interface IShuttleTransportPayload
    {
        bool IsReadyForShuttle { get; }
        float RequiredCargoCapacity { get; }
        float MinimumUsableCargoCapacity { get; }
        bool CanUseMultipleTrips { get; }
        bool TryLoad(ShuttleServiceComponent shuttle, ShuttleTransportRequest request);
        ShuttlePayloadArrivalResult OnShuttleArrived(
            ShuttleServiceComponent shuttle, ShuttleTransportRequest request);
    }

    public enum ShuttlePayloadArrivalResult
    {
        Completed,
        RetryableWait,
        RecoveryRequired
    }

    /// <summary>
    /// Central B2 transport authority. It owns request identity, persistence,
    /// assignment, batching, and trip state; voyage movement remains owned by
    /// ShuttleVoyageComponent.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Colony/Vehicles/Shuttle Manager")]
    public sealed class ShuttleManager : MonoBehaviour, ISimulationTickable, ISimulationTickPriority,
        IFreightLegProvider
    {
        private const int TerminalHistoryLimit = 128;
        private static long nextRequestId;
        private static long nextTripId;
        private static long currentTick;
        private readonly List<ShuttleTransferEndpoint> endpoints =
            new List<ShuttleTransferEndpoint>();
        private readonly List<ShuttleServiceComponent> shuttles =
            new List<ShuttleServiceComponent>();
        private readonly List<ShuttleTransportRequest> requests =
            new List<ShuttleTransportRequest>();
        private readonly List<ShuttleTrip> trips = new List<ShuttleTrip>();
        private readonly Dictionary<string, ShuttleTransportRequest> byCorrelation =
            new Dictionary<string, ShuttleTransportRequest>(StringComparer.Ordinal);

        public static ShuttleManager Instance { get; private set; }
        public static long CurrentTick => currentTick;
        public static IReadOnlyList<ShuttleTransportRequest> ActiveRequestsStatic =>
            Instance != null ? Instance.Requests : Array.Empty<ShuttleTransportRequest>();
        public IReadOnlyList<ShuttleTransferEndpoint> Endpoints => endpoints;
        public IReadOnlyList<ShuttleServiceComponent> Shuttles => shuttles;
        public IReadOnlyList<ShuttleTransportRequest> Requests => requests;
        public IReadOnlyList<ShuttleTrip> Trips => trips;
        public IReadOnlyList<ShuttleTransportRequest> ActiveRequests => SelectRequests(true);
        public IReadOnlyList<ShuttleTransportRequest> RecentRequests => SelectRequests(false);
        public IReadOnlyList<ShuttleTrip> ActiveTrips => SelectTrips(true);
        public IReadOnlyList<ShuttleTrip> RecentTrips => SelectTrips(false);
        public ShuttleTrip CurrentTrip
        {
            get
            {
                for (int index = trips.Count - 1; index >= 0; index--)
                {
                    ShuttleTrip trip = trips[index];
                    if (trip != null && trip.State != ShuttleTripState.Completed &&
                        trip.State != ShuttleTripState.Cancelled)
                        return trip;
                }
                return null;
            }
        }
        public int SimulationTickPriority => 260;

        public event Action<ShuttleTransportRequest> RequestCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one active ShuttleManager is supported.", this);
                enabled = false;
                return;
            }
            Instance = this;
        }

        private void OnEnable() => SimulationManager.RegisterTickable(this);

        private void OnDisable() => SimulationManager.UnregisterTickable(this);

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            nextRequestId = 0L;
            nextTripId = 0L;
            currentTick = 0L;
        }

        public void RegisterEndpoint(ShuttleTransferEndpoint endpoint)
        {
            if (endpoint != null && !endpoints.Contains(endpoint))
            {
                endpoints.Add(endpoint);
                Debug.Log(
                    "[B2Route] Shuttle endpoint registered: id=" + endpoint.StableId +
                    ", anchor=" + endpoint.TransferAnchor.position + ".",
                    endpoint);
            }
        }

        public void UnregisterEndpoint(ShuttleTransferEndpoint endpoint) => endpoints.Remove(endpoint);

        public void RegisterShuttle(ShuttleServiceComponent shuttle)
        {
            if (shuttle != null && !shuttles.Contains(shuttle))
            {
                shuttles.Add(shuttle);
                Debug.Log(
                    "[B2Route] Shuttle registered: id=" + shuttle.StableId + ".",
                    shuttle);
            }
        }

        public void UnregisterShuttle(ShuttleServiceComponent shuttle) => shuttles.Remove(shuttle);

        public bool CanService(ShuttleTransferEndpoint origin,
            ShuttleTransferEndpoint destination, ShuttlePayloadType payloadType)
        {
            return origin != null && destination != null && origin != destination &&
                endpoints.Contains(origin) && endpoints.Contains(destination);
        }

        public string GetStableKey() => SceneStableIdentity.GetKey(this);

        public bool TryQuoteFreightLeg(
            LogisticsRouteLeg leg,
            float shipmentQuantity,
            out ShuttleFreightQuote quote)
        {
            quote = null;
            if (leg == null || leg.Type != LogisticsRouteLegType.ShuttleFreight ||
                !IsFinitePositive(shipmentQuantity) || leg.OriginEndpoint == null ||
                leg.DestinationEndpoint == null ||
                !CanService(leg.OriginEndpoint, leg.DestinationEndpoint, ShuttlePayloadType.Freight))
                return false;

            float tripCapacity = GetAvailableFreightCapacity();
            if (tripCapacity + 0.0001f < shipmentQuantity)
                return false;
            quote = new ShuttleFreightQuote(this, leg, shipmentQuantity, tripCapacity);
            return true;
        }

        bool IFreightLegProvider.TryAcceptQuote(
            IFreightLegQuote quote,
            FreightDeliveryJob job,
            string executionId,
            out IFreightLegExecution execution)
        {
            execution = null;
            if (!(quote is ShuttleFreightQuote shuttleQuote) || shuttleQuote.Provider != this ||
                job == null || job.CurrentLeg == null || job.CurrentLeg != shuttleQuote.Leg ||
                job.CurrentLeg.Type != LogisticsRouteLegType.ShuttleFreight ||
                job.Quantity > shuttleQuote.ShipmentQuantity + 0.0001f ||
                string.IsNullOrWhiteSpace(executionId) || FreightLogisticsManager.Instance == null)
                return false;

            var shuttleExecution = new ShuttleFreightExecution(
                FreightLogisticsManager.Instance, job, executionId);
            ShuttleTransportRequest request = GetOrCreateFreightRequest(
                job.Allocation, job.CurrentLegIndex, job.CurrentLeg, shuttleExecution);
            if (request == null)
            {
                shuttleExecution.Complete(job);
                return false;
            }

            shuttleExecution.BindRequest(request);
            if (shuttleExecution.IsReadyForShuttle)
                MarkPayloadReady(request);
            execution = shuttleExecution;
            return true;
        }

        private float GetAvailableFreightCapacity()
        {
            float capacity = 0f;
            for (int index = 0; index < shuttles.Count; index++)
            {
                ShuttleServiceComponent shuttle = shuttles[index];
                if (shuttle == null || !shuttle.isActiveAndEnabled ||
                    !shuttle.CanCarry(ShuttlePayloadType.Freight) || shuttle.CargoInventory == null)
                    continue;
                // Quote against configured hold size. Pilot, trip occupancy and
                // current cargo are dispatch-time conditions; accepted freight
                // requests remain queued while those conditions clear.
                capacity = Mathf.Max(capacity, shuttle.CargoInventory.Capacity);
            }
            // A missing fleet is a dispatch wait, not a reason to erase the
            // shuttle leg. Freight execution can use multiple shuttle trips.
            return capacity > 0f ? capacity : float.MaxValue;
        }

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        public ShuttleTransportRequest GetOrCreatePassengerRequest(
            PersonnelRoutePlan plan, int legIndex, PersonnelRouteLeg leg,
            ColonistIdentity passenger, int priority = 0)
        {
            if (plan == null || leg == null || leg.Type != PersonnelRouteLegType.Shuttle ||
                leg.OriginEndpoint == null || leg.DestinationEndpoint == null)
                return null;

            string key = plan.StableId + ":leg:" + legIndex.ToString(CultureInfo.InvariantCulture);
            ShuttleTransportRequest request = FindOrCreate(key, ShuttlePayloadType.Passenger,
                leg.OriginEndpoint, leg.DestinationEndpoint, priority);
            request.AddPayload(passenger);
            return request;
        }

        public ShuttleTransportRequest GetOrCreateFreightRequest(
            FreightAllocation allocation, int legIndex, LogisticsRouteLeg leg,
            IShuttleTransportPayload payload, int priority = 0, int haulIndex = 0)
        {
            if (allocation == null || leg == null || leg.Type != LogisticsRouteLegType.ShuttleFreight ||
                leg.OriginEndpoint == null || leg.DestinationEndpoint == null || haulIndex < 0)
                return null;

            string key = allocation.Id + ":leg:" + legIndex.ToString(CultureInfo.InvariantCulture);
            if (haulIndex > 0)
                key += ":haul:" + haulIndex.ToString(CultureInfo.InvariantCulture);
            ShuttleTransportRequest request = FindOrCreate(key, ShuttlePayloadType.Freight,
                leg.OriginEndpoint, leg.DestinationEndpoint, priority);
            request.AddPayload(payload);
            if (payload != null && payload.IsReadyForShuttle)
                request.IsPayloadReady = true;
            return request;
        }

        public bool MarkPayloadReady(ShuttleTransportRequest request)
        {
            if (request == null || request.IsTerminal || request.IsPhysicallyTransferred)
                return false;
            bool wasReady = request.IsPayloadReady;
            ShuttleTransportRequestState previousState = request.State;
            request.IsPayloadReady = true;
            if (request.State == ShuttleTransportRequestState.Queued ||
                request.State == ShuttleTransportRequestState.WaitingForPayload)
                request.SetState(ShuttleTransportRequestState.ReadyForPickup);
            if (!wasReady)
            {
                object payload = request.Payload;
                string actor = payload is ColonistIdentity person ? person.name : "payload";
                ShuttleDiagnosticLog.Record("transport_payload_ready",
                    "request=" + request.Id + ", actor=" + actor +
                    ", state=" + request.State + ", origin=" + request.Origin.StableId +
                    ", destination=" + request.Destination.StableId);
            }
            if (previousState != ShuttleTransportRequestState.ReadyForPickup &&
                request.State == ShuttleTransportRequestState.ReadyForPickup)
                ShuttleDiagnosticLog.Record("transport_request_ready_for_pickup",
                    "request=" + request.Id + ", origin=" + request.Origin.StableId +
                    ", destination=" + request.Destination.StableId);
            return true;
        }

        public bool CancelRequest(ShuttleTransportRequest request) => request != null && request.TryCancel();

        public void SimulationTick(float deltaGameHours)
        {
            currentTick++;
            CleanupDestroyedRegistrations();
            for (int index = 0; index < shuttles.Count; index++)
                shuttles[index]?.SimulationTickTransport();
            ScheduleReadyRequests();
            PruneTerminalRequests();
            PruneTerminalTrips();
        }

        internal void NotifyRequestCompleted(ShuttleTransportRequest request)
        {
            RequestCompleted?.Invoke(request);
        }

        internal void NotifyTripCompleted(ShuttleTrip trip)
        {
            if (trip == null)
                return;
            trip.State = ShuttleTripState.Completed;
            if (!trips.Contains(trip))
                trips.Add(trip);
        }

        private ShuttleTransportRequest FindOrCreate(string correlationKey,
            ShuttlePayloadType payloadType, ShuttleTransferEndpoint origin,
            ShuttleTransferEndpoint destination, int priority)
        {
            if (byCorrelation.TryGetValue(correlationKey, out ShuttleTransportRequest existing) &&
                existing != null && !existing.IsTerminal)
                return existing;

            string id = "shuttle-request-" +
                (++nextRequestId).ToString("D6", CultureInfo.InvariantCulture);
            ShuttleTransportRequest request = new ShuttleTransportRequest(
                id, correlationKey, payloadType, origin, destination, priority, currentTick);
            request.StateChanged += HandleRequestStateChanged;
            requests.Add(request);
            byCorrelation[correlationKey] = request;
            return request;
        }

        private void HandleRequestStateChanged(ShuttleTransportRequest request)
        {
            if (request != null && request.State == ShuttleTransportRequestState.Completed)
                NotifyRequestCompleted(request);
        }

        private void ScheduleReadyRequests()
        {
            for (int index = 0; index < requests.Count; index++)
            {
                ShuttleTransportRequest request = requests[index];
                if (request == null || request.IsTerminal || request.AssignedTrip != null)
                    continue;
                if (!request.IsPayloadReady)
                {
                    if (request.State == ShuttleTransportRequestState.Queued)
                        request.SetState(ShuttleTransportRequestState.WaitingForPayload);
                    continue;
                }
                if (request.State == ShuttleTransportRequestState.Queued ||
                    request.State == ShuttleTransportRequestState.WaitingForPayload ||
                    request.State == ShuttleTransportRequestState.Blocked ||
                    request.State == ShuttleTransportRequestState.WaitingForPilot)
                    request.SetState(ShuttleTransportRequestState.ReadyForPickup);
            }

            List<ShuttleTransportRequest> candidates = new List<ShuttleTransportRequest>();
            for (int index = 0; index < requests.Count; index++)
            {
                ShuttleTransportRequest request = requests[index];
                if (request != null && request.State == ShuttleTransportRequestState.ReadyForPickup)
                    candidates.Add(request);
            }
            candidates.Sort(CompareRequests);

            for (int index = 0; index < candidates.Count; index++)
            {
                ShuttleTransportRequest request = candidates[index];
                if (request.AssignedTrip != null)
                    continue;

                ShuttleServiceComponent shuttle = FindAvailableShuttle(request);
                if (shuttle == null)
                {
                    request.WaitReason = GetWaitReason(request);
                    continue; // Persistent need: remain ReadyForPickup/Queued.
                }

                ShuttleTrip trip = new ShuttleTrip(
                    "shuttle-trip-" + (++nextTripId).ToString("D6", CultureInfo.InvariantCulture),
                    shuttle, request.Origin, request.Destination, currentTick);
                AddCompatibleRequests(trip, candidates, index, shuttle);
                trips.Add(trip);
                for (int requestIndex = 0; requestIndex < trip.Requests.Count; requestIndex++)
                    trip.Requests[requestIndex].WaitReason = string.Empty;
                ShuttleDiagnosticLog.Record("trip_created",
                    "trip=" + trip.Id + ", shuttle=" + shuttle.StableId +
                    ", origin=" + trip.Origin.StableId +
                    ", destination=" + trip.Destination.StableId +
                    ", passengers=" + trip.PassengerCount +
                    ", freight=" + trip.FreightCount);
                if (!shuttle.TryAcceptTrip(trip))
                {
                    string waitReason = shuttle.TripWaitReason;
                    shuttle.RollbackRejectedTrip(trip, waitReason);
                    trip.State = ShuttleTripState.Cancelled;
                    trip.WaitReason = waitReason;
                    for (int requestIndex = 0; requestIndex < trip.Requests.Count; requestIndex++)
                    {
                        ShuttleTransportRequest failed = trip.Requests[requestIndex];
                        failed.AssignedTrip = null;
                        failed.WaitReason = waitReason;
                        failed.SetState(ShuttleTransportRequestState.ReadyForPickup);
                    }
                }
            }
        }

        private void AddCompatibleRequests(ShuttleTrip trip,
            List<ShuttleTransportRequest> candidates, int firstIndex,
            ShuttleServiceComponent shuttle)
        {
            ShuttleTransportRequest first = candidates[firstIndex];
            int passengerCapacity = Mathf.Max(0, shuttle.PassengerCapacity);
            int freightCapacity = Mathf.Max(0, shuttle.FreightCapacity);
            float cargoCapacity = shuttle.CargoInventory != null
                ? shuttle.CargoInventory.FreeCapacity : 0f;
            float freightCargoUsed = 0f;
            int passengers = 0;
            int freight = 0;
            for (int index = firstIndex; index < candidates.Count; index++)
            {
                ShuttleTransportRequest request = candidates[index];
                if (request.AssignedTrip != null || request.Origin != first.Origin ||
                    request.Destination != first.Destination)
                    continue;
                if (request.PayloadType == ShuttlePayloadType.Passenger && passengers >= passengerCapacity)
                    continue;
                if (request.PayloadType == ShuttlePayloadType.Freight && freight >= freightCapacity)
                    continue;
                IShuttleTransportPayload freightPayload =
                    request.Payload as IShuttleTransportPayload;
                float freightLoadCapacity = 0f;
                if (request.PayloadType == ShuttlePayloadType.Freight)
                {
                    if (freightPayload == null)
                        continue;
                    float remainingHold = Mathf.Max(0f, cargoCapacity - freightCargoUsed);
                    freightLoadCapacity = freightPayload.CanUseMultipleTrips
                        ? Mathf.Min(freightPayload.RequiredCargoCapacity, remainingHold)
                        : freightPayload.RequiredCargoCapacity;
                    if (freightLoadCapacity + 0.0001f <
                            freightPayload.MinimumUsableCargoCapacity ||
                        freightCargoUsed + freightLoadCapacity > cargoCapacity + 0.0001f)
                        continue;
                }
                trip.Add(request);
                request.AssignedTrip = trip;
                request.SetState(ShuttleTransportRequestState.Assigned);
                if (request.PayloadType == ShuttlePayloadType.Passenger)
                    passengers++;
                else
                {
                    freight++;
                    freightCargoUsed += freightLoadCapacity;
                }
            }
            trip.PassengerCount = passengers;
            trip.FreightCount = freight;
        }

        private ShuttleServiceComponent FindAvailableShuttle(ShuttleTransportRequest request)
        {
            ShuttleServiceComponent best = null;
            float bestAvailableCargo = -1f;
            for (int index = 0; index < shuttles.Count; index++)
            {
                ShuttleServiceComponent candidate = shuttles[index];
                if (candidate == null || !candidate.CanStartTripNow ||
                    !candidate.CanCarry(request.PayloadType))
                    continue;
                if (request.PayloadType == ShuttlePayloadType.Freight &&
                    request.Payload is IShuttleTransportPayload freightPayload &&
                    (candidate.CargoInventory == null ||
                     candidate.CargoInventory.FreeCapacity + 0.0001f <
                        (freightPayload.CanUseMultipleTrips
                            ? freightPayload.MinimumUsableCargoCapacity
                            : freightPayload.RequiredCargoCapacity)))
                    continue;
                float availableCargo = request.PayloadType == ShuttlePayloadType.Freight &&
                    candidate.CargoInventory != null
                    ? candidate.CargoInventory.FreeCapacity
                    : 0f;
                if (best == null || availableCargo > bestAvailableCargo + 0.0001f ||
                    Mathf.Abs(availableCargo - bestAvailableCargo) <= 0.0001f &&
                    string.CompareOrdinal(candidate.StableId, best.StableId) < 0)
                {
                    best = candidate;
                    bestAvailableCargo = availableCargo;
                }
            }
            return best;
        }

        private string GetWaitReason(ShuttleTransportRequest request)
        {
            bool compatibleVehicleExists = false;
            bool pilotAvailable = false;
            bool vehicleBusy = false;
            bool cargoCapacityAvailable = false;
            for (int index = 0; index < shuttles.Count; index++)
            {
                ShuttleServiceComponent shuttle = shuttles[index];
                if (shuttle == null || !shuttle.isActiveAndEnabled ||
                    !shuttle.CanCarry(request.PayloadType))
                    continue;
                compatibleVehicleExists = true;
                pilotAvailable |= shuttle.PilotService != null &&
                                  shuttle.PilotService.CanProvidePilotNow;
                vehicleBusy |= shuttle.CurrentTrip != null || shuttle.Voyage == null ||
                               shuttle.Voyage.Phase != ShuttleVoyagePhase.Docked;
                if (request.PayloadType != ShuttlePayloadType.Freight ||
                    !(request.Payload is IShuttleTransportPayload payload))
                    cargoCapacityAvailable = true;
                else if (shuttle.CargoInventory != null &&
                         shuttle.CargoInventory.FreeCapacity + 0.0001f >=
                            (payload.CanUseMultipleTrips
                                ? payload.MinimumUsableCargoCapacity
                                : payload.RequiredCargoCapacity))
                    cargoCapacityAvailable = true;
            }

            if (!compatibleVehicleExists)
            {
                IReadOnlyList<ShuttleServiceComponent> knownServices =
                    ShuttleServiceComponent.KnownServices;
                for (int index = 0; index < knownServices.Count; index++)
                    if (knownServices[index] != null && knownServices[index].CanCarry(request.PayloadType))
                        return "waiting_for_service";
                return "no_compatible_vehicle";
            }
            if (!pilotAvailable)
                return "waiting_for_pilot";
            if (!cargoCapacityAvailable)
                return "waiting_for_cargo_capacity";
            return vehicleBusy ? "waiting_for_vehicle" : "waiting_for_service";
        }

        private void CleanupDestroyedRegistrations()
        {
            endpoints.RemoveAll(item => item == null);
            shuttles.RemoveAll(item => item == null);
        }

        private void PruneTerminalRequests()
        {
            int terminalCount = 0;
            for (int index = 0; index < requests.Count; index++)
                if (requests[index] != null && requests[index].IsTerminal)
                    terminalCount++;
            int excess = terminalCount - TerminalHistoryLimit;
            for (int index = 0; index < requests.Count && excess > 0;)
            {
                ShuttleTransportRequest request = requests[index];
                if (request != null && request.IsTerminal)
                {
                    requests.RemoveAt(index);
                    request.StateChanged -= HandleRequestStateChanged;
                    if (byCorrelation.TryGetValue(request.CorrelationKey,
                            out ShuttleTransportRequest correlated) && correlated == request)
                        byCorrelation.Remove(request.CorrelationKey);
                    excess--;
                }
                else
                {
                    index++;
                }
            }
        }

        private void PruneTerminalTrips()
        {
            int terminalCount = 0;
            for (int index = 0; index < trips.Count; index++)
                if (trips[index] != null &&
                    (trips[index].State == ShuttleTripState.Completed ||
                     trips[index].State == ShuttleTripState.Cancelled))
                    terminalCount++;
            int excess = terminalCount - TerminalHistoryLimit;
            for (int index = 0; index < trips.Count && excess > 0;)
            {
                ShuttleTrip trip = trips[index];
                if (trip != null &&
                    (trip.State == ShuttleTripState.Completed ||
                     trip.State == ShuttleTripState.Cancelled))
                {
                    trips.RemoveAt(index);
                    excess--;
                }
                else
                {
                    index++;
                }
            }
        }

        private IReadOnlyList<ShuttleTransportRequest> SelectRequests(bool activeOnly)
        {
            List<ShuttleTransportRequest> selected = new List<ShuttleTransportRequest>();
            for (int index = 0; index < requests.Count; index++)
            {
                ShuttleTransportRequest request = requests[index];
                bool activeRequest = request != null && !request.IsTerminal;
                if (request != null && activeRequest == activeOnly)
                    selected.Add(request);
            }
            return selected.ToArray();
        }

        private IReadOnlyList<ShuttleTrip> SelectTrips(bool activeOnly)
        {
            List<ShuttleTrip> selected = new List<ShuttleTrip>();
            for (int index = 0; index < trips.Count; index++)
            {
                ShuttleTrip trip = trips[index];
                bool activeTrip = trip != null && trip.State != ShuttleTripState.Completed &&
                                  trip.State != ShuttleTripState.Cancelled;
                if (trip != null && activeTrip == activeOnly)
                    selected.Add(trip);
            }
            return selected.ToArray();
        }

        private static int CompareRequests(ShuttleTransportRequest left, ShuttleTransportRequest right)
        {
            int priority = right.Priority.CompareTo(left.Priority);
            if (priority != 0)
                return priority;
            int age = left.CreatedTick.CompareTo(right.CreatedTick);
            return age != 0 ? age : string.CompareOrdinal(left.Id, right.Id);
        }
    }
}
