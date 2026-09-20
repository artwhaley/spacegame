using System;
using UnityEngine;

namespace Colony.Interactions
{
    public interface IActivitySequence
    {
        string SequenceId { get; }
        bool ExternallyRequestable { get; }
        int CycleCount { get; }
        bool IsInfinite { get; }
        ActivitySequenceStep[] Steps { get; }
    }

    [Serializable]
    public sealed class ActivitySequenceStep
    {
        [SerializeField] private string activityId;
        [SerializeField, Min(0f)] private float dwellSeconds = 3f;
        [SerializeField, Min(1)] private int loopCount = 1;

        public string ActivityId => activityId;
        public float DwellSeconds => Mathf.Max(0f, dwellSeconds);
        public int LoopCount => Mathf.Max(1, loopCount);
    }

    [Serializable]
    public sealed class FacilitySequenceBinding : IActivitySequence
    {
        [SerializeField] private string sequenceId;
        [SerializeField] private bool externallyRequestable = true;
        [SerializeField, Min(0)] private int cycleCount;
        [SerializeField] private ActivitySequenceStep[] steps = Array.Empty<ActivitySequenceStep>();

        public string SequenceId => sequenceId;
        public bool ExternallyRequestable => externallyRequestable;
        public int CycleCount => Mathf.Max(0, cycleCount);
        public bool IsInfinite => cycleCount <= 0;
        public ActivitySequenceStep[] Steps => steps;
    }
}
