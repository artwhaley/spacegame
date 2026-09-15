using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    public enum RecipeExecutionMode
    {
        Continuous,
        Batch
    }

    [CreateAssetMenu(menuName = "Asteroid Colony/Recipe Definition", fileName = "RecipeDefinition")]
    public class RecipeDefinition : ScriptableObject
    {
        public string stableId;
        public string displayName;
        [TextArea]
        public string description;
        public RecipeExecutionMode executionMode = RecipeExecutionMode.Continuous;
        public float durationHours = 1f;
        public List<ResourceAmount> inputs = new List<ResourceAmount>();
        public List<ResourceAmount> outputs = new List<ResourceAmount>();
        public RecipeStaffingRule staffingRule = new RecipeStaffingRule();

        [SerializeField] private bool valid;
        [SerializeField] private string validationError;

        public bool IsValid => Validate(out _);
        public string ValidationError => Validate(out string error) ? string.Empty : error;

        public bool Validate(out string error)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                error = "Recipe stableId is required.";
                return false;
            }

            if (durationHours <= 0f || float.IsNaN(durationHours) || float.IsInfinity(durationHours))
            {
                error = "Recipe durationHours must be finite and greater than zero.";
                return false;
            }

            if (outputs == null || outputs.Count == 0)
            {
                error = "Recipe requires at least one output.";
                return false;
            }

            if (!ValidateAmounts(inputs, "input", out error) ||
                !ValidateAmounts(outputs, "output", out error))
                return false;

            if (executionMode == RecipeExecutionMode.Continuous)
            {
                if (!AllResourcesFractional(inputs) || !AllResourcesFractional(outputs))
                {
                    error = "Continuous recipes may reference only Fractional resources.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private bool ValidateAmounts(List<ResourceAmount> amounts, string label, out string error)
        {
            if (amounts == null)
            {
                error = string.Empty;
                return true;
            }

            for (int i = 0; i < amounts.Count; i++)
            {
                ResourceAmount amount = amounts[i];
                if (amount.amount <= 0f)
                {
                    error = $"Recipe {label} amount {i} must be greater than zero.";
                    return false;
                }
                if (!amount.Validate(out error))
                    return false;
                if (executionMode == RecipeExecutionMode.Batch && amount.resource.IsDiscrete &&
                    !ResourceQuantityRules.IsWhole(amount.amount))
                {
                    error = $"Batch recipe {label} amount {i} is Discrete and must be whole.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool AllResourcesFractional(List<ResourceAmount> amounts)
        {
            if (amounts == null)
                return true;
            for (int i = 0; i < amounts.Count; i++)
                if (amounts[i].resource == null || amounts[i].resource.IsDiscrete)
                    return false;
            return true;
        }

        private void OnValidate()
        {
            stableId = stableId != null ? stableId.Trim() : string.Empty;
            displayName = displayName != null ? displayName.Trim() : string.Empty;
            valid = Validate(out validationError);
            if (!valid && !string.IsNullOrEmpty(validationError))
                Debug.LogError($"Invalid recipe {name}: {validationError}", this);
        }
    }
}
