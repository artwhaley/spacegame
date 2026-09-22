namespace AsteroidColony.Stress
{
    public enum StressObservationMode
    {
        Disabled,
        Summary,
        Detailed
    }

    public enum StressEventKind
    {
        RunStarted,
        RunCompleted,
        ActivityRequested,
        ActivityReserved,
        NavigationStarted,
        ActivityStarted,
        ExitStarted,
        ReservationReleased,
        ActivityFailed,
        AnimationFailed,
        InvariantViolation
    }

    public enum StressMetric
    {
        RenderedFrames,
        SimulationTicks,
        SimulationSeconds,
        ActivityRequests,
        ReservationsAcquired,
        NavigationStarts,
        ActivitiesStarted,
        ExitsStarted,
        ReservationsReleased,
        ActivityFailures,
        AnimationFailures,
        InvariantViolations,
        CriticalHungerNormalActivity,
        OrphanedReservations,
        ActiveWithoutReservation,
        InvalidNavigationState,
        DroppedDetailedEvents,
        DroppedFailureRecords,
        FoodQueries,
        FoodCandidateEvaluations,
        FoodSelections,
        OffDutyQueries,
        OffDutyCandidateEvaluations,
        Count
    }
}
