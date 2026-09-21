using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AsteroidColony
{
    [DisallowMultipleComponent]
    public sealed class SimulationLogManager : MonoBehaviour
    {
        public static SimulationLogManager Instance { get; private set; }

        [SerializeField, Min(1)] private int capacity = 2048;
        [SerializeField] private bool writeJsonl = true;
        [SerializeField] private bool echoToConsole = true;
        [SerializeField] private List<SimulationLogEntry> entries =
            new List<SimulationLogEntry>();

        private long nextSequenceNumber;
        private string jsonlPath;

        public int Capacity => capacity;
        public IReadOnlyList<SimulationLogEntry> Entries =>
            entries ?? (IReadOnlyList<SimulationLogEntry>)Array.Empty<SimulationLogEntry>();
        public string JsonlPath => jsonlPath ?? string.Empty;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one active SimulationLogManager is supported.", this);
                enabled = false;
                return;
            }

            Instance = this;
            capacity = Mathf.Max(1, capacity);
            BeginSession();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void BeginSession()
        {
            string sessionId = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'");
            jsonlPath = Path.Combine(
                Application.persistentDataPath,
                $"spacegame-simulation-log-{sessionId}.jsonl");

            if (!writeJsonl)
                return;

            try
            {
                File.WriteAllText(jsonlPath, string.Empty);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Simulation log JSONL unavailable: {exception.Message}", this);
            }

            Append(
                "session.started",
                "System",
                "Info",
                new SimulationLogSubject(name, "SimulationLogManager"),
                null,
                new[] { new SimulationLogField("path", jsonlPath) });
        }

        public SimulationLogEntry Record(
            string eventKey,
            string category,
            string severity = "Info",
            UnityEngine.Object primarySubject = null,
            UnityEngine.Object secondarySubject = null,
            params SimulationLogField[] fields)
        {
            return Append(
                eventKey,
                category,
                severity,
                primarySubject != null ? new SimulationLogSubject(primarySubject) : null,
                secondarySubject != null ? new SimulationLogSubject(secondarySubject) : null,
                fields);
        }

        public SimulationLogEntry Record(
            string eventKey,
            string category,
            string severity,
            SimulationLogSubject primarySubject,
            SimulationLogSubject secondarySubject,
            params SimulationLogField[] fields)
        {
            return Append(eventKey, category, severity, primarySubject, secondarySubject, fields);
        }

        public SimulationLogEntry RecordLegacy(
            string eventKey,
            string subject,
            string detail,
            string correlation)
        {
            List<SimulationLogField> fields = new List<SimulationLogField>();
            if (!string.IsNullOrEmpty(detail))
                fields.Add(new SimulationLogField("detail", detail));
            if (!string.IsNullOrEmpty(correlation))
                fields.Add(new SimulationLogField("correlation", correlation));

            return Append(
                eventKey,
                "System",
                "Info",
                string.IsNullOrEmpty(subject)
                    ? null
                    : new SimulationLogSubject(subject, "LegacySubject"),
                null,
                fields);
        }

        public IReadOnlyList<SimulationLogEntry> Query(
            UnityEngine.Object subject = null,
            UnityEngine.Object target = null,
            string category = null,
            string eventKey = null,
            float minimumGameHour = float.NegativeInfinity,
            float maximumGameHour = float.PositiveInfinity)
        {
            List<SimulationLogEntry> result = new List<SimulationLogEntry>();
            if (entries == null)
                return result;

            string subjectId = subject != null ? subject.GetEntityId().ToString() : string.Empty;
            string targetId = target != null ? target.GetEntityId().ToString() : string.Empty;
            for (int index = 0; index < entries.Count; index++)
            {
                SimulationLogEntry entry = entries[index];
                if (entry == null ||
                    entry.GameHour < minimumGameHour ||
                    entry.GameHour > maximumGameHour ||
                    (!string.IsNullOrEmpty(category) && entry.Category != category) ||
                    (!string.IsNullOrEmpty(eventKey) && entry.EventKey != eventKey) ||
                    (subject != null && !Matches(entry.PrimarySubject, subjectId) &&
                     !Matches(entry.SecondarySubject, subjectId)) ||
                    (target != null && !Matches(entry.SecondarySubject, targetId)))
                {
                    continue;
                }

                result.Add(entry);
            }

            return result;
        }

        public static SimulationLogEntry RecordEvent(
            string eventKey,
            string category,
            string severity = "Info",
            UnityEngine.Object primarySubject = null,
            UnityEngine.Object secondarySubject = null,
            params SimulationLogField[] fields)
        {
            if (Instance == null)
                return null;

            return Instance.Record(
                eventKey,
                category,
                severity,
                primarySubject,
                secondarySubject,
                fields);
        }

        public static SimulationLogEntry RecordLegacyEvent(
            string eventKey,
            string subject,
            string detail = "",
            string correlation = "")
        {
            return Instance != null
                ? Instance.RecordLegacy(eventKey, subject, detail, correlation)
                : null;
        }

        private SimulationLogEntry Append(
            string eventKey,
            string category,
            string severity,
            SimulationLogSubject primarySubject,
            SimulationLogSubject secondarySubject,
            IEnumerable<SimulationLogField> fields)
        {
            if (entries == null)
                entries = new List<SimulationLogEntry>();

            SimulationLogEntry entry = new SimulationLogEntry(
                ++nextSequenceNumber,
                SimulationManager.Instance != null
                    ? SimulationManager.Instance.CurrentGameHour
                    : 0f,
                eventKey,
                category,
                severity,
                primarySubject,
                secondarySubject,
                fields);

            entries.Add(entry);
            int safeCapacity = Mathf.Max(1, capacity);
            while (entries.Count > safeCapacity)
                entries.RemoveAt(0);

            if (writeJsonl && !string.IsNullOrEmpty(jsonlPath))
            {
                try
                {
                    File.AppendAllText(jsonlPath, JsonUtility.ToJson(entry) + Environment.NewLine);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Simulation log JSONL write failed: {exception.Message}", this);
                }
            }

            if (echoToConsole)
                Debug.Log(entry.Render(), this);

            return entry;
        }

        private static bool Matches(SimulationLogSubject subject, string entityId)
        {
            return subject != null && subject.EntityId == entityId;
        }
    }
}
