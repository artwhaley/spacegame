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
        StaffingBlocked,
        CompletedBatchWaitingForOutput,
        Disabled
    }

    /// <summary>
    /// Executes one selected recipe against one local inventory. It owns no
    /// transport behavior; outputs remain in this inventory until stock policy
    /// or another local consumer moves them.
    /// </summary>
    public class ResourceConverterComponent : MonoBehaviour, ISimulationTickable
    {
        public InventoryComponent inventory;
        public StaffingComponent staffing;
        public List<RecipeDefinition> availableRecipes = new List<RecipeDefinition>();
        public RecipeDefinition activeRecipe;
        public bool operationalEnabled = true;

        [SerializeField] private ResourceConverterState state = ResourceConverterState.Idle;
        [SerializeField] private float progress;
        [SerializeField] private float throughputMultiplier = 1f;
        [SerializeField] private string blockedReason;

        private bool batchActive;
        private float batchProgress;
        private bool started;

        private const float QuantityEpsilon = 0.0001f;

        public ResourceConverterState State => state;
        public float Progress => progress;
        public float ThroughputMultiplier => throughputMultiplier;
        public string BlockedReason => blockedReason;

        private void Awake()
        {
            if (inventory == null)
                inventory = GetComponent<InventoryComponent>();
        }

        private void Start()
        {
            started = true;
            if (SimulationManager.Instance != null)
                SimulationManager.Instance.Register(this);
        }

        private void OnEnable()
        {
            if (started && SimulationManager.Instance != null)
                SimulationManager.Instance.Register(this);
        }

        private void OnDisable()
        {
            if (SimulationManager.Instance != null)
                SimulationManager.Instance.Unregister(this);
        }

        public void SelectRecipe(RecipeDefinition recipe)
        {
            if (recipe == null || availableRecipes.Count == 0 || availableRecipes.Contains(recipe))
                activeRecipe = recipe;
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

            IReadOnlyList<ColonistAgent> workers = staffing != null
                ? staffing.GetWorkingWorkers()
                : new List<ColonistAgent>();
            RecipeStaffingRule rule = activeRecipe.staffingRule ?? new RecipeStaffingRule();
            throughputMultiplier = rule.CalculateThroughputMultiplier(workers);
            if (!rule.IsStaffed(workers))
            {
                SetState(ResourceConverterState.StaffingBlocked, "Required staffing unavailable");
                return;
            }

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
                if (!HasOutputCapacity(recipe))
                {
                    SetState(ResourceConverterState.OutputBlocked, "Complete batch output does not fit");
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

            batchActive = false;
            batchProgress = 0f;
            progress = 0f;
            SetState(ResourceConverterState.Idle, string.Empty);
        }

        private float GetInputLimit(RecipeDefinition recipe)
        {
            float limit = float.MaxValue;
            for (int i = 0; i < recipe.inputs.Count; i++)
            {
                ResourceAmount input = recipe.inputs[i];
                limit = Mathf.Min(limit, inventory.GetAvailable(input.resource) / input.amount);
            }
            return limit;
        }

        private float GetOutputLimit(RecipeDefinition recipe)
        {
            float limit = float.MaxValue;
            for (int i = 0; i < recipe.outputs.Count; i++)
            {
                ResourceAmount output = recipe.outputs[i];
                limit = Mathf.Min(limit, inventory.GetFreeCapacity(output.resource) / output.amount);
            }
            return limit;
        }

        private bool HasCompleteInputs(RecipeDefinition recipe)
        {
            for (int i = 0; i < recipe.inputs.Count; i++)
            {
                ResourceAmount input = recipe.inputs[i];
                if (inventory.GetAvailable(input.resource) + QuantityEpsilon < input.amount)
                    return false;
            }
            return true;
        }

        private bool HasOutputCapacity(RecipeDefinition recipe)
        {
            for (int i = 0; i < recipe.outputs.Count; i++)
            {
                ResourceAmount output = recipe.outputs[i];
                if (inventory.GetFreeCapacity(output.resource) + QuantityEpsilon < output.amount)
                    return false;
            }
            return true;
        }

        private bool ConsumeBatchInputs(RecipeDefinition recipe)
        {
            List<ResourceAmount> removed = new List<ResourceAmount>();
            for (int i = 0; i < recipe.inputs.Count; i++)
            {
                ResourceAmount input = recipe.inputs[i];
                float amount = inventory.Remove(input.resource, input.amount);
                if (amount < input.amount - QuantityEpsilon)
                {
                    for (int j = 0; j < removed.Count; j++)
                        inventory.Add(removed[j].resource, removed[j].amount);
                    if (amount > 0f)
                        inventory.Add(input.resource, amount);
                    return false;
                }
                removed.Add(new ResourceAmount { resource = input.resource, amount = amount });
            }
            return true;
        }

        private bool ApplyContinuous(RecipeDefinition recipe, float recipeProgress)
        {
            List<ResourceAmount> removed = new List<ResourceAmount>();
            for (int i = 0; i < recipe.inputs.Count; i++)
            {
                ResourceAmount input = recipe.inputs[i];
                float requested = input.amount * recipeProgress;
                float amount = inventory.Remove(input.resource, requested);
                if (amount < requested - QuantityEpsilon)
                {
                    for (int j = 0; j < removed.Count; j++)
                        inventory.Add(removed[j].resource, removed[j].amount);
                    if (amount > 0f)
                        inventory.Add(input.resource, amount);
                    return false;
                }
                removed.Add(new ResourceAmount { resource = input.resource, amount = amount });
            }

            List<ResourceAmount> added = new List<ResourceAmount>();
            for (int i = 0; i < recipe.outputs.Count; i++)
            {
                ResourceAmount output = recipe.outputs[i];
                float requested = output.amount * recipeProgress;
                float amount = inventory.Add(output.resource, requested);
                if (amount < requested - QuantityEpsilon)
                {
                    for (int j = 0; j < added.Count; j++)
                        inventory.Remove(added[j].resource, added[j].amount);
                    if (amount > 0f)
                        inventory.Remove(output.resource, amount);
                    for (int j = 0; j < removed.Count; j++)
                        inventory.Add(removed[j].resource, removed[j].amount);
                    return false;
                }
                added.Add(new ResourceAmount { resource = output.resource, amount = amount });
            }
            return true;
        }

        private bool EmitBatchOutputs(RecipeDefinition recipe)
        {
            List<ResourceAmount> added = new List<ResourceAmount>();
            for (int i = 0; i < recipe.outputs.Count; i++)
            {
                ResourceAmount output = recipe.outputs[i];
                float amount = inventory.Add(output.resource, output.amount);
                if (amount < output.amount - QuantityEpsilon)
                {
                    for (int j = 0; j < added.Count; j++)
                        inventory.Remove(added[j].resource, added[j].amount);
                    if (amount > 0f)
                        inventory.Remove(output.resource, amount);
                    return false;
                }
                added.Add(new ResourceAmount { resource = output.resource, amount = amount });
            }
            return true;
        }

        private void SetState(ResourceConverterState newState, string reason)
        {
            state = newState;
            blockedReason = reason;
        }
    }
}
