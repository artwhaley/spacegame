using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Writes a compact supply-chain sidecar beside the full simulation log.
    /// The main log and console stream remain owned by SimulationLogManager.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Colony/Logistics/Supply Chain Debug Log")]
    public sealed class SupplyChainDebugLog : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
    {
        [SerializeField, Min(0.05f)] private float snapshotIntervalGameHours = 0.25f;

        private readonly Dictionary<string, float> lastNoOfferHour = new Dictionary<string, float>();
        private StreamWriter writer;
        private SimulationLogManager subscribedLogManager;
        private float nextSnapshotGameHour;
        private bool writerFailed;

        public int SimulationTickPriority => 5000;
        public string SidecarPath { get; private set; } = string.Empty;

        private void OnEnable()
        {
            SimulationManager.RegisterTickable(this);
        }

        private void Start()
        {
            EnsureConnected();
            if (writer != null)
            {
                WriteSnapshot("initial");
                nextSnapshotGameHour = CurrentGameHour + Mathf.Max(0.05f, snapshotIntervalGameHours);
            }
        }

        private void OnDisable()
        {
            SimulationManager.UnregisterTickable(this);
            if (subscribedLogManager != null)
                subscribedLogManager.EntryRecorded -= HandleEntryRecorded;
            subscribedLogManager = null;
            CloseWriter();
        }

        public void SimulationTick(float deltaGameHours)
        {
            EnsureConnected();
            if (writer == null)
                return;

            float gameHour = CurrentGameHour;
            if (gameHour + 0.0001f < nextSnapshotGameHour)
                return;

            WriteSnapshot("interval");
            nextSnapshotGameHour = gameHour + Mathf.Max(0.05f, snapshotIntervalGameHours);
        }

        private void EnsureConnected()
        {
            SimulationLogManager current = SimulationLogManager.Instance;
            if (current != subscribedLogManager)
            {
                if (subscribedLogManager != null)
                    subscribedLogManager.EntryRecorded -= HandleEntryRecorded;
                subscribedLogManager = current;
                if (subscribedLogManager != null)
                    subscribedLogManager.EntryRecorded += HandleEntryRecorded;
            }

            if (writer != null || writerFailed || current == null)
                return;

            try
            {
                string sourcePath = current.JsonlPath;
                string directory = Path.GetDirectoryName(sourcePath);
                if (string.IsNullOrEmpty(directory))
                    directory = Application.persistentDataPath;
                Directory.CreateDirectory(directory);

                string stem = Path.GetFileNameWithoutExtension(sourcePath);
                if (string.IsNullOrEmpty(stem))
                    stem = "spacegame-simulation-log-" + DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'");
                SidecarPath = Path.Combine(directory, stem + "-supply-chain.jsonl");
                writer = new StreamWriter(SidecarPath, false, new UTF8Encoding(false))
                {
                    AutoFlush = true
                };

                TraceRow header = NewRow("session", "supply_chain_trace.started");
                Add(header, "sourceLog", sourcePath);
                Add(header, "snapshotIntervalGameHours", F(Mathf.Max(0.05f, snapshotIntervalGameHours)));
                Add(header, "mainLogUnchanged", "true");
                Add(header, "noOfferSampleIntervalGameHours", F(Mathf.Max(0.05f, snapshotIntervalGameHours)));
                Write(header);
            }
            catch (Exception exception)
            {
                FailWriter(exception);
            }
        }

        private void HandleEntryRecorded(SimulationLogEntry entry)
        {
            if (writer == null || entry == null || !ShouldCapture(entry))
                return;

            if (entry.EventKey == "food.no_offer" && !ShouldCaptureNoOffer(entry))
                return;

            try
            {
                TraceRow row = NewRow("event", entry.EventKey);
                row.sourceSequenceNumber = entry.SequenceNumber;
                row.gameHour = entry.GameHour;
                row.category = entry.Category;
                row.severity = entry.Severity;
                row.subject = entry.PrimarySubject != null ? entry.PrimarySubject.DisplayName : string.Empty;
                row.target = entry.SecondarySubject != null ? entry.SecondarySubject.DisplayName : string.Empty;
                for (int i = 0; i < entry.Fields.Count; i++)
                {
                    SimulationLogField field = entry.Fields[i];
                    if (field != null)
                        row.fields.Add(new SimulationLogField(field.Key, field.Value));
                }
                Write(row);
            }
            catch (Exception exception)
            {
                FailWriter(exception);
            }
        }

        private bool ShouldCapture(SimulationLogEntry entry)
        {
            if (entry.Category == "Logistics")
                return true;
            if (entry.Category == "Production")
                return entry.EventKey != "production.food_output";
            if (entry.Category == "Food")
            {
                switch (entry.EventKey)
                {
                    case "food.no_offer":
                    case "food.offer_created":
                    case "food.meal_committed":
                    case "food.meal_completed":
                    case "food.inventory_reserved":
                    case "food.inventory_reservation_failed":
                    case "food.inventory_released":
                    case "food.offer_rejected":
                        return true;
                    default:
                        return false;
                }
            }
            if (entry.Category == "Activity")
                return entry.EventKey.StartsWith("activity.", StringComparison.Ordinal);
            return entry.EventKey == "work.left_for_critical_need" ||
                   entry.EventKey == "colonist.need.critical_hunger";
        }

        private bool ShouldCaptureNoOffer(SimulationLogEntry entry)
        {
            string subject = entry.PrimarySubject != null
                ? entry.PrimarySubject.EntityId + "/" + entry.PrimarySubject.DisplayName
                : "unknown";
            string key = subject + "/" + entry.Reason;
            float previous;
            if (lastNoOfferHour.TryGetValue(key, out previous) &&
                entry.GameHour - previous < Mathf.Max(0.05f, snapshotIntervalGameHours))
                return false;
            lastNoOfferHour[key] = entry.GameHour;
            return true;
        }

        private void WriteSnapshot(string reason)
        {
            try
            {
                float gameHour = CurrentGameHour;
                IReadOnlyList<LogisticsStockComponent> stocks = LogisticsStockComponent.Active;
                for (int i = 0; i < stocks.Count; i++)
                {
                    LogisticsStockComponent stock = stocks[i];
                    if (stock == null || stock.Inventory == null || stock.Policies == null)
                        continue;

                    ResourceConverterComponent converter = stock.GetComponent<ResourceConverterComponent>();
                    FoodServiceComponent foodService = stock.GetComponent<FoodServiceComponent>();
                    for (int p = 0; p < stock.Policies.Count; p++)
                    {
                        LogisticsStockPolicyEntry policy = stock.Policies[p];
                        if (policy == null || policy.resource == null)
                            continue;

                        TraceRow row = NewSnapshotRow(gameHour, reason, stock.name);
                        Add(row, "locationKey", stock.GetStableKey());
                        Add(row, "role", policy.role.ToString());
                        Add(row, "resource", policy.resource.name);
                        Add(row, "onHand", F(stock.Inventory.GetOnHand(policy.resource)));
                        Add(row, "available", F(stock.Inventory.GetAvailable(policy.resource)));
                        Add(row, "reserved", F(stock.Inventory.GetReserved(policy.resource)));
                        Add(row, "capacity", F(stock.Inventory.GetCapacity(policy.resource)));
                        Add(row, "freeCapacity", F(stock.Inventory.GetFreeCapacity(policy.resource)));
                        Add(row, "target", F(policy.ResolveTarget(stock.Inventory)));
                        Add(row, "reorderThreshold", F(policy.reorderThreshold));
                        Add(row, "emergencyThreshold", F(policy.emergencyThreshold));
                        Add(row, "minimumPickup", F(policy.minimumPickup));
                        if (converter != null)
                        {
                            Add(row, "productionState", converter.State.ToString());
                            Add(row, "productionReason", converter.BlockedReason ?? string.Empty);
                            Add(row, "productionRecipe", converter.activeRecipe != null
                                ? converter.activeRecipe.stableId
                                : string.Empty);
                            float recipeOutputRate = GetRecipeOutputRate(converter.activeRecipe, policy.resource);
                            Add(row, "recipeOutputPerGameHour", F(recipeOutputRate));
                            Add(row, "staffedRecipeOutputPerGameHour", F(recipeOutputRate * converter.ThroughputMultiplier));
                            Add(row, "productionRateMultiplier", F(converter.ThroughputMultiplier));
                        }
                        if (foodService != null)
                        {
                            Add(row, "requiresStaff", foodService.RequiresStaff.ToString());
                            Add(row, "publicStaffed", foodService.HasActivePublicStaff(gameHour).ToString());
                            Add(row, "selfServicePolicy", foodService.SelfServicePolicy.ToString());
                            Add(row, "eatActivityId", foodService.EatActivityId);
                            Add(row, "hungerRecoveryPerMeal", F(foodService.HungerRecoveryPerMeal));
                            Add(row, "mealDurationGameHours", F(foodService.MealDurationGameHours));
                            Add(row, "foodAvailableForMeals", F(foodService.FoodAvailable));
                        }
                        Write(row);
                    }
                }

                FreightLogisticsManager freight = FreightLogisticsManager.Instance;
                if (freight != null)
                {
                    for (int i = 0; i < freight.Orders.Count; i++)
                    {
                        FreightOrder order = freight.Orders[i];
                        if (order == null)
                            continue;
                        TraceRow row = NewSnapshotRow(gameHour, reason, order.Id);
                        row.eventKey = "supply.snapshot.order";
                        Add(row, "requester", order.Requester != null ? order.Requester.name : string.Empty);
                        Add(row, "resource", order.Resource != null ? order.Resource.name : string.Empty);
                        Add(row, "open", order.IsOpen.ToString());
                        Add(row, "requested", F(order.Requested));
                        Add(row, "delivered", F(order.Delivered));
                        Add(row, "committed", F(order.Committed));
                        Add(row, "uncovered", F(order.Uncovered));
                        Write(row);
                    }
                    for (int i = 0; i < freight.Jobs.Count; i++)
                    {
                        FreightDeliveryJob job = freight.Jobs[i];
                        if (job == null)
                            continue;
                        TraceRow row = NewSnapshotRow(gameHour, reason, job.Allocation.Id);
                        row.eventKey = "supply.snapshot.job";
                        Add(row, "state", job.State.ToString());
                        Add(row, "resource", job.Resource != null ? job.Resource.name : string.Empty);
                        Add(row, "quantity", F(job.Quantity));
                        Add(row, "pickedUp", job.HasPickedUp.ToString());
                        Add(row, "provider", job.ActiveLegExecution != null &&
                            job.ActiveLegExecution.ProviderContext != null
                                ? job.ActiveLegExecution.ProviderContext.name : string.Empty);
                        Add(row, "source", job.Source != null ? job.Source.name : string.Empty);
                        Add(row, "destination", job.Destination != null ? job.Destination.name : string.Empty);
                        Add(row, "emergency", job.IsEmergencyWork.ToString());
                        Write(row);
                    }
                }

                IReadOnlyList<WalkingFreightWorkService> services = WalkingFreightWorkService.Active;
                for (int i = 0; i < services.Count; i++)
                {
                    WalkingFreightWorkService service = services[i];
                    if (service == null)
                        continue;
                    TraceRow row = NewSnapshotRow(gameHour, reason, service.name);
                    row.eventKey = "supply.snapshot.work_service";
                    Add(row, "workplace", service.Workplace != null ? service.Workplace.name : string.Empty);
                    Add(row, "routineEnabled", service.RoutineFreightEnabled.ToString());
                    Add(row, "routineRole", service.RoutineRole != null
                        ? service.RoutineRole.StableId : string.Empty);
                    Add(row, "emergencyEnabled", service.EmergencyFreightEnabled.ToString());
                    Add(row, "activeExecutions", service.ActiveExecutionCount.ToString());
                    Write(row);
                }
            }
            catch (Exception exception)
            {
                FailWriter(exception);
            }
        }

        private static float GetRecipeOutputRate(RecipeDefinition recipe, ResourceDefinition resource)
        {
            if (recipe == null || resource == null || recipe.durationHours <= 0f || recipe.outputs == null)
                return 0f;

            float amount = 0f;
            for (int i = 0; i < recipe.outputs.Count; i++)
                if (recipe.outputs[i].resource == resource)
                    amount += recipe.outputs[i].amount;
            return amount / recipe.durationHours;
        }

        private ResourceDefinition FindFoodResource()
        {
            IReadOnlyList<LogisticsStockComponent> stocks = LogisticsStockComponent.Active;
            for (int i = 0; i < stocks.Count; i++)
            {
                LogisticsStockComponent stock = stocks[i];
                if (stock == null || stock.Policies == null)
                    continue;
                for (int p = 0; p < stock.Policies.Count; p++)
                    if (stock.Policies[p] != null && stock.Policies[p].resource != null &&
                        stock.Policies[p].resource.name == "Food")
                        return stock.Policies[p].resource;
            }
            return null;
        }

        private TraceRow NewSnapshotRow(float gameHour, string reason, string subject)
        {
            TraceRow row = NewRow("snapshot", "supply.snapshot.stock");
            row.gameHour = gameHour;
            row.subject = subject ?? string.Empty;
            Add(row, "snapshotReason", reason);
            return row;
        }

        private static TraceRow NewRow(string recordType, string eventKey)
        {
            return new TraceRow
            {
                recordType = recordType,
                eventKey = eventKey,
                gameHour = CurrentGameHour,
                fields = new List<SimulationLogField>()
            };
        }

        private static void Add(TraceRow row, string key, string value)
        {
            row.fields.Add(new SimulationLogField(key, value ?? string.Empty));
        }

        private static string F(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private void Write(TraceRow row)
        {
            if (writer == null)
                return;
            try
            {
                writer.WriteLine(JsonUtility.ToJson(row));
            }
            catch (Exception exception)
            {
                FailWriter(exception);
            }
        }

        private void FailWriter(Exception exception)
        {
            if (writerFailed)
                return;
            writerFailed = true;
            CloseWriter();
            Debug.LogWarning("Supply-chain sidecar logging stopped: " + exception.Message, this);
        }

        private void CloseWriter()
        {
            if (writer == null)
                return;
            try
            {
                writer.Flush();
                writer.Dispose();
            }
            catch
            {
                // The sidecar is diagnostic only; shutdown should not affect the simulation.
            }
            writer = null;
        }

        private static float CurrentGameHour => SimulationManager.Instance != null
            ? SimulationManager.Instance.CurrentGameHour
            : 0f;

        [Serializable]
        private sealed class TraceRow
        {
            public string recordType;
            public long sourceSequenceNumber;
            public float gameHour;
            public string eventKey;
            public string category;
            public string severity;
            public string subject;
            public string target;
            public List<SimulationLogField> fields;
        }
    }
}
