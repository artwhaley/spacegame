using System.Reflection;
using Colony.Interactions;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class ColonistBrainTests
    {
        private GameObject colonistObject;
        private GameObject workforceObject;
        private GameObject simulationObject;
        private GameObject workplaceObject;
        private GameObject sleepFacilityObject;
        private JobRoleDefinition role;

        [TearDown]
        public void TearDown()
        {
            if (colonistObject != null)
                Object.DestroyImmediate(colonistObject);
            if (workforceObject != null)
                Object.DestroyImmediate(workforceObject);
            if (simulationObject != null)
                Object.DestroyImmediate(simulationObject);
            if (workplaceObject != null)
                Object.DestroyImmediate(workplaceObject);
            if (sleepFacilityObject != null)
                Object.DestroyImmediate(sleepFacilityObject);
            if (role != null)
                Object.DestroyImmediate(role);
        }

        [Test]
        public void NotSleepyRemainsIdle()
        {
            ColonistBrain brain = CreateBrain(0f);

            brain.SimulationTick(1f);

            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.Idle));
        }

        [Test]
        public void MissingSleepTargetRemainsIdle()
        {
            ColonistBrain brain = CreateBrain(70f);

            brain.SimulationTick(1f);

            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.Idle));
        }

        [Test]
        public void NoAssignmentHasNoUpcomingWorkObligation()
        {
            ColonistBrain brain = CreateBrain(80f);
            CreateSchedule(0f, null, false);

            Assert.That(
                (bool)InvokePrivate(
                    brain,
                    "HasUpcomingObligationWithin",
                    0.5f),
                Is.False);
        }

        [Test]
        public void NextShiftOutsideLeadWindowHasNoImminentObligation()
        {
            ColonistBrain brain = CreateBrain(80f);
            CreateSchedule(0f, new DailyShiftWindow(2f, 6f), true);

            Assert.That(
                (bool)InvokePrivate(
                    brain,
                    "HasUpcomingObligationWithin",
                    0.5f),
                Is.False);
        }

        [Test]
        public void NextShiftInsideLeadWindowIsAnImminentObligation()
        {
            ColonistBrain brain = CreateBrain(80f);
            CreateSchedule(1.6f, new DailyShiftWindow(2f, 6f), true);

            Assert.That(
                (bool)InvokePrivate(
                    brain,
                    "HasUpcomingObligationWithin",
                    0.5f),
                Is.True);
        }

        [Test]
        public void CurrentShiftIsAnObligationEvenAfterLeadWindow()
        {
            ColonistBrain brain = CreateBrain(80f);
            CreateSchedule(3f, new DailyShiftWindow(2f, 6f), true);

            Assert.That(
                (bool)InvokePrivate(
                    brain,
                    "HasUpcomingObligationWithin",
                    0.5f),
                Is.True);
        }

        [Test]
        public void SleepyColonistInsideWorkLeadWindowDoesNotRequestSleep()
        {
            ColonistBrain brain = CreateBrain(80f);
            CreateSchedule(1.6f, new DailyShiftWindow(2f, 6f), true);
            ConfigureSleepTarget();
            ColonistActivityRunner runner =
                colonistObject.GetComponent<ColonistActivityRunner>();

            brain.SimulationTick(0.1f);

            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.Idle));
            Assert.That(runner.Phase, Is.EqualTo(ActivityPhase.Idle));
        }

        [Test]
        public void CurrentWorkWithUnavailablePhysicalTargetRemainsIdle()
        {
            ColonistBrain brain = CreateBrain(0f);
            CreateSchedule(3f, new DailyShiftWindow(2f, 6f), true);

            brain.SimulationTick(0.1f);

            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.Idle));
        }

        [Test]
        public void ShiftEndRequestsWorkStopButKeepsBrainWorkingDuringPhysicalExit()
        {
            ColonistBrain brain = CreateBrain(0f);
            CreateSchedule(3f, new DailyShiftWindow(2f, 6f), true);
            ConfigureWorkingLifecycle(brain);
            SetPrivateField(
                simulationObject.GetComponent<SimulationManager>(),
                "currentGameHour",
                6f);

            brain.SimulationTick(0.1f);

            ColonistActivityRunner runner =
                colonistObject.GetComponent<ColonistActivityRunner>();
            Assert.That(
                (bool)InvokePrivate(brain, "workStopRequested"),
                Is.True);
            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.Working));
            Assert.That(runner.HasActiveRequest, Is.True);
            Assert.That(runner.IsActivityActive, Is.False);
        }

        [Test]
        public void WorkExitCompletesOnlyAfterRunnerReleasesRequest()
        {
            ColonistBrain brain = CreateBrain(0f);
            CreateSchedule(3f, new DailyShiftWindow(2f, 6f), true);
            ConfigureWorkingLifecycle(brain);
            SetPrivateField(
                simulationObject.GetComponent<SimulationManager>(),
                "currentGameHour",
                6f);

            ColonistActivityRunner runner =
                colonistObject.GetComponent<ColonistActivityRunner>();
            brain.SimulationTick(0.1f);
            brain.SimulationTick(0.1f);

            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.Working));
            Assert.That(runner.HasActiveRequest, Is.True);

            SetPrivateField(runner, "reservation", null);
            brain.SimulationTick(0.1f);

            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.Idle));
        }

        [Test]
        public void AssignmentDisappearingDuringWorkRequestsPhysicalStop()
        {
            ColonistBrain brain = CreateBrain(0f);
            CreateSchedule(3f, new DailyShiftWindow(2f, 6f), true);
            ConfigureWorkingLifecycle(brain);

            WorkforceManager workforceManager =
                workforceObject.GetComponent<WorkforceManager>();
            ColonistIdentity identity = colonistObject.GetComponent<ColonistIdentity>();
            Assert.That(
                workforceManager.Unassign(identity),
                Is.EqualTo(WorkAssignmentResult.Applied));

            brain.SimulationTick(0.1f);

            Assert.That(
                (bool)InvokePrivate(brain, "workStopRequested"),
                Is.True);
            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.Working));
            Assert.That(
                colonistObject.GetComponent<ColonistActivityRunner>().HasActiveRequest,
                Is.True);
        }

        private ColonistBrain CreateBrain(float fatigue)
        {
            colonistObject = new GameObject("Colonist Brain Test");
            ColonistIdentity identity =
                colonistObject.AddComponent<ColonistIdentity>();
            ColonistStatsComponent stats =
                colonistObject.AddComponent<ColonistStatsComponent>();
            colonistObject.AddComponent<ColonistAssignments>();
            ColonistTargetResolver targetResolver =
                colonistObject.AddComponent<ColonistTargetResolver>();
            ColonistActivityRunner activityRunner =
                colonistObject.AddComponent<ColonistActivityRunner>();
            ColonistBrain brain = colonistObject.AddComponent<ColonistBrain>();

            SetPrivateField(stats, "fatigue", fatigue);
            SetPrivateField(brain, "stats", stats);
            SetPrivateField(brain, "identity", identity);
            SetPrivateField(brain, "targetResolver", targetResolver);
            SetPrivateField(brain, "activityRunner", activityRunner);
            return brain;
        }

        private void CreateSchedule(
            float currentGameHour,
            DailyShiftWindow shift,
            bool assign)
        {
            workforceObject = new GameObject("Workforce Manager Test");
            WorkforceManager workforceManager =
                workforceObject.AddComponent<WorkforceManager>();
            InvokePrivate(workforceManager, "Awake");

            simulationObject = new GameObject("Simulation Manager Test");
            SimulationManager simulationManager =
                simulationObject.AddComponent<SimulationManager>();
            SetPrivateField(simulationManager, "currentGameHour", currentGameHour);
            InvokePrivate(simulationManager, "Awake");

            if (!assign)
                return;

            role = ScriptableObject.CreateInstance<JobRoleDefinition>();
            SetPrivateField(role, "stableId", "farmer");
            SetPrivateField(role, "displayName", "Farmer");
            WorkplaceComponent workplace = CreateWorkplace(role);
            ColonistIdentity identity =
                colonistObject.GetComponent<ColonistIdentity>();
            Assert.That(
                workforceManager.Assign(
                    identity,
                    workplace,
                    role,
                    shift),
                Is.EqualTo(WorkAssignmentResult.Applied));
        }

        private WorkplaceComponent CreateWorkplace(JobRoleDefinition offeredRole)
        {
            workplaceObject = new GameObject("Workplace Test");
            InteractableFacility facility =
                workplaceObject.AddComponent<InteractableFacility>();
            Transform approach = new GameObject("Approach").transform;
            approach.SetParent(workplaceObject.transform, false);
            FacilityActivityBinding activity = new FacilityActivityBinding();
            SetPrivateField(activity, "activityId", "Farm");
            SetPrivateField(activity, "reservationGroup", "Farm01");
            SetPrivateField(activity, "approachAnchor", approach);
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

        private void ConfigureSleepTarget()
        {
            sleepFacilityObject = new GameObject("Sleep Facility Test");
            InteractableFacility facility =
                sleepFacilityObject.AddComponent<InteractableFacility>();
            Transform approach = new GameObject("Sleep Approach").transform;
            approach.SetParent(sleepFacilityObject.transform, false);
            FacilityActivityBinding activity = new FacilityActivityBinding();
            SetPrivateField(activity, "activityId", "Sleep");
            SetPrivateField(activity, "reservationGroup", "Bed01");
            SetPrivateField(activity, "approachAnchor", approach);
            SetPrivateField(facility, "activities", new[] { activity });

            ColonistAssignments assignments =
                colonistObject.GetComponent<ColonistAssignments>();
            SetPrivateField(
                assignments,
                "sleepTarget",
                new ActivityTarget(facility, "Sleep"));
        }

        private void ConfigureWorkingLifecycle(ColonistBrain brain)
        {
            ColonistActivityRunner runner =
                colonistObject.GetComponent<ColonistActivityRunner>();
            InteractableFacility facility =
                workplaceObject.GetComponent<InteractableFacility>();
            Assert.That(
                facility.TryGetBinding("Farm", out FacilityActivityBinding binding),
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
            SetPrivateField(runner, "activityActive", true);
            SetPrivateField(runner, "exitInProgress", true);
            SetPrivateProperty(runner, "Phase", ActivityPhase.Busy);

            SetPrivateField(
                brain,
                "workTargetInProgress",
                new ActivityTarget(facility, "Farm"));
            SetPrivateField(brain, "workStopRequested", false);
            SetPrivateField(brain, "state", ColonistBrainState.Working);
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
    }
}
