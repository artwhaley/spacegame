using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Common operational and crew state shared by ships.</summary>
    public class ShipComponent : MonoBehaviour
    {
        public string displayName = "Ship";
        public bool operationalEnabled = true;
        public ColonistAgent assignedPilot;
        public WorkerClassDefinition requiredPilotClass;

        private LocationAnchor shipLocation;

        public bool HasQualifiedPilot => assignedPilot != null &&
            (requiredPilotClass == null || assignedPilot.HasClass(requiredPilotClass));

        public bool IsOperationallyCrewed => operationalEnabled && HasQualifiedPilot &&
            (shipLocation == null || assignedPilot.currentLocation == shipLocation);

        private void Awake()
        {
            shipLocation = GetComponent<LocationAnchor>();
        }

        private void Start()
        {
            if (assignedPilot == null || shipLocation == null)
                return;

            assignedPilot.MoveToLocation(shipLocation);
            assignedPilot.activity = ColonistActivity.OnDutyCrew;
            SimulationLog.Log($"{assignedPilot.displayName} aboard {displayName}");
        }
    }
}
