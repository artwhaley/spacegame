using UnityEngine;

namespace AsteroidColony
{
    public enum ColonistRole
    {
        BridgeCrew,
        ShuttlePilot,
        Maintenance,
        Farmer
    }

    public enum ColonistActivity
    {
        Idle,
        WaitingForTransport,
        Passenger,
        Working,
        Resting,
        OnDutyCrew
    }

    /// <summary>
    /// A single colonist. Logical location is tracked via LocationAnchor references;
    /// the transform may simply be moved/parented when the location changes (no walking).
    /// </summary>
    public class ColonistAgent : MonoBehaviour
    {
        public string displayName;
        public ColonistRole role;
        public LocationAnchor home;
        public LocationAnchor currentLocation;
        public LocationAnchor assignedWorkplace;
        public ColonistActivity activity;

        private void Start()
        {
            if (PopulationManager.Instance != null)
                PopulationManager.Instance.Register(this);
        }

        private void OnDestroy()
        {
            if (PopulationManager.Instance != null)
                PopulationManager.Instance.Unregister(this);
        }

        /// <summary>Changes the colonist's logical location and parents the transform to the anchor.</summary>
        public void MoveToLocation(LocationAnchor newLocation)
        {
            currentLocation = newLocation;
            if (newLocation != null)
                transform.SetParent(newLocation.transform, true);
        }
    }
}