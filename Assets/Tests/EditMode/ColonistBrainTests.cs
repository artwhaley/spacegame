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
