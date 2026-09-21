namespace AsteroidColony
{
    public sealed class ScheduledWorkOccurrence
    {
        public ScheduledWorkOccurrence(
            WorkAssignment assignment,
            float startGameHour,
            float endGameHour)
        {
            Assignment = assignment;
            StartGameHour = startGameHour;
            EndGameHour = endGameHour;
        }

        public WorkAssignment Assignment { get; }
        public float StartGameHour { get; }
        public float EndGameHour { get; }
        public float DurationHours => EndGameHour - StartGameHour;

        public bool IsActiveAt(float absoluteGameHour)
        {
            return !float.IsNaN(absoluteGameHour) &&
                   !float.IsInfinity(absoluteGameHour) &&
                   absoluteGameHour >= StartGameHour &&
                   absoluteGameHour < EndGameHour;
        }

        public float TimeUntilStart(float absoluteGameHour)
        {
            if (float.IsNaN(absoluteGameHour) ||
                float.IsInfinity(absoluteGameHour))
            {
                return float.PositiveInfinity;
            }

            return StartGameHour > absoluteGameHour
                ? StartGameHour - absoluteGameHour
                : 0f;
        }
    }
}
