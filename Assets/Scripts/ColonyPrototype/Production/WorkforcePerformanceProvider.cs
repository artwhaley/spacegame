using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Bridges canonical Bob-era WorkforceManager truth into the reusable facility
    /// performance seam. A scheduled assignment alone is not enough: the worker must be
    /// genuinely active in the workplace's bound activity.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorkforcePerformanceProvider : MonoBehaviour, IFacilityPerformanceProvider
    {
        public WorkplaceComponent requiredWorkplace;
        public JobRoleDefinition requiredRole;
        [Min(1)] public int minimumActiveWorkers = 1;

        [SerializeField] private string lastBlockedReason;

        public string LastBlockedReason => lastBlockedReason;

        public void PublishPerformance(
            FacilityPerformanceSnapshot snapshot,
            float absoluteGameHour)
        {
            if (snapshot == null)
                return;

            if (requiredWorkplace == null || requiredRole == null ||
                minimumActiveWorkers < 1 || WorkforceManager.Instance == null)
            {
                lastBlockedReason = "workforce configuration missing";
                snapshot.AddBlocker(lastBlockedReason);
                return;
            }

            bool operational = WorkforceManager.Instance.HasEnoughActiveWorkers(
                requiredWorkplace,
                requiredRole,
                minimumActiveWorkers,
                0f,
                absoluteGameHour);
            lastBlockedReason = operational
                ? string.Empty
                : "required workforce is not genuinely active";
            if (!operational)
                snapshot.AddBlocker(lastBlockedReason);
        }

        private void OnValidate()
        {
            minimumActiveWorkers = Mathf.Max(1, minimumActiveWorkers);
        }
    }
}
