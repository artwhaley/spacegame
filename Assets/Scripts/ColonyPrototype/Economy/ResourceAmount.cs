using System;

namespace AsteroidColony
{
    [Serializable]
    public struct ResourceAmount
    {
        public ResourceDefinition resource;
        public float amount;

        public bool Validate(out string error)
        {
            return ResourceQuantityRules.TryValidate(resource, amount, out error);
        }
    }
}
