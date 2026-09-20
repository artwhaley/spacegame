using System;
using UnityEngine;

namespace Colony.Interactions
{
    public enum ContactTimingMode
    {
        HoldForSegment,
        NormalizedWindow
    }

    [Serializable]
    public sealed class AnimationContact
    {
        [SerializeField] private string channel;
        [SerializeField] private Transform target;
        [SerializeField, Range(0f, 1f)] private float maximumWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float positionWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float rotationWeight = 1f;
        [SerializeField] private ContactTimingMode timingMode = ContactTimingMode.HoldForSegment;
        [SerializeField, Range(0f, 1f)] private float startNormalizedTime;
        [SerializeField, Range(0f, 1f)] private float fullWeightNormalizedTime = 0.15f;
        [SerializeField, Range(0f, 1f)] private float releaseStartNormalizedTime = 0.85f;
        [SerializeField, Range(0f, 1f)] private float endNormalizedTime = 1f;
        [SerializeField, Min(0f)] private float blendInSeconds = 0.15f;
        [SerializeField, Min(0f)] private float blendOutSeconds = 0.15f;

        public string Channel => channel;
        public Transform Target => target;
        public float MaximumWeight => Mathf.Clamp01(maximumWeight);
        public float PositionWeight => Mathf.Clamp01(positionWeight);
        public float RotationWeight => Mathf.Clamp01(rotationWeight);
        public ContactTimingMode TimingMode => timingMode;
        public float BlendInSeconds => Mathf.Max(0f, blendInSeconds);
        public float BlendOutSeconds => Mathf.Max(0f, blendOutSeconds);

        public float EvaluateWeight(float normalizedProgress)
        {
            if (timingMode == ContactTimingMode.HoldForSegment)
            {
                return MaximumWeight;
            }

            float start = Mathf.Clamp01(startNormalizedTime);
            float full = Mathf.Clamp01(Mathf.Max(start, fullWeightNormalizedTime));
            float release = Mathf.Clamp01(Mathf.Max(full, releaseStartNormalizedTime));
            float end = Mathf.Clamp01(Mathf.Max(release, endNormalizedTime));
            float progress = Mathf.Clamp01(normalizedProgress);

            if (progress <= start || progress >= end)
            {
                return 0f;
            }

            if (progress < full)
            {
                return MaximumWeight * Mathf.InverseLerp(start, full, progress);
            }

            if (progress <= release)
            {
                return MaximumWeight;
            }

            return MaximumWeight * (1f - Mathf.InverseLerp(release, end, progress));
        }
    }
}
