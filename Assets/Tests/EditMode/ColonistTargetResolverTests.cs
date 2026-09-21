using System.Reflection;
using Colony.Interactions;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class ColonistTargetResolverTests
    {
        private GameObject colonistObject;
        private GameObject facilityObject;
        private GameObject workplaceObject;
        private GameObject workforceObject;
        private GameObject foodManagerObject;
        private GameObject foodFacilityObject;
        private JobRoleDefinition role;

        [TearDown]
        public void TearDown()
        {
            if (colonistObject != null)
                Object.DestroyImmediate(colonistObject);

            if (facilityObject != null)
                Object.DestroyImmediate(facilityObject);
            if (workplaceObject != null)
                Object.DestroyImmediate(workplaceObject);
            if (workforceObject != null)
                Object.DestroyImmediate(workforceObject);
            if (foodManagerObject != null)
                Object.DestroyImmediate(foodManagerObject);
            if (foodFacilityObject != null)
                Object.DestroyImmediate(foodFacilityObject);
            if (role != null)
                Object.DestroyImmediate(role);
        }

        [Test]
        public void SleepResolvesExactPersonalAssignment()
        {
            ColonistAssignments assignments = CreateAssignments();
            ColonistTargetResolver resolver =
                colonistObject.AddComponent<ColonistTargetResolver>();
            InteractableFacility facility = CreateFacility();
            ActivityTarget expected = CreateTarget(facility, "Sleep");
            SetPrivateField(assignments, "sleepTarget", expected);

            bool result = resolver.TryResolveTarget(
                ActivityPurpose.Sleep,
                out ActivityTarget target);

            Assert.That(result, Is.True);
            Assert.That(target, Is.SameAs(expected));
            Assert.That(target.Facility, Is.SameAs(facility));
            Assert.That(target.ActivityId, Is.EqualTo("Sleep"));
        }

        [Test]
        public void MissingSleepAssignmentReturnsFalseAndNull()
        {
            CreateAssignments();
            ColonistTargetResolver resolver =
                colonistObject.AddComponent<ColonistTargetResolver>();

            bool result = resolver.TryResolveTarget(
                ActivityPurpose.Sleep,
                out ActivityTarget target);

            Assert.That(result, Is.False);
            Assert.That(target, Is.Null);
        }

        [Test]
        public void ResolverWithoutAssignmentsFailsCleanly()
        {
            colonistObject = new GameObject("Resolver Without Assignments Test");
            ColonistTargetResolver resolver =
                colonistObject.AddComponent<ColonistTargetResolver>();

            bool result = resolver.TryResolveTarget(
                ActivityPurpose.Sleep,
                out ActivityTarget target);

            Assert.That(result, Is.False);
            Assert.That(target, Is.Null);
        }

        [Test]
        public void WorkResolvesExactAssignedWorkplaceActivity()
        {
            colonistObject = new GameObject("Colonist Work Resolver Test");
            ColonistIdentity identity =
                colonistObject.AddComponent<ColonistIdentity>();
            ColonistTargetResolver resolver =
                colonistObject.AddComponent<ColonistTargetResolver>();
            WorkforceManager workforceManager = CreateWorkforceManager();
            role = ScriptableObject.CreateInstance<JobRoleDefinition>();
            SetPrivateField(role, "stableId", "farmer");
            WorkplaceComponent workplace = CreateWorkplace(role);

            Assert.That(
                workforceManager.Assign(
                    identity,
                    workplace,
                    role,
                    new DailyShiftWindow(8f, 14f)),
                Is.EqualTo(WorkAssignmentResult.Applied));

            bool result = resolver.TryResolveTarget(
                ActivityPurpose.Work,
                out ActivityTarget target);

            Assert.That(result, Is.True);
            Assert.That(target, Is.Not.Null);
            Assert.That(target.Facility, Is.SameAs(workplace.Facility));
            Assert.That(target.ActivityId, Is.EqualTo("Farm"));
        }

        [Test]
        public void WorkWithoutWorkforceManagerFailsCleanly()
        {
            colonistObject = new GameObject("Work Without Manager Test");
            colonistObject.AddComponent<ColonistIdentity>();
            ColonistTargetResolver resolver =
                colonistObject.AddComponent<ColonistTargetResolver>();

            bool result = resolver.TryResolveTarget(
                ActivityPurpose.Work,
                out ActivityTarget target);

            Assert.That(result, Is.False);
            Assert.That(target, Is.Null);
        }

        [Test]
        public void WorkWithoutRegularAssignmentFailsCleanly()
        {
            colonistObject = new GameObject("Work Without Assignment Test");
            colonistObject.AddComponent<ColonistIdentity>();
            ColonistTargetResolver resolver =
                colonistObject.AddComponent<ColonistTargetResolver>();
            CreateWorkforceManager();

            bool result = resolver.TryResolveTarget(
                ActivityPurpose.Work,
                out ActivityTarget target);

            Assert.That(result, Is.False);
            Assert.That(target, Is.Null);
        }

        [Test]
        public void EatResolvesRegisteredPublicFoodService()
        {
            colonistObject = new GameObject("Colonist Eat Resolver Test");
            ColonistTargetResolver resolver =
                colonistObject.AddComponent<ColonistTargetResolver>();
            CreateFoodManagerAndService();

            bool result = resolver.TryResolveTarget(
                ActivityPurpose.Eat,
                out ActivityTarget target);

            Assert.That(result, Is.True);
            Assert.That(target, Is.Not.Null);
            Assert.That(target.ActivityId, Is.EqualTo("Eat"));
            Assert.That(target.Facility, Is.SameAs(foodFacilityObject.GetComponent<InteractableFacility>()));
        }

        [Test]
        public void EatWithoutFoodManagerFailsCleanly()
        {
            colonistObject = new GameObject("Eat Without Manager Test");
            ColonistTargetResolver resolver =
                colonistObject.AddComponent<ColonistTargetResolver>();

            bool result = resolver.TryResolveTarget(
                ActivityPurpose.Eat,
                out ActivityTarget target);

            Assert.That(result, Is.False);
            Assert.That(target, Is.Null);
        }

        [Test]
        public void EatWithReservedFoodServiceReturnsFalse()
        {
            colonistObject = new GameObject("Reserved Eat Resolver Test");
            ColonistTargetResolver resolver =
                colonistObject.AddComponent<ColonistTargetResolver>();
            CreateFoodManagerAndService();
            InteractableFacility facility =
                foodFacilityObject.GetComponent<InteractableFacility>();
            Assert.That(facility.TryAcquire("Eat01", foodManagerObject, out _), Is.True);

            bool result = resolver.TryResolveTarget(
                ActivityPurpose.Eat,
                out ActivityTarget target);

            Assert.That(result, Is.False);
            Assert.That(target, Is.Null);
        }

        private ColonistAssignments CreateAssignments()
        {
            colonistObject = new GameObject("Colonist Target Resolver Test");
            return colonistObject.AddComponent<ColonistAssignments>();
        }

        private InteractableFacility CreateFacility()
        {
            facilityObject = new GameObject("Assigned Facility Test");
            return facilityObject.AddComponent<InteractableFacility>();
        }

        private WorkforceManager CreateWorkforceManager()
        {
            workforceObject = new GameObject("Workforce Manager Test");
            WorkforceManager workforceManager =
                workforceObject.AddComponent<WorkforceManager>();
            InvokePrivate(workforceManager, "Awake");
            return workforceManager;
        }

        private WorkplaceComponent CreateWorkplace(JobRoleDefinition offeredRole)
        {
            workplaceObject = new GameObject("Assigned Workplace Test");
            InteractableFacility facility =
                workplaceObject.AddComponent<InteractableFacility>();
            FacilityActivityBinding activity = new FacilityActivityBinding();
            SetPrivateField(activity, "activityId", "Farm");
            SetPrivateField(activity, "reservationGroup", "Farm01");
            SetPrivateField(facility, "activities", new[] { activity });

            WorkplaceRoleBinding roleBinding = new WorkplaceRoleBinding();
            SetPrivateField(roleBinding, "role", offeredRole);
            SetPrivateField(roleBinding, "activityId", "Farm");
            SetPrivateField(roleBinding, "maximumConcurrentScheduledWorkers", 1);
            WorkplaceComponent workplace =
                workplaceObject.AddComponent<WorkplaceComponent>();
            SetPrivateField(workplace, "roles", new[] { roleBinding });
            return workplace;
        }

        private void CreateFoodManagerAndService()
        {
            foodManagerObject = new GameObject("Food Manager Test");
            foodManagerObject.AddComponent<FoodManager>();

            foodFacilityObject = new GameObject("Food Facility Test");
            InteractableFacility facility =
                foodFacilityObject.AddComponent<InteractableFacility>();
            Transform approach = new GameObject("Food Approach").transform;
            approach.SetParent(foodFacilityObject.transform, false);
            FacilityActivityBinding binding = new FacilityActivityBinding();
            SetPrivateField(binding, "activityId", "Eat");
            SetPrivateField(binding, "reservationGroup", "Eat01");
            SetPrivateField(binding, "externallyRequestable", true);
            SetPrivateField(binding, "approachAnchor", approach);
            SetPrivateField(facility, "activities", new[] { binding });
            FoodServiceComponent service =
                foodFacilityObject.AddComponent<FoodServiceComponent>();
            SetPrivateField(service, "facility", facility);
            SetPrivateField(service, "eatActivityId", "Eat");
            SetPrivateField(service, "hungerRecoveryPerGameHour", 60f);
        }

        private static ActivityTarget CreateTarget(
            InteractableFacility facility,
            string activityId)
        {
            ActivityTarget target = new ActivityTarget();
            SetPrivateField(target, "facility", facility);
            SetPrivateField(target, "activityId", activityId);
            return target;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field {fieldName}.");
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing private method {methodName}.");
            method.Invoke(target, null);
        }
    }
}
