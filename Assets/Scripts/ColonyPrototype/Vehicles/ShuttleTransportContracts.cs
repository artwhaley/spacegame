using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace AsteroidColony
{
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
            if (State == state)
                return;
            State = state;
            StateChanged?.Invoke(this);
            if (IsTerminal && state == ShuttleTransportRequestState.Completed)
                Completed?.Invoke(this);
        }

        internal void Complete()
        {
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
        bool TryLoad(ShuttleServiceComponent shuttle, ShuttleTransportRequest request);
        void OnShuttleArrived(ShuttleServiceComponent shuttle, ShuttleTransportRequest request);
    }

    /// <summary>
    /// Central B2 transport authority. It owns request identity, persistence,
    /// assignment, batching, and trip state; voyage movement remains owned by
    /// ShuttleVoyageComponent.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Colony/Vehicles/Shuttle Manager")]
    public sealed class ShuttleManager : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
    {
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
                endpoints.Contains(origin) && endpoints.Contains(destination) &&
                shuttles.Count > 0;
        }

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
            IShuttleTransportPayload payload, int priority = 0)
        {
            if (allocation == null || leg == null || leg.Type != LogisticsRouteLegType.ShuttleFreight ||
                leg.OriginEndpoint == null || leg.DestinationEndpoint == null)
                return null;

            string key = allocation.Id + ":leg:" + legIndex.ToString(CultureInfo.InvariantCulture);
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
            request.IsPayloadReady = true;
            if (request.State == ShuttleTransportRequestState.Queued ||
                request.State == ShuttleTransportRequestState.WaitingForPayload)
                request.SetState(ShuttleTransportRequestState.ReadyForPickup);
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
                    continue; // Persistent need: remain ReadyForPickup/Queued.

                ShuttleTrip trip = new ShuttleTrip(
                    "shuttle-trip-" + (++nextTripId).ToString("D6", CultureInfo.InvariantCulture),
                    shuttle, request.Origin, request.Destination, currentTick);
                AddCompatibleRequests(trip, candidates, index, shuttle);
                trips.Add(trip);
                if (!shuttle.TryAcceptTrip(trip))
                {
                    trip.State = ShuttleTripState.Blocked;
                    for (int requestIndex = 0; requestIndex < trip.Requests.Count; requestIndex++)
                    {
                        ShuttleTransportRequest failed = trip.Requests[requestIndex];
                        failed.AssignedTrip = null;
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
                trip.Add(request);
                request.AssignedTrip = trip;
                request.SetState(ShuttleTransportRequestState.Assigned);
                if (request.PayloadType == ShuttlePayloadType.Passenger)
                    passengers++;
                else
                    freight++;
            }
            trip.PassengerCount = passengers;
            trip.FreightCount = freight;
        }

        private ShuttleServiceComponent FindAvailableShuttle(ShuttleTransportRequest request)
        {
            ShuttleServiceComponent best = null;
            for (int index = 0; index < shuttles.Count; index++)
            {
                ShuttleServiceComponent candidate = shuttles[index];
                if (candidate == null || !candidate.isActiveAndEnabled || candidate.CurrentTrip != null ||
                    candidate.Voyage == null || candidate.Voyage.Phase != ShuttleVoyagePhase.Docked ||
                    candidate.PilotReleasePending || !candidate.CanCarry(request.PayloadType))
                    continue;
                if (best == null || string.CompareOrdinal(candidate.StableId, best.StableId) < 0)
                    best = candidate;
            }
            return best;
        }

        private void CleanupDestroyedRegistrations()
        {
            endpoints.RemoveAll(item => item == null);
            shuttles.RemoveAll(item => item == null);
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
