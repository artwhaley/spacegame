using System;
using System.IO;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Small dev-only append-only transition trace. It is deliberately separate
    /// from the rolling Inspector log so a finished Play Mode run remains
    /// explainable after live objects have been destroyed.
    /// </summary>
    public static class ReadinessHistory
    {
        [Serializable]
        private class HistoryEntry
        {
            public string timestamp;
            public float gameHour;
            public string eventType;
            public string subject;
            public string detail;
            public string correlation;
        }

        private static string sessionPath;
        private static bool initialized;
        private static string sessionId;

        public static string Path
        {
            get
            {
                EnsureInitialized();
                return sessionPath;
            }
        }

        public static void BeginSession()
        {
            if (initialized)
                return;

            sessionId = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'");
            sessionPath = System.IO.Path.Combine(
                Application.persistentDataPath, $"spacegame-readiness-history-{sessionId}.jsonl");
            initialized = true;
            try
            {
                File.WriteAllText(sessionPath, string.Empty);
                Record("session.started", sessionId, sessionPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Readiness history unavailable: {exception.Message}");
            }
        }

        public static void Record(string eventType, string subject = "", string detail = "", string correlation = "")
        {
            EnsureInitialized();
            HistoryEntry entry = new HistoryEntry
            {
                timestamp = DateTime.UtcNow.ToString("O"),
                gameHour = SimulationManager.Instance != null ? SimulationManager.Instance.CurrentGameHour : 0f,
                eventType = eventType ?? string.Empty,
                subject = subject ?? string.Empty,
                detail = detail ?? string.Empty,
                correlation = correlation ?? string.Empty
            };
            try
            {
                File.AppendAllText(sessionPath, JsonUtility.ToJson(entry) + Environment.NewLine);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Readiness history write failed: {exception.Message}");
            }
        }

        private static void EnsureInitialized()
        {
            if (initialized)
                return;
            BeginSession();
        }
    }
}
