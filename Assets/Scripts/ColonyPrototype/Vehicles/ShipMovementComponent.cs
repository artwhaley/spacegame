using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Direct simulation-time movement used by the prototype ships.</summary>
    public class ShipMovementComponent : MonoBehaviour
    {
        public float movementSpeed = 12f;
        public const float ArrivalEpsilon = 0.05f;

        public bool MoveToward(LocationAnchor target, float deltaGameHours)
        {
            return target == null || MoveToward(target.transform.position, deltaGameHours);
        }

        public bool MoveToward(Vector3 target, float deltaGameHours)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, target, Mathf.Max(0f, movementSpeed) * Mathf.Max(0f, deltaGameHours));
            return Vector3.Distance(transform.position, target) <= ArrivalEpsilon;
        }
    }
}
