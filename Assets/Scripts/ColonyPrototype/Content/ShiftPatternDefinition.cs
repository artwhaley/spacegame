using System;
using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>One named window inside the repeating 24-hour day.</summary>
    [Serializable]
    public class ShiftDefinition
    {
        public string shiftId;
        public string displayName;
        public float startHour;
        public float durationHours = 8f;

        /// <summary>True when the supplied hour of day is on duty.</summary>
        public bool ContainsHourOfDay(float hourOfDay)
        {
            if (durationHours >= SimulationTime.HoursPerDay)
                return true;

            float end = startHour + durationHours;
            if (end <= SimulationTime.HoursPerDay)
                return hourOfDay >= startHour && hourOfDay < end;
            // Wraps past midnight.
            return hourOfDay >= startHour || hourOfDay < end - SimulationTime.HoursPerDay;
        }
    }

    /// <summary>
    /// A repeating daily schedule of explicit shifts. The system never decides by itself
    /// whether workers should overlap or alternate; content authors choose.
    /// </summary>
    [CreateAssetMenu(menuName = "Asteroid Colony/Shift Pattern Definition", fileName = "ShiftPatternDefinition")]
    public class ShiftPatternDefinition : ScriptableObject
    {
        public string stableId;
        public string displayName;
        public List<ShiftDefinition> shifts = new List<ShiftDefinition>();

        public ShiftDefinition FindShift(string shiftId)
        {
            if (string.IsNullOrEmpty(shiftId) || shifts == null)
                return null;
            for (int i = 0; i < shifts.Count; i++)
            {
                ShiftDefinition shift = shifts[i];
                if (shift != null && shift.shiftId == shiftId)
                    return shift;
            }
            return null;
        }

        public bool HasShift(string shiftId)
        {
            return FindShift(shiftId) != null;
        }

        /// <summary>True when the named shift is on duty at the supplied absolute simulation hour.</summary>
        public bool IsShiftActive(string shiftId, float absoluteGameHour)
        {
            ShiftDefinition shift = FindShift(shiftId);
            if (shift == null)
                return false;
            return shift.ContainsHourOfDay(SimulationTime.HourOfDayAt(absoluteGameHour));
        }

        public bool Validate(out string error)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                error = "Shift pattern stableId is required.";
                return false;
            }

            if (shifts == null || shifts.Count == 0)
            {
                error = "Shift pattern requires at least one shift.";
                return false;
            }

            var seen = new HashSet<string>();
            for (int i = 0; i < shifts.Count; i++)
            {
                ShiftDefinition shift = shifts[i];
                if (shift == null)
                {
                    error = $"Shift {i} is null.";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(shift.shiftId))
                {
                    error = $"Shift {i} requires a non-empty shiftId.";
                    return false;
                }
                if (!seen.Add(shift.shiftId))
                {
                    error = $"Shift id '{shift.shiftId}' is duplicated.";
                    return false;
                }
                if (shift.startHour < 0f || shift.startHour >= SimulationTime.HoursPerDay)
                {
                    error = $"Shift '{shift.shiftId}' startHour must be in [0, 24).";
                    return false;
                }
                if (shift.durationHours <= 0f || shift.durationHours > SimulationTime.HoursPerDay)
                {
                    error = $"Shift '{shift.shiftId}' durationHours must be in (0, 24].";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private void OnValidate()
        {
            stableId = stableId != null ? stableId.Trim() : string.Empty;
            displayName = displayName != null ? displayName.Trim() : string.Empty;
            if (!Validate(out string error))
                Debug.LogError($"Invalid shift pattern {name}: {error}", this);
        }
    }
}
