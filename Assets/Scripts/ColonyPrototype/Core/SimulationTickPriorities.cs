namespace AsteroidColony
{
    /// <summary>Named ordering for the logistics simulation tick pipeline.</summary>
    public static class SimulationTickPriorities
    {
        public const int LogisticsStockPublication = 300;
        public const int LogisticsDispatch = 310;
        public const int LogisticsWorkExecution = 320;
        public const int LegacyTransportDispatch = 300;
    }
}
