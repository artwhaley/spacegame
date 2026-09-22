using System.Collections.Generic;
using System.Reflection;
using Colony.Interactions;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    /// <summary>
    /// Stack 2 contention coverage for one scarce Eat seat and one staffed public counter.
    /// Reservations are acquired through the real facility API and private runner fields are
    /// configured explicitly, so nothing here depends on NavMesh availability or on which scene
    /// happens to be open in the Editor.
    /// </summary>
    public class FoodContentionTests
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
        public void OneEatSeatCannotBeOwnedByTwoDinersAtOnce()
        {
            Cafeteria cafeteria = CreateCafeteria(
                requiresStaff: true,
                FoodSelfServicePolicy.AssignedWorkers);
            TestColonist bob = CreateColonist("Bob");
            TestColonist dana = CreateColonist("Dana");

            Assert.That(
                cafeteria.Facility.TryAcquire("Eat01", bob.Runner, out FacilityReservationToken first),
                Is.True);
            Assert.That(first, Is.Not.Null);
            Assert.That(cafeteria.Facility.IsReserved("Eat01"), Is.True);

            Assert.That(
                cafeteria.Facility.TryAcquire("Eat01", dana.Runner, out FacilityReservationToken second),
                Is.False);
            Assert.That(second, Is.Null);
            Assert.That(
                cafeteria.Facility.IsReserved("Eat01"),
                Is.True,
                "The losing colonist must not clear the winner's reservation.");

            Assert.That(cafeteria.Facility.Release(first), Is.True);
            Assert.That(cafeteria.Facility.IsReserved("Eat01"), Is.False);
            Assert.That(
                cafeteria.Facility.TryAcquire("Eat01", dana.Runner, out FacilityReservationToken retry),
                Is.True);
            Assert.That(retry, Is.Not.Null);
        }

        [Test]
        public void WaitingDinerFindsNoTargetAndReconsidersLaterWithoutLeaks()
        {
            WorkforceManager workforce = CreateWorkforceManager();
            SimulationLogManager log = CreateSimulationLogManager();
            Cafeteria cafeteria = CreateCafeteria(
                requiresStaff: true,
                FoodSelfServicePolicy.AssignedWorkers);
            FoodManager manager = CreateFoodManager(cafeteria);
            TestColonist alice = CreateColonist("Alice");
            TestColonist bob = CreateColonist("Bob");
            TestColonist dana = CreateColonist("Dana");
            AssignWorker(workforce, alice, cafeteria, 6f, 18f);
            SetWorkerPhysicallyServing(alice, cafeteria);

            Assert.That(
                manager.TryFindFoodTarget(new FoodQuery(bob.Identity, Vector3.zero, 10f), out ActivityTarget bobTarget),
                Is.True);
            Assert.That(bobTarget.ActivityId, Is.EqualTo("Eat"));

            // Bob takes the only seat.
            Assert.That(
                cafeteria.Facility.TryAcquire("Eat01", bob.Runner, out FacilityReservationToken bobSeat),
                Is.True);

            Assert.That(
                manager.TryFindFoodTarget(new FoodQuery(dana.Identity, Vector3.zero, 10f), out ActivityTarget danaTarget),
                Is.False);
            Assert.That(danaTarget, Is.Null);

            // A failed discovery must not leak a reservation on Dana's behalf.
            Assert.That(dana.Runner.HasActiveRequest, Is.False);
            Assert.That(dana.Runner.Phase, Is.EqualTo(ActivityPhase.Idle));
            Assert.That(cafeteria.Facility.IsReserved("Eat01"), Is.True);

            Assert.That(CountEntries(log, "food.target_selected", "Bob"), Is.EqualTo(1));
            Assert.That(CountEntries(log, "food.no_target", "Dana"), Is.EqualTo(1));

            Assert.That(cafeteria.Facility.Release(bobSeat), Is.True);
            Assert.That(
                manager.TryFindFoodService(
                    new FoodQuery(dana.Identity, Vector3.zero, 10f),
                    out FoodServiceOpportunity opportunity),
                Is.True);
            Assert.That(opportunity.AccessMode, Is.EqualTo(FoodServiceAccessMode.PublicStaffed));
        }

        [Test]
        public void TwoPublicDinersDiscoverTheSameStaffedCounterSeparately()
        {
            WorkforceManager workforce = CreateWorkforceManager();
            SimulationLogManager log = CreateSimulationLogManager();
            Cafeteria cafeteria = CreateCafeteria(
                requiresStaff: true,
                FoodSelfServicePolicy.AssignedWorkers);
            FoodManager manager = CreateFoodManager(cafeteria);
            TestColonist alice = CreateColonist("Alice");
            TestColonist bob = CreateColonist("Bob");
            TestColonist dana = CreateColonist("Dana");
            AssignWorker(workforce, alice, cafeteria, 6f, 18f);
            SetWorkerPhysicallyServing(alice, cafeteria);

            Assert.That(
                manager.TryFindFoodService(
                    new FoodQuery(bob.Identity, Vector3.zero, 10f),
                    out FoodServiceOpportunity bobOpportunity),
                Is.True);
            Assert.That(
                manager.TryFindFoodService(
                    new FoodQuery(dana.Identity, Vector3.zero, 10f),
                    out FoodServiceOpportunity danaOpportunity),
                Is.True);

            Assert.That(bobOpportunity.AccessMode, Is.EqualTo(FoodServiceAccessMode.PublicStaffed));
            Assert.That(danaOpportunity.AccessMode, Is.EqualTo(FoodServiceAccessMode.PublicStaffed));
            Assert.That(bobOpportunity.Target.Facility, Is.SameAs(cafeteria.Facility));
            Assert.That(danaOpportunity.Target.Facility, Is.SameAs(cafeteria.Facility));

            Assert.That(CountEntries(log, "food.target_selected", "Bob"), Is.EqualTo(1));
            Assert.That(CountEntries(log, "food.target_selected", "Dana"), Is.EqualTo(1));
        }

        [Test]
        public void CriticallyHungryServerLeavesWorkClosesTheCounterAndFeedsHerself()
        {
            WorkforceManager workforce = CreateWorkforceManager();
            CreateSimulationManager(10f);
            SimulationLogManager log = CreateSimulationLogManager();
            Cafeteria cafeteria = CreateCafeteria(
                requiresStaff: true,
                FoodSelfServicePolicy.AssignedWorkers);
            FoodManager manager = CreateFoodManager(cafeteria);
            TestColonist alice = CreateColonist("Alice");
            TestColonist bob = CreateColonist("Bob");
            AssignWorker(workforce, alice, cafeteria, 6f, 18f);
            SetWorkerPhysicallyServing(alice, cafeteria);
            ConfigureWorkingLifecycle(alice, cafeteria, "ServeFood");

            Assert.That(cafeteria.Service.HasActivePublicStaff(10f), Is.True);
            Assert.That(
                manager.TryFindFoodService(
                    new FoodQuery(bob.Identity, Vector3.zero, 10f),
                    out FoodServiceOpportunity publicOpportunity),
                Is.True);
            Assert.That(
                publicOpportunity.AccessMode,
                Is.EqualTo(FoodServiceAccessMode.PublicStaffed));

            SetPrivateField(alice.Stats, "hunger", 95f);
            alice.Brain.SimulationTick(0.1f);

            Assert.That((bool)GetPrivateField(alice.Brain, "workStopRequested"), Is.True);
            Assert.That(CountEntries(log, "work.left_for_critical_need", "Alice"), Is.EqualTo(1));

            // Once she has physically left the station the counter closes to new diners.
            ClearRunnerState(alice);
            Assert.That(cafeteria.Service.HasActivePublicStaff(10f), Is.False);
            Assert.That(
                manager.TryFindFoodService(
                    new FoodQuery(bob.Identity, Vector3.zero, 10f),
                    out FoodServiceOpportunity closedOpportunity),
                Is.False);
            Assert.That(closedOpportunity, Is.Null);
            Assert.That(
                cafeteria.Service.EvaluateAccess(alice.Identity, 10f),
                Is.EqualTo(FoodServiceAccessMode.SelfService));
            Assert.That(
                manager.TryFindFoodService(
                    new FoodQuery(alice.Identity, Vector3.zero, 10f),
                    out FoodServiceOpportunity aliceOpportunity),
                Is.True);
            Assert.That(aliceOpportunity.AccessMode, Is.EqualTo(FoodServiceAccessMode.SelfService));
            Assert.That(aliceOpportunity.Target.ActivityId, Is.EqualTo("Eat"));
        }

        [Test]
        public void ServedDinerFinishesTheMealAfterTheServerLeaves()
        {
            WorkforceManager workforce = CreateWorkforceManager();
            CreateSimulationManager(10f);
            Cafeteria cafeteria = CreateCafeteria(
                requiresStaff: true,
                FoodSelfServicePolicy.AssignedWorkers);
            FoodManager manager = CreateFoodManager(cafeteria);
            TestColonist alice = CreateColonist("Alice");
            TestColonist bob = CreateColonist("Bob");
            TestColonist dana = CreateColonist("Dana");
            AssignWorker(workforce, alice, cafeteria, 6f, 18f);
            SetWorkerPhysicallyServing(alice, cafeteria);
            ConfigureEatingLifecycle(bob, cafeteria, hunger: 80f);

            Assert.That(bob.Runner.CurrentReservationGroup, Is.EqualTo("Eat01"));

            // Alice clocks out while Bob's meal is already underway.
            ClearRunnerState(alice);

            bob.Brain.SimulationTick(0.1f);

            Assert.That((bool)GetPrivateField(bob.Brain, "eatStopRequested"), Is.False);
            Assert.That(bob.Brain.State, Is.EqualTo(ColonistBrainState.Eating));
            Assert.That(bob.Runner.HasActiveRequest, Is.True);
            Assert.That(bob.Runner.CurrentReservationGroup, Is.EqualTo("Eat01"));
            Assert.That(
                bob.Stats.EffectiveHungerPerGameHour,
                Is.EqualTo(-60f).Within(0.0001f));

            // New public diners are refused, but the seated diner keeps eating.
            Assert.That(
                cafeteria.Service.EvaluateAccess(dana.Identity, 10f),
                Is.EqualTo(FoodServiceAccessMode.Unavailable));
            Assert.That(
                manager.TryFindFoodService(
                    new FoodQuery(dana.Identity, Vector3.zero, 10f),
                    out FoodServiceOpportunity refusedOpportunity),
                Is.False);
            Assert.That(refusedOpportunity, Is.Null);
        }

        [Test]
        public void DinerStillSeekingLosesAccessWhenTheServerLeaves()
        {
            WorkforceManager workforce = CreateWorkforceManager();
            CreateSimulationManager(10f);
            Cafeteria cafeteria = CreateCafeteria(
                requiresStaff: true,
                FoodSelfServicePolicy.AssignedWorkers);
            CreateFoodManager(cafeteria);
            TestColonist alice = CreateColonist("Alice");
            TestColonist dana = CreateColonist("Dana");
            AssignWorker(workforce, alice, cafeteria, 6f, 18f);
            SetWorkerPhysicallyServing(alice, cafeteria);

            SetPrivateField(dana.Stats, "hunger", 80f);
            ConfigurePendingEatRequest(dana, cafeteria);

            // She is on her way but has not been served yet.
            ClearRunnerState(alice);

            dana.Brain.SimulationTick(0.1f);

            Assert.That((bool)GetPrivateField(dana.Brain, "eatStopRequested"), Is.True);
            Assert.That(dana.Brain.LastDecisionReason, Is.EqualTo("food_access_lost"));
        }

        [Test]
        public void SelfServicePermissionEndsWhenTheEmployeeIsReassignedAway()
        {
            WorkforceManager workforce = CreateWorkforceManager();
            Cafeteria cafeteria = CreateCafeteria(
                requiresStaff: true,
                FoodSelfServicePolicy.AssignedWorkers);
            WorkplaceComponent farm = CreateWorkplace(
                CreateRole("farmer"),
                "Farm",
                "Farm01");
            TestColonist alice = CreateColonist("Alice");
            AssignWorker(workforce, alice, cafeteria, 6f, 18f);

            Assert.That(
                cafeteria.Service.EvaluateAccess(alice.Identity, 20f),
                Is.EqualTo(FoodServiceAccessMode.SelfService));

            Assert.That(
                workforce.Assign(
                    alice.Identity,
                    farm,
                    farm.Roles[0].Role,
                    new DailyShiftWindow(20f, 23f)),
                Is.EqualTo(WorkAssignmentResult.Applied));

            Assert.That(
                cafeteria.Service.EvaluateAccess(alice.Identity, 20f),
                Is.EqualTo(FoodServiceAccessMode.Unavailable));
        }

        private static int CountEntries(
            SimulationLogManager log,
            string eventKey,
            string displayName)
        {
            int count = 0;
            IReadOnlyList<SimulationLogEntry> entries = log.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                SimulationLogEntry entry = entries[index];
                if (entry == null ||
                    !string.Equals(entry.EventKey, eventKey, System.StringComparison.Ordinal) ||
                    entry.PrimarySubject == null ||
                    !string.Equals(
                        entry.PrimarySubject.DisplayName,
                        displayName,
                        System.StringComparison.Ordinal))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private void ConfigureWorkingLifecycle(
            TestColonist colonist,
            Cafeteria cafeteria,
            string activityId)
        {
            SetPrivateField(
                colonist.Brain,
                "workTargetInProgress",
                new ActivityTarget(cafeteria.Facility, activityId));
            SetPrivateField(colonist.Brain, "workStopRequested", false);
            SetPrivateField(colonist.Brain, "state", ColonistBrainState.Working);
        }

        private void ConfigureEatingLifecycle(
            TestColonist colonist,
            Cafeteria cafeteria,
            float hunger)
        {
            Assert.That(
                cafeteria.Facility.TryGetBinding("Eat", out FacilityActivityBinding binding),
                Is.True);
            Assert.That(
                cafeteria.Facility.TryAcquire(
                    binding.ReservationGroup,
                    colonist.Runner,
                    out FacilityReservationToken token),
                Is.True);

            SetPrivateField(colonist.Runner, "currentFacility", cafeteria.Facility);
            SetPrivateField(colonist.Runner, "currentBinding", binding);
            SetPrivateField(colonist.Runner, "reservation", token);
            SetPrivateField(colonist.Runner, "activityActive", true);
            SetPrivateField(colonist.Runner, "exitInProgress", false);
            SetPrivateProperty(colonist.Runner, "Phase", ActivityPhase.Busy);

            SetPrivateField(colonist.Stats, "hunger", hunger);
            SetPrivateField(
                colonist.Brain,
                "eatTargetInProgress",
                new ActivityTarget(cafeteria.Facility, "Eat"));
            SetPrivateField(colonist.Brain, "eatStopRequested", false);
            SetPrivateField(colonist.Brain, "state", ColonistBrainState.Eating);
        }

        private void ConfigurePendingEatRequest(
            TestColonist colonist,
            Cafeteria cafeteria)
        {
            Assert.That(
                cafeteria.Facility.TryGetBinding("Eat", out FacilityActivityBinding binding),
                Is.True);
            Assert.That(
                cafeteria.Facility.TryAcquire(
                    binding.ReservationGroup,
                    colonist.Runner,
                    out FacilityReservationToken token),
                Is.True);

            SetPrivateField(colonist.Runner, "currentFacility", cafeteria.Facility);
            SetPrivateField(colonist.Runner, "currentBinding", binding);
            SetPrivateField(colonist.Runner, "reservation", token);
            SetPrivateField(colonist.Runner, "activityActive", false);
            SetPrivateField(colonist.Runner, "exitInProgress", false);
            SetPrivateProperty(colonist.Runner, "Phase", ActivityPhase.Reserved);

            SetPrivateField(
                colonist.Brain,
                "eatTargetInProgress",
                new ActivityTarget(cafeteria.Facility, "Eat"));
            SetPrivateField(colonist.Brain, "eatStopRequested", false);
            SetPrivateField(colonist.Brain, "state", ColonistBrainState.EatSeeking);
        }

        private void SetWorkerPhysicallyServing(
            TestColonist colonist,
            Cafeteria cafeteria)
        {
            Assert.That(
                cafeteria.Facility.TryGetBinding(
                    "ServeFood",
                    out FacilityActivityBinding binding),
                Is.True);
            Assert.That(
                cafeteria.Facility.TryAcquire(
                    binding.ReservationGroup,
                    colonist.Runner,
                    out FacilityReservationToken token),
                Is.True);

            SetPrivateField(colonist.Runner, "currentFacility", cafeteria.Facility);
            SetPrivateField(colonist.Runner, "currentBinding", binding);
            SetPrivateField(colonist.Runner, "reservation", token);
            SetPrivateField(colonist.Runner, "activityActive", true);
            SetPrivateField(colonist.Runner, "exitInProgress", false);
            SetPrivateProperty(colonist.Runner, "Phase", ActivityPhase.Busy);
        }

        private static void ClearRunnerState(TestColonist colonist)
        {
            FacilityReservationToken token =
                (FacilityReservationToken)GetPrivateField(colonist.Runner, "reservation");
            InteractableFacility facility =
                (InteractableFacility)GetPrivateField(colonist.Runner, "currentFacility");
            if (token != null && facility != null)
                facility.Release(token);

            SetPrivateField(colonist.Runner, "reservation", null);
            SetPrivateField(colonist.Runner, "currentFacility", null);
            SetPrivateField(colonist.Runner, "currentBinding", null);
            SetPrivateField(colonist.Runner, "activityActive", false);
            SetPrivateField(colonist.Runner, "exitInProgress", false);
            SetPrivateProperty(colonist.Runner, "Phase", ActivityPhase.Idle);
        }

        private void AssignWorker(
            WorkforceManager workforce,
            TestColonist colonist,
            Cafeteria cafeteria,
            float startHour,
            float endHour)
        {
            Assert.That(
                workforce.Assign(
                    colonist.Identity,
                    cafeteria.Workplace,
                    cafeteria.Role,
                    new DailyShiftWindow(startHour, endHour)),
                Is.EqualTo(WorkAssignmentResult.Applied));
        }

        private WorkplaceComponent CreateWorkplace(
            JobRoleDefinition role,
            string activityId,
            string reservationGroup)
        {
            GameObject workplaceObject = new GameObject("Workplace Test " + activityId);
            sceneObjects.Add(workplaceObject);
            InteractableFacility facility =
                workplaceObject.AddComponent<InteractableFacility>();
            Transform approach = new GameObject(activityId + " Approach").transform;
            approach.SetParent(workplaceObject.transform, false);
            FacilityActivityBinding binding = new FacilityActivityBinding();
            SetPrivateField(binding, "activityId", activityId);
            SetPrivateField(binding, "reservationGroup", reservationGroup);
            SetPrivateField(binding, "externallyRequestable", true);
            SetPrivateField(binding, "approachAnchor", approach);
            SetPrivateField(facility, "activities", new[] { binding });

            WorkplaceRoleBinding roleBinding = new WorkplaceRoleBinding();
            SetPrivateField(roleBinding, "role", role);
            SetPrivateField(roleBinding, "activityId", activityId);
            SetPrivateField(roleBinding, "maximumConcurrentScheduledWorkers", 4);

            WorkplaceComponent workplace = workplaceObject.AddComponent<WorkplaceComponent>();
            SetPrivateField(workplace, "facility", facility);
            SetPrivateField(workplace, "roles", new[] { roleBinding });
            return workplace;
        }

        private Cafeteria CreateCafeteria(
            bool requiresStaff,
            FoodSelfServicePolicy policy)
        {
            Cafeteria cafeteria = new Cafeteria();
            cafeteria.GameObject = new GameObject("Cafeteria Test");
            sceneObjects.Add(cafeteria.GameObject);
            cafeteria.Facility = cafeteria.GameObject.AddComponent<InteractableFacility>();

            FacilityActivityBinding eat = CreateBinding(
                cafeteria.GameObject.transform,
                "Eat",
                "Eat01");
            FacilityActivityBinding serve = CreateBinding(
                cafeteria.GameObject.transform,
                "ServeFood",
                "ServeFood01");
            SetPrivateField(cafeteria.Facility, "activities", new[] { eat, serve });

            cafeteria.Role = CreateRole("cafeteria_worker");
            WorkplaceRoleBinding roleBinding = new WorkplaceRoleBinding();
            SetPrivateField(roleBinding, "role", cafeteria.Role);
            SetPrivateField(roleBinding, "activityId", "ServeFood");
            SetPrivateField(roleBinding, "maximumConcurrentScheduledWorkers", 1);
            cafeteria.Workplace = cafeteria.GameObject.AddComponent<WorkplaceComponent>();
            SetPrivateField(cafeteria.Workplace, "facility", cafeteria.Facility);
            SetPrivateField(cafeteria.Workplace, "roles", new[] { roleBinding });

            cafeteria.Service = cafeteria.GameObject.AddComponent<FoodServiceComponent>();
            SetPrivateField(cafeteria.Service, "facility", cafeteria.Facility);
            SetPrivateField(cafeteria.Service, "eatActivityId", "Eat");
            SetPrivateField(cafeteria.Service, "hungerRecoveryPerGameHour", 60f);
            SetPrivateField(cafeteria.Service, "requiresStaff", requiresStaff);
            SetPrivateField(
                cafeteria.Service,
                "requiredWorkplace",
                requiresStaff ? cafeteria.Workplace : null);
            SetPrivateField(
                cafeteria.Service,
                "requiredRole",
                requiresStaff ? cafeteria.Role : null);
            SetPrivateField(cafeteria.Service, "minimumActiveWorkers", 1);
            SetPrivateField(cafeteria.Service, "selfServicePolicy", policy);
            return cafeteria;
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

        private FoodManager CreateFoodManager(Cafeteria cafeteria)
        {
            GameObject managerObject = new GameObject("Food Manager Test");
            sceneObjects.Add(managerObject);
            FoodManager manager = managerObject.AddComponent<FoodManager>();
            SetStaticInstance(typeof(FoodManager), "Instance", manager);
            // Register only this test's service so a service that happens to live in the open
            // Editor scene can never win the nearest-service selection.
            List<FoodServiceComponent> services = new List<FoodServiceComponent>();
            services.Add(cafeteria.Service);
            SetPrivateField(manager, "services", services);
            return manager;
        }

        private WorkforceManager CreateWorkforceManager()
        {
            GameObject managerObject = new GameObject("Workforce Manager Test");
            sceneObjects.Add(managerObject);
            WorkforceManager manager = managerObject.AddComponent<WorkforceManager>();
            SetStaticInstance(typeof(WorkforceManager), "Instance", manager);
            return manager;
        }

        private SimulationManager CreateSimulationManager(float currentGameHour)
        {
            GameObject simulationObject = new GameObject("Simulation Manager Test");
            sceneObjects.Add(simulationObject);
            SimulationManager simulation =
                simulationObject.AddComponent<SimulationManager>();
            SetPrivateField(simulation, "currentGameHour", currentGameHour);
            SetStaticInstance(typeof(SimulationManager), "Instance", simulation);
            return simulation;
        }

        private SimulationLogManager CreateSimulationLogManager()
        {
            GameObject logObject = new GameObject("Simulation Log Manager Test");
            sceneObjects.Add(logObject);
            SimulationLogManager log = logObject.AddComponent<SimulationLogManager>();
            SetStaticInstance(typeof(SimulationLogManager), "Instance", log);
            return log;
        }

        private TestColonist CreateColonist(string displayName)
        {
            TestColonist colonist = new TestColonist();
            colonist.GameObject = new GameObject("Colonist (" + displayName + ")");
            sceneObjects.Add(colonist.GameObject);

            colonist.Identity = colonist.GameObject.AddComponent<ColonistIdentity>();
            SetPrivateField(colonist.Identity, "displayName", displayName);
            colonist.Stats = colonist.GameObject.AddComponent<ColonistStatsComponent>();
            colonist.Assignments = colonist.GameObject.AddComponent<ColonistAssignments>();
            colonist.Resolver = colonist.GameObject.AddComponent<ColonistTargetResolver>();
            colonist.Runner = colonist.GameObject.AddComponent<ColonistActivityRunner>();
            colonist.Brain = colonist.GameObject.AddComponent<ColonistBrain>();

            SetPrivateField(colonist.Stats, "activityRunner", colonist.Runner);
            SetPrivateField(colonist.Stats, "identity", colonist.Identity);
            SetPrivateField(colonist.Resolver, "assignments", colonist.Assignments);
            SetPrivateField(colonist.Resolver, "identity", colonist.Identity);
            SetPrivateField(colonist.Brain, "stats", colonist.Stats);
            SetPrivateField(colonist.Brain, "identity", colonist.Identity);
            SetPrivateField(colonist.Brain, "targetResolver", colonist.Resolver);
            SetPrivateField(colonist.Brain, "activityRunner", colonist.Runner);
            InvokePrivate(colonist.Stats, "Awake");
            return colonist;
        }

        private JobRoleDefinition CreateRole(string stableId)
        {
            JobRoleDefinition role = ScriptableObject.CreateInstance<JobRoleDefinition>();
            assets.Add(role);
            SetPrivateField(role, "stableId", stableId);
            SetPrivateField(role, "displayName", stableId);
            return role;
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field {fieldName}.");
            return field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field {fieldName}.");
            field.SetValue(target, value);
        }

        private static void SetPrivateProperty(
            object target,
            string propertyName,
            object value)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"Missing property {propertyName}.");
            MethodInfo setter = property.GetSetMethod(true);
            Assert.That(setter, Is.Not.Null, $"Property {propertyName} is not writable.");
            setter.Invoke(target, new[] { value });
        }

        private static void SetStaticInstance(
            System.Type type,
            string propertyName,
            object value)
        {
            PropertyInfo property = type.GetProperty(
                propertyName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"Missing static property {propertyName}.");
            MethodInfo setter = property.GetSetMethod(true);
            Assert.That(setter, Is.Not.Null, $"Static property {propertyName} is not writable.");
            setter.Invoke(null, new[] { value });
        }

        private static object InvokePrivate(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing private method {methodName}.");
            return method.Invoke(target, arguments);
        }

        private sealed class Cafeteria
        {
            public GameObject GameObject;
            public InteractableFacility Facility;
            public WorkplaceComponent Workplace;
            public FoodServiceComponent Service;
            public JobRoleDefinition Role;
        }

        private sealed class TestColonist
        {
            public GameObject GameObject;
            public ColonistIdentity Identity;
            public ColonistStatsComponent Stats;
            public ColonistAssignments Assignments;
            public ColonistTargetResolver Resolver;
            public ColonistActivityRunner Runner;
            public ColonistBrain Brain;
        }
    }
}
