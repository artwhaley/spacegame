using Colony.Interactions;
using UnityEngine;
using System.Collections.Generic;

namespace AsteroidColony
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ColonistIdentity), typeof(ColonistMotor), typeof(ColonistActivityRunner))]
    [RequireComponent(typeof(InventoryComponent))]
    [RequireComponent(typeof(WalkingFreightRunner))]
    [AddComponentMenu("Colony/Logistics/Walking Freight Carrier")]
    public sealed class WalkingFreightCarrierComponent : MonoBehaviour
    {
        private static readonly List<WalkingFreightCarrierComponent> active =
            new List<WalkingFreightCarrierComponent>();
        [SerializeField, Min(0.01f)] private float maximumCargoQuantity = 10f;
        [SerializeField] private bool emergencyOnly;
        [SerializeField] private WorkplaceComponent emergencyWorkplace;
        [SerializeField] private ColonistIdentity identity;
        [SerializeField] private ColonistBrain brain;
        [SerializeField] private ColonistMotor motor;
        [SerializeField] private ColonistActivityRunner activityRunner;
        [SerializeField] private InventoryComponent cargoInventory;
        [SerializeField] private WalkingFreightRunner runner;
        private FreightDeliveryJob currentJob;

        public ColonistIdentity Identity => identity;
        public ColonistBrain Brain => brain;
        public ColonistMotor Motor => motor;
        public ColonistActivityRunner ActivityRunner => activityRunner;
        public InventoryComponent CargoInventory => cargoInventory;
        public WalkingFreightRunner Runner => runner;
        public FreightDeliveryJob CurrentJob => currentJob;
        public float MaximumCargoQuantity => maximumCargoQuantity;
        public bool EmergencyOnly => emergencyOnly;
        public bool HasActiveJob => currentJob != null && !currentJob.IsTerminal;
        public bool HasCargo => cargoInventory != null && currentJob != null &&
            cargoInventory.GetOnHand(currentJob.Resource) > 0f;
        public static IReadOnlyList<WalkingFreightCarrierComponent> Active => active;

        private void Awake()
        {
            ResolveComponents();
        }

        private void OnEnable()
        {
            if (!active.Contains(this))
                active.Add(this);
        }

        private void OnDisable() => active.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActive()
        {
            active.Clear();
        }

        public bool CanAcceptRoutineJob(out WorkAssignment assignment)
        {
            assignment = null;
            if (emergencyOnly || HasActiveJob || !isActiveAndEnabled ||
                identity == null || brain == null || runner == null ||
                WorkforceManager.Instance == null || SimulationManager.Instance == null ||
                !WorkforceManager.Instance.TryGetCurrentDuty(
                    identity, SimulationManager.Instance.CurrentGameHour, out assignment))
            {
                return false;
            }

            return assignment.Workplace.ExecutionMode == WorkplaceExecutionMode.MobileDuty &&
                   brain.State == ColonistBrainState.Working;
        }

        public bool CanAcceptEmergencyJob(WorkplaceComponent workplace)
        {
            if (!emergencyOnly || HasActiveJob || !isActiveAndEnabled ||
                workplace == null || workplace != emergencyWorkplace ||
                brain == null || runner == null || WorkforceManager.Instance == null ||
                SimulationManager.Instance == null)
            {
                return false;
            }

            return WorkforceManager.Instance.HasEnoughActiveWorkers(
                workplace,
                GetAssignedRole(workplace),
                1,
                0f,
                SimulationManager.Instance.CurrentGameHour) &&
                   brain.CanBeginWorkExcursion(workplace);
        }

        public bool BeginEmergencyExcursion(WorkplaceComponent workplace)
        {
            return CanAcceptEmergencyJob(workplace) &&
                   brain.TryBeginWorkExcursion(workplace);
        }

        public bool Assign(FreightDeliveryJob job)
        {
            ResolveComponents();
            if (job == null || HasActiveJob || runner == null)
                return false;

            currentJob = job;
            if (runner.Begin(job))
                return true;

            currentJob = null;
            return false;
        }

        public void Complete(FreightDeliveryJob job)
        {
            if (job == null || currentJob != job)
                return;

            currentJob = null;
            runner?.Clear(job);
            if (!emergencyOnly && brain != null && brain.State == ColonistBrainState.Working &&
                WorkforceManager.Instance != null && SimulationManager.Instance != null &&
                WorkforceManager.Instance.TryGetCurrentDuty(
                    identity, SimulationManager.Instance.CurrentGameHour, out WorkAssignment assignment) &&
                assignment.Workplace.ExecutionMode == WorkplaceExecutionMode.MobileDuty &&
                assignment.Workplace.DutyAnchor != null)
            {
                motor?.MoveTo(assignment.Workplace.DutyAnchor);
            }
        }

        public void Configure(float capacity, bool isEmergencyOnly,
            WorkplaceComponent authorizedWorkplace = null)
        {
            ResolveComponents();
            maximumCargoQuantity = Mathf.Max(0.01f, capacity);
            emergencyOnly = isEmergencyOnly;
            emergencyWorkplace = authorizedWorkplace;
        }

        private JobRoleDefinition GetAssignedRole(WorkplaceComponent workplace)
        {
            if (WorkforceManager.Instance == null ||
                !WorkforceManager.Instance.TryGetAssignment(identity, out WorkAssignment assignment) ||
                assignment.Workplace != workplace)
            {
                return null;
            }
            return assignment.Role;
        }

        private void ResolveComponents()
        {
            if (identity == null)
                identity = GetComponent<ColonistIdentity>();
            if (brain == null)
                brain = GetComponent<ColonistBrain>();
            if (motor == null)
                motor = GetComponent<ColonistMotor>();
            if (activityRunner == null)
                activityRunner = GetComponent<ColonistActivityRunner>();
            if (cargoInventory == null)
                cargoInventory = GetComponent<InventoryComponent>();
            if (runner == null)
                runner = GetComponent<WalkingFreightRunner>();
        }
    }
}
