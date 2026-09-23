using System.Collections.Generic;
using System.Reflection;
using Colony.Interactions;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class ResourceConverterTests
    {
        private GameObject converterObject;
        private InventoryComponent inventory;
        private ResourceDefinition input;
        private ResourceDefinition output;
        private ResourceDefinition wrench;
        private List<Object> createdObjects;

        [SetUp]
        public void SetUp()
        {
            createdObjects = new List<Object>();
            converterObject = new GameObject("Converter Test");
            inventory = converterObject.AddComponent<InventoryComponent>();
            input = CreateResource("Input", ResourceQuantityMode.Fractional);
            output = CreateResource("Output", ResourceQuantityMode.Fractional);
            wrench = CreateResource("Wrench", ResourceQuantityMode.Discrete);
        }

        [TearDown]
        public void TearDown()
        {
            PropertyInfo workforceInstance = typeof(WorkforceManager).GetProperty(
                "Instance",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            workforceInstance.GetSetMethod(true).Invoke(null, new object[] { null });
            Object.DestroyImmediate(converterObject);
            for (int i = 0; i < createdObjects.Count; i++)
                Object.DestroyImmediate(createdObjects[i]);
        }

        [Test]
        public void ContinuousConversionSupportsFractionalProgress()
        {
            ConfigureInventory(input, 20f, 10f);
            ConfigureInventory(output, 20f, 0f);
            RecipeDefinition recipe = CreateRecipe("continuous", RecipeExecutionMode.Continuous,
                new ResourceAmount { resource = input, amount = 4f },
                new ResourceAmount { resource = output, amount = 1f });
            recipe.durationHours = 2f;
            ResourceConverterComponent converter = CreateConverter(recipe);

            converter.SimulationTick(0.4f);

            Assert.That(inventory.GetOnHand(input), Is.EqualTo(9.2f).Within(0.0001f));
            Assert.That(inventory.GetOnHand(output), Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(converter.State, Is.EqualTo(ResourceConverterState.Running));
        }

        [Test]
        public void ContinuousDiscreteRecipeIsRefused()
        {
            ConfigureInventory(wrench, 2f, 0f);
            RecipeDefinition recipe = CreateRecipe("invalid", RecipeExecutionMode.Continuous,
                new ResourceAmount { resource = input, amount = 1f },
                new ResourceAmount { resource = wrench, amount = 1f });
            Assert.That(recipe.Validate(out _), Is.False);

            ResourceConverterComponent converter = CreateConverter(recipe);
            converter.SimulationTick(1f);
            Assert.That(converter.State, Is.EqualTo(ResourceConverterState.Idle));
            Assert.That(inventory.GetOnHand(wrench), Is.EqualTo(0f));
        }

        [Test]
        public void BatchDiscreteOutputAppearsOnlyOnCompletion()
        {
            ConfigureInventory(input, 4f, 2f);
            ConfigureInventory(wrench, 1f, 0f);
            RecipeDefinition recipe = CreateRecipe("batch", RecipeExecutionMode.Batch,
                new ResourceAmount { resource = input, amount = 2f },
                new ResourceAmount { resource = wrench, amount = 1f });
            recipe.durationHours = 2f;
            ResourceConverterComponent converter = CreateConverter(recipe);

            converter.SimulationTick(1f);
            Assert.That(inventory.GetOnHand(wrench), Is.EqualTo(0f));
            converter.SimulationTick(1f);
            Assert.That(inventory.GetOnHand(wrench), Is.EqualTo(1f));
            Assert.That(ResourceQuantityRules.IsWhole(inventory.GetOnHand(wrench)), Is.True);
        }

        [Test]
        public void CompletedBatchWaitsWithoutLosingOutput()
        {
            ConfigureInventory(input, 4f, 2f);
            ConfigureInventory(wrench, 1f, 0f);
            RecipeDefinition recipe = CreateRecipe("blocked", RecipeExecutionMode.Batch,
                new ResourceAmount { resource = input, amount = 2f },
                new ResourceAmount { resource = wrench, amount = 1f });
            recipe.durationHours = 1f;
            ResourceConverterComponent converter = CreateConverter(recipe);

            converter.SimulationTick(0.1f);
            inventory.Add(wrench, 1f);
            converter.SimulationTick(1f);
            Assert.That(converter.State, Is.EqualTo(ResourceConverterState.CompletedBatchWaitingForOutput));
            inventory.Remove(wrench, 1f);
            converter.SimulationTick(0.1f);
            Assert.That(inventory.GetOnHand(wrench), Is.EqualTo(1f));
        }

        [Test]
        public void FacilityPerformanceGatesAndScalesConversion()
        {
            ConfigureInventory(input, 10f, 10f);
            ConfigureInventory(output, 10f, 0f);
            FacilityEffectDefinition rate = ScriptableObject.CreateInstance<FacilityEffectDefinition>();
            createdObjects.Add(rate);
            TestEffectProvider provider = converterObject.AddComponent<TestEffectProvider>();
            provider.effect = rate;
            provider.multiplier = 1f;
            FacilityPerformanceComponent performance = converterObject.AddComponent<FacilityPerformanceComponent>();
            RecipeDefinition recipe = CreateRecipe("productive", RecipeExecutionMode.Continuous,
                new ResourceAmount { resource = input, amount = 1f },
                new ResourceAmount { resource = output, amount = 1f });
            ResourceConverterComponent converter = CreateConverter(recipe);
            converter.performance = performance;
            converter.productionRateEffect = rate;

            provider.blocker = "Understaffed: Farm Operator 0/1 active";
            converter.SimulationTick(1f);
            Assert.That(converter.State, Is.EqualTo(ResourceConverterState.StaffingBlocked));
            Assert.That(inventory.GetOnHand(output), Is.EqualTo(0f));

            provider.blocker = null;
            provider.multiplier = 0.5f;
            converter.SimulationTick(1f);
            Assert.That(converter.State, Is.EqualTo(ResourceConverterState.Running));
            Assert.That(inventory.GetOnHand(output), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(inventory.GetOnHand(input), Is.EqualTo(9.5f).Within(0.0001f));
        }

        [Test]
        public void ConverterWithoutPerformanceRunsAtFullRate()
        {
            ConfigureInventory(input, 4f, 2f);
            ConfigureInventory(output, 4f, 0f);
            RecipeDefinition recipe = CreateRecipe("automated", RecipeExecutionMode.Continuous,
                new ResourceAmount { resource = input, amount = 1f },
                new ResourceAmount { resource = output, amount = 1f });
            ResourceConverterComponent converter = CreateConverter(recipe);

            converter.SimulationTick(1f);

            Assert.That(converter.State, Is.EqualTo(ResourceConverterState.Running));
            Assert.That(inventory.GetOnHand(output), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void WorkforceProviderRequiresGenuineActiveWorkForFoodProduction()
        {
            ResourceDefinition food = CreateResource("Food", ResourceQuantityMode.Discrete);
            food.hungerRecoveryPerUnit = 90f;
            food.consumptionDurationGameHours = 0.25f;
            ConfigureInventory(input, 20f, 20f);
            ConfigureInventory(food, 20f, 0f);

            GameObject farmObject = new GameObject("Farm");
            createdObjects.Add(farmObject);
            InteractableFacility farmFacility = farmObject.AddComponent<InteractableFacility>();
            Transform approachAnchor = new GameObject("Farm Approach").transform;
            approachAnchor.SetParent(farmObject.transform);
            SetPrivateField(farmFacility, "activities", new[]
            {
                CreateActivityBinding("FarmWork", "FarmWork", approachAnchor)
            });

            WorkplaceComponent workplace = farmObject.AddComponent<WorkplaceComponent>();
            JobRoleDefinition farmer = ScriptableObject.CreateInstance<JobRoleDefinition>();
            farmer.name = "Farmer";
            SetPrivateField(farmer, "stableId", "farmer");
            createdObjects.Add(farmer);
            SetPrivateField(workplace, "roles", new[]
            {
                CreateWorkplaceRoleBinding(farmer, "FarmWork")
            });

            GameObject colonistObject = new GameObject("Farmer Colonist");
            createdObjects.Add(colonistObject);
            ColonistIdentity colonist = colonistObject.AddComponent<ColonistIdentity>();
            ColonistActivityRunner runner = colonistObject.AddComponent<ColonistActivityRunner>();
            FacilityActivityBinding farmBinding;
            Assert.That(farmFacility.TryGetBinding("FarmWork", out farmBinding), Is.True);

            GameObject workforceObject = new GameObject("Workforce");
            createdObjects.Add(workforceObject);
            WorkforceManager workforce = workforceObject.AddComponent<WorkforceManager>();
            PropertyInfo workforceInstance = typeof(WorkforceManager).GetProperty(
                "Instance",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            workforceInstance.GetSetMethod(true).Invoke(null, new object[] { workforce });
            Assert.That(
                workforce.Assign(
                    colonist,
                    workplace,
                    farmer,
                    new DailyShiftWindow(0f, 8f)),
                Is.EqualTo(WorkAssignmentResult.Applied));

            FacilityPerformanceComponent performance =
                converterObject.AddComponent<FacilityPerformanceComponent>();
            WorkforcePerformanceProvider provider =
                converterObject.AddComponent<WorkforcePerformanceProvider>();
            provider.requiredWorkplace = workplace;
            provider.requiredRole = farmer;
            provider.minimumActiveWorkers = 1;

            RecipeDefinition recipe = CreateRecipe(
                "farm food",
                RecipeExecutionMode.Batch,
                new ResourceAmount { resource = input, amount = 1f },
                new ResourceAmount { resource = food, amount = 1f });
            ResourceConverterComponent converter = CreateConverter(recipe);
            converter.performance = performance;

            // Assignment and an in-shift schedule are not enough by themselves.
            converter.SimulationTick(1f);
            Assert.That(converter.State, Is.EqualTo(ResourceConverterState.StaffingBlocked));
            Assert.That(inventory.GetOnHand(food), Is.EqualTo(0f));

            SetPrivateField(runner, "currentFacility", farmFacility);
            SetPrivateField(runner, "currentBinding", farmBinding);
            SetPrivateField(runner, "activityActive", true);
            SetPrivateField(runner, "exitInProgress", false);

            converter.SimulationTick(0.5f);
            Assert.That(converter.State, Is.EqualTo(ResourceConverterState.Running));
            Assert.That(inventory.GetOnHand(food), Is.EqualTo(0f));
            converter.SimulationTick(0.5f);
            Assert.That(converter.State, Is.EqualTo(ResourceConverterState.Idle));
            Assert.That(inventory.GetOnHand(food), Is.EqualTo(1f));
            Assert.That(ResourceQuantityRules.IsWhole(inventory.GetOnHand(food)), Is.True);

            SetPrivateField(runner, "activityActive", false);
            converter.SimulationTick(1f);
            Assert.That(converter.State, Is.EqualTo(ResourceConverterState.StaffingBlocked));
            Assert.That(inventory.GetOnHand(food), Is.EqualTo(1f).Within(0.0001f));
        }

        private ResourceConverterComponent CreateConverter(RecipeDefinition recipe)
        {
            ResourceConverterComponent converter = converterObject.AddComponent<ResourceConverterComponent>();
            converter.inventory = inventory;
            converter.activeRecipe = recipe;
            converter.availableRecipes.Add(recipe);
            return converter;
        }

        private RecipeDefinition CreateRecipe(string id, RecipeExecutionMode mode,
            ResourceAmount recipeInput, ResourceAmount recipeOutput)
        {
            RecipeDefinition recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            recipe.name = id;
            recipe.stableId = id;
            recipe.executionMode = mode;
            recipe.inputs.Add(recipeInput);
            recipe.outputs.Add(recipeOutput);
            createdObjects.Add(recipe);
            return recipe;
        }

        private ResourceDefinition CreateResource(string id, ResourceQuantityMode mode)
        {
            ResourceDefinition resource = ScriptableObject.CreateInstance<ResourceDefinition>();
            resource.name = id;
            resource.stableId = id;
            resource.quantityMode = mode;
            createdObjects.Add(resource);
            return resource;
        }

        private void ConfigureInventory(ResourceDefinition resource, float capacity, float onHand)
        {
            FieldInfo entriesField = typeof(InventoryComponent).GetField(
                "entries", BindingFlags.Instance | BindingFlags.NonPublic);
            var entries = (List<InventoryEntry>)entriesField.GetValue(inventory);
            entries.Add(new InventoryEntry { resource = resource, capacity = capacity, onHand = onHand });
        }

        private static FacilityActivityBinding CreateActivityBinding(
            string activityId,
            string reservationGroup,
            Transform approachAnchor)
        {
            FacilityActivityBinding binding = new FacilityActivityBinding();
            SetPrivateField(binding, "activityId", activityId);
            SetPrivateField(binding, "reservationGroup", reservationGroup);
            SetPrivateField(binding, "externallyRequestable", true);
            SetPrivateField(binding, "approachAnchor", approachAnchor);
            return binding;
        }

        private static WorkplaceRoleBinding CreateWorkplaceRoleBinding(
            JobRoleDefinition role,
            string activityId)
        {
            WorkplaceRoleBinding binding = new WorkplaceRoleBinding();
            SetPrivateField(binding, "role", role);
            SetPrivateField(binding, "activityId", activityId);
            SetPrivateField(binding, "maximumConcurrentScheduledWorkers", 1);
            return binding;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field {fieldName}.");
            field.SetValue(target, value);
        }
    }
}
