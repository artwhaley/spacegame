using System.Reflection;
using Colony.Interactions;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class WorkplaceComponentTests
    {
        private GameObject workplaceObject;
        private JobRoleDefinition farmer;
        private JobRoleDefinition doctor;

        [TearDown]
        public void TearDown()
        {
            if (workplaceObject != null)
                Object.DestroyImmediate(workplaceObject);
            if (farmer != null)
                Object.DestroyImmediate(farmer);
            if (doctor != null)
                Object.DestroyImmediate(doctor);
        }

        [Test]
        public void ConfiguredWorkplaceOffersConfiguredRole()
        {
            WorkplaceComponent workplace = CreateWorkplace();
            WorkplaceRoleBinding binding = CreateBinding(farmer, "Farm", 1);
            SetPrivateField(workplace, "roles", new[] { binding });

            Assert.That(workplace.OffersRole(farmer), Is.True);
        }

        [Test]
        public void UnofferedRoleReturnsFalse()
        {
            WorkplaceComponent workplace = CreateWorkplace();
            SetPrivateField(workplace, "roles", new[] {
                CreateBinding(farmer, "Farm", 1)
            });

            Assert.That(workplace.OffersRole(doctor), Is.False);
        }

        [Test]
        public void TryGetRoleBindingReturnsExactConfiguredBinding()
        {
            WorkplaceComponent workplace = CreateWorkplace();
            WorkplaceRoleBinding expected = CreateBinding(farmer, "Farm", 1);
            SetPrivateField(workplace, "roles", new[] { expected });

            Assert.That(
                workplace.TryGetRoleBinding(
                    farmer,
                    out WorkplaceRoleBinding actual),
                Is.True);
            Assert.That(actual, Is.SameAs(expected));
        }

        [Test]
        public void DuplicateOrNullRoleCannotMasqueradeAsValidLookup()
        {
            WorkplaceComponent workplace = CreateWorkplace();
            WorkplaceRoleBinding first = CreateBinding(farmer, "Farm", 1);
            WorkplaceRoleBinding duplicate = CreateBinding(farmer, "Farm", 1);
            SetPrivateField(workplace, "roles", new[] { first, duplicate });

            Assert.That(workplace.OffersRole(farmer), Is.False);
            Assert.That(workplace.OffersRole(null), Is.False);

            SetPrivateField(workplace, "roles", new WorkplaceRoleBinding[] { null });
            Assert.That(workplace.OffersRole(farmer), Is.False);
        }

        [Test]
        public void MissingFacilityActivityIsNotAUsableRoleBinding()
        {
            WorkplaceComponent workplace = CreateWorkplace();
            SetPrivateField(workplace, "roles", new[] {
                CreateBinding(farmer, "Missing", 1)
            });

            Assert.That(workplace.OffersRole(farmer), Is.False);
        }

        private WorkplaceComponent CreateWorkplace()
        {
            workplaceObject = new GameObject("Workplace Test");
            InteractableFacility facility =
                workplaceObject.AddComponent<InteractableFacility>();
            FacilityActivityBinding activity = new FacilityActivityBinding();
            SetPrivateField(activity, "activityId", "Farm");
            SetPrivateField(facility, "activities", new[] { activity });

            farmer = ScriptableObject.CreateInstance<JobRoleDefinition>();
            doctor = ScriptableObject.CreateInstance<JobRoleDefinition>();
            SetPrivateField(farmer, "stableId", "farmer");
            SetPrivateField(doctor, "stableId", "doctor");
            return workplaceObject.AddComponent<WorkplaceComponent>();
        }

        private static WorkplaceRoleBinding CreateBinding(
            JobRoleDefinition role,
            string activityId,
            int capacity)
        {
            WorkplaceRoleBinding binding = new WorkplaceRoleBinding();
            SetPrivateField(binding, "role", role);
            SetPrivateField(binding, "activityId", activityId);
            SetPrivateField(binding, "maximumConcurrentScheduledWorkers", capacity);
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
