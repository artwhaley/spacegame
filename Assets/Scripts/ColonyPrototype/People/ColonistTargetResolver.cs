using UnityEngine;

namespace AsteroidColony
{
    public enum ActivityPurpose
    {
        Sleep
    }

    [DisallowMultipleComponent]
    public sealed class ColonistTargetResolver : MonoBehaviour
    {
        [SerializeField]
        private ColonistAssignments assignments;

        private void Awake()
        {
            if (assignments == null)
                assignments = GetComponent<ColonistAssignments>();
        }

        public bool TryResolveTarget(
            ActivityPurpose purpose,
            out ActivityTarget target)
        {
            if (TryResolvePersonalAssignment(purpose, out target))
                return true;

            // FUTURE RESOLUTION STRATEGY:
            // Obligations such as employment will directly provide
            // an assigned work/activity target.

            // FUTURE RESOLUTION STRATEGY:
            // Unassigned public needs such as eating will discover
            // and rank registered activity opportunities.

            target = null;
            return false;
        }

        private bool TryResolvePersonalAssignment(
            ActivityPurpose purpose,
            out ActivityTarget target)
        {
            target = null;

            if (assignments == null)
                return false;

            switch (purpose)
            {
                case ActivityPurpose.Sleep:
                    return assignments.TryGetSleepTarget(out target);

                default:
                    return false;
            }
        }
    }
}
