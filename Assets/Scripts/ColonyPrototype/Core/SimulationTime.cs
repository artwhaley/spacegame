using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Shared calendar view over the monotonic simulation clock. Simulation
    /// systems keep using absolute elapsed game hours for durations and rates;
    /// this helper is only for daily schedules and human-readable presentation.
    /// </summary>
    public static class SimulationTime
    {
        public const float HoursPerDay = 24f;

        public static int DayIndexAt(float absoluteGameHour)
        {
            if (float.IsNaN(absoluteGameHour) || float.IsInfinity(absoluteGameHour))
                return 0;
            return Mathf.FloorToInt(absoluteGameHour / HoursPerDay);
        }

        public static int DayNumberAt(float absoluteGameHour)
        {
            return DayIndexAt(absoluteGameHour) + 1;
        }

        public static float HourOfDayAt(float absoluteGameHour)
        {
            if (float.IsNaN(absoluteGameHour) || float.IsInfinity(absoluteGameHour))
                return 0f;

            float hour = absoluteGameHour - HoursPerDay * Mathf.Floor(absoluteGameHour / HoursPerDay);
            if (hour < 0f)
                hour += HoursPerDay;
            if (hour >= HoursPerDay)
                hour = 0f;
            return hour;
        }

        /// <summary>Formats elapsed time as a one-based day and 24-hour clock.</summary>
        public static string FormatTimestamp(float absoluteGameHour)
        {
            if (float.IsNaN(absoluteGameHour) || float.IsInfinity(absoluteGameHour))
                absoluteGameHour = 0f;

            int totalMinutes = Mathf.Max(0, Mathf.RoundToInt(absoluteGameHour * 60f));
            int minutesPerDay = Mathf.RoundToInt(HoursPerDay * 60f);
            int dayNumber = totalMinutes / minutesPerDay + 1;
            int minuteOfDay = totalMinutes % minutesPerDay;
            return $"Day {dayNumber} {minuteOfDay / 60:00}:{minuteOfDay % 60:00}";
        }

        public static string FormatHourOfDay(float hourOfDay)
        {
            float normalized = HourOfDayAt(hourOfDay);
            int totalMinutes = Mathf.Clamp(Mathf.RoundToInt(normalized * 60f), 0, 24 * 60);
            if (totalMinutes == 24 * 60)
                totalMinutes = 0;
            return $"{totalMinutes / 60:00}:{totalMinutes % 60:00}";
        }
    }
}
