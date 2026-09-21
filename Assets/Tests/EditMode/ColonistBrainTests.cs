using System.Collections.Generic;
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
        private GameObject foodManagerObject;
        private GameObject foodFacilityObject;
        private GameObject offDutyManagerObject;
        private readonly List<GameObject> recreationObjects = new List<GameObject>();
        private GameObject staffedWorkplaceObject;
        private JobRoleDefinition role;
        private JobRoleDefinition staffedRole;

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
            if (foodManagerObject != null)
                Object.DestroyImmediate(foodManagerObject);
            if (foodFacilityObject != null)
                Object.DestroyImmediate(foodFacilityObject);
            if (offDutyManagerObject != null)
                Object.DestroyImmediate(offDutyManagerObject);
            for (int index = recreationObjects.Count - 1; index >= 0; index--)
            {
                if (recreationObjects[index] != null)
                    Object.DestroyImmediate(recreationObjects[index]);
            }

            recreationObjects.Clear();
            if (staffedWorkplaceObject != null)
                Object.DestroyImmediate(staffedWorkplaceObject);
            if (role != null)
                Object.DestroyImmediate(role);
            if (staffedRole != null)
                Object.DestroyImmediate(staffedRole);
        }

        [Test]
        public void DiscretionaryNeedsSatisfiedDoesNotSeekOffDuty()
        {
            ColonistBrain brain = CreateBrain(0f);
            CreateSchedule(0f, null, false);
            CreateRecreationProvider(60f, 0f, "play");

            brain.SimulationTick(0.1f);

            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.Idle));
            Assert.That(
                brain.LastDecisionReason,
                Is.EqualTo("discretionary_needs_satisfied"));
        }

        [Test]
        public void StimulationDriveSelectsAStimulatingActivity()
        {
            ColonistBrain brain = CreateBrain(0f);
            CreateSchedule(0f, null, false);
            CreateRecreationProvider(60f, 0f, "play");
            SetStimulationNeed(80f);

            brain.SimulationTick(0.1f);

            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.OffDutySeeking));
            Assert.That(brain.ActiveOffDutyDrive, Is.EqualTo(OffDutyDrive.Stimulation));
        }

        [Test]
        public void RelaxationDriveExcludesAStimulatingOnlyActivity()
        {
            ColonistBrain brain = CreateBrain(0f);
            CreateSchedule(0f, null, false);
            CreateRecreationProvider(60f, 0f, "play");
            SetRelaxationNeed(80f);

            brain.SimulationTick(0.1f);

            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.Idle));
            Assert.That(brain.LastDecision, Is.EqualTo("offduty.no_target"));
        }

        [Test]
        public void HigherNormalizedPressureIsTriedFirst()
        {
            ColonistBrain brain = CreateBrain(0f);
            CreateSchedule(0f, null, false);
            CreateRecreationProvider(10f, 10f, "walk");
            SetStimulationNeed(60f);
            SetRelaxationNeed(100f);

            brain.SimulationTick(0.1f);

            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.OffDutySeeking));
            Assert.That(brain.ActiveOffDutyDrive, Is.EqualTo(OffDutyDrive.Relaxation));
        }

        [Test]
        public void CoolingPrimaryDriveFallsBackToTheSecondaryDrive()
        {
            ColonistBrain brain = CreateBrain(0f);
            CreateSchedule(0f, null, false);
            CreateRecreationProvider(60f, 0f, "play");
            CreateRecreationProvider(0f, 60f, "relax");
            SetStimulationNeed(100f);
            SetRelaxationNeed(60f);
            brain.OffDutyCompletionHistory.RecordCompletion("play", 0f);

            brain.SimulationTick(0.1f);

            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.OffDutySeeking));
            Assert.That(brain.ActiveOffDutyDrive, Is.EqualTo(OffDutyDrive.Relaxation));
        }

        [Test]
        public void CompletedActivityStartsCooldownAndIsNotImmediatelyReselected()
        {
            ColonistBrain brain = CreateBrain(0f);
            CreateSchedule(0f, null, false);
            CreateRecreationProvider(60f, 0f, "play");
            SetStimulationNeed(80f);
            brain.SimulationTick(0.1f);
            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.OffDutySeeking));

            // Genuine full-duration completion.
            SetPrivateField(brain, "state", ColonistBrainState.OffDutyActive);
            SetPrivateField(brain, "actualActiveOffDutyGameHours", 1f);
            brain.SimulationTick(0.1f);

            Assert.That((bool)InvokePrivate(brain, "offDutyStopRequested"), Is.True);
            Assert.That(
                brain.OffDutyCompletionHistory.IsOnCooldown("play", 12f, 0f),
                Is.True);

            ResetOffDutyLifecycle(brain);
            brain.SimulationTick(0.1f);

            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.Idle));
            Assert.That(brain.LastDecisionReason, Is.EqualTo("cooldown"));
        }

        [Test]
        public void InterruptedActivityDoesNotStartCooldown()
        {
            ColonistBrain brain = CreateBrain(0f);
            CreateSchedule(0f, null, false);
            CreateRecreationProvider(60f, 0f, "play");
            SetStimulationNeed(80f);
            brain.SimulationTick(0.1f);
            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.OffDutySeeking));

            SetPrivateField(brain, "state", ColonistBrainState.OffDutyActive);
            SetPrivateField(brain, "actualActiveOffDutyGameHours", 0.25f);
            SetPrivateField(
                colonistObject.GetComponent<ColonistStatsComponent>(),
                "hunger",
                80f);
            brain.SimulationTick(0.1f);

            Assert.That((bool)InvokePrivate(brain, "offDutyStopRequested"), Is.True);
            Assert.That(
                brain.OffDutyCompletionHistory.IsOnCooldown("play", 12f, 0f),
                Is.False);
        }

        [Test]
        public void EatSeekingStopsWhenPublicFoodAccessIsLost()
        {
            ColonistBrain brain = CreateBrain(0f);
            CreateSchedule(10f, null, false);
            ConfigureStaffedFoodServiceRequiringAWorker();
            ConfigurePendingEatRequest();
            SetPrivateField(
                colonistObject.GetComponent<ColonistStatsComponent>(),
                "hunger",
                80f);
            SetPrivateField(
                brain,
                "eatTargetInProgress",
                new ActivityTarget(foodFacilityObject.GetComponent<InteractableFacility>(), "Eat"));
            SetPrivateField(brain, "state", ColonistBrainState.EatSeeking);

            brain.SimulationTick(0.1f);

            Assert.That((bool)InvokePrivate(brain, "eatStopRequested"), Is.True);
            Assert.That(brain.LastDecisionReason, Is.EqualTo("food_access_lost"));
        }

        [Test]
        public void EatingContinuesWhenPublicStaffingIsUnavailable()
        {
            ColonistBrain brain = CreateBrain(0f);
            CreateSchedule(10f, null, false);
            ConfigureStaffedFoodServiceRequiringAWorker();
            ConfigureEatingLifecycle(brain, hunger: 80f);
            FoodServiceComponent service =
                foodFacilityObject.GetComponent<FoodServiceComponent>();

            brain.SimulationTick(0.1f);

            Assert.That((bool)InvokePrivate(brain, "eatStopRequested"), Is.False);
            Assert.That(service.HasActivePublicStaff(10f), Is.False);
            Assert.That(
                service.EvaluateAccess(
                    colonistObject.GetComponent<ColonistIdentity>(),
                    10f),
                Is.EqualTo(FoodServiceAccessMode.Unavailable));
            // Hunger recovery keeps applying to the diner even though the counter is closed.
            Assert.That(
                colonistObject.GetComponent<ColonistStatsComponent>().EffectiveHungerPerGameHour,
                Is.EqualTo(-60f).Within(0.0001f));
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

        [Test]
        public void HungryColonistStartsEatSeekingWhenNoWorkIsDue()
        {
            ColonistBrain brain = CreateBrain(0f);
            CreateSchedule(0f, null, false);
            CreateFoodManagerAndService();
            ConfigurePendingEatRequest();
            SetPrivateField(
                colonistObject.GetComponent<ColonistStatsComponent>(),
                "hunger",
                60f);

            brain.SimulationTick(0.1f);

            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.EatSeeking));
            Assert.That(
                colonistObject.GetComponent<ColonistActivityRunner>().CurrentActivityId,
                Is.EqualTo("Eat"));
        }

        [Test]
        public void HungerDepletionRequestsStopButWaitsForPhysicalRelease()
        {
            ColonistBrain brain = CreateBrain(0f);
            CreateSchedule(0f, null, false);
            CreateFoodManagerAndService();
            ConfigureEatingLifecycle(brain, hunger: 0f);

            brain.SimulationTick(0.1f);

            ColonistActivityRunner runner = colonistObject.GetComponent<ColonistActivityRunner>();
            Assert.That((bool)InvokePrivate(brain, "eatStopRequested"), Is.True);
            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.Eating));
            Assert.That(runner.HasActiveRequest, Is.True);
            Assert.That(runner.IsActivityActive, Is.False);

            SetPrivateField(runner, "reservation", null);
            brain.SimulationTick(0.1f);

            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.Idle));
        }

        [Test]
        public void ImminentWorkRequestsEatStopAndWorkWinsAfterRelease()
        {
            ColonistBrain brain = CreateBrain(0f);
            CreateSchedule(1.6f, new DailyShiftWindow(2f, 6f), true);
            CreateFoodManagerAndService();
            ConfigureEatingLifecycle(brain, hunger: 80f);

            brain.SimulationTick(0.1f);

            ColonistActivityRunner runner = colonistObject.GetComponent<ColonistActivityRunner>();
            Assert.That((bool)InvokePrivate(brain, "eatStopRequested"), Is.False);
            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.Eating));
            Assert.That(runner.HasActiveRequest, Is.True);

            SetPrivateField(
                simulationObject.GetComponent<SimulationManager>(),
                "currentGameHour",
                2f);
            brain.SimulationTick(0.1f);
            Assert.That((bool)InvokePrivate(brain, "eatStopRequested"), Is.True);

            SetPrivateField(runner, "reservation", null);
            brain.SimulationTick(0.1f);
            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.Idle));
            brain.SimulationTick(0.1f);
            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.WorkSeeking));
        }

        [Test]
        public void SleepyThresholdDoesNotInterruptCommittedMeal()
        {
            ColonistBrain brain = CreateBrain(70f);
            CreateSchedule(0f, null, false);
            CreateFoodManagerAndService();
            ConfigureEatingLifecycle(brain, hunger: 80f);

            brain.SimulationTick(0.1f);

            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.Eating));
            Assert.That((bool)InvokePrivate(brain, "eatStopRequested"), Is.False);
        }

        [Test]
        public void InvalidFoodServiceStopsEatLifecycleSafely()
        {
            ColonistBrain brain = CreateBrain(0f);
            CreateSchedule(0f, null, false);
            CreateFoodManagerAndService();
            ConfigureEatingLifecycle(brain, hunger: 80f);
            foodFacilityObject.GetComponent<FoodServiceComponent>().enabled = false;

            brain.SimulationTick(0.1f);

            Assert.That((bool)InvokePrivate(brain, "eatStopRequested"), Is.True);
            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.Eating));
        }

        [Test]
        public void SleepWinsWhenColonistIsBothSleepyAndHungry()
        {
            ColonistBrain brain = CreateBrain(80f);
            CreateSchedule(0f, null, false);
            ConfigureSleepTarget();
            CreateFoodManagerAndService();
            SetPrivateField(
                colonistObject.GetComponent<ColonistStatsComponent>(),
                "hunger",
                80f);

            brain.SimulationTick(0.1f);

            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.SleepSeeking));
            Assert.That(
                colonistObject.GetComponent<ColonistActivityRunner>().CurrentActivityId,
                Is.EqualTo("Sleep"));
        }

        [Test]
        public void CriticalHungerWinsWhenColonistIsSleepy()
        {
            ColonistBrain brain = CreateBrain(80f);
            CreateSchedule(0f, null, false);
            ConfigureSleepTarget();
            CreateFoodManagerAndService();
            SetPrivateField(
                colonistObject.GetComponent<ColonistStatsComponent>(),
                "hunger",
                95f);

            brain.SimulationTick(0.1f);

            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.EatSeeking));
            Assert.That(
                colonistObject.GetComponent<ColonistActivityRunner>().CurrentActivityId,
                Is.EqualTo("Eat"));
        }

        [Test]
        public void CriticalHungerWinsBeforeBeginningCurrentWork()
        {
            ColonistBrain brain = CreateBrain(0f);
            CreateSchedule(3f, new DailyShiftWindow(2f, 6f), true);
            CreateFoodManagerAndService();
            SetPrivateField(
                colonistObject.GetComponent<ColonistStatsComponent>(),
                "hunger",
                95f);

            brain.SimulationTick(0.1f);

            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.EatSeeking));
        }

        [Test]
        public void CriticalHungerRequestsPhysicalWorkExit()
        {
            ColonistBrain brain = CreateBrain(0f);
            CreateSchedule(3f, new DailyShiftWindow(2f, 6f), true);
            ConfigureWorkingLifecycle(brain);
            SetPrivateField(
                colonistObject.GetComponent<ColonistStatsComponent>(),
                "hunger",
                95f);

            brain.SimulationTick(0.1f);

            Assert.That((bool)InvokePrivate(brain, "workStopRequested"), Is.True);
            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.Working));
        }

        [Test]
        public void OrdinaryEatingStopsWhenWorkBecomesCurrent()
        {
            ColonistBrain brain = CreateBrain(0f);
            CreateSchedule(1.6f, new DailyShiftWindow(2f, 6f), true);
            CreateFoodManagerAndService();
            ConfigureEatingLifecycle(brain, hunger: 80f);

            brain.SimulationTick(0.1f);
            Assert.That((bool)InvokePrivate(brain, "eatStopRequested"), Is.False);

            SetPrivateField(
                simulationObject.GetComponent<SimulationManager>(),
                "currentGameHour",
                2f);
            brain.SimulationTick(0.1f);

            Assert.That((bool)InvokePrivate(brain, "eatStopRequested"), Is.True);
        }

        [Test]
        public void CriticalEatingContinuesUntilBelowCriticalThreshold()
        {
            ColonistBrain brain = CreateBrain(0f);
            CreateSchedule(3f, new DailyShiftWindow(2f, 6f), true);
            CreateFoodManagerAndService();
            ConfigureEatingLifecycle(brain, hunger: 95f);

            brain.SimulationTick(0.1f);
            Assert.That((bool)InvokePrivate(brain, "eatStopRequested"), Is.False);

            SetPrivateField(
                colonistObject.GetComponent<ColonistStatsComponent>(),
                "hunger",
                89f);
            brain.SimulationTick(0.1f);

            Assert.That((bool)InvokePrivate(brain, "eatStopRequested"), Is.True);
        }

        [Test]
        public void HungryColonistWithImminentWorkCanBeginEatingBeforeShift()
        {
            ColonistBrain brain = CreateBrain(0f);
            CreateSchedule(1.6f, new DailyShiftWindow(2f, 6f), true);
            CreateFoodManagerAndService();
            SetPrivateField(
                colonistObject.GetComponent<ColonistStatsComponent>(),
                "hunger",
                80f);

            brain.SimulationTick(0.1f);

            Assert.That(brain.State, Is.EqualTo(ColonistBrainState.EatSeeking));
            Assert.That(
                colonistObject.GetComponent<ColonistActivityRunner>().CurrentActivityId,
                Is.EqualTo("Eat"));
        }

        private void ResetOffDutyLifecycle(ColonistBrain brain)
        {
            SetPrivateField(brain, "state", ColonistBrainState.Idle);
            SetPrivateField(brain, "opportunityInProgress", null);
            SetPrivateField(brain, "offDutyStopRequested", false);
            SetPrivateField(brain, "activeOffDutyDrive", null);
            SetPrivateField(brain, "actualActiveOffDutyGameHours", 0f);
        }

        private void SetStimulationNeed(float value)
        {
            SetPrivateField(
                colonistObject.GetComponent<ColonistStatsComponent>(),
                "stimulationNeed",
                value);
        }

        private void SetRelaxationNeed(float value)
        {
            SetPrivateField(
                colonistObject.GetComponent<ColonistStatsComponent>(),
                "relaxationNeed",
                value);
        }

        private void CreateRecreationProvider(
            float stimulationRecovery,
            float relaxationRecovery,
            string cooldownKey)
        {
            if (offDutyManagerObject == null)
            {
                offDutyManagerObject = new GameObject("OffDuty Manager Test");
                offDutyManagerObject.AddComponent<OffDutyManager>();
            }

            GameObject recreationObject = new GameObject("Recreation Test " + cooldownKey);
            recreationObjects.Add(recreationObject);
            InteractableFacility facility =
                recreationObject.AddComponent<InteractableFacility>();
            Transform approach = new GameObject("Play Approach").transform;
            approach.SetParent(recreationObject.transform, false);
            FacilityActivityBinding facilityBinding = new FacilityActivityBinding();
            SetPrivateField(facilityBinding, "activityId", "play");
            SetPrivateField(facilityBinding, "reservationGroup", "Play01");
            SetPrivateField(facilityBinding, "externallyRequestable", true);
            SetPrivateField(facilityBinding, "approachAnchor", approach);
            SetPrivateField(facility, "activities", new[] { facilityBinding });

            OffDutyComponent provider = recreationObject.AddComponent<OffDutyComponent>();
            OffDutyActivityBinding activity = new OffDutyActivityBinding();
            SetPrivateField(activity, "activityId", "play");
            SetPrivateField(activity, "plannedDurationGameHours", 1f);
            SetPrivateField(activity, "enabled", true);
            SetPrivateField(activity, "cooldownGameHours", 12f);
            SetPrivateField(activity, "cooldownKey", cooldownKey);
            SetPrivateField(activity, "stimulationRecoveryPerGameHour", stimulationRecovery);
            SetPrivateField(activity, "relaxationRecoveryPerGameHour", relaxationRecovery);
            SetPrivateField(provider, "activities", new[] { activity });
        }

        private void ConfigureStaffedFoodServiceRequiringAWorker()
        {
            CreateFoodManagerAndService();

            staffedRole = ScriptableObject.CreateInstance<JobRoleDefinition>();
            SetPrivateField(staffedRole, "stableId", "cafeteria_worker");
            SetPrivateField(staffedRole, "displayName", "Cafeteria Worker");

            staffedWorkplaceObject = new GameObject("Cafeteria Workplace Test");
            InteractableFacility workplaceFacility =
                staffedWorkplaceObject.AddComponent<InteractableFacility>();
            Transform approach = new GameObject("Serve Approach").transform;
            approach.SetParent(staffedWorkplaceObject.transform, false);
            FacilityActivityBinding serveBinding = new FacilityActivityBinding();
            SetPrivateField(serveBinding, "activityId", "ServeFood");
            SetPrivateField(serveBinding, "reservationGroup", "CafeteriaWorker01");
            SetPrivateField(serveBinding, "externallyRequestable", true);
            SetPrivateField(serveBinding, "approachAnchor", approach);
            SetPrivateField(workplaceFacility, "activities", new[] { serveBinding });

            WorkplaceRoleBinding roleBinding = new WorkplaceRoleBinding();
            SetPrivateField(roleBinding, "role", staffedRole);
            SetPrivateField(roleBinding, "activityId", "ServeFood");
            SetPrivateField(roleBinding, "maximumConcurrentScheduledWorkers", 1);
            WorkplaceComponent workplace =
                staffedWorkplaceObject.AddComponent<WorkplaceComponent>();
            SetPrivateField(workplace, "roles", new[] { roleBinding });

            FoodServiceComponent service =
                foodFacilityObject.GetComponent<FoodServiceComponent>();
            SetPrivateField(service, "requiresStaff", true);
            SetPrivateField(service, "requiredWorkplace", workplace);
            SetPrivateField(service, "requiredRole", staffedRole);
            SetPrivateField(service, "minimumActiveWorkers", 1);
            SetPrivateField(service, "selfServicePolicy", FoodSelfServicePolicy.None);
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

        private void ConfigureEatingLifecycle(ColonistBrain brain, float hunger)
        {
            ColonistActivityRunner runner =
                colonistObject.GetComponent<ColonistActivityRunner>();
            InteractableFacility facility =
                foodFacilityObject.GetComponent<InteractableFacility>();
            Assert.That(
                facility.TryGetBinding("Eat", out FacilityActivityBinding binding),
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
            SetPrivateField(runner, "exitInProgress", false);
            SetPrivateProperty(runner, "Phase", ActivityPhase.Busy);

            SetPrivateField(
                colonistObject.GetComponent<ColonistStatsComponent>(),
                "hunger",
                hunger);
            SetPrivateField(
                brain,
                "eatTargetInProgress",
                new ActivityTarget(facility, "Eat"));
            SetPrivateField(brain, "eatStopRequested", false);
            SetPrivateField(brain, "state", ColonistBrainState.Eating);
        }

        private void ConfigurePendingEatRequest()
        {
            ColonistActivityRunner runner =
                colonistObject.GetComponent<ColonistActivityRunner>();
            InteractableFacility facility =
                foodFacilityObject.GetComponent<InteractableFacility>();
            Assert.That(
                facility.TryGetBinding("Eat", out FacilityActivityBinding binding),
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
            SetPrivateField(runner, "activityActive", false);
            SetPrivateField(runner, "exitInProgress", false);
            SetPrivateProperty(runner, "Phase", ActivityPhase.Reserved);
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
