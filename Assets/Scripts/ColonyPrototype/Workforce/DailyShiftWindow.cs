using System;
using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    [Serializable]
    public sealed class DailyShiftWindow
    {
        [SerializeField]
        private float startHour;

        [SerializeField]
        private float endHour;

        public DailyShiftWindow()
        {
        }

        public DailyShiftWindow(float startHour, float endHour)
        {
            this.startHour = startHour;
            this.endHour = endHour;
        }

        public float StartHour => startHour;
        public float EndHour => endHour;

        public bool IsConfigured => Validate(out _);

        public float DurationHours
        {
            get
            {
                if (!IsConfigured)
                    return 0f;

                return endHour > startHour
                    ? endHour - startHour
                    : SimulationTime.HoursPerDay - startHour + endHour;
            }
        }

        public bool ContainsHourOfDay(float hourOfDay)
        {
            if (!IsConfigured ||
                float.IsNaN(hourOfDay) ||
                float.IsInfinity(hourOfDay) ||
                hourOfDay < 0f ||
                hourOfDay >= SimulationTime.HoursPerDay)
            {
                return false;
            }

            return endHour > startHour
                ? hourOfDay >= startHour && hourOfDay < endHour
                : hourOfDay >= startHour || hourOfDay < endHour;
        }

        public bool Overlaps(DailyShiftWindow other)
        {
            if (other == null || !IsConfigured || !other.IsConfigured)
                return false;

            List<HourSegment> thisSegments = GetSegments();
            List<HourSegment> otherSegments = other.GetSegments();
            for (int thisIndex = 0; thisIndex < thisSegments.Count; thisIndex++)
            {
                HourSegment thisSegment = thisSegments[thisIndex];
                for (int otherIndex = 0; otherIndex < otherSegments.Count; otherIndex++)
                {
                    HourSegment otherSegment = otherSegments[otherIndex];
                    if (thisSegment.Start < otherSegment.End &&
                        otherSegment.Start < thisSegment.End)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool Validate(out string error)
        {
            if (float.IsNaN(startHour) || float.IsInfinity(startHour))
            {
                error = "Start hour must be finite.";
                return false;
            }

            if (float.IsNaN(endHour) || float.IsInfinity(endHour))
            {
                error = "End hour must be finite.";
                return false;
            }

            if (startHour < 0f || startHour >= SimulationTime.HoursPerDay)
            {
                error = "Start hour must be at least 0 and less than 24.";
                return false;
            }

            if (endHour < 0f || endHour >= SimulationTime.HoursPerDay)
            {
                error = "End hour must be at least 0 and less than 24.";
                return false;
            }

            if (startHour == endHour)
            {
                error = "Start and end hours must differ.";
                return false;
            }

            error = null;
            return true;
        }

        private List<HourSegment> GetSegments()
        {
            List<HourSegment> segments = new List<HourSegment>(2);
            if (endHour > startHour)
            {
                segments.Add(new HourSegment(startHour, endHour));
            }
            else
            {
                segments.Add(new HourSegment(startHour, SimulationTime.HoursPerDay));
                segments.Add(new HourSegment(0f, endHour));
            }

            return segments;
        }

        private readonly struct HourSegment
        {
            public HourSegment(float start, float end)
            {
                Start = start;
                End = end;
            }

            public float Start { get; }
            public float End { get; }
        }
    }
}
