using System;
using System.Collections.Generic;

namespace AsteroidColony
{
    public sealed class OffDutyCompletionRecord
    {
        public OffDutyCompletionRecord(string cooldownKey, float lastCompletedAbsoluteGameHour)
        {
            CooldownKey = cooldownKey ?? string.Empty;
            LastCompletedAbsoluteGameHour = lastCompletedAbsoluteGameHour;
        }

        public string CooldownKey { get; }
        public float LastCompletedAbsoluteGameHour { get; }
    }

    /// <summary>
    /// Records when a discretionary activity was genuinely completed so that the same
    /// authored cooldown key cannot be chosen back-to-back. This is a plain C# object owned
    /// by the colonist brain; it is not a component, and it is keyed by semantic cooldown key
    /// rather than by facility instance so it stays friendly to future save/load.
    /// </summary>
    public sealed class OffDutyCompletionHistory
    {
        private readonly List<OffDutyCompletionRecord> records =
            new List<OffDutyCompletionRecord>();

        public IReadOnlyList<OffDutyCompletionRecord> Records => records;

        /// <summary>
        /// Records a successful completion. Blank keys and invalid hours are ignored.
        /// Recording the same key again replaces the previous timestamp.
        /// </summary>
        public void RecordCompletion(string cooldownKey, float absoluteGameHour)
        {
            if (string.IsNullOrWhiteSpace(cooldownKey) || !IsFinite(absoluteGameHour))
                return;

            string key = cooldownKey.Trim();
            for (int index = 0; index < records.Count; index++)
            {
                if (string.Equals(records[index].CooldownKey, key, StringComparison.Ordinal))
                {
                    records[index] = new OffDutyCompletionRecord(key, absoluteGameHour);
                    return;
                }
            }

            records.Add(new OffDutyCompletionRecord(key, absoluteGameHour));
        }

        public bool TryGetLastCompletion(string cooldownKey, out float absoluteGameHour)
        {
            absoluteGameHour = 0f;
            if (string.IsNullOrWhiteSpace(cooldownKey))
                return false;

            string key = cooldownKey.Trim();
            for (int index = 0; index < records.Count; index++)
            {
                if (string.Equals(records[index].CooldownKey, key, StringComparison.Ordinal))
                {
                    absoluteGameHour = records[index].LastCompletedAbsoluteGameHour;
                    return true;
                }
            }

            return false;
        }

        public bool IsOnCooldown(
            string cooldownKey,
            float cooldownGameHours,
            float currentAbsoluteGameHour)
        {
            return TryGetRemainingCooldown(
                cooldownKey,
                cooldownGameHours,
                currentAbsoluteGameHour,
                out float remaining) &&
                remaining > 0f;
        }

        public bool TryGetRemainingCooldown(
            string cooldownKey,
            float cooldownGameHours,
            float currentAbsoluteGameHour,
            out float remainingGameHours)
        {
            remainingGameHours = 0f;
            if (!IsFinite(cooldownGameHours) ||
                cooldownGameHours <= 0f ||
                !IsFinite(currentAbsoluteGameHour) ||
                !TryGetLastCompletion(cooldownKey, out float completedAt))
            {
                return false;
            }

            float remaining = completedAt + cooldownGameHours - currentAbsoluteGameHour;
            remainingGameHours = remaining > 0f ? remaining : 0f;
            return true;
        }

        public bool TryGetCooldownUntil(
            string cooldownKey,
            float cooldownGameHours,
            out float cooldownUntilGameHour)
        {
            cooldownUntilGameHour = 0f;
            if (!IsFinite(cooldownGameHours) ||
                cooldownGameHours <= 0f ||
                !TryGetLastCompletion(cooldownKey, out float completedAt))
            {
                return false;
            }

            cooldownUntilGameHour = completedAt + cooldownGameHours;
            return true;
        }

        public void Clear()
        {
            records.Clear();
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
