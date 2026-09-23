using System;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Authoritative kinematic state. Angular velocity and acceleration are
    /// world-space vectors measured in degrees per simulated second.
    /// </summary>
    [Serializable]
    public struct ShuttleFlightState
    {
        public Vector3 position;
        public Vector3 velocity;
        public Quaternion rotation;
        public Vector3 angularVelocity;
        public Vector3 lastLinearAcceleration;
        public Vector3 lastAngularAcceleration;

        public ShuttleFlightState(Vector3 position, Vector3 velocity, Quaternion rotation,
            Vector3 angularVelocity)
        {
            this.position = position;
            this.velocity = velocity;
            this.rotation = ShuttleFlightIntegrator.Normalize(rotation);
            this.angularVelocity = angularVelocity;
            lastLinearAcceleration = Vector3.zero;
            lastAngularAcceleration = Vector3.zero;
        }

        public static ShuttleFlightState FromTransform(Transform root)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));
            return new ShuttleFlightState(root.position, Vector3.zero, root.rotation, Vector3.zero);
        }

        public bool IsFinite => ShuttleFlightIntegrator.IsFinite(position) &&
            ShuttleFlightIntegrator.IsFinite(velocity) &&
            ShuttleFlightIntegrator.IsFinite(rotation) &&
            ShuttleFlightIntegrator.IsFinite(angularVelocity) &&
            ShuttleFlightIntegrator.IsFinite(lastLinearAcceleration) &&
            ShuttleFlightIntegrator.IsFinite(lastAngularAcceleration);
    }
}
