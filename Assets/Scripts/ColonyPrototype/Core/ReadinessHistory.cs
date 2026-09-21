namespace AsteroidColony
{
    /// <summary>
    /// Narrow compatibility adapter for legacy callers. SimulationLogManager is
    /// the single canonical event history and owns structured JSONL output.
    /// </summary>
    public static class ReadinessHistory
    {
        public static string Path
        {
            get => SimulationLogManager.Instance != null
                ? SimulationLogManager.Instance.JsonlPath
                : string.Empty;
        }

        public static void BeginSession()
        {
            SimulationLogManager.Instance?.BeginSession();
        }

        public static void Record(string eventType, string subject = "", string detail = "", string correlation = "")
        {
            SimulationLogManager.RecordLegacyEvent(
                eventType,
                subject,
                detail,
                correlation);
        }
    }
}
