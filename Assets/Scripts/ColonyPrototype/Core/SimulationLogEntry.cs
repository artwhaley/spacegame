using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AsteroidColony
{
    [Serializable]
    public sealed class SimulationLogField
    {
        [SerializeField] private string key;
        [SerializeField] private string value;

        public SimulationLogField()
        {
        }

        public SimulationLogField(string key, object value)
        {
            this.key = key ?? string.Empty;
            this.value = value != null ? value.ToString() : string.Empty;
        }

        public string Key => key ?? string.Empty;
        public string Value => value ?? string.Empty;
    }

    [Serializable]
    public sealed class SimulationLogSubject
    {
        [SerializeField] private string displayName;
        [SerializeField] private string subjectKind;
        [SerializeField] private string entityId;

        [NonSerialized] private UnityEngine.Object runtimeObject;

        public SimulationLogSubject()
        {
        }

        public SimulationLogSubject(UnityEngine.Object subject)
        {
            runtimeObject = subject;
            displayName = subject != null ? subject.name : string.Empty;
            subjectKind = subject != null ? subject.GetType().Name : string.Empty;
            entityId = subject != null ? subject.GetEntityId().ToString() : string.Empty;
        }

        public SimulationLogSubject(string displayName, string subjectKind = "")
        {
            this.displayName = displayName ?? string.Empty;
            this.subjectKind = subjectKind ?? string.Empty;
        }

        public UnityEngine.Object RuntimeObject => runtimeObject;
        public string DisplayName => displayName ?? string.Empty;
        public string SubjectKind => subjectKind ?? string.Empty;
        public string EntityId => entityId ?? string.Empty;
    }

    [Serializable]
    public sealed class SimulationLogEntry
    {
        [SerializeField] private long sequenceNumber;
        [SerializeField] private float gameHour;
        [SerializeField] private string realTimestamp;
        [SerializeField] private string eventKey;
        [SerializeField] private string category;
        [SerializeField] private string severity;
        [SerializeField] private SimulationLogSubject primarySubject;
        [SerializeField] private SimulationLogSubject secondarySubject;
        [SerializeField] private List<SimulationLogField> fields =
            new List<SimulationLogField>();

        public SimulationLogEntry()
        {
        }

        internal SimulationLogEntry(
            long sequenceNumber,
            float gameHour,
            string eventKey,
            string category,
            string severity,
            SimulationLogSubject primarySubject,
            SimulationLogSubject secondarySubject,
            IEnumerable<SimulationLogField> fields)
        {
            this.sequenceNumber = sequenceNumber;
            this.gameHour = gameHour;
            realTimestamp = DateTime.UtcNow.ToString("O");
            this.eventKey = eventKey ?? string.Empty;
            this.category = category ?? string.Empty;
            this.severity = severity ?? string.Empty;
            this.primarySubject = primarySubject;
            this.secondarySubject = secondarySubject;
            this.fields = fields != null
                ? new List<SimulationLogField>(fields)
                : new List<SimulationLogField>();
        }

        public long SequenceNumber => sequenceNumber;
        public float GameHour => gameHour;
        public string RealTimestamp => realTimestamp ?? string.Empty;
        public string EventKey => eventKey ?? string.Empty;
        public string Category => category ?? string.Empty;
        public string Severity => severity ?? string.Empty;
        public SimulationLogSubject PrimarySubject => primarySubject;
        public SimulationLogSubject SecondarySubject => secondarySubject;
        public IReadOnlyList<SimulationLogField> Fields =>
            fields ?? (IReadOnlyList<SimulationLogField>)Array.Empty<SimulationLogField>();

        public string Reason => TryGetField("reason", out string value) ? value : string.Empty;

        public bool TryGetField(string key, out string value)
        {
            if (fields != null)
            {
                for (int index = 0; index < fields.Count; index++)
                {
                    SimulationLogField field = fields[index];
                    if (field != null && string.Equals(field.Key, key, StringComparison.Ordinal))
                    {
                        value = field.Value;
                        return true;
                    }
                }
            }

            value = string.Empty;
            return false;
        }

        public string Render()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append('[');
            builder.Append(SimulationTime.FormatTimestamp(gameHour));
            builder.Append("] ");
            builder.Append(primarySubject != null ? primarySubject.DisplayName : "System");
            builder.Append(" | ");
            builder.Append(category);
            builder.Append(" | ");
            builder.Append(eventKey);

            if (secondarySubject != null && !string.IsNullOrEmpty(secondarySubject.DisplayName))
            {
                builder.Append(" | target=");
                builder.Append(secondarySubject.DisplayName);
            }

            if (fields != null)
            {
                for (int index = 0; index < fields.Count; index++)
                {
                    SimulationLogField field = fields[index];
                    if (field == null || string.IsNullOrEmpty(field.Key))
                        continue;

                    builder.Append(" | ");
                    builder.Append(field.Key);
                    builder.Append('=');
                    builder.Append(field.Value);
                }
            }

            return builder.ToString();
        }
    }
}
