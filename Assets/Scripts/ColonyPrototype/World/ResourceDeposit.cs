using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// One finite, local resource source. Extraction is explicit; deposits never
    /// replenish themselves or create logistics work.
    /// </summary>
    public class ResourceDeposit : MonoBehaviour
    {
        public string displayName;
        public ResourceType resourceType = ResourceType.Ice;
        public float startingQuantity = 500f;
        [SerializeField] private float remainingQuantity = 500f;
        public bool extractionEnabled = true;

        [SerializeField] private bool depletionLogged;

        public float RemainingQuantity => remainingQuantity;

        public Vector3 WorldPosition => transform.position;

        private void Reset()
        {
            remainingQuantity = startingQuantity;
        }

        private void OnValidate()
        {
            startingQuantity = Mathf.Max(0f, startingQuantity);
            remainingQuantity = Mathf.Clamp(remainingQuantity, 0f, startingQuantity);
        }

        /// <summary>Extracts no more than the requested amount or remaining stock.</summary>
        public float Extract(float requestedAmount)
        {
            if (!extractionEnabled || requestedAmount <= 0f || remainingQuantity <= 0f)
                return 0f;

            float extracted = Mathf.Min(requestedAmount, remainingQuantity);
            remainingQuantity = Mathf.Max(0f, remainingQuantity - extracted);

            if (remainingQuantity <= 0f && !depletionLogged)
            {
                depletionLogged = true;
                SimulationLog.Log($"{displayName} depleted");
            }

            return extracted;
        }
    }
}
