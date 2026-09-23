using UnityEngine;

namespace AsteroidColony
{
    public enum DockingPortState
    {
        Free,
        Reserved,
        Occupied,
        Closed
    }

    /// <summary>A reusable authored berth with one reservation/occupant at a time.</summary>
    public sealed class DockingPortComponent : MonoBehaviour
    {
        [Header("Authored port nodes")]
        [SerializeField] private Transform nodeDocking;
        [SerializeField] private Transform nodeApproach;
        [SerializeField] private Transform nodeClearance;

        [Header("Capture tolerances")]
        [Min(0.001f)] public float capturePositionTolerance = 0.1f;
        [Min(0f)] public float captureRelativeSpeedTolerance = 0.1f;
        [Range(0.001f, 180f)] public float captureAngleToleranceDegrees = 1f;
        [Min(0f)] public float captureAngularSpeedTolerance = 2f;

        [Header("Initial runtime state")]
        [SerializeField] private DockingPortState state = DockingPortState.Free;
        [SerializeField] private ShuttleDockingProbeComponent reservationHolder;
        [SerializeField] private ShuttleDockingProbeComponent occupant;

        public Transform DockingNode { get => nodeDocking; set => nodeDocking = value; }
        public Transform ApproachNode { get => nodeApproach; set => nodeApproach = value; }
        public Transform ClearanceNode { get => nodeClearance; set => nodeClearance = value; }
        public DockingPortState State => state;
        public ShuttleDockingProbeComponent ReservationHolder => reservationHolder;
        public ShuttleDockingProbeComponent Occupant => occupant;

        private void Awake()
        {
            if (Application.isPlaying && !ValidateConfiguration(out string reason))
                Debug.LogError($"{name}: invalid docking port: {reason}", this);
        }

        public bool ValidateConfiguration(out string reason)
        {
            if (!IsChildNode(nodeDocking) || !IsChildNode(nodeApproach) || !IsChildNode(nodeClearance))
            {
                reason = "required node references are missing or are not children of the port root";
                return false;
            }
            if (!PositiveFinite(capturePositionTolerance) || !NonNegativeFinite(captureRelativeSpeedTolerance) ||
                !PositiveFinite(captureAngleToleranceDegrees) || captureAngleToleranceDegrees > 180f ||
                !NonNegativeFinite(captureAngularSpeedTolerance))
            {
                reason = "capture tolerances must be finite; position/angle must be positive and speed tolerances non-negative";
                return false;
            }

            switch (state)
            {
                case DockingPortState.Free:
                case DockingPortState.Closed:
                    if (reservationHolder != null || occupant != null)
                    {
                        reason = $"{state} port has a reservation holder or occupant";
                        return false;
                    }
                    break;
                case DockingPortState.Reserved:
                    if (reservationHolder == null || occupant != null)
                    {
                        reason = "Reserved port must have one reservation holder and no occupant";
                        return false;
                    }
                    break;
                case DockingPortState.Occupied:
                    if (occupant == null || reservationHolder != null)
                    {
                        reason = "Occupied port must have one occupant and no reservation holder";
                        return false;
                    }
                    break;
                default:
                    reason = "port state is not recognized";
                    return false;
            }

            reason = "valid";
            return true;
        }

        public bool TryReserve(ShuttleDockingProbeComponent shuttle)
        {
            return TryReserve(shuttle, out _);
        }

        public bool TryReserve(ShuttleDockingProbeComponent shuttle, out string reason)
        {
            if (shuttle == null)
            {
                reason = "missing Shuttle probe";
                return false;
            }
            if (!ValidateConfiguration(out reason))
                return false;
            if (state == DockingPortState.Reserved && reservationHolder == shuttle)
            {
                reason = "already reserved by this Shuttle";
                return true;
            }
            if (state != DockingPortState.Free)
            {
                reason = state == DockingPortState.Closed ? "port is closed" : $"port is {state.ToString().ToLowerInvariant()}";
                return false;
            }

            state = DockingPortState.Reserved;
            reservationHolder = shuttle;
            occupant = null;
            reason = "reserved";
            SimulationLog.Log($"{name} reserved for {shuttle.name}");
            return true;
        }

        public bool TryOccupy(ShuttleDockingProbeComponent shuttle)
        {
            return TryOccupy(shuttle, out _);
        }

        public bool TryOccupy(ShuttleDockingProbeComponent shuttle, out string reason)
        {
            if (shuttle == null)
            {
                reason = "missing Shuttle probe";
                return false;
            }
            if (!ValidateConfiguration(out reason))
                return false;
            if (state == DockingPortState.Occupied && occupant == shuttle)
            {
                reason = "already occupied by this Shuttle";
                return true;
            }
            if (state != DockingPortState.Reserved || reservationHolder != shuttle)
            {
                reason = "Shuttle does not hold this port reservation";
                return false;
            }

            state = DockingPortState.Occupied;
            reservationHolder = null;
            occupant = shuttle;
            reason = "occupied";
            SimulationLog.Log($"{name} captured by {shuttle.name}");
            return true;
        }

        public bool Release(ShuttleDockingProbeComponent shuttle)
        {
            if (shuttle == null ||
                (state != DockingPortState.Reserved || reservationHolder != shuttle) &&
                (state != DockingPortState.Occupied || occupant != shuttle))
                return false;

            state = DockingPortState.Free;
            reservationHolder = null;
            occupant = null;
            SimulationLog.Log($"{name} released by {shuttle.name}");
            return true;
        }

        public bool Close()
        {
            if (state == DockingPortState.Closed)
                return true;
            if (state != DockingPortState.Free)
                return false;
            state = DockingPortState.Closed;
            return true;
        }

        public bool Open()
        {
            if (state == DockingPortState.Free)
                return true;
            if (state != DockingPortState.Closed)
                return false;
            state = DockingPortState.Free;
            return true;
        }

        private bool IsChildNode(Transform node)
        {
            return node != null && node != transform && node.IsChildOf(transform);
        }

        private static bool PositiveFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static bool NonNegativeFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (nodeDocking == null)
                nodeDocking = DockingPoseUtility.FindChildByNames(transform, "nodeDocking");
            if (nodeApproach == null)
                nodeApproach = DockingPoseUtility.FindChildByNames(transform, "nodeApproach");
            if (nodeClearance == null)
                nodeClearance = DockingPoseUtility.FindChildByNames(transform, "nodeClearance");
        }
#endif
    }
}
