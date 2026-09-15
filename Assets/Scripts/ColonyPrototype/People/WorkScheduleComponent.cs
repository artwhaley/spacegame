using UnityEngine;

namespace AsteroidColony
{
    public enum WorkScheduleState
    {
        AwaitingWorkTransport,
        Working,
        AwaitingReturnTransport,
        Resting
    }

    /// <summary>
    /// Reusable current two-location work/rest cycle. It owns passenger requests
    /// and activity transitions, but not job allocation or production.
    /// </summary>
    public class WorkScheduleComponent : MonoBehaviour, ISimulationTickable
    {
        public LocationAnchor workplace;
        public LocationAnchor home;
        public StaffingComponent staffing;
        public float workDurationHours = 8f;
        public float restDurationHours = 8f;
        [Range(1, 10)] public int passengerPriority = 10;

        [SerializeField] private WorkScheduleState state = WorkScheduleState.Resting;
        [SerializeField] private float shiftTimeRemaining;
        [SerializeField] private int presentWorkingWorkers;

        private float workHoursAccumulated;
        private float restHoursAccumulated;
        private bool started;

        public WorkScheduleState State => state;
        public float ShiftTimeRemaining => shiftTimeRemaining;
        public int PresentWorkingWorkers => presentWorkingWorkers;

        private void Awake()
        {
            if (workplace == null)
                workplace = GetComponent<LocationAnchor>();
            if (staffing == null)
                staffing = GetComponent<StaffingComponent>();
        }

        private void Start()
        {
            started = true;
            if (SimulationManager.Instance != null)
                SimulationManager.Instance.Register(this);
            RequestWorkTransport();
        }

        private void OnEnable()
        {
            if (started && SimulationManager.Instance != null)
                SimulationManager.Instance.Register(this);
        }

        private void OnDisable()
        {
            if (SimulationManager.Instance != null)
                SimulationManager.Instance.Unregister(this);
        }

        public void SimulationTick(float deltaGameHours)
        {
            switch (state)
            {
                case WorkScheduleState.AwaitingWorkTransport:
                    if (AllWorkersAt(workplace))
                        BeginWorking();
                    else
                        RequestWorkTransport();
                    break;

                case WorkScheduleState.Working:
                    TickWorking(deltaGameHours);
                    break;

                case WorkScheduleState.AwaitingReturnTransport:
                    if (AllWorkersAt(GetHome()))
                        BeginResting();
                    else
                        RequestReturnTransport();
                    break;

                case WorkScheduleState.Resting:
                    TickResting(deltaGameHours);
                    break;
            }
        }

        private void TickWorking(float deltaGameHours)
        {
            presentWorkingWorkers = staffing != null ? staffing.WorkingCount : 0;
            workHoursAccumulated += Mathf.Max(0f, deltaGameHours);
            shiftTimeRemaining = Mathf.Max(0f, workDurationHours - workHoursAccumulated);
            if (workHoursAccumulated >= workDurationHours)
                EndWorkShift();
        }

        private void TickResting(float deltaGameHours)
        {
            presentWorkingWorkers = 0;
            restHoursAccumulated += Mathf.Max(0f, deltaGameHours);
            shiftTimeRemaining = Mathf.Max(0f, restDurationHours - restHoursAccumulated);
            if (restHoursAccumulated >= restDurationHours)
                RequestWorkTransport();
        }

        private void BeginWorking()
        {
            if (staffing != null)
            {
                for (int i = 0; i < staffing.assignedWorkers.Count; i++)
                    if (staffing.assignedWorkers[i] != null)
                        staffing.assignedWorkers[i].activity = ColonistActivity.Working;
            }
            workHoursAccumulated = 0f;
            shiftTimeRemaining = workDurationHours;
            state = WorkScheduleState.Working;
            SimulationLog.Log($"Workers arrived at {GetWorkplaceName()} and began working");
        }

        private void EndWorkShift()
        {
            if (staffing != null)
            {
                for (int i = 0; i < staffing.assignedWorkers.Count; i++)
                    if (staffing.assignedWorkers[i] != null)
                        staffing.assignedWorkers[i].activity = ColonistActivity.WaitingForTransport;
            }
            state = WorkScheduleState.AwaitingReturnTransport;
            SimulationLog.Log($"Workers finished their work shift at {GetWorkplaceName()}");
            RequestReturnTransport();
        }

        private void BeginResting()
        {
            if (staffing != null)
            {
                for (int i = 0; i < staffing.assignedWorkers.Count; i++)
                    if (staffing.assignedWorkers[i] != null)
                        staffing.assignedWorkers[i].activity = ColonistActivity.Resting;
            }
            restHoursAccumulated = 0f;
            shiftTimeRemaining = restDurationHours;
            state = WorkScheduleState.Resting;
            SimulationLog.Log("Workers returned home and began resting");
        }

        private void RequestWorkTransport()
        {
            if (ContractManager.Instance == null || staffing == null ||
                staffing.assignedWorkers.Count == 0 || HasActivePassengerWork() ||
                !AllWorkersAt(GetHome()))
                return;

            if (ContractManager.Instance.CreatePassengerContract(
                GetHome(), workplace, staffing.assignedWorkers, passengerPriority) != null)
            {
                state = WorkScheduleState.AwaitingWorkTransport;
                SimulationLog.Log($"Workers requested transport to {GetWorkplaceName()}");
            }
        }

        private void RequestReturnTransport()
        {
            if (ContractManager.Instance == null || staffing == null ||
                HasActivePassengerWork() || !AllWorkersAt(workplace))
                return;

            if (ContractManager.Instance.CreatePassengerContract(
                workplace, GetHome(), staffing.assignedWorkers, passengerPriority) != null)
                SimulationLog.Log("Workers requested transport home");
        }

        private bool HasActivePassengerWork()
        {
            if (ContractManager.Instance == null || staffing == null)
                return false;
            return ContractManager.Instance.HasActivePassengerForAny(staffing.assignedWorkers);
        }

        private bool AllWorkersAt(LocationAnchor location)
        {
            if (location == null || staffing == null || staffing.assignedWorkers.Count == 0)
                return false;
            for (int i = 0; i < staffing.assignedWorkers.Count; i++)
            {
                ColonistAgent worker = staffing.assignedWorkers[i];
                if (worker == null || worker.currentLocation != location)
                    return false;
            }
            return true;
        }

        private LocationAnchor GetHome()
        {
            if (home != null)
                return home;
            if (staffing != null)
                for (int i = 0; i < staffing.assignedWorkers.Count; i++)
                    if (staffing.assignedWorkers[i] != null && staffing.assignedWorkers[i].home != null)
                        return staffing.assignedWorkers[i].home;
            return null;
        }

        private string GetWorkplaceName()
        {
            return workplace == null ? "workplace" : workplace.displayName;
        }

        private void OnValidate()
        {
            workDurationHours = Mathf.Max(0f, workDurationHours);
            restDurationHours = Mathf.Max(0f, restDurationHours);
            passengerPriority = Mathf.Clamp(passengerPriority, 1, 10);
        }
    }
}
