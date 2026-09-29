using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Modern executor for one physical ShuttleFreight leg.</summary>
    public sealed class ShuttleFreightExecution : IFreightLegExecution, IShuttleTransportPayload
    {
        private readonly FreightLogisticsManager manager;
        private FreightDeliveryJob job;
        private ShuttleTransportRequest request;
        private ShuttleServiceComponent shuttle;
        private bool wasBoundToShuttle;
        private int nextHaulIndex = 1;

        internal ShuttleFreightExecution(FreightLogisticsManager owner,
            FreightDeliveryJob assignedJob, string executionId)
        {
            manager = owner;
            job = assignedJob;
            ExecutionId = executionId;
            LegIndex = assignedJob != null ? assignedJob.CurrentLegIndex : -1;
            IsActive = true;
        }

        public string ExecutionId { get; }
        public int LegIndex { get; }
        public bool IsActive { get; private set; }
        public bool ProviderAvailable => wasBoundToShuttle
            ? shuttle != null && shuttle.isActiveAndEnabled
            : ShuttleManager.Instance != null && ShuttleManager.Instance.isActiveAndEnabled;
        public bool ExecutorAvailable => wasBoundToShuttle
            ? shuttle != null
            : ShuttleManager.Instance != null;
        public bool IsEmergency => false;
        public bool RequiresPersonnelRouteForPickup => false;
        public bool RequiresPersonnelRouteForLoadedArrival => false;
        public bool CompletesAcceptedQuantityAcrossLoads => true;
        public bool DefersReleaseUntilAcceptedWorkCompletes => false;
        public bool HasPendingWorkerRelease => false;
        public float TripCapacity => CargoInventory != null
            ? CargoInventory.FreeCapacity
            : RequiredCargoCapacity;
        public float PositioningDistance => 0f;
        public InventoryComponent CargoInventory => shuttle != null ? shuttle.CargoInventory : null;
        public UnityEngine.Object ProviderContext => shuttle != null ? shuttle : manager;
        public float RequiredCargoCapacity
        {
            get
            {
                FreightLegProgress progress = job != null ? job.LegProgress : null;
                return progress != null && progress.LegIndex == LegIndex
                    ? progress.RemainingAtOrigin
                    : job != null ? job.Quantity : 0f;
            }
        }
        public float MinimumUsableCargoCapacity => job != null && job.Resource != null &&
            job.Resource.IsDiscrete ? 1f : 0.0001f;
        public bool CanUseMultipleTrips => true;
        public bool IsReadyForShuttle
        {
            get
            {
                if (!IsActive || job == null || job.Reservation == null ||
                    !job.Reservation.IsActive || job.CurrentLeg == null ||
                    job.CurrentLeg.Origin == null || job.CurrentLeg.Origin.Inventory == null ||
                    job.Reservation.Owner != job.CurrentLeg.Origin.Inventory)
                    return false;

                FreightLegProgress progress = job.LegProgress;
                float required = progress != null && progress.LegIndex == LegIndex
                    ? progress.RemainingAtOrigin
                    : job.Quantity;
                return required > 0.0001f &&
                    job.Reservation.Remaining + 0.0001f >= required &&
                    job.CurrentLeg.Origin.Inventory.GetReserved(job.Resource) + 0.0001f >= required;
            }
        }

        internal void BindRequest(ShuttleTransportRequest transportRequest) => request = transportRequest;

        internal void BindShuttle(ShuttleServiceComponent service)
        {
            shuttle = service;
            wasBoundToShuttle = service != null;
        }

        public bool TryLoad(ShuttleServiceComponent service, ShuttleTransportRequest transportRequest)
        {
            if (!IsActive || job == null || manager == null || service == null ||
                !service.isActiveAndEnabled ||
                transportRequest != request)
                return false;
            BindShuttle(service);
            FreightExecutionCorrelation correlation =
                new FreightExecutionCorrelation(job.Allocation.Id, LegIndex, ExecutionId);
            bool loaded = manager.ReportExecution(job, this, correlation,
                FreightExecutionReport.ProviderAtSource);
            if (loaded)
            {
                transportRequest.IsPhysicallyTransferred = true;
                transportRequest.SetState(ShuttleTransportRequestState.LoadingOrBoarding);
            }
            return loaded;
        }

        public ShuttlePayloadArrivalResult OnShuttleArrived(ShuttleServiceComponent service,
            ShuttleTransportRequest transportRequest)
        {
            if (service == null || !service.isActiveAndEnabled)
                return ShuttlePayloadArrivalResult.RetryableWait;
            if (job != null && (job.IsTerminal || job.CurrentLegIndex != LegIndex))
                return ShuttlePayloadArrivalResult.Completed;
            if (!IsActive || job == null || manager == null || transportRequest != request)
                return ShuttlePayloadArrivalResult.RecoveryRequired;

            BindShuttle(service);
            float quantityBefore = job.Quantity;
            FreightExecutionCorrelation correlation =
                new FreightExecutionCorrelation(job.Allocation.Id, LegIndex, ExecutionId);
            bool accepted = manager.ReportExecution(job, this, correlation,
                FreightExecutionReport.LoadedLegArrived);
            if (job.IsTerminal || job.CurrentLegIndex != LegIndex)
                return ShuttlePayloadArrivalResult.Completed;
            if (accepted && job.LegProgress != null &&
                job.LegProgress.RemainingAtOrigin > 0.0001f)
            {
                ShuttleManager shuttleManager = ShuttleManager.Instance;
                ShuttleTransportRequest continuation = shuttleManager != null
                    ? shuttleManager.GetOrCreateFreightRequest(
                        job.Allocation, LegIndex, job.CurrentLeg, this,
                        transportRequest.Priority, nextHaulIndex++)
                    : null;
                if (continuation == null)
                    return ShuttlePayloadArrivalResult.RecoveryRequired;

                transportRequest.Complete();
                BindRequest(continuation);
                return ShuttlePayloadArrivalResult.Completed;
            }
            if (job.State == FreightJobState.Blocked && job.RetryAtTick == long.MaxValue)
                return ShuttlePayloadArrivalResult.RecoveryRequired;
            if (job.Quantity + 0.0001f < quantityBefore || job.State == FreightJobState.Blocked)
                return ShuttlePayloadArrivalResult.RetryableWait;
            return accepted
                ? ShuttlePayloadArrivalResult.RetryableWait
                : ShuttlePayloadArrivalResult.RecoveryRequired;
        }

        public void Complete(FreightDeliveryJob assignedJob)
        {
            if (assignedJob == job)
            {
                IsActive = false;
                if (request != null && !request.IsPhysicallyTransferred && !request.IsTerminal)
                    ShuttleManager.Instance?.CancelRequest(request);
            }
        }
    }
}
