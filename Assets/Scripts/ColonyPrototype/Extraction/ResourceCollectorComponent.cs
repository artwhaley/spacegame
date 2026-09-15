using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Generic deposit-to-cargo extraction mechanics.</summary>
    public class ResourceCollectorComponent : MonoBehaviour
    {
        public ResourceDefinition collectableResource;
        public float extractionRatePerGameHour = 1f;
        public InventoryComponent destinationCargo;

        [SerializeField] private float discreteExtractionAccumulator;
        private const float QuantityEpsilon = 0.0001f;

        public float DiscreteExtractionAccumulator => discreteExtractionAccumulator;

        private void Awake()
        {
            if (destinationCargo == null)
                destinationCargo = GetComponent<InventoryComponent>();
        }

        /// <summary>Extracts as much legal resource as the cargo can accept.</summary>
        public float Collect(ResourceDeposit deposit, float deltaGameHours)
        {
            if (deposit == null || !deposit.extractionEnabled ||
                deposit.resource != collectableResource || destinationCargo == null ||
                collectableResource == null || extractionRatePerGameHour <= 0f)
                return 0f;

            float freeCargo = destinationCargo.GetFreeCapacity(collectableResource);
            if (freeCargo <= QuantityEpsilon)
                return 0f;

            if (collectableResource.IsDiscrete)
            {
                discreteExtractionAccumulator += extractionRatePerGameHour * Mathf.Max(0f, deltaGameHours);
                float wholeWork = Mathf.Floor(discreteExtractionAccumulator + ResourceQuantityRules.WholeNumberEpsilon);
                float request = Mathf.Min(wholeWork, Mathf.Floor(freeCargo + ResourceQuantityRules.WholeNumberEpsilon));
                if (request < 1f)
                    return 0f;

                float extracted = deposit.Extract(request);
                float loaded = destinationCargo.Add(collectableResource, extracted);
                discreteExtractionAccumulator = Mathf.Max(0f, discreteExtractionAccumulator - loaded);
                return loaded;
            }

            float requested = Mathf.Min(
                extractionRatePerGameHour * Mathf.Max(0f, deltaGameHours), freeCargo);
            if (requested <= QuantityEpsilon)
                return 0f;

            float fractionalExtracted = deposit.Extract(requested);
            return destinationCargo.Add(collectableResource, fractionalExtracted);
        }
    }
}
