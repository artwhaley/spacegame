using UnityEngine;

namespace AsteroidColony
{
    // TEMP COMPATIBILITY: remove in T10/T13 when contract ownership is vehicle-generic.
    public enum ShuttleState
    {
        Idle,
        TravelingToPickup,
        Loading,
        TravelingToDestination,
        Unloading
    }

    /// <summary>
    /// TEMP COMPATIBILITY: thin adapter for the pre-T10 ShuttleController API.
    /// Execution lives entirely in TransportExecutorComponent.
    /// </summary>
    public class ShuttleController : MonoBehaviour, ISimulationTickable
    {
        public ShipComponent ship;
        public TransportVehicleComponent vehicle;
        public TransportExecutorComponent executor;

        public TransportContract currentContract => executor != null ? executor.CurrentContract : null;

        public string displayName => vehicle != null ? vehicle.DisplayName : name;
        public bool IsAvailable => vehicle != null && vehicle.IsAvailable;
        public bool HasActiveWork => vehicle != null && vehicle.HasActiveWork;
        public ShuttleState State => executor == null ? ShuttleState.Idle : (ShuttleState)executor.State;
        public int AboardPassengers => executor != null ? executor.AboardPassengers : 0;

        private bool started;

        private void Awake()
        {
            if (ship == null)
                ship = GetComponent<ShipComponent>();
            if (vehicle == null)
                vehicle = GetComponent<TransportVehicleComponent>();
            if (executor == null)
                executor = GetComponent<TransportExecutorComponent>();
        }

        private void Start()
        {
            started = true;
            RegisterWithManagers();
            if (LogisticsManager.Instance != null)
                SimulationLog.Log($"{displayName} registered with LogisticsManager");
            else
                SimulationLog.Log($"{displayName}: LogisticsManager not found at startup");
        }

        private void OnEnable()
        {
            RegisterWithManagers();
            if (!started)
                return;

            SimulationLog.Log($"{displayName} back online");
            if (LogisticsManager.Instance != null)
                LogisticsManager.Instance.TryAssignNext();
        }

        private void OnDisable()
        {
            if (LogisticsManager.Instance != null)
                LogisticsManager.Instance.UnregisterShuttle(this);
            if (SimulationManager.Instance != null)
                SimulationManager.Instance.Unregister(this);
        }

        private void RegisterWithManagers()
        {
            if (LogisticsManager.Instance != null)
                LogisticsManager.Instance.RegisterShuttle(this);
            if (SimulationManager.Instance != null)
                SimulationManager.Instance.Register(this);
        }

        public float GetFreeCargoCapacity(ResourceDefinition resource)
        {
            return vehicle != null ? vehicle.GetFreeCargoCapacity(resource) : 0f;
        }

        public string AvailabilityBlocker()
        {
            return vehicle != null ? vehicle.AvailabilityBlocker() : "no transport vehicle component";
        }

        public void StartContract(TransportContract contract)
        {
            if (executor != null)
                executor.StartContract(contract);
        }

        public void SimulationTick(float deltaGameHours)
        {
            if (executor != null)
                executor.SimulationTick(deltaGameHours);
        }
    }
}
