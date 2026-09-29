using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    public enum ResourceConverterState
    {
        Idle,
        Running,
        InputBlocked,
        OutputBlocked,
        /// <summary>Blocked by facility performance (for example staffing).</summary>
        StaffingBlocked,
        CompletedBatchWaitingForOutput,
        Disabled
    }

    /// <summary>
    /// Executes one selected recipe against one local inventory. It owns no
    /// transport behavior; outputs remain in this inventory until stock policy
    /// or another local consumer moves them.
    /// </summary>
    public class ResourceConverterComponent : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
    {
        public InventoryComponent inventory;
        public FacilityPerformanceComponent performance;
        public FacilityEffectDefinition productionRateEffect;
        public List<RecipeDefinition> availableRecipes = new List<RecipeDefinition>();
        public RecipeDefinition activeRecipe;
        public bool operationalEnabled = true;

        [SerializeField] private ResourceConverterState state = ResourceConverterState.Idle;
        [SerializeField] private float progress;
        [SerializeField] private float throughputMultiplier = 1f;
        [SerializeField] private string blockedReason;

        [SerializeField] private bool batchActive;
        [SerializeField] private float batchProgress;

        [System.NonSerialized] private bool productionStateLogged;
        [System.NonSerialized] private ResourceConverterState lastLoggedState;
        [System.NonSerialized] private string lastLoggedReason;

        private const float QuantityEpsilon = 0.0001f;

        public ResourceConverterState State => state;
        public float Progress => progress;
        public float ThroughputMultiplier => throughputMultiplier;
        public string BlockedReason => blockedReason;
        public bool BatchActive => batchActive;
        public float BatchProgress => batchProgress;
        public int SimulationTickPriority => 200;

        private void Awake()
        {
            if (inventory == null)
                inventory = GetComponent<InventoryComponent>();
        }

        private void OnEnable()
        {
            SimulationManager.RegisterTickable(this);
        }

        private void OnDisable()
        {
            SimulationManager.UnregisterTickable(this);
            // Keep batchActive/batchProgress and all inventory reservations intact;
            // disabling pauses work rather than cancelling the batch.
            SetState(ResourceConverterState.Disabled, "Disabled");
        }

        public void SelectRecipe(RecipeDefinition recipe)
        {
            TrySelectRecipe(recipe, out _);
        }

        public bool TrySelectRecipe(RecipeDefinition recipe, out string reason)
        {
            reason = string.Empty;
            if (batchActive)
            {
                reason = "cannot switch recipe during an active batch";
                return false;
            }
            if (recipe != null && (availableRecipes == null || !availableRecipes.Contains(recipe)))
            {
                reason = "recipe is not available to this converter";
                return false;
            }
            activeRecipe = recipe;
            return true;
        }

        public void SimulationTick(float deltaGameHours)
        {
            progress = 0f;
            throughputMultiplier = 1f;
            if (!operationalEnabled)
            {
                SetState(ResourceConverterState.Disabled, "Disabled");
                return;
            }
            string recipeError = string.Empty;
            if (inventory == null || activeRecipe == null || !activeRecipe.Validate(out recipeError))
            {
                SetState(ResourceConverterState.Idle, string.IsNullOrEmpty(recipeError) ? "No active recipe" : recipeError);
                return;
            }

            // Facility performance is the only operational input. Staffing never
            // talks to production; it publishes performance and this consumes it.
            if (performance != null && !performance.IsOperational)
            {
                throughputMultiplier = 0f;
                SetState(ResourceConverterState.StaffingBlocked,
                    string.IsNullOrEmpty(performance.BlockSummary)
                        ? "Facility not operational"
                        : performance.BlockSummary);
                return;
            }
            throughputMultiplier = performance != null
                ? performance.GetMultiplier(productionRateEffect)
                : 1f;

            if (activeRecipe.executionMode == RecipeExecutionMode.Batch)
                TickBatch(deltaGameHours, activeRecipe);
            else
                TickContinuous(deltaGameHours, activeRecipe);
        }

        private void TickContinuous(float deltaGameHours, RecipeDefinition recipe)
        {
            float progressThisTick = Mathf.Max(0f, deltaGameHours) / recipe.durationHours * throughputMultiplier;
            if (progressThisTick <= QuantityEpsilon)
            {
                SetState(ResourceConverterState.Idle, string.Empty);
                return;
            }

            float inputLimit = GetInputLimit(recipe);
            if (inputLimit <= QuantityEpsilon)
            {
                SetState(ResourceConverterState.InputBlocked, "Input unavailable");
                return;
            }

            float outputLimit = GetOutputLimit(recipe);
            if (outputLimit <= QuantityEpsilon)
            {
                SetState(ResourceConverterState.OutputBlocked, "Output capacity unavailable");
                return;
            }

            float actualProgress = Mathf.Min(progressThisTick, Mathf.Min(inputLimit, outputLimit));
            if (!ApplyContinuous(recipe, actualProgress))
            {
                SetState(ResourceConverterState.InputBlocked, "Inventory mutation rejected");
                return;
            }

            progress = actualProgress;
            RecordProductionOutput(recipe, actualProgress);
            SetState(ResourceConverterState.Running, string.Empty);
        }

        private void TickBatch(float deltaGameHours, RecipeDefinition recipe)
        {
            if (!batchActive)
            {
                if (!HasCompleteInputs(recipe))
                {
                    SetState(ResourceConverterState.InputBlocked, "Complete batch inputs unavailable");
                    return;
                }
                if (!HasBatchStartCapacity(recipe))
                {
                    SetState(ResourceConverterState.OutputBlocked,
                        "Batch inputs cannot free enough shared capacity for outputs");
                    return;
                }
                if (!ConsumeBatchInputs(recipe))
                {
                    SetState(ResourceConverterState.InputBlocked, "Batch input mutation rejected");
                    return;
                }
                batchActive = true;
                batchProgress = 0f;
            }

            batchProgress = Mathf.Min(1f,
                batchProgress + Mathf.Max(0f, deltaGameHours) / recipe.durationHours * throughputMultiplier);
            progress = batchProgress;
            if (batchProgress < 1f - QuantityEpsilon)
            {
                SetState(ResourceConverterState.Running, string.Empty);
                return;
            }

            if (!HasOutputCapacity(recipe))
            {
                SetState(ResourceConverterState.CompletedBatchWaitingForOutput,
                    "Completed batch is waiting for output capacity");
                return;
            }

            if (!EmitBatchOutputs(recipe))
            {
                SetState(ResourceConverterState.CompletedBatchWaitingForOutput,
                    "Completed batch output mutation rejected");
                return;
            }

            RecordProductionOutput(recipe, 1f);
            batchActive = false;
            batchProgress = 0f;
            progress = 0f;
            SetState(ResourceConverterState.Idle, string.Empty);
        }

        private float GetInputLimit(RecipeDefinition recipe)
        {
            float limit = float.MaxValue;
            Dictionary<ResourceDefinition, float> inputs = AggregateAmounts(recipe.inputs);
            foreach (KeyValuePair<ResourceDefinition, float> input in inputs)
            {
                limit = Mathf.Min(limit, inventory.GetAvailable(input.Key) / input.Value);
            }
            return limit;
        }

        private float GetOutputLimit(RecipeDefinition recipe)
        {
            float inputPerBatch = SumAmounts(AggregateAmounts(recipe.inputs));
            float capacityPerBatch = 0f;
            for (int i = 0; i < recipe.outputs.Count; i++)
                capacityPerBatch += Mathf.Max(0f, recipe.outputs[i].amount);
            float netGrowth = capacityPerBatch - inputPerBatch;
            return netGrowth > QuantityEpsilon
                ? inventory.FreeCapacity / netGrowth
                : float.MaxValue;
        }

        private bool HasCompleteInputs(RecipeDefinition recipe)
        {
            Dictionary<ResourceDefinition, float> inputs = AggregateAmounts(recipe.inputs);
            foreach (KeyValuePair<ResourceDefinition, float> input in inputs)
            {
                if (inventory.GetAvailable(input.Key) + QuantityEpsilon < input.Value)
                    return false;
            }
            return true;
        }

        private bool HasOutputCapacity(RecipeDefinition recipe)
        {
            float capacityPerBatch = 0f;
            for (int i = 0; i < recipe.outputs.Count; i++)
                capacityPerBatch += Mathf.Max(0f, recipe.outputs[i].amount);
            return inventory.FreeCapacity + QuantityEpsilon >= capacityPerBatch;
        }

        private bool HasBatchStartCapacity(RecipeDefinition recipe)
        {
            float inputs = SumAmounts(AggregateAmounts(recipe.inputs));
            float outputs = SumAmounts(AggregateAmounts(recipe.outputs));
            return inventory.FreeCapacity + inputs + QuantityEpsilon >= outputs;
        }

        private bool ConsumeBatchInputs(RecipeDefinition recipe)
        {
            return inventory.TryApplyRecipe(recipe.inputs, null, 1f, out _);
        }

        private bool ApplyContinuous(RecipeDefinition recipe, float recipeProgress)
        {
            return inventory.TryApplyRecipe(recipe.inputs, recipe.outputs, recipeProgress, out _);
        }

        private bool EmitBatchOutputs(RecipeDefinition recipe)
        {
            return inventory.TryApplyRecipe(null, recipe.outputs, 1f, out _);
        }

        private static Dictionary<ResourceDefinition, float> AggregateAmounts(
            IReadOnlyList<ResourceAmount> amounts)
        {
            var totals = new Dictionary<ResourceDefinition, float>();
            if (amounts == null)
                return totals;
            for (int index = 0; index < amounts.Count; index++)
            {
                ResourceAmount amount = amounts[index];
                if (amount.resource == null || amount.amount <= 0f)
                    continue;
                totals.TryGetValue(amount.resource, out float current);
                totals[amount.resource] = current + amount.amount;
            }
            return totals;
        }

        private static float SumAmounts(Dictionary<ResourceDefinition, float> amounts)
        {
            float total = 0f;
            if (amounts != null)
                foreach (float amount in amounts.Values)
                    total += amount;
            return total;
        }

        private void SetState(ResourceConverterState newState, string reason)
        {
            if (!productionStateLogged ||
                newState != lastLoggedState ||
                !string.Equals(reason, lastLoggedReason, System.StringComparison.Ordinal))
            {
                if (newState == ResourceConverterState.Running &&
                    lastLoggedState != ResourceConverterState.Running)
                {
                    SimulationLogManager.RecordEvent(
                        "production.started",
                        "Production",
                        "Info",
                        this,
                        null,
                        new SimulationLogField("state", newState.ToString()));
                }
                else if (newState == ResourceConverterState.StaffingBlocked ||
                         newState == ResourceConverterState.InputBlocked ||
                         newState == ResourceConverterState.OutputBlocked ||
                         newState == ResourceConverterState.Disabled)
                {
                    SimulationLogManager.RecordEvent(
                        "production.blocked",
                        "Production",
                        "Warning",
                        this,
                        null,
                        new SimulationLogField("state", newState.ToString()),
                        new SimulationLogField("reason", reason ?? string.Empty));
                }

                productionStateLogged = true;
                lastLoggedState = newState;
                lastLoggedReason = reason;
            }
            state = newState;
            blockedReason = reason;
        }

        private void RecordProductionOutput(RecipeDefinition recipe, float recipeProgress)
        {
            if (recipe == null || recipe.outputs == null || recipeProgress <= QuantityEpsilon)
                return;

            for (int index = 0; index < recipe.outputs.Count; index++)
            {
                ResourceAmount output = recipe.outputs[index];
                if (output.resource == null)
                    continue;

                SimulationLogManager.RecordEvent(
                    "production.recipe_output",
                    "Production",
                    "Info",
                    this,
                    null,
                    new SimulationLogField("resource", output.resource.name),
                    new SimulationLogField("amount", output.amount * recipeProgress));
            }
        }
    }
}
