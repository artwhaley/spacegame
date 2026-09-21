using System;
using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    public enum WorkAssignmentResult
    {
        Applied,
        RejectedMissingColonist,
        RejectedMissingWorkplace,
        RejectedMissingRole,
        RejectedInvalidShift,
        RejectedRoleNotOffered,
        RejectedAtScheduledCapacity
    }

    [DisallowMultipleComponent]
    public sealed class WorkforceManager : MonoBehaviour
    {
        public static WorkforceManager Instance { get; private set; }

        [SerializeField]
        private List<WorkAssignment> assignments = new List<WorkAssignment>();

        public IReadOnlyList<WorkAssignment> Assignments =>
            assignments ?? Array.Empty<WorkAssignment>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one active WorkforceManager is supported.", this);
                enabled = false;
                return;
            }

            Instance = this;
            ValidateAuthoredAssignments();
        }

        private void OnValidate()
        {
            ValidateAuthoredAssignments();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool TryGetAssignment(
            ColonistIdentity colonist,
            out WorkAssignment assignment)
        {
            assignment = null;
            if (colonist == null || assignments == null)
                return false;

            for (int index = 0; index < assignments.Count; index++)
            {
                WorkAssignment candidate = assignments[index];
                if (candidate != null && candidate.Colonist == colonist)
                {
                    assignment = candidate;
                    return true;
                }
            }

            return false;
        }

        public WorkAssignmentResult Assign(
            ColonistIdentity colonist,
            WorkplaceComponent workplace,
            JobRoleDefinition role,
            DailyShiftWindow shift)
        {
            WorkAssignmentResult result =
                ValidateAssignment(colonist, workplace, role, shift);
            if (result != WorkAssignmentResult.Applied)
                return result;

            if (assignments == null)
                assignments = new List<WorkAssignment>();

            for (int index = assignments.Count - 1; index >= 0; index--)
            {
                WorkAssignment existing = assignments[index];
                if (existing != null && existing.Colonist == colonist)
                    assignments.RemoveAt(index);
            }

            assignments.Add(new WorkAssignment(colonist, workplace, role, shift));
            return WorkAssignmentResult.Applied;
        }

        public WorkAssignmentResult Unassign(ColonistIdentity colonist)
        {
            if (colonist == null)
                return WorkAssignmentResult.RejectedMissingColonist;

            if (assignments == null)
                return WorkAssignmentResult.Applied;

            for (int index = assignments.Count - 1; index >= 0; index--)
            {
                WorkAssignment assignment = assignments[index];
                if (assignment != null && assignment.Colonist == colonist)
                    assignments.RemoveAt(index);
            }

            return WorkAssignmentResult.Applied;
        }

        public WorkAssignmentResult ValidateAssignment(
            ColonistIdentity colonist,
            WorkplaceComponent workplace,
            JobRoleDefinition role,
            DailyShiftWindow shift)
        {
            if (colonist == null)
                return WorkAssignmentResult.RejectedMissingColonist;
            if (workplace == null)
                return WorkAssignmentResult.RejectedMissingWorkplace;
            if (role == null)
                return WorkAssignmentResult.RejectedMissingRole;
            if (shift == null || !shift.IsConfigured)
                return WorkAssignmentResult.RejectedInvalidShift;

            if (!workplace.TryGetRoleBinding(role, out WorkplaceRoleBinding binding))
                return WorkAssignmentResult.RejectedRoleNotOffered;

            if (WouldExceedScheduledCapacity(
                    colonist,
                    workplace,
                    role,
                    shift,
                    binding.MaximumConcurrentScheduledWorkers))
            {
                return WorkAssignmentResult.RejectedAtScheduledCapacity;
            }

            return WorkAssignmentResult.Applied;
        }

        public bool TryGetCurrentShift(
            ColonistIdentity colonist,
            float absoluteGameHour,
            out ScheduledWorkOccurrence occurrence)
        {
            occurrence = null;
            if (!TryGetConfiguredAssignment(colonist, out WorkAssignment assignment) ||
                !IsFinite(absoluteGameHour))
            {
                return false;
            }

            int dayIndex = SimulationTime.DayIndexAt(absoluteGameHour);
            ScheduledWorkOccurrence today = CreateOccurrence(assignment, dayIndex);
            if (today.IsActiveAt(absoluteGameHour))
            {
                occurrence = today;
                return true;
            }

            if (assignment.Shift.EndHour < assignment.Shift.StartHour)
            {
                ScheduledWorkOccurrence previousDay =
                    CreateOccurrence(assignment, dayIndex - 1);
                if (previousDay.IsActiveAt(absoluteGameHour))
                {
                    occurrence = previousDay;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetNextShift(
            ColonistIdentity colonist,
            float absoluteGameHour,
            out ScheduledWorkOccurrence occurrence)
        {
            occurrence = null;
            if (!TryGetConfiguredAssignment(colonist, out WorkAssignment assignment) ||
                !IsFinite(absoluteGameHour))
            {
                return false;
            }

            int dayIndex = SimulationTime.DayIndexAt(absoluteGameHour);
            ScheduledWorkOccurrence today = CreateOccurrence(assignment, dayIndex);
            occurrence = today.StartGameHour > absoluteGameHour
                ? today
                : CreateOccurrence(assignment, dayIndex + 1);
            return true;
        }

        public bool TryGetCurrentOrNextShift(
            ColonistIdentity colonist,
            float absoluteGameHour,
            out ScheduledWorkOccurrence occurrence)
        {
            if (TryGetCurrentShift(colonist, absoluteGameHour, out occurrence))
                return true;

            return TryGetNextShift(colonist, absoluteGameHour, out occurrence);
        }

        private bool TryGetConfiguredAssignment(
            ColonistIdentity colonist,
            out WorkAssignment assignment)
        {
            return TryGetAssignment(colonist, out assignment) &&
                   assignment.IsConfigured;
        }

        private bool WouldExceedScheduledCapacity(
            ColonistIdentity colonist,
            WorkplaceComponent workplace,
            JobRoleDefinition role,
            DailyShiftWindow proposedShift,
            int capacity)
        {
            List<ScheduleEvent> events = new List<ScheduleEvent>();
            if (assignments != null)
            {
                for (int index = 0; index < assignments.Count; index++)
                {
                    WorkAssignment existing = assignments[index];
                    if (existing == null ||
                        !existing.IsConfigured ||
                        existing.Colonist == colonist ||
                        existing.Workplace != workplace ||
                        existing.Role != role)
                    {
                        continue;
                    }

                    AddShiftEvents(existing.Shift, events);
                }
            }

            AddShiftEvents(proposedShift, events);
            events.Sort(CompareScheduleEvents);

            int activeWorkers = 0;
            for (int index = 0; index < events.Count; index++)
            {
                activeWorkers += events[index].Delta;
                if (activeWorkers > capacity)
                    return true;
            }

            return false;
        }

        private static void AddShiftEvents(
            DailyShiftWindow shift,
            List<ScheduleEvent> events)
        {
            if (shift.EndHour > shift.StartHour)
            {
                AddSegmentEvents(shift.StartHour, shift.EndHour, events);
                return;
            }

            AddSegmentEvents(
                shift.StartHour,
                SimulationTime.HoursPerDay,
                events);
            AddSegmentEvents(0f, shift.EndHour, events);
        }

        private static void AddSegmentEvents(
            float startHour,
            float endHour,
            List<ScheduleEvent> events)
        {
            if (endHour <= startHour)
                return;

            events.Add(new ScheduleEvent(startHour, 1));
            events.Add(new ScheduleEvent(endHour, -1));
        }

        private static int CompareScheduleEvents(
            ScheduleEvent left,
            ScheduleEvent right)
        {
            int timeComparison = left.Hour.CompareTo(right.Hour);
            return timeComparison != 0
                ? timeComparison
                : left.Delta.CompareTo(right.Delta);
        }

        private static ScheduledWorkOccurrence CreateOccurrence(
            WorkAssignment assignment,
            int startDayIndex)
        {
            float startGameHour =
                startDayIndex * SimulationTime.HoursPerDay + assignment.Shift.StartHour;
            return new ScheduledWorkOccurrence(
                assignment,
                startGameHour,
                startGameHour + assignment.Shift.DurationHours);
        }

        private void ValidateAuthoredAssignments()
        {
            if (assignments == null)
                return;

            HashSet<ColonistIdentity> seenColonists =
                new HashSet<ColonistIdentity>();
            for (int index = 0; index < assignments.Count; index++)
            {
                WorkAssignment assignment = assignments[index];
                if (assignment == null)
                {
                    Debug.LogError(
                        $"{name}: workforce assignment {index} is null.",
                        this);
                    continue;
                }

                if (!assignment.IsConfigured)
                {
                    Debug.LogError(
                        $"{name}: workforce assignment {index} is structurally invalid.",
                        this);
                }

                if (assignment.Colonist != null &&
                    !seenColonists.Add(assignment.Colonist))
                {
                    Debug.LogError(
                        $"{name}: workforce roster contains duplicate assignments for {assignment.Colonist.name}.",
                        this);
                }
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private readonly struct ScheduleEvent
        {
            public ScheduleEvent(float hour, int delta)
            {
                Hour = hour;
                Delta = delta;
            }

            public float Hour { get; }
            public int Delta { get; }
        }
    }
}
