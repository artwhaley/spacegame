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
        public bool IsEmergency => false;
        public float PositioningDistance => 0f;
        public InventoryComponent CargoInventory => shuttle != null ? shuttle.CargoInventory : null;
        public UnityEngine.Object ProviderContext => shuttle != null ? shuttle : manager;
        public bool IsReadyForShuttle => IsActive && job != null && job.Reservation != null &&
            job.Reservation.IsActive && job.CurrentLeg != null &&
            job.CurrentLeg.Origin != null &&
            job.Reservation.Owner == job.CurrentLeg.Origin.Inventory;

        internal void BindRequest(ShuttleTransportRequest transportRequest) => request = transportRequest;

        internal void BindShuttle(ShuttleServiceComponent service) => shuttle = service;

        public bool TryLoad(ShuttleServiceComponent service, ShuttleTransportRequest transportRequest)
        {
            if (!IsActive || job == null || manager == null || service == null ||
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

        public void OnShuttleArrived(ShuttleServiceComponent service,
            ShuttleTransportRequest transportRequest)
        {
            if (!IsActive || job == null || manager == null || transportRequest != request)
                return;
            BindShuttle(service);
            FreightExecutionCorrelation correlation =
                new FreightExecutionCorrelation(job.Allocation.Id, LegIndex, ExecutionId);
            manager.ReportExecution(job, this, correlation,
                FreightExecutionReport.LoadedLegArrived);
        }

        public void Complete(FreightDeliveryJob assignedJob)
        {
            if (assignedJob == job)
                IsActive = false;
        }
    }
}
