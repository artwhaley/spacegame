using System.Collections.Generic;
using System.Reflection;
using Colony.Interactions;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class FoodServiceAccessTests
    {
        private readonly List<GameObject> sceneObjects = new List<GameObject>();
        private readonly List<ScriptableObject> assets = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            for (int index = sceneObjects.Count - 1; index >= 0; index--)
            {
                if (sceneObjects[index] != null)
                    Object.DestroyImmediate(sceneObjects[index]);
            }

            for (int index = assets.Count - 1; index >= 0; index--)
            {
                if (assets[index] != null)
                    Object.DestroyImmediate(assets[index]);
            }

            sceneObjects.Clear();
            assets.Clear();
        }

        [Test]
        public void UnstaffedServiceIsPubliclyAvailable()
        {
            CafeteriaFixture cafeteria = CreateCafeteria(
                requiresStaff: false,
                FoodSelfServicePolicy.None);

            Assert.That(cafeteria.Service.IsConfigured, Is.True);
            Assert.That(
                cafeteria.Service.EvaluateAccess(null, 10f),
                Is.EqualTo(FoodServiceAccessMode.Public));
        }

        [Test]
        public void AssignedWorkerWhoIsNotWorkingDoesNotOpenPublicService()
        {
            WorkforceManager workforce = CreateWorkforceManager();
            CafeteriaFixture cafeteria = CreateCafeteria(
                requiresStaff: true,
                FoodSelfServicePolicy.None);
            ColonistIdentity worker = CreateColonist("Alice");
            AssignWorker(workforce, worker, cafeteria, cafeteria.Role, 8f, 14f);

            Assert.That(cafeteria.Service.HasActivePublicStaff(10f), Is.False);
            Assert.That(
                cafeteria.Service.EvaluateAccess(null, 10f),
                Is.EqualTo(FoodServiceAccessMode.Unavailable));
        }

        [Test]
        public void WorkerWalkingToWorkDoesNotOpenPublicService()
        {
            WorkforceManager workforce = CreateWorkforceManager();
            CafeteriaFixture cafeteria = CreateCafeteria(
                requiresStaff: true,
                FoodSelfServicePolicy.None);
            ColonistIdentity worker = CreateColonist("Alice");
            AssignWorker(workforce, worker, cafeteria, cafeteria.Role, 8f, 14f);
            SetWorkerRunnerState(cafeteria.Facility, worker, "ServeFood", activityActive: false);

            Assert.That(cafeteria.Service.HasActivePublicStaff(10f), Is.False);
        }

        [Test]
        public void PhysicallyWorkingWorkerOpensPublicService()
        {
            WorkforceManager workforce = CreateWorkforceManager();
            CafeteriaFixture cafeteria = CreateCafeteria(
                requiresStaff: true,
                FoodSelfServicePolicy.None);
            ColonistIdentity worker = CreateColonist("Alice");
            AssignWorker(workforce, worker, cafeteria, cafeteria.Role, 8f, 14f);
            SetWorkerRunnerState(cafeteria.Facility, worker, "ServeFood", activityActive: true);

            Assert.That(cafeteria.Service.HasActivePublicStaff(10f), Is.True);
            Assert.That(
                cafeteria.Service.EvaluateAccess(null, 10f),
                Is.EqualTo(FoodServiceAccessMode.PublicStaffed));
        }

        [Test]
        public void WorkerActiveAtWrongWorkplaceDoesNotOpenPublicService()
        {
            WorkforceManager workforce = CreateWorkforceManager();
            CafeteriaFixture cafeteria = CreateCafeteria(
                requiresStaff: true,
                FoodSelfServicePolicy.None,
                sharedRole: null);
            CafeteriaFixture otherCafeteria = CreateCafeteria(
                requiresStaff: true,
                FoodSelfServicePolicy.None,
                sharedRole: cafeteria.Role);
            ColonistIdentity worker = CreateColonist("Alice");
            AssignWorker(workforce, worker, cafeteria, cafeteria.Role, 8f, 14f);
            SetWorkerRunnerState(
                otherCafeteria.Facility,
                worker,
                "ServeFood",
                activityActive: true);

            Assert.That(cafeteria.Service.HasActivePublicStaff(10f), Is.False);
            Assert.That(otherCafeteria.Service.HasActivePublicStaff(10f), Is.False);
        }

        [Test]
        public void WorkerActiveAtWrongActivityDoesNotOpenPublicService()
        {
            WorkforceManager workforce = CreateWorkforceManager();
            CafeteriaFixture cafeteria = CreateCafeteria(
                requiresStaff: true,
                FoodSelfServicePolicy.None);
            ColonistIdentity worker = CreateColonist("Alice");
            AssignWorker(workforce, worker, cafeteria, cafeteria.Role, 8f, 14f);
            SetWorkerRunnerState(cafeteria.Facility, worker, "Eat", activityActive: true);

            Assert.That(cafeteria.Service.HasActivePublicStaff(10f), Is.False);
        }

        [Test]
        public void MinimumActiveWorkersIsRespected()
        {
            WorkforceManager workforce = CreateWorkforceManager();
            CafeteriaFixture cafeteria = CreateCafeteria(
                requiresStaff: true,
                FoodSelfServicePolicy.None,
                minimumActiveWorkers: 2);
            ColonistIdentity first = CreateColonist("Alice");
            ColonistIdentity second = CreateColonist("Carol");
            AssignWorker(workforce, first, cafeteria, cafeteria.Role, 8f, 14f);
            AssignWorker(workforce, second, cafeteria, cafeteria.Role, 8f, 14f);

            SetWorkerRunnerState(cafeteria.Facility, first, "ServeFood", activityActive: true);
            Assert.That(cafeteria.Service.HasActivePublicStaff(10f), Is.False);

            SetWorkerRunnerState(cafeteria.Facility, second, "ServeFood", activityActive: true);
            Assert.That(cafeteria.Service.HasActivePublicStaff(10f), Is.True);
        }

        [Test]
        public void AssignedEmployeeOffShiftMaySelfServe()
        {
            WorkforceManager workforce = CreateWorkforceManager();
            CafeteriaFixture cafeteria = CreateCafeteria(
                requiresStaff: true,
                FoodSelfServicePolicy.AssignedWorkers);
            ColonistIdentity employee = CreateColonist("Alice");
            AssignWorker(workforce, employee, cafeteria, cafeteria.Role, 8f, 14f);

            // 20:00: her shift is over and nobody is serving, but she is still assigned here.
            Assert.That(cafeteria.Service.HasActivePublicStaff(20f), Is.False);
            Assert.That(
                cafeteria.Service.EvaluateAccess(employee, 20f),
                Is.EqualTo(FoodServiceAccessMode.SelfService));
        }

        [Test]
        public void EmployeeAssignmentToAnotherWorkplaceCannotSelfServe()
        {
            WorkforceManager workforce = CreateWorkforceManager();
            CafeteriaFixture cafeteria = CreateCafeteria(
                requiresStaff: true,
                FoodSelfServicePolicy.AssignedWorkers);
            CafeteriaFixture otherCafeteria = CreateCafeteria(
                requiresStaff: true,
                FoodSelfServicePolicy.None,
                sharedRole: cafeteria.Role);
            ColonistIdentity employee = CreateColonist("Alice");
            AssignWorker(workforce, employee, otherCafeteria, cafeteria.Role, 8f, 14f);

            Assert.That(
                cafeteria.Service.EvaluateAccess(employee, 20f),
                Is.EqualTo(FoodServiceAccessMode.Unavailable));
        }

        [Test]
        public void EmployeeWithAnotherRoleCannotSelfServe()
        {
            WorkforceManager workforce = CreateWorkforceManager();
            CafeteriaFixture cafeteria = CreateCafeteria(
                requiresStaff: true,
                FoodSelfServicePolicy.AssignedWorkers);
            ColonistIdentity employee = CreateColonist("Alice");
            AssignWorker(workforce, employee, cafeteria, cafeteria.SecondaryRole, 8f, 14f);

            Assert.That(
                cafeteria.Service.EvaluateAccess(employee, 20f),
                Is.EqualTo(FoodServiceAccessMode.Unavailable));
        }

        [Test]
        public void UnassignedColonistCannotSelfServe()
        {
            CreateWorkforceManager();
            CafeteriaFixture cafeteria = CreateCafeteria(
                requiresStaff: true,
                FoodSelfServicePolicy.AssignedWorkers);
            ColonistIdentity outsider = CreateColonist("Bob");

            Assert.That(
                cafeteria.Service.EvaluateAccess(outsider, 10f),
                Is.EqualTo(FoodServiceAccessMode.Unavailable));
        }

        [Test]
        public void EveryonePolicyAllowsAnOutsiderToSelfServe()
        {
            CreateWorkforceManager();
            CafeteriaFixture cafeteria = CreateCafeteria(
                requiresStaff: true,
                FoodSelfServicePolicy.Everyone);
            ColonistIdentity outsider = CreateColonist("Bob");

            Assert.That(
                cafeteria.Service.EvaluateAccess(outsider, 10f),
                Is.EqualTo(FoodServiceAccessMode.SelfService));
        }

        [Test]
        public void NonePolicyDeniesAnOutsider()
        {
            CreateWorkforceManager();
            CafeteriaFixture cafeteria = CreateCafeteria(
                requiresStaff: true,
                FoodSelfServicePolicy.None);
            ColonistIdentity outsider = CreateColonist("Bob");

            Assert.That(
                cafeteria.Service.EvaluateAccess(outsider, 10f),
                Is.EqualTo(FoodServiceAccessMode.Unavailable));
        }

        [Test]
        public void StructuralConfigurationSurvivesStaffingChanges()
        {
            WorkforceManager workforce = CreateWorkforceManager();
            CafeteriaFixture cafeteria = CreateCafeteria(
                requiresStaff: true,
                FoodSelfServicePolicy.None);
            ColonistIdentity worker = CreateColonist("Alice");
            AssignWorker(workforce, worker, cafeteria, cafeteria.Role, 8f, 14f);
            SetWorkerRunnerState(cafeteria.Facility, worker, "ServeFood", activityActive: true);
            Assert.That(cafeteria.Service.IsConfigured, Is.True);
            Assert.That(cafeteria.Service.HasActivePublicStaff(10f), Is.True);

            SetWorkerRunnerState(cafeteria.Facility, worker, "ServeFood", activityActive: false);
            workforce.Unassign(worker);

            // Runtime availability changed; structural configuration must not have.
            Assert.That(cafeteria.Service.HasActivePublicStaff(10f), Is.False);
            Assert.That(cafeteria.Service.IsConfigured, Is.True);
        }

        [Test]
        public void TwoSeekersReceiveDifferentAccessFromTheSameCafeteria()
        {
            WorkforceManager workforce = CreateWorkforceManager();
            CafeteriaFixture cafeteria = CreateCafeteria(
                requiresStaff: true,
                FoodSelfServicePolicy.AssignedWorkers);
            ColonistIdentity employee = CreateColonist("Alice");
            ColonistIdentity outsider = CreateColonist("Bob");
            AssignWorker(workforce, employee, cafeteria, cafeteria.Role, 8f, 14f);
            FoodManager manager = CreateFoodManager();

            Assert.That(
                manager.TryFindFoodService(
                    new FoodQuery(employee, Vector3.zero, 20f),
                    out FoodServiceOpportunity employeeOpportunity),
                Is.True);
            Assert.That(
                employeeOpportunity.AccessMode,
                Is.EqualTo(FoodServiceAccessMode.SelfService));

            Assert.That(
                manager.TryFindFoodService(
                    new FoodQuery(outsider, Vector3.zero, 20f),
                    out FoodServiceOpportunity outsiderOpportunity),
                Is.False);
            Assert.That(outsiderOpportunity, Is.Null);
        }

        [Test]
        public void PublicWorkerBecomingActiveMakesTheCafeteriaDiscoverable()
        {
            WorkforceManager workforce = CreateWorkforceManager();
            CafeteriaFixture cafeteria = CreateCafeteria(
                requiresStaff: true,
                FoodSelfServicePolicy.None);
            ColonistIdentity worker = CreateColonist("Alice");
            ColonistIdentity outsider = CreateColonist("Bob");
            AssignWorker(workforce, worker, cafeteria, cafeteria.Role, 8f, 14f);
            FoodManager manager = CreateFoodManager();
            FoodQuery query = new FoodQuery(outsider, Vector3.zero, 10f);

            Assert.That(
                manager.TryFindFoodService(query, out FoodServiceComponent closedService),
                Is.False);
            Assert.That(closedService, Is.Null);

            SetWorkerRunnerState(cafeteria.Facility, worker, "ServeFood", activityActive: true);

            Assert.That(
                manager.TryFindFoodService(query, out FoodServiceOpportunity opportunity),
                Is.True);
            Assert.That(
                opportunity.AccessMode,
                Is.EqualTo(FoodServiceAccessMode.PublicStaffed));
            Assert.That(opportunity.Target.ActivityId, Is.EqualTo("Eat"));
        }

        [Test]
        public void RequesterAwareLoggingIsNotGloballySuppressed()
        {
            GameObject logObject = new GameObject("Simulation Log Manager");
            sceneObjects.Add(logObject);
            SimulationLogManager log = logObject.AddComponent<SimulationLogManager>();
            Assert.That(SimulationLogManager.Instance, Is.SameAs(log));

            CreateCafeteria(requiresStaff: false, FoodSelfServicePolicy.None);
            ColonistIdentity alice = CreateColonist("Alice");
            ColonistIdentity bob = CreateColonist("Bob");
            FoodManager manager = CreateFoodManager();

            Assert.That(
                manager.TryFindFoodTarget(new FoodQuery(alice, Vector3.zero, 10f), out _),
                Is.True);
            Assert.That(
                manager.TryFindFoodTarget(new FoodQuery(bob, Vector3.zero, 10f), out _),
                Is.True);
            // A repeated request from the same seeker is still deduplicated for that seeker.
            Assert.That(
                manager.TryFindFoodTarget(new FoodQuery(alice, Vector3.zero, 10f), out _),
                Is.True);

            Assert.That(CountSelectionEntries(), Is.EqualTo(2));
            Assert.That(HasSelectionEntryFor("Alice"), Is.True);
            Assert.That(HasSelectionEntryFor("Bob"), Is.True);
        }

        private int CountSelectionEntries()
        {
            return CountSelectionEntries(null);
        }

        private bool HasSelectionEntryFor(string displayName)
        {
            return CountSelectionEntries(displayName) > 0;
        }

        private static int CountSelectionEntries(string displayName)
        {
            if (SimulationLogManager.Instance == null)
                return 0;

            int count = 0;
            IReadOnlyList<SimulationLogEntry> entries = SimulationLogManager.Instance.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                SimulationLogEntry entry = entries[index];
                if (entry == null ||
                    !string.Equals(entry.EventKey, "food.target_selected", System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (displayName != null &&
                    (entry.PrimarySubject == null ||
                     !string.Equals(
                         entry.PrimarySubject.DisplayName,
                         displayName,
                         System.StringComparison.Ordinal)))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private FoodManager CreateFoodManager()
        {
            GameObject managerObject = new GameObject("Food Manager");
            sceneObjects.Add(managerObject);
            return managerObject.AddComponent<FoodManager>();
        }

        private WorkforceManager CreateWorkforceManager()
        {
            GameObject managerObject = new GameObject("Workforce Manager");
            sceneObjects.Add(managerObject);
            return managerObject.AddComponent<WorkforceManager>();
        }

        private ColonistIdentity CreateColonist(string displayName)
        {
            GameObject colonistObject = new GameObject(displayName);
            sceneObjects.Add(colonistObject);
            ColonistIdentity identity = colonistObject.AddComponent<ColonistIdentity>();
            colonistObject.AddComponent<ColonistActivityRunner>();
            return identity;
        }

        private JobRoleDefinition CreateRole(string stableId)
        {
            JobRoleDefinition role = ScriptableObject.CreateInstance<JobRoleDefinition>();
            assets.Add(role);
            SetPrivateField(role, "stableId", stableId);
            SetPrivateField(role, "displayName", stableId);
            return role;
        }

        private CafeteriaFixture CreateCafeteria(
            bool requiresStaff,
            FoodSelfServicePolicy selfServicePolicy,
            int minimumActiveWorkers = 1,
            JobRoleDefinition sharedRole = null)
        {
            CafeteriaFixture fixture = new CafeteriaFixture();
            fixture.GameObject = new GameObject("Cafeteria");
            sceneObjects.Add(fixture.GameObject);
            fixture.Facility = fixture.GameObject.AddComponent<InteractableFacility>();

            fixture.EatBinding = CreateBinding(
                fixture.GameObject.transform,
                "Eat",
                "Eat01");
            fixture.ServeBinding = CreateBinding(
                fixture.GameObject.transform,
                "ServeFood",
                "CafeteriaWorker01");
            SetPrivateField(
                fixture.Facility,
                "activities",
                new[] { fixture.EatBinding, fixture.ServeBinding });

            fixture.Role = sharedRole ?? CreateRole("cafeteria_worker");
            fixture.SecondaryRole = CreateRole("cafeteria_janitor");

            WorkplaceRoleBinding workerBinding = new WorkplaceRoleBinding();
            SetPrivateField(workerBinding, "role", fixture.Role);
            SetPrivateField(workerBinding, "activityId", "ServeFood");
            SetPrivateField(workerBinding, "maximumConcurrentScheduledWorkers", 4);

            WorkplaceRoleBinding secondaryBinding = new WorkplaceRoleBinding();
            SetPrivateField(secondaryBinding, "role", fixture.SecondaryRole);
            SetPrivateField(secondaryBinding, "activityId", "Eat");
            SetPrivateField(secondaryBinding, "maximumConcurrentScheduledWorkers", 4);

            fixture.Workplace = fixture.GameObject.AddComponent<WorkplaceComponent>();
            SetPrivateField(
                fixture.Workplace,
                "roles",
                new[] { workerBinding, secondaryBinding });

            fixture.Service = fixture.GameObject.AddComponent<FoodServiceComponent>();
            SetPrivateField(fixture.Service, "facility", fixture.Facility);
            SetPrivateField(fixture.Service, "eatActivityId", "Eat");
            SetPrivateField(fixture.Service, "hungerRecoveryPerGameHour", 60f);
            SetPrivateField(fixture.Service, "requiresStaff", requiresStaff);
            SetPrivateField(
                fixture.Service,
                "requiredWorkplace",
                requiresStaff ? fixture.Workplace : null);
            SetPrivateField(
                fixture.Service,
                "requiredRole",
                requiresStaff ? fixture.Role : null);
            SetPrivateField(fixture.Service, "minimumActiveWorkers", minimumActiveWorkers);
            SetPrivateField(fixture.Service, "selfServicePolicy", selfServicePolicy);
            return fixture;
        }

        private static FacilityActivityBinding CreateBinding(
            Transform parent,
            string activityId,
            string reservationGroup)
        {
            Transform approach = new GameObject(activityId + " Approach").transform;
            approach.SetParent(parent, false);

            FacilityActivityBinding binding = new FacilityActivityBinding();
            SetPrivateField(binding, "activityId", activityId);
            SetPrivateField(binding, "reservationGroup", reservationGroup);
            SetPrivateField(binding, "externallyRequestable", true);
            SetPrivateField(binding, "approachAnchor", approach);
            return binding;
        }

        private void AssignWorker(
            WorkforceManager workforce,
            ColonistIdentity colonist,
            CafeteriaFixture cafeteria,
            JobRoleDefinition role,
            float startHour,
            float endHour)
        {
            Assert.That(
                workforce.Assign(
                    colonist,
                    cafeteria.Workplace,
                    role,
                    new DailyShiftWindow(startHour, endHour)),
                Is.EqualTo(WorkAssignmentResult.Applied));
        }

        private static void SetWorkerRunnerState(
            InteractableFacility facility,
            ColonistIdentity colonist,
            string activityId,
            bool activityActive)
        {
            ColonistActivityRunner runner = colonist.GetComponent<ColonistActivityRunner>();
            Assert.That(
                facility.TryGetBinding(activityId, out FacilityActivityBinding binding),
                Is.True);
            Assert.That(
                facility.TryAcquire(
                    binding.ReservationGroup,
                    runner,
                    out FacilityReservationToken token),
                Is.True);

            SetPrivateField(runner, "currentFacility", facility);
            SetPrivateField(runner, "currentBinding", binding);
            SetPrivateField(runner, "reservation", token);
            SetPrivateField(runner, "activityActive", activityActive);
            SetPrivateField(runner, "exitInProgress", false);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field {fieldName}.");
            field.SetValue(target, value);
        }

        private sealed class CafeteriaFixture
        {
            public GameObject GameObject;
            public InteractableFacility Facility;
            public WorkplaceComponent Workplace;
            public FoodServiceComponent Service;
            public JobRoleDefinition Role;
            public JobRoleDefinition SecondaryRole;
            public FacilityActivityBinding EatBinding;
            public FacilityActivityBinding ServeBinding;
        }
    }
}
