using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Solves a Shuttle root pose from the relative pose of its mating probe.</summary>
    public static class DockingPoseUtility
    {
        public static bool TryGetProbePoseRelativeToRoot(Transform shuttleRoot, Transform probe,
            out Vector3 localPosition, out Quaternion localRotation)
        {
            localPosition = Vector3.zero;
            localRotation = Quaternion.identity;
            if (shuttleRoot == null || probe == null || probe == shuttleRoot || !probe.IsChildOf(shuttleRoot))
                return false;

            localPosition = shuttleRoot.InverseTransformPoint(probe.position);
            localRotation = ShuttleFlightIntegrator.Normalize(Quaternion.Inverse(shuttleRoot.rotation) * probe.rotation);
            return ShuttleFlightIntegrator.IsFinite(localPosition) &&
                ShuttleFlightIntegrator.IsFinite(localRotation);
        }

        public static bool TrySolveRootPose(Transform shuttleRoot, Transform probe, Transform targetProbe,
            out Vector3 rootPosition, out Quaternion rootRotation)
        {
            rootPosition = Vector3.zero;
            rootRotation = Quaternion.identity;
            if (targetProbe == null || !TryGetProbePoseRelativeToRoot(
                    shuttleRoot, probe, out Vector3 localPosition, out Quaternion localRotation))
                return false;
            return TrySolveRootPose(localPosition, localRotation, targetProbe.position, targetProbe.rotation,
                out rootPosition, out rootRotation);
        }

        public static bool TrySolveRootPose(Vector3 probeLocalPosition, Quaternion probeLocalRotation,
            Vector3 targetProbePosition, Quaternion targetProbeRotation,
            out Vector3 rootPosition, out Quaternion rootRotation)
        {
            rootPosition = Vector3.zero;
            rootRotation = Quaternion.identity;
            if (!ShuttleFlightIntegrator.IsFinite(probeLocalPosition) ||
                !ShuttleFlightIntegrator.IsFinite(probeLocalRotation) ||
                !ShuttleFlightIntegrator.IsFinite(targetProbePosition) ||
                !ShuttleFlightIntegrator.IsFinite(targetProbeRotation))
                return false;

            rootRotation = ShuttleFlightIntegrator.Normalize(
                targetProbeRotation * Quaternion.Inverse(probeLocalRotation));
            rootPosition = targetProbePosition - rootRotation * probeLocalPosition;
            return ShuttleFlightIntegrator.IsFinite(rootPosition) &&
                ShuttleFlightIntegrator.IsFinite(rootRotation);
        }

#if UNITY_EDITOR
        public static Transform FindChildByNames(Transform root, params string[] names)
        {
            if (root == null || names == null)
                return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                for (int n = 0; n < names.Length; n++)
                    if (child.name == names[n])
                        return child;
                Transform nested = FindChildByNames(child, names);
                if (nested != null)
                    return nested;
            }
            return null;
        }
#endif
    }
}
