using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Workplace-owned operational Pilot service. It discovers the current
    /// assigned/on-duty Pilot through WorkforceManager; no colonist identity is
    /// embedded in the Shuttle runtime.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Colony/Vehicles/Shuttle Pilot Work Service")]
    public sealed class ShuttlePilotWorkService : MonoBehaviour, IWorkExecutionOwner,
        ISimulationTickable, ISimulationTickPriority
    {
        [SerializeField] private ShuttleBaseComponent homeBase;
        [SerializeField] private WorkplaceComponent workplace;
        [SerializeField] private JobRoleDefinition pilotRole;

        private ShuttleServiceComponent vehicle;
        private WorkExecutionLease lease;
        private bool releasePending;
        private string lastAcquisitionDiagnostic;

        public ShuttleBaseComponent HomeBase => homeBase;
        public WorkplaceComponent Workplace => workplace;
        public JobRoleDefinition PilotRole => pilotRole;
        public ColonistIdentity Pilot => lease != null && lease.IsActive ? lease.Worker : null;
        public WorkExecutionLease ActiveLease => lease;
        public bool PilotReleasePending => releasePending;
        public int SimulationTickPriority => 325;

        public void Configure(ShuttleBaseComponent baseComponent,
            WorkplaceComponent operationsWorkplace, JobRoleDefinition role)
        {
            homeBase = baseComponent;
            workplace = operationsWorkplace;
            pilotRole = role;
        }

        private void Reset() => ResolveReferences();

        private void Awake() => ResolveReferences();

        private void OnEnable() => SimulationManager.RegisterTickable(this);

        private void OnDisable() => SimulationManager.UnregisterTickable(this);

        internal void BindVehicle(ShuttleServiceComponent shuttle) => vehicle = shuttle;

        public bool TryAcquirePilot()
        {
            ResolveReferences();
            if (lease != null && lease.IsActive)
                return true;
            if (workplace == null || pilotRole == null || WorkforceManager.Instance == null ||
                SimulationManager.Instance == null)
            {
                LogAcquisitionDiagnostic("missing dependency: workplace=" + (workplace != null) +
                    ", role=" + (pilotRole != null) + ", workforce=" +
                    (WorkforceManager.Instance != null) + ", simulation=" +
                    (SimulationManager.Instance != null));
                return false;
            }

            var assignments = WorkforceManager.Instance.Assignments;
            float gameHour = SimulationManager.Instance.CurrentGameHour;
            bool foundAssignment = false;
            for (int index = 0; index < assignments.Count; index++)
            {
                WorkAssignment assignment = assignments[index];
                ColonistIdentity worker = assignment != null ? assignment.Colonist : null;
                if (worker == null || assignment.Workplace != workplace || assignment.Role != pilotRole)
                    continue;
                foundAssignment = true;
                ColonistBrain brain = worker.GetComponent<ColonistBrain>();
                bool onDuty = WorkforceManager.Instance.IsGenuinelyOnDuty(
                    worker, workplace, pilotRole, gameHour);
                if (!onDuty)
                {
                    LogAcquisitionDiagnostic("candidate=" + worker.name +
                        ", brainState=" + (brain != null ? brain.State.ToString() : "missing") +
                        ", genuinelyOnDuty=false");
                    continue;
                }
                if (brain != null && brain.TryAcquireWorkExecution(workplace, this, out lease))
                {
                    releasePending = false;
                    lastAcquisitionDiagnostic = null;
                    SimulationLog.Log("[B2Pilot] acquired " + worker.name +
                        " for " + (vehicle != null ? vehicle.name : "unbound shuttle") +
                        "; position=" + worker.transform.position);
                    return true;
                }
                LogAcquisitionDiagnostic("candidate=" + worker.name +
                    ", genuinelyOnDuty=true, brainState=" +
                    (brain != null ? brain.State.ToString() : "missing") +
                    ", work execution lease rejected");
            }
            if (!foundAssignment)
                LogAcquisitionDiagnostic("no matching pilot assignment for workplace=" +
                    workplace.name + ", role=" + pilotRole.name);
            return false;
        }

        private void LogAcquisitionDiagnostic(string diagnostic)
        {
            if (diagnostic == lastAcquisitionDiagnostic)
                return;
            lastAcquisitionDiagnostic = diagnostic;
            SimulationLog.Log("[B2Pilot] waiting: " + diagnostic);
        }

        public WorkReleaseDisposition RequestRelease(WorkExecutionLease requestedLease,
            WorkReleaseReason reason)
        {
            if (requestedLease == null || requestedLease != lease || !requestedLease.IsActive)
                return WorkReleaseDisposition.ReleasedNow;

            bool home = vehicle != null && homeBase != null && vehicle.CurrentDock == homeBase.DockingPort &&
                vehicle.Voyage != null && vehicle.Voyage.Phase == ShuttleVoyagePhase.Docked &&
                vehicle.CurrentTrip == null;
            if (!home)
            {
                releasePending = true;
                if (vehicle != null && vehicle.CurrentTrip == null)
                    vehicle.TryReturnHome();
                return WorkReleaseDisposition.Deferred;
            }

            releasePending = false;
            requestedLease.Release();
            lease = null;
            return WorkReleaseDisposition.ReleasedNow;
        }

        internal void NotifyTripCompleted(ShuttleServiceComponent shuttle)
        {
            vehicle = shuttle;
            if (releasePending && vehicle != null && vehicle.CurrentDock == homeBase?.DockingPort)
                ReleaseAtHomeIfSafe();
        }

        public void SimulationTick(float deltaGameHours)
        {
            if (lease == null || !lease.IsActive || !releasePending || vehicle == null)
                return;
            if (vehicle.CurrentTrip != null)
                return;
            if (vehicle.CurrentDock != homeBase?.DockingPort)
            {
                if (vehicle.Voyage != null && vehicle.Voyage.Phase == ShuttleVoyagePhase.Docked)
                    vehicle.TryReturnHome();
                return;
            }
            ReleaseAtHomeIfSafe();
        }

        private void ReleaseAtHomeIfSafe()
        {
            if (lease == null || !lease.IsActive || vehicle == null || vehicle.CurrentTrip != null ||
                vehicle.CurrentDock != homeBase?.DockingPort ||
                vehicle.Voyage == null || vehicle.Voyage.Phase != ShuttleVoyagePhase.Docked)
                return;
            lease.Release();
            lease = null;
            releasePending = false;
        }

        private void ResolveReferences()
        {
            if (homeBase == null)
                homeBase = GetComponent<ShuttleBaseComponent>();
            if (workplace == null && homeBase != null)
                workplace = homeBase.OperationsWorkplace;
        }
    }
}
