using System.Collections.Generic;
using System.Reflection;
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
        public void RequiredClassBlocksUntilEligibleWorkerIsWorking()
        {
            ConfigureInventory(input, 4f, 2f);
            ConfigureInventory(output, 2f, 0f);
            WorkerClassDefinition requiredClass = ScriptableObject.CreateInstance<WorkerClassDefinition>();
            createdObjects.Add(requiredClass);
            StaffingComponent staffing = converterObject.AddComponent<StaffingComponent>();
            ColonistAgent worker = new GameObject("Worker").AddComponent<ColonistAgent>();
            createdObjects.Add(worker.gameObject);
            worker.currentLocation = converterObject.AddComponent<LocationAnchor>();
            worker.activity = ColonistActivity.Working;
            staffing.workplace = worker.currentLocation;
            staffing.assignedWorkers.Add(worker);
            RecipeDefinition recipe = CreateRecipe("staffed", RecipeExecutionMode.Continuous,
                new ResourceAmount { resource = input, amount = 1f },
                new ResourceAmount { resource = output, amount = 1f });
            recipe.staffingRule.minimumWorkers = 1;
            recipe.staffingRule.requiredClass = requiredClass;
            ResourceConverterComponent converter = CreateConverter(recipe);
            converter.staffing = staffing;

            converter.SimulationTick(1f);
            Assert.That(converter.State, Is.EqualTo(ResourceConverterState.StaffingBlocked));
            worker.classes.Add(requiredClass);
            converter.SimulationTick(1f);
            Assert.That(converter.State, Is.EqualTo(ResourceConverterState.Running));
        }

        private ResourceConverterComponent CreateConverter(RecipeDefinition recipe)
        {
            ResourceConverterComponent converter = converterObject.AddComponent<ResourceConverterComponent>();
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
    }
}
