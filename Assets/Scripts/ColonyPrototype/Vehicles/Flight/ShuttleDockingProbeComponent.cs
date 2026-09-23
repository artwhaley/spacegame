using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Serialized link to the Shuttle-side mechanical mating probe.</summary>
    public sealed class ShuttleDockingProbeComponent : MonoBehaviour
    {
        [Tooltip("Shuttle-side mating node. Its blue +Z axis points toward the socket and opposes the port node's blue +Z axis at capture.")]
        [SerializeField] private Transform nodeDocking;

        public Transform ProbeTransform
        {
            get => nodeDocking;
            set => nodeDocking = value;
        }

        public bool ValidateConfiguration(out string reason)
        {
            if (nodeDocking == null)
            {
                reason = "missing Shuttle nodeDocking reference (nodeDocking or node_docking)";
                return false;
            }
            if (nodeDocking == transform || !nodeDocking.IsChildOf(transform))
            {
                reason = "Shuttle nodeDocking must be a child of the Shuttle root";
                return false;
            }
            reason = "valid";
            return true;
        }

        private void Awake()
        {
            if (Application.isPlaying && !ValidateConfiguration(out string reason))
                Debug.LogError($"{name}: invalid docking probe: {reason}", this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (nodeDocking == null)
                nodeDocking = DockingPoseUtility.FindChildByNames(transform, "nodeDocking", "node_docking");
        }
#endif
    }
}
