using System;

namespace AsteroidColony
{
    /// <summary>
    /// Single source of truth for legal runtime quantities. Discrete quantities
    /// stay represented as floats for this refactor, but only whole values may
    /// cross a mutation boundary.
    /// </summary>
    public static class ResourceQuantityRules
    {
        public const float WholeNumberEpsilon = 0.0001f;

        public static bool TryValidate(ResourceDefinition resource, float amount, out string error)
        {
            if (resource == null)
            {
                error = "Resource is required.";
                return false;
            }

            if (float.IsNaN(amount) || float.IsInfinity(amount))
            {
                error = $"{resource.name} quantity must be finite.";
                return false;
            }

            if (amount < 0f)
            {
                error = $"{resource.name} quantity cannot be negative.";
                return false;
            }

            if (resource.IsDiscrete && !IsWhole(amount))
            {
                error = $"{resource.name} is Discrete and requires a whole quantity; received {amount}.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool IsWhole(float amount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount))
                return false;
            return Math.Abs(amount - Math.Round(amount)) <= WholeNumberEpsilon;
        }

        /// <summary>
        /// Normalizes a legal near-integer discrete quantity. Invalid quantities
        /// return false and are never silently floored.
        /// </summary>
        public static bool TryNormalize(ResourceDefinition resource, float amount, out float normalized)
        {
            normalized = 0f;
            if (!TryValidate(resource, amount, out _))
                return false;

            normalized = resource.IsDiscrete
                ? (float)Math.Round(amount)
                : amount;
            return true;
        }
    }
}
