using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class NoCodeAuthoringProofTests
    {
        private GameObject facility;
        private ResourceDefinition input;
        private ResourceDefinition wrench;
        private RecipeDefinition recipe;

        [SetUp]
        public void SetUp()
        {
            facility = new GameObject("Temporary Converter Facility");
            facility.AddComponent<LocationAnchor>();
            InventoryComponent inventory = facility.AddComponent<InventoryComponent>();
            input = CreateResource("TestInput", ResourceQuantityMode.Fractional);
            wrench = CreateResource("TestWrench", ResourceQuantityMode.Discrete);
            Configure(inventory, input, 4f, 2f);
            Configure(inventory, wrench, 1f, 0f);
            recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            recipe.stableId = "test-batch-authoring";
            recipe.executionMode = RecipeExecutionMode.Batch;
            recipe.durationHours = 2f;
            recipe.inputs.Add(new ResourceAmount { resource = input, amount = 2f });
            recipe.outputs.Add(new ResourceAmount { resource = wrench, amount = 1f });
            ResourceConverterComponent converter = facility.AddComponent<ResourceConverterComponent>();
            converter.availableRecipes.Add(recipe);
            converter.activeRecipe = recipe;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(recipe);
            Object.DestroyImmediate(input);
            Object.DestroyImmediate(wrench);
            Object.DestroyImmediate(facility);
        }

        [Test]
        public void BatchRecipeRunsWithOnlyGenericComposition()
        {
            InventoryComponent inventory = facility.GetComponent<InventoryComponent>();
            ResourceConverterComponent converter = facility.GetComponent<ResourceConverterComponent>();

            converter.SimulationTick(1f);
            Assert.That(inventory.GetOnHand(wrench), Is.EqualTo(0f));
            converter.SimulationTick(1f);

            Assert.That(inventory.GetOnHand(wrench), Is.EqualTo(1f));
            Assert.That(inventory.GetOnHand(input), Is.EqualTo(0f));
        }

        private static ResourceDefinition CreateResource(string stableId, ResourceQuantityMode mode)
        {
            ResourceDefinition resource = ScriptableObject.CreateInstance<ResourceDefinition>();
            resource.stableId = stableId;
            resource.quantityMode = mode;
            return resource;
        }

        private static void Configure(InventoryComponent inventory, ResourceDefinition resource,
            float capacity, float onHand)
        {
            FieldInfo entriesField = typeof(InventoryComponent).GetField(
                "entries", BindingFlags.Instance | BindingFlags.NonPublic);
            var entries = (List<InventoryEntry>)entriesField.GetValue(inventory);
            entries.Add(new InventoryEntry { resource = resource, capacity = capacity, onHand = onHand });
        }
    }
}
