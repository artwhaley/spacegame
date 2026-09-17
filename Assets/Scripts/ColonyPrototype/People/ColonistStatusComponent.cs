using System;
using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// High-level duty phases. These intentionally remain separate from
    /// <see cref="ColonistActivity"/>: activity describes what the colonist is
    /// physically doing right now, while this state describes the work
    /// obligation that owns that activity.
    /// </summary>
    public enum ColonistDutyState
    {
        /// <summary>No current work obligation; the colonist is released or resting.</summary>
        ReleasedResting,

        /// <summary>An assigned shift is active and the colonist is getting ready or travelling to it.</summary>
        ScheduledShift,

        /// <summary>Scheduled work cannot currently proceed; inspect DutyBlocker.</summary>
        Blocked,

        /// <summary>The colonist is ready to accept new work for the active shift.</summary>
        AcceptingNewWork,

        /// <summary>The shift has ended, but already-committed work must finish first.</summary>
        CompletingCommittedWork,

        /// <summary>No new work is accepted; the colonist or vehicle is travelling home.</summary>
        ReturningHome
    }

    public enum DutyEndReason
    {
        ShiftEnded,
        Exhausted,
        Reassigned,
        Unassigned,
        WorkplaceUnavailable
    }

    [Serializable]
    public class DutyRecord
    {
        public StaffingComponent workplace;
        public StaffingComponent crewStaffing;
        public ShipComponent ship;
        public bool isPilotDuty;
        public StaffingRoleDefinition role;
        public string shiftId;
        public float startGameHour;
        public float endGameHour;
        public float workedGameHours;
        public float fatigueAtStart;
        public float fatigueAtReleaseRequest = -1f;
        public float fatigueAtEnd = -1f;
        public DutyEndReason endReason;
        public bool releaseRequested;
        public float releaseRequestedGameHour;
        public DutyEndReason releaseReason;
    }

    /// <summary>
    /// Colonist-owned personal status. Staffing decides when work or sleep is
    /// occurring and applies those intents here; this component does not know
    /// about transport or job selection.
    /// </summary>
    [DisallowMultipleComponent]
    public class ColonistStatusComponent : MonoBehaviour
    {
        [Header("Fatigue")]
        [Range(0f, 1f)] public float fatigue;
        [Min(0f)] public float workFatiguePerHour = 0.10f;
        [Min(0f)] public float sleepRecoveryPerHour = 0.10f;
        [Range(0f, 1f)] public float exhaustionThreshold = 0.90f;
        [Range(0f, 1f)] public float recoveredThreshold = 0.20f;

        [SerializeField] private bool exhausted;
        [SerializeField] private ColonistDutyState currentDutyState = ColonistDutyState.ReleasedResting;
        [SerializeField] private string dutyBlocker;
        [SerializeField] private List<DutyRecord> dutyHistory = new List<DutyRecord>();
        [SerializeField] private int maximumDutyRecords = 32;
        [SerializeField] private DutyRecord activeDuty;

        public float Fatigue => fatigue;
        public bool IsExhausted => exhausted;
        public ColonistDutyState CurrentDutyState => currentDutyState;
        public string DutyBlocker => dutyBlocker;
        public bool HasActiveDuty => activeDuty != null;
        public DutyRecord ActiveDuty => activeDuty;
        public IReadOnlyList<DutyRecord> DutyHistory => dutyHistory;

        private void Awake()
        {
            ClampAuthoring();
            RefreshExhaustionLatch();
        }

        private void OnValidate()
        {
            ClampAuthoring();
            RefreshExhaustionLatch();
        }

        public void ApplyWorkFatigue(float deltaGameHours, float exertionMultiplier)
        {
            if (deltaGameHours <= 0f || float.IsNaN(deltaGameHours) || float.IsInfinity(deltaGameHours))
                return;
            if (exertionMultiplier < 0f || float.IsNaN(exertionMultiplier) || float.IsInfinity(exertionMultiplier))
                exertionMultiplier = 0f;
            AdjustFatigue(deltaGameHours * SafeNonNegative(workFatiguePerHour) * exertionMultiplier);
        }

        public void ApplySleepRecovery(float deltaGameHours, float restfulnessMultiplier)
        {
            if (deltaGameHours <= 0f || float.IsNaN(deltaGameHours) || float.IsInfinity(deltaGameHours))
                return;
            if (restfulnessMultiplier < 0f || float.IsNaN(restfulnessMultiplier) || float.IsInfinity(restfulnessMultiplier))
                restfulnessMultiplier = 0f;
            AdjustFatigue(-deltaGameHours * SafeNonNegative(sleepRecoveryPerHour) * restfulnessMultiplier);
        }

        public void AdjustFatigue(float delta)
        {
            if (float.IsNaN(delta) || float.IsInfinity(delta))
                return;
            fatigue = Mathf.Clamp01(fatigue + delta);
            RefreshExhaustionLatch();
        }

        /// <summary>
        /// Records the work-obligation phase without changing activity, fatigue,
        /// or duty history. Staffing and crew components own the transition
        /// decisions; this component only stores the inspectable state.
        /// </summary>
        public void SetDutyState(ColonistDutyState nextState)
        {
            currentDutyState = nextState;
            if (nextState != ColonistDutyState.Blocked)
                dutyBlocker = string.Empty;
        }

        public void SetDutyState(ColonistDutyState nextState, string blocker)
        {
            currentDutyState = nextState;
            dutyBlocker = nextState == ColonistDutyState.Blocked ? blocker ?? string.Empty : string.Empty;
        }

        public void BeginDuty(
            StaffingComponent workplace, StaffingRoleDefinition role, string shiftId, float gameHour)
        {
            if (activeDuty != null &&
                (activeDuty.workplace != workplace || activeDuty.role != role || activeDuty.shiftId != shiftId))
                EndDuty(gameHour, DutyEndReason.Reassigned);

            if (activeDuty == null)
            {
                activeDuty = new DutyRecord
                {
                    workplace = workplace,
                    ship = null,
                    isPilotDuty = false,
                    role = role,
                    shiftId = shiftId,
                    startGameHour = gameHour,
                    endGameHour = gameHour,
                    workedGameHours = 0f,
                    fatigueAtStart = fatigue,
                    fatigueAtReleaseRequest = -1f,
                    fatigueAtEnd = -1f
                };
                LogDutyStarted(activeDuty);
            }
        }

        public void BeginPilotDuty(
            ShipComponent ship, StaffingRoleDefinition role, string shiftId, float gameHour)
        {
            if (activeDuty != null &&
                (!activeDuty.isPilotDuty || activeDuty.ship != ship || activeDuty.role != role ||
                 activeDuty.shiftId != shiftId))
                EndDuty(gameHour, DutyEndReason.Reassigned);

            if (activeDuty == null)
            {
                activeDuty = new DutyRecord
                {
                    workplace = null,
                    crewStaffing = ship != null ? ship.crewStaffing : null,
                    ship = ship,
                    isPilotDuty = true,
                    role = role,
                    shiftId = shiftId,
                    startGameHour = gameHour,
                    endGameHour = gameHour,
                    workedGameHours = 0f,
                    fatigueAtStart = fatigue,
                    fatigueAtReleaseRequest = -1f,
                    fatigueAtEnd = -1f,
                    releaseRequested = false,
                    releaseRequestedGameHour = -1f
                };
                LogDutyStarted(activeDuty);
            }
        }

        public void RequestDutyRelease(float gameHour, DutyEndReason reason)
        {
            if (activeDuty == null)
                return;
            if (!activeDuty.releaseRequested)
            {
                activeDuty.releaseRequested = true;
                activeDuty.releaseRequestedGameHour = gameHour;
                activeDuty.releaseReason = reason;
                activeDuty.fatigueAtReleaseRequest = fatigue;
                SimulationLog.Log($"{ColonistLabel()} duty release requested: {reason} (fatigue {fatigue:0.00})");
                ReadinessHistory.Record("duty.release_requested", ColonistLabel(),
                    $"{reason}; fatigue {fatigue:0.00}", activeDuty.shiftId);
            }
        }

        public void AddWorkedTime(float deltaGameHours)
        {
            if (activeDuty == null || deltaGameHours <= 0f ||
                float.IsNaN(deltaGameHours) || float.IsInfinity(deltaGameHours))
                return;
            activeDuty.workedGameHours += deltaGameHours;
            activeDuty.endGameHour = activeDuty.startGameHour + activeDuty.workedGameHours;
        }

        public void EndDuty(float gameHour, DutyEndReason reason)
        {
            if (activeDuty == null)
                return;
            activeDuty.endGameHour = Mathf.Max(activeDuty.startGameHour, gameHour);
            activeDuty.fatigueAtEnd = fatigue;
            activeDuty.endReason = reason;
            DutyRecord completedDuty = activeDuty;
            if (dutyHistory == null)
                dutyHistory = new List<DutyRecord>();
            dutyHistory.Add(completedDuty);
            int cap = Mathf.Max(0, maximumDutyRecords);
            while (dutyHistory.Count > cap)
                dutyHistory.RemoveAt(0);
            SimulationLog.Log($"{ColonistLabel()} duty ended: {reason} (fatigue {completedDuty.fatigueAtEnd:0.00}, worked {completedDuty.workedGameHours:0.00}h)");
            ReadinessHistory.Record("duty.ended", ColonistLabel(),
                $"{reason}; fatigue {completedDuty.fatigueAtEnd:0.00}; worked {completedDuty.workedGameHours:0.00}h",
                completedDuty.shiftId);
            activeDuty = null;
        }

        private void LogDutyStarted(DutyRecord duty)
        {
            string target = duty.isPilotDuty
                ? (duty.ship != null ? duty.ship.displayName : "<ship>")
                : (duty.workplace != null ? duty.workplace.DisplayName : "<workplace>");
            string roleName = duty.role != null ? duty.role.displayName : "<role>";
            SimulationLog.Log($"{ColonistLabel()} duty started: {target} / {roleName} / shift {duty.shiftId} (fatigue {duty.fatigueAtStart:0.00})");
            ReadinessHistory.Record("duty.started", ColonistLabel(),
                $"{target} / {roleName} / shift {duty.shiftId}; fatigue {duty.fatigueAtStart:0.00}",
                duty.isPilotDuty && duty.ship != null ? duty.ship.displayName : duty.shiftId);
        }

        private string ColonistLabel()
        {
            ColonistAgent colonist = GetComponent<ColonistAgent>();
            if (colonist != null && !string.IsNullOrEmpty(colonist.displayName))
                return colonist.displayName;
            return name;
        }

        private void ClampAuthoring()
        {
            fatigue = Mathf.Clamp01(SafeNonNegative(fatigue));
            workFatiguePerHour = SafeNonNegative(workFatiguePerHour);
            sleepRecoveryPerHour = SafeNonNegative(sleepRecoveryPerHour);
            exhaustionThreshold = Mathf.Clamp01(SafeNonNegative(exhaustionThreshold));
            recoveredThreshold = Mathf.Clamp01(SafeNonNegative(recoveredThreshold));
            if (recoveredThreshold >= exhaustionThreshold)
                recoveredThreshold = Mathf.Max(0f, exhaustionThreshold - 0.01f);
            maximumDutyRecords = Mathf.Max(0, maximumDutyRecords);
            if (dutyHistory == null)
                dutyHistory = new List<DutyRecord>();
        }

        private void RefreshExhaustionLatch()
        {
            exhaustionThreshold = Mathf.Clamp01(SafeNonNegative(exhaustionThreshold));
            recoveredThreshold = Mathf.Clamp01(SafeNonNegative(recoveredThreshold));
            if (recoveredThreshold >= exhaustionThreshold)
                recoveredThreshold = Mathf.Max(0f, exhaustionThreshold - 0.01f);
            if (fatigue >= exhaustionThreshold)
                exhausted = true;
            else if (fatigue <= recoveredThreshold)
                exhausted = false;
        }

        private static float SafeNonNegative(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
        }
    }
}
