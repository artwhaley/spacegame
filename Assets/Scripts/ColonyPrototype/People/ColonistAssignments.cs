using UnityEngine;

namespace AsteroidColony
{
    [DisallowMultipleComponent]
    public sealed class ColonistAssignments : MonoBehaviour
    {
        [SerializeField]
        private ActivityTarget sleepTarget = new ActivityTarget();

        public ActivityTarget SleepTarget => sleepTarget;

        public bool TryGetSleepTarget(out ActivityTarget target)
        {
            target = sleepTarget;

            return target != null &&
                   target.IsConfigured;
        }

        private void OnValidate()
        {
            if (sleepTarget == null ||
                sleepTarget.Facility == null ||
                string.IsNullOrWhiteSpace(sleepTarget.ActivityId))
            {
                return;
            }

            if (!sleepTarget.Facility.TryGetBinding(
                    sleepTarget.ActivityId,
                    out _))
            {
                Debug.LogWarning(
                    $"{name}: assigned Sleep target references activity " +
                    $"'{sleepTarget.ActivityId}', but {sleepTarget.Facility.name} " +
                    "does not contain that activity.",
                    this);
            }
        }
    }
}
