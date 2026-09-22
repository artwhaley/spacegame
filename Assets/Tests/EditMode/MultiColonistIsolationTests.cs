using System.Collections.Generic;
using System.Reflection;
using Colony.Interactions;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    /// <summary>
    /// Stack 2 isolation coverage: several independent colonists sharing the same managers must
    /// not share mutable state. Every assertion here is deterministic and free of NavMesh
    /// dependency, so the suite does not depend on which scene happens to be open in the Editor.
    /// </summary>
    [Category("Regression")]

    public class MultiColonistIsolationTests
    {
        private const float BaselineLeisureRate = 4f;

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
        public void NeedsAreIndependentPerColonist()
        {
            TestColonist bob = CreateColonist("Bob");
            TestColonist alice = CreateColonist("Alice");

            bob.Stats.AdjustHunger(80f);
            bob.Stats.AdjustStimulationNeed(70f);
            alice.Stats.AdjustRelaxationNeed(60f);

            Assert.That(bob.Stats.Hunger, Is.EqualTo(80f).Within(0.0001f));
            Assert.That(alice.Stats.Hunger, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(bob.Stats.StimulationNeed, Is.EqualTo(70f).Within(0.0001f));
            Assert.That(alice.Stats.StimulationNeed, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(alice.Stats.RelaxationNeed, Is.EqualTo(60f).Within(0.0001f));
            Assert.That(bob.Stats.RelaxationNeed, Is.EqualTo(0f).Within(0.0001f));

            Assert.That(bob.Stats.IsHungry, Is.True);
            Assert.That(alice.Stats.IsHungry, Is.False);
            Assert.That(bob.Stats.NeedsStimulation, Is.True);
            Assert.That(alice.Stats.NeedsStimulation, Is.False);
        }



        [Test]
        public void CompletionCooldownIsPerColonist()
        {
            OffDutyManager manager = CreateOffDutyManager();
            OffDutyComponent provider = CreateRecreationProvider(
                "play",
                "Play01",
                "play",
                stimulationRecovery: 60f,
                relaxationRecovery: 0f);
            UseOnlyProviders(manager, provider);

            TestColonist bob = CreateColonist("Bob");
            TestColonist dana = CreateColonist("Dana");

            bob.Brain.OffDutyCompletionHistory.RecordCompletion("play", 10f);

            Assert.That(
                bob.Brain.OffDutyCompletionHistory.IsOnCooldown("play", 12f, 11f),
                Is.True);
            Assert.That(
                dana.Brain.OffDutyCompletionHistory.IsOnCooldown("play", 12f, 11f),
                Is.False);

            // One colonist's personal recreation history must not become colony history.
            manager.SubmitBid(ActivityBidTestHelpers.CreateOffDutyBid(
                bob.Identity,
                OffDutyDrive.Stimulation,
                bob.Brain.OffDutyCompletionHistory));
            manager.SimulationTick(0.1f);
            Assert.That(manager.TryPeekOffer(bob.Identity, 1L, out _), Is.False);

            manager.SubmitBid(ActivityBidTestHelpers.CreateOffDutyBid(
                dana.Identity,
                OffDutyDrive.Stimulation,
                dana.Brain.OffDutyCompletionHistory));
            manager.SimulationTick(0.1f);
            Assert.That(manager.TryPeekOffer(dana.Identity, 1L, out OffDutyOffer danaOffer), Is.True);
            Assert.That(danaOffer.Opportunity.Target.ActivityId, Is.EqualTo("play"));
        }

        [Test]
        public void EachColonistResolvesOnlyItsOwnSleepAssignment()
        {
            InteractableFacility commandPod = CreateSleepFacility();
            TestColonist bob = CreateColonist("Bob");
            TestColonist alice = CreateColonist("Alice");
            TestColonist charlie = CreateColonist("Charlie");
            TestColonist dana = CreateColonist("Dana");

            AssignSleep(commandPod, bob, "Sleep01");
            AssignSleep(commandPod, alice, "Sleep02");
            AssignSleep(commandPod, charlie, "Sleep03");
            AssignSleep(commandPod, dana, "Sleep04");

            Assert.That(ResolveSleepActivity(bob), Is.EqualTo("Sleep01"));
            Assert.That(ResolveSleepActivity(alice), Is.EqualTo("Sleep02"));
            Assert.That(ResolveSleepActivity(charlie), Is.EqualTo("Sleep03"));
            Assert.That(ResolveSleepActivity(dana), Is.EqualTo("Sleep04"));

            HashSet<string> groups = new HashSet<string>();
            Assert.That(groups.Add(ResolveSleepGroup(commandPod, bob)), Is.True);
            Assert.That(groups.Add(ResolveSleepGroup(commandPod, alice)), Is.True);
            Assert.That(groups.Add(ResolveSleepGroup(commandPod, charlie)), Is.True);
            Assert.That(groups.Add(ResolveSleepGroup(commandPod, dana)), Is.True);
        }

        [Test]
        public void FourColonistsCanHoldDistinctBedsOnOneFacilityAtOnce()
        {
            InteractableFacility commandPod = CreateSleepFacility();
            TestColonist bob = CreateColonist("Bob");
            TestColonist alice = CreateColonist("Alice");
            TestColonist charlie = CreateColonist("Charlie");
            TestColonist dana = CreateColonist("Dana");

            List<FacilityReservationToken> tokens = new List<FacilityReservationToken>();
            tokens.Add(AcquireBed(commandPod, "Sleep01", bob));
            tokens.Add(AcquireBed(commandPod, "Sleep02", alice));
            tokens.Add(AcquireBed(commandPod, "Sleep03", charlie));
            tokens.Add(AcquireBed(commandPod, "Sleep04", dana));

            for (int index = 0; index < tokens.Count; index++)
            {
                Assert.That(tokens[index], Is.Not.Null, $"Bed {index + 1} was not reserved.");
            }

            Assert.That(commandPod.IsReserved("Bed01"), Is.True);
            Assert.That(commandPod.IsReserved("Bed04"), Is.True);
            // A fifth sleeper must not be able to duplicate an occupied bed group.
            Assert.That(
                commandPod.TryAcquire("Bed01", alice.Runner, out FacilityReservationToken duplicate),
                Is.False);
            Assert.That(duplicate, Is.Null);

            Assert.That(commandPod.Release(tokens[0]), Is.True);
            Assert.That(
                commandPod.TryAcquire("Bed01", alice.Runner, out FacilityReservationToken reclaimed),
                Is.True);
            Assert.That(reclaimed, Is.Not.Null);
        }


        [Test]
        public void WorkforceAssignmentsRemainPerColonist()
        {
            WorkforceManager workforce = CreateWorkforceManager();
            JobRoleDefinition farmer = CreateRole("farmer");
            JobRoleDefinition cafeteria = CreateRole("cafeteria_worker");
            JobRoleDefinition operatorRole = CreateRole("command_operator");
            WorkplaceComponent farm = CreateWorkplace(farmer, "Farm", "Farm01");
            WorkplaceComponent cafeteriaWorkplace = CreateWorkplace(
                cafeteria,
                "ServeFood",
                "ServeFood01");
            WorkplaceComponent command = CreateWorkplace(operatorRole, "Command", "Command01");

            TestColonist bob = CreateColonist("Bob");
            TestColonist alice = CreateColonist("Alice");
            TestColonist charlie = CreateColonist("Charlie");
            TestColonist dana = CreateColonist("Dana");

            Assert.That(
                workforce.Assign(bob.Identity, farm, farmer, new DailyShiftWindow(8f, 16f)),
                Is.EqualTo(WorkAssignmentResult.Applied));
            Assert.That(
                workforce.Assign(alice.Identity, cafeteriaWorkplace, cafeteria,
                    new DailyShiftWindow(6f, 18f)),
                Is.EqualTo(WorkAssignmentResult.Applied));
            Assert.That(
                workforce.Assign(charlie.Identity, command, operatorRole,
                    new DailyShiftWindow(8f, 16f)),
                Is.EqualTo(WorkAssignmentResult.Applied));

            Assert.That(workforce.Assignments.Count, Is.EqualTo(3));
            Assert.That(
                workforce.TryGetAssignment(dana.Identity, out WorkAssignment danaAssignment),
                Is.False);
            Assert.That(danaAssignment, Is.Null);

            Assert.That(
                workforce.TryGetAssignment(bob.Identity, out WorkAssignment bobAssignment),
                Is.True);
            Assert.That(bobAssignment.Workplace, Is.SameAs(farm));

            // Assigning the unassigned colonist must not disturb anyone else.
            Assert.That(
                workforce.Assign(dana.Identity, farm, farmer, new DailyShiftWindow(8f, 16f)),
                Is.EqualTo(WorkAssignmentResult.Applied));
            Assert.That(workforce.Assignments.Count, Is.EqualTo(4));
            Assert.That(
                workforce.TryGetAssignment(alice.Identity, out WorkAssignment aliceAssignment),
                Is.True);
            Assert.That(aliceAssignment.Workplace, Is.SameAs(cafeteriaWorkplace));

            Assert.That(workforce.Unassign(bob.Identity), Is.EqualTo(WorkAssignmentResult.Applied));
            Assert.That(
                workforce.TryGetAssignment(alice.Identity, out _),
                Is.True);
            Assert.That(
                workforce.TryGetAssignment(charlie.Identity, out _),
                Is.True);
            Assert.That(
                workforce.TryGetAssignment(dana.Identity, out _),
                Is.True);

            // Reassigning must not leave two records for the same colonist.
            Assert.That(
                workforce.Assign(
                    bob.Identity,
                    cafeteriaWorkplace,
                    cafeteria,
                    new DailyShiftWindow(6f, 18f)),
                Is.EqualTo(WorkAssignmentResult.Applied));
            int bobRecords = CountAssignmentsFor(workforce, bob.Identity);
            Assert.That(bobRecords, Is.EqualTo(1));
        }




        [Test]
        public void ColonistEventsUseTheColonistIdentityAsPrimarySubject()
        {
            SimulationLogManager log = CreateSimulationLogManager();
            SetStaticInstance(typeof(SimulationLogManager), "Instance", log);
            TestColonist bob = CreateColonist("Bob");
            TestColonist alice = CreateColonist("Alice");

            // A decision recorded by the brain and a need transition recorded by the stats
            // component must both resolve to the same colonist subject.
            bob.Brain.SimulationTick(0.1f);
            bob.Stats.AdjustHunger(80f);
            alice.Stats.AdjustHunger(80f);

            IReadOnlyList<SimulationLogEntry> bobEntries = log.Query(subject: bob.Identity);
            Assert.That(bobEntries.Count, Is.GreaterThan(0));

            bool sawDecision = false;
            bool sawNeedTransition = false;
            for (int index = 0; index < bobEntries.Count; index++)
            {
                SimulationLogEntry entry = bobEntries[index];
                Assert.That(entry.PrimarySubject, Is.Not.Null);
                Assert.That(entry.PrimarySubject.DisplayName, Is.EqualTo("Bob"));

                if (string.Equals(entry.EventKey, "colonist.decision.blocked", System.StringComparison.Ordinal))
                    sawDecision = true;
                if (string.Equals(entry.EventKey, "colonist.need.hungry", System.StringComparison.Ordinal))
                    sawNeedTransition = true;
            }

            Assert.That(sawDecision, Is.True, "A brain decision must be attributed to the colonist.");
            Assert.That(
                sawNeedTransition,
                Is.True,
                "A need transition recorded by the stats component must be attributed to the colonist.");

            // Alice's history must not contain Bob's events, and vice versa.
            IReadOnlyList<SimulationLogEntry> aliceEntries = log.Query(subject: alice.Identity);
            Assert.That(aliceEntries.Count, Is.GreaterThan(0));
            for (int index = 0; index < aliceEntries.Count; index++)
            {
                Assert.That(aliceEntries[index].PrimarySubject, Is.Not.Null);
                Assert.That(
                    aliceEntries[index].PrimarySubject.DisplayName,
                    Is.EqualTo("Alice"));
            }
        }


        private static int CountAssignmentsFor(
            WorkforceManager workforce,
            ColonistIdentity colonist)
        {
            int count = 0;
            IReadOnlyList<WorkAssignment> assignments = workforce.Assignments;
            for (int index = 0; index < assignments.Count; index++)
            {
                WorkAssignment assignment = assignments[index];
                if (assignment != null && assignment.Colonist == colonist)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Replaces the manager's provider list with exactly the given providers so a test can
        /// never accidentally discover recreation that happens to live in the open Editor scene.
        /// </summary>
        private static void UseOnlyProviders(
            OffDutyManager manager,
            params OffDutyComponent[] providers)
        {
            List<OffDutyComponent> list = new List<OffDutyComponent>();
            for (int index = 0; index < providers.Length; index++)
                list.Add(providers[index]);
            SetPrivateField(manager, "providers", list);
        }

        private OffDutyQuery CreateDriveQuery(
            TestColonist colonist,
            OffDutyDrive drive,
            float currentGameHour)
        {
            return new OffDutyQuery(
                colonist.Identity,
                colonist.GameObject.transform.position,
                1f,
                drive,
                colonist.Brain.OffDutyCompletionHistory,
                currentGameHour);
        }

        private static string ResolveSleepActivity(TestColonist colonist)
        {
            Assert.That(
                colonist.Resolver.TryResolveTarget(
                    ActivityPurpose.Sleep,
                    out ActivityTarget target),
                Is.True);
            return target.ActivityId;
        }

        private static string ResolveSleepGroup(
            InteractableFacility facility,
            TestColonist colonist)
        {
            string activityId = ResolveSleepActivity(colonist);
            Assert.That(
                facility.TryGetBinding(activityId, out FacilityActivityBinding binding),
                Is.True);
            return binding.ReservationGroup;
        }

        private FacilityReservationToken AcquireBed(
            InteractableFacility facility,
            string activityId,
            TestColonist colonist)
        {
            Assert.That(
                facility.TryGetBinding(activityId, out FacilityActivityBinding binding),
                Is.True);
            Assert.That(
                facility.TryAcquire(binding.ReservationGroup, colonist.Runner, out FacilityReservationToken token),
                Is.True);
            return token;
        }

        private void AssignSleep(
            InteractableFacility facility,
            TestColonist colonist,
            string activityId)
        {
            SetPrivateField(
                colonist.Assignments,
                "sleepTarget",
                new ActivityTarget(facility, activityId));
        }

        private void ConfigureGenuinelyActiveActivity(
            TestColonist colonist,
            OffDutyComponent provider,
            string activityId)
        {
            InteractableFacility facility = provider.Facility;
            Assert.That(
                facility.TryGetBinding(activityId, out FacilityActivityBinding binding),
                Is.True);
            Assert.That(
                facility.TryAcquire(
                    binding.ReservationGroup,
                    colonist.Runner,
                    out FacilityReservationToken token),
                Is.True);

            SetPrivateField(colonist.Runner, "currentFacility", facility);
            SetPrivateField(colonist.Runner, "currentBinding", binding);
            SetPrivateField(colonist.Runner, "reservation", token);
            SetPrivateField(colonist.Runner, "activityActive", true);
            SetPrivateField(colonist.Runner, "exitInProgress", false);
            SetPrivateProperty(colonist.Runner, "Phase", ActivityPhase.Busy);
        }

        private InteractableFacility CreateSleepFacility()
        {
            GameObject facilityObject = new GameObject("CommandPod Test");
            sceneObjects.Add(facilityObject);
            InteractableFacility facility =
                facilityObject.AddComponent<InteractableFacility>();

            FacilityActivityBinding[] bindings = new FacilityActivityBinding[4];
            for (int index = 0; index < bindings.Length; index++)
            {
                Transform approach =
                    new GameObject("Sleep0" + (index + 1) + " Approach").transform;
                approach.SetParent(facilityObject.transform, false);
                FacilityActivityBinding binding = new FacilityActivityBinding();
                SetPrivateField(binding, "activityId", "Sleep0" + (index + 1));
                SetPrivateField(binding, "reservationGroup", "Bed0" + (index + 1));
                SetPrivateField(binding, "externallyRequestable", true);
                SetPrivateField(binding, "approachAnchor", approach);
                bindings[index] = binding;
            }

            SetPrivateField(facility, "activities", bindings);
            return facility;
        }

        private OffDutyComponent CreateRecreationProvider(
            string activityId,
            string reservationGroup,
            string cooldownKey,
            float stimulationRecovery,
            float relaxationRecovery)
        {
            GameObject recreationObject =
                new GameObject("Recreation Test " + cooldownKey);
            sceneObjects.Add(recreationObject);

            InteractableFacility facility =
                recreationObject.AddComponent<InteractableFacility>();
            Transform approach = new GameObject(activityId + " Approach").transform;
            approach.SetParent(recreationObject.transform, false);
            FacilityActivityBinding facilityBinding = new FacilityActivityBinding();
            SetPrivateField(facilityBinding, "activityId", activityId);
            SetPrivateField(facilityBinding, "reservationGroup", reservationGroup);
            SetPrivateField(facilityBinding, "externallyRequestable", true);
            SetPrivateField(facilityBinding, "approachAnchor", approach);
            SetPrivateField(facility, "activities", new[] { facilityBinding });

            OffDutyComponent provider = recreationObject.AddComponent<OffDutyComponent>();
            SetPrivateField(provider, "facility", facility);

            OffDutyActivityBinding activity = new OffDutyActivityBinding();
            SetPrivateField(activity, "activityId", activityId);
            SetPrivateField(activity, "plannedDurationGameHours", 1f);
            SetPrivateField(activity, "enabled", true);
            SetPrivateField(activity, "cooldownGameHours", 12f);
            SetPrivateField(activity, "cooldownKey", cooldownKey);
            SetPrivateField(
                activity,
                "stimulationRecoveryPerGameHour",
                stimulationRecovery);
            SetPrivateField(
                activity,
                "relaxationRecoveryPerGameHour",
                relaxationRecovery);
            SetPrivateField(provider, "activities", new[] { activity });
            return provider;
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

            // Capture the threshold baseline explicitly instead of depending on whether the
            // Editor invoked Awake when the component was added.
            InvokePrivate(colonist.Stats, "Awake");
            return colonist;
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
            FieldInfo field = target.GetType().GetField(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
                return field.GetValue(target);

            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing private method {methodName}.");
            return method.Invoke(target, arguments);
        }

        private OffDutyManager CreateOffDutyManager()
        {
            GameObject managerObject = new GameObject("OffDuty Manager Test");
            sceneObjects.Add(managerObject);
            return managerObject.AddComponent<OffDutyManager>();
        }

        private WorkforceManager CreateWorkforceManager()
        {
            GameObject managerObject = new GameObject("Workforce Manager Test");
            sceneObjects.Add(managerObject);
            return managerObject.AddComponent<WorkforceManager>();
        }

        private SimulationManager CreateSimulationManager(float currentGameHour)
        {
            GameObject simulationObject = new GameObject("Simulation Manager Test");
            sceneObjects.Add(simulationObject);
            SimulationManager simulation =
                simulationObject.AddComponent<SimulationManager>();
            SetPrivateField(simulation, "currentGameHour", currentGameHour);
            return simulation;
        }

        private SimulationLogManager CreateSimulationLogManager()
        {
            GameObject logObject = new GameObject("Simulation Log Manager Test");
            sceneObjects.Add(logObject);
            return logObject.AddComponent<SimulationLogManager>();
        }

        private JobRoleDefinition CreateRole(string stableId)
        {
            JobRoleDefinition role = ScriptableObject.CreateInstance<JobRoleDefinition>();
            assets.Add(role);
            SetPrivateField(role, "stableId", stableId);
            SetPrivateField(role, "displayName", stableId);
            return role;
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
