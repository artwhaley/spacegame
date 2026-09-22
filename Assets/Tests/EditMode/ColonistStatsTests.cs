using System.Collections.Generic;
using System.Reflection;
using Colony.Interactions;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class ColonistStatsTests
    {
        private GameObject statsObject;
        private ColonistStatsComponent stats;
        private GameObject runnerObject;
        private GameObject foodFacilityObject;
        private GameObject offDutyFacilityObject;
        private GameObject logManagerObject;

        [SetUp]
        public void SetUp()
        {
            statsObject = new GameObject("Colonist Stats Test");
            stats = statsObject.AddComponent<ColonistStatsComponent>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(statsObject);
            if (runnerObject != null)
                Object.DestroyImmediate(runnerObject);
            if (foodFacilityObject != null)
                Object.DestroyImmediate(foodFacilityObject);
            if (offDutyFacilityObject != null)
                Object.DestroyImmediate(offDutyFacilityObject);
            if (logManagerObject != null)
                Object.DestroyImmediate(logManagerObject);
        }

        [Test]
        public void StimulationAndRelaxationAccumulateAtBaselineRates()
        {
            stats.SimulationTick(2f);

            Assert.That(stats.StimulationNeed, Is.EqualTo(8f).Within(0.0001f));
            Assert.That(stats.RelaxationNeed, Is.EqualTo(8f).Within(0.0001f));
        }

        [Test]
        public void LeisureDrivesClampAtZero()
        {
            SetStimulationNeed(3f);
            SetRelaxationNeed(3f);

            stats.AdjustStimulationNeed(-10f);
            stats.AdjustRelaxationNeed(-10f);

            Assert.That(stats.StimulationNeed, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(stats.RelaxationNeed, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void LeisureDrivesMayExceedTheirThresholds()
        {
            SetStimulationNeed(95f);
            SetRelaxationNeed(95f);

            stats.AdjustStimulationNeed(30f);
            stats.AdjustRelaxationNeed(30f);

            Assert.That(stats.StimulationNeed, Is.EqualTo(125f).Within(0.0001f));
            Assert.That(stats.RelaxationNeed, Is.EqualTo(125f).Within(0.0001f));
            Assert.That(stats.NeedsStimulation, Is.True);
            Assert.That(stats.NeedsRelaxation, Is.True);
        }

        [Test]
        public void LeisureThresholdsUseAuthoredValues()
        {
            Assert.That(
                stats.BaselineStimulationNeedPerGameHour,
                Is.EqualTo(4f).Within(0.0001f));
            Assert.That(
                stats.BaselineRelaxationNeedPerGameHour,
                Is.EqualTo(4f).Within(0.0001f));
            Assert.That(stats.StimulationNeedThreshold, Is.EqualTo(50f).Within(0.0001f));
            Assert.That(stats.RelaxationNeedThreshold, Is.EqualTo(50f).Within(0.0001f));

            SetStimulationNeed(49f);
            SetRelaxationNeed(49f);
            Assert.That(stats.NeedsStimulation, Is.False);
            Assert.That(stats.NeedsRelaxation, Is.False);

            SetStimulationNeed(50f);
            SetRelaxationNeed(50f);
            Assert.That(stats.NeedsStimulation, Is.True);
            Assert.That(stats.NeedsRelaxation, Is.True);
        }

        [Test]
        public void InvalidValuesCannotPoisonLeisureDrivesOrRates()
        {
            SetStimulationNeed(20f);
            SetRelaxationNeed(20f);

            stats.AdjustStimulationNeed(float.NaN);
            stats.AdjustStimulationNeed(float.PositiveInfinity);
            stats.AdjustStimulationNeed(float.NegativeInfinity);
            stats.AdjustRelaxationNeed(float.NaN);
            stats.AdjustRelaxationNeed(float.PositiveInfinity);
            stats.AdjustRelaxationNeed(float.NegativeInfinity);

            Assert.That(stats.StimulationNeed, Is.EqualTo(20f).Within(0.0001f));
            Assert.That(stats.RelaxationNeed, Is.EqualTo(20f).Within(0.0001f));
            Assert.That(
                stats.EffectiveStimulationPerGameHour,
                Is.EqualTo(4f).Within(0.0001f));
            Assert.That(
                stats.EffectiveRelaxationPerGameHour,
                Is.EqualTo(4f).Within(0.0001f));
        }

        [Test]
        public void LeisureThresholdTransitionsAreLoggedOnlyOnStateChange()
        {
            logManagerObject = new GameObject("Simulation Log Manager Test");
            SimulationLogManager log = logManagerObject.AddComponent<SimulationLogManager>();
            Assert.That(SimulationLogManager.Instance, Is.SameAs(log));

            SetStimulationNeed(50f);
            stats.AdjustStimulationNeed(0f);
            stats.AdjustStimulationNeed(10f);

            Assert.That(CountEntries("colonist.need.stimulation"), Is.EqualTo(1));

            SetStimulationNeed(0f);
            stats.AdjustStimulationNeed(0f);

            Assert.That(CountEntries("colonist.need.stimulation"), Is.EqualTo(2));
        }

        [Test]
        public void ActivePlayReducesStimulationNeed()
        {
            CreateRunnerAndStats();
            ActivateOffDutyActivity(
                stimulationRecovery: 60f,
                relaxationRecovery: 0f,
                activityActive: true,
                exitInProgress: false);
            SetStimulationNeed(100f);

            Assert.That(
                stats.EffectiveStimulationPerGameHour,
                Is.EqualTo(-56f).Within(0.0001f));
            stats.SimulationTick(1f);

            Assert.That(stats.StimulationNeed, Is.EqualTo(44f).Within(0.0001f));
        }

        [Test]
        public void ActiveRelaxingActivityReducesRelaxationNeedOnly()
        {
            CreateRunnerAndStats();
            ActivateOffDutyActivity(0f, 50f, activityActive: true, exitInProgress: false);
            SetStimulationNeed(100f);
            SetRelaxationNeed(100f);

            stats.SimulationTick(1f);

            Assert.That(stats.RelaxationNeed, Is.EqualTo(54f).Within(0.0001f));
            Assert.That(stats.StimulationNeed, Is.EqualTo(104f).Within(0.0001f));
        }

        [Test]
        public void ActivitySatisfyingBothDrivesAffectsBoth()
        {
            CreateRunnerAndStats();
            ActivateOffDutyActivity(10f, 30f, activityActive: true, exitInProgress: false);
            SetStimulationNeed(100f);
            SetRelaxationNeed(100f);

            stats.SimulationTick(1f);

            Assert.That(stats.StimulationNeed, Is.EqualTo(94f).Within(0.0001f));
            Assert.That(stats.RelaxationNeed, Is.EqualTo(74f).Within(0.0001f));
        }

        [Test]
        public void WalkingToPlayUsesBaselineLeisureRates()
        {
            CreateRunnerAndStats();
            ActivateOffDutyActivity(60f, 60f, activityActive: false, exitInProgress: false);
            SetStimulationNeed(100f);
            SetRelaxationNeed(100f);

            stats.SimulationTick(1f);

            Assert.That(stats.StimulationNeed, Is.EqualTo(104f).Within(0.0001f));
            Assert.That(stats.RelaxationNeed, Is.EqualTo(104f).Within(0.0001f));
        }

        [Test]
        public void ExitingPlayUsesBaselineLeisureRates()
        {
            CreateRunnerAndStats();
            ActivateOffDutyActivity(60f, 60f, activityActive: true, exitInProgress: true);
            SetStimulationNeed(100f);
            SetRelaxationNeed(100f);

            stats.SimulationTick(1f);

            Assert.That(stats.StimulationNeed, Is.EqualTo(104f).Within(0.0001f));
            Assert.That(stats.RelaxationNeed, Is.EqualTo(104f).Within(0.0001f));
        }

        [Test]
        public void DisabledOffDutyActivityUsesBaselineLeisureRates()
        {
            CreateRunnerAndStats();
            ActivateOffDutyActivity(
                60f,
                60f,
                activityActive: true,
                exitInProgress: false,
                activityEnabled: false);
            SetStimulationNeed(100f);
            SetRelaxationNeed(100f);

            stats.SimulationTick(1f);

            Assert.That(stats.StimulationNeed, Is.EqualTo(104f).Within(0.0001f));
            Assert.That(stats.RelaxationNeed, Is.EqualTo(104f).Within(0.0001f));
        }

        [Test]
        public void ActivityWithoutOffDutyBindingUsesBaselineLeisureRates()
        {
            CreateRunnerAndStats();
            ColonistActivityRunner runner = runnerObject.GetComponent<ColonistActivityRunner>();
            FacilityActivityBinding sleep = CreateSleepBinding();
            SetRunnerState(runner, sleep, activityActive: true, exitInProgress: false);
            SetStimulationNeed(100f);
            SetRelaxationNeed(100f);

            stats.SimulationTick(1f);

            Assert.That(stats.StimulationNeed, Is.EqualTo(104f).Within(0.0001f));
            Assert.That(stats.RelaxationNeed, Is.EqualTo(104f).Within(0.0001f));
        }


        [Test]
        public void BaselineFatigueAccumulatesOverGameHours()
        {
            SetFatigue(20f);

            stats.SimulationTick(2f);

            Assert.That(stats.Fatigue, Is.EqualTo(30f).Within(0.0001f));
        }

        [Test]
        public void FatigueCannotFallBelowZero()
        {
            SetFatigue(3f);

            stats.AdjustFatigue(-10f);

            Assert.That(stats.Fatigue, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void FatigueMayExceedOneHundred()
        {
            SetFatigue(95f);

            stats.AdjustFatigue(30f);

            Assert.That(stats.Fatigue, Is.EqualTo(125f).Within(0.0001f));
        }

        [Test]
        public void SleepyThresholdUsesSeventyPoints()
        {
            SetFatigue(69f);
            Assert.That(stats.IsSleepy, Is.False);

            SetFatigue(70f);
            Assert.That(stats.IsSleepy, Is.True);
        }

        [Test]
        public void ExhaustionThresholdUsesOneHundredPoints()
        {
            SetFatigue(99f);
            Assert.That(stats.IsExhausted, Is.False);

            SetFatigue(100f);
            Assert.That(stats.IsExhausted, Is.True);

            SetFatigue(130f);
            Assert.That(stats.IsExhausted, Is.True);
        }

        [Test]
        public void InvalidValuesCannotPoisonFatigueOrRate()
        {
            SetFatigue(20f);

            stats.AdjustFatigue(float.NaN);
            stats.AdjustFatigue(float.PositiveInfinity);
            stats.AdjustFatigue(float.NegativeInfinity);

            Assert.That(stats.Fatigue, Is.EqualTo(20f).Within(0.0001f));
            Assert.That(stats.EffectiveFatiguePerGameHour, Is.EqualTo(5f).Within(0.0001f));
        }

        [Test]
        public void BaselineHungerAccumulatesOverGameHours()
        {
            SetHunger(20f);

            stats.SimulationTick(2f);

            Assert.That(stats.Hunger, Is.EqualTo(30f).Within(0.0001f));
        }

        [Test]
        public void HungerCannotFallBelowZero()
        {
            SetHunger(3f);

            stats.AdjustHunger(-1000f);

            Assert.That(stats.Hunger, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void HungerIsCappedAtStarvationThreshold()
        {
            SetHunger(95f);

            stats.AdjustHunger(1000f);

            Assert.That(stats.Hunger, Is.EqualTo(100f).Within(0.0001f));
        }

        [Test]
        public void SerializedHungerIsClampedAtRuntimeInitialization()
        {
            SetHunger(150f);

            InvokePrivate(stats, "Awake");

            Assert.That(stats.Hunger, Is.EqualTo(100f).Within(0.0001f));
        }

        [Test]
        public void HungryThresholdUsesSixtyPoints()
        {
            SetHunger(59f);
            Assert.That(stats.IsHungry, Is.False);

            SetHunger(60f);
            Assert.That(stats.IsHungry, Is.True);
        }

        [Test]
        public void StarvationThresholdUsesOneHundredPoints()
        {
            SetHunger(99f);
            Assert.That(stats.IsStarving, Is.False);

            SetHunger(100f);
            Assert.That(stats.IsStarving, Is.True);

            SetHunger(130f);
            Assert.That(stats.IsStarving, Is.True);
        }

        [Test]
        public void InvalidValuesCannotPoisonHungerOrRate()
        {
            SetHunger(20f);

            stats.AdjustHunger(float.NaN);
            stats.AdjustHunger(float.PositiveInfinity);
            stats.AdjustHunger(float.NegativeInfinity);

            Assert.That(stats.Hunger, Is.EqualTo(20f).Within(0.0001f));
            Assert.That(stats.EffectiveHungerPerGameHour, Is.EqualTo(5f).Within(0.0001f));
        }

        [Test]
        public void InvalidSimulationDeltaDoesNotChangePhysiology()
        {
            SetFatigue(20f);
            SetHunger(30f);

            stats.SimulationTick(0f);
            stats.SimulationTick(float.NaN);
            stats.SimulationTick(float.PositiveInfinity);

            Assert.That(stats.Fatigue, Is.EqualTo(20f).Within(0.0001f));
            Assert.That(stats.Hunger, Is.EqualTo(30f).Within(0.0001f));
        }

        [Test]
        public void ActiveSleepDoesNotRecoverHungerInProduction()
        {
            CreateRunnerAndStats();
            FacilityActivityBinding sleep = CreateSleepBinding();
            SetRunnerState(runnerObject.GetComponent<ColonistActivityRunner>(), sleep,
                activityActive: true, exitInProgress: false);
            SetHunger(50f);

            stats.SimulationTick(1f);

            Assert.That(stats.Hunger, Is.EqualTo(58f).Within(0.0001f));
        }

        [Test]
        public void ActiveFacilityIsExposedOnlyDuringGenuineActivity()
        {
            ColonistActivityRunner runner = CreateRunner();
            InteractableFacility facility = CreateFoodFacility(
                out _, out FacilityActivityBinding binding);
            SetPrivateField(runner, "currentFacility", facility);
            SetRunnerState(runner, binding, activityActive: true, exitInProgress: false);

            Assert.That(runner.ActiveFacility, Is.SameAs(facility));

            SetPrivateField(runner, "exitInProgress", true);
            Assert.That(runner.ActiveFacility, Is.Null);
        }

        [Test]
        public void ActiveEatUsesConfiguredFoodRecoveryRate()
        {
            CreateRunnerAndStats();
            InteractableFacility facility = CreateFoodFacility(
                out _, out FacilityActivityBinding binding);
            ColonistActivityRunner runner = runnerObject.GetComponent<ColonistActivityRunner>();
            SetPrivateField(runner, "currentFacility", facility);
            SetRunnerState(runner, binding, activityActive: true, exitInProgress: false);
            SetHunger(50f);

            Assert.That(stats.EffectiveHungerPerGameHour, Is.EqualTo(-60f).Within(0.0001f));
            stats.SimulationTick(1f);

            Assert.That(stats.Hunger, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void EatEntryAndExitUseBaselineHungerRate()
        {
            CreateRunnerAndStats();
            InteractableFacility facility = CreateFoodFacility(
                out _, out FacilityActivityBinding binding);
            ColonistActivityRunner runner = runnerObject.GetComponent<ColonistActivityRunner>();
            SetPrivateField(runner, "currentFacility", facility);
            SetRunnerState(runner, binding, activityActive: false, exitInProgress: false);
            SetHunger(50f);

            stats.SimulationTick(1f);
            Assert.That(stats.Hunger, Is.EqualTo(58f).Within(0.0001f));

            SetPrivateField(runner, "activityActive", true);
            SetPrivateField(runner, "exitInProgress", true);
            SetHunger(50f);
            stats.SimulationTick(1f);

            Assert.That(stats.Hunger, Is.EqualTo(58f).Within(0.0001f));
        }

        [Test]
        public void DisabledFoodServiceCannotControlHunger()
        {
            CreateRunnerAndStats();
            InteractableFacility facility = CreateFoodFacility(
                out FoodServiceComponent service,
                out FacilityActivityBinding binding);
            service.enabled = false;
            ColonistActivityRunner runner = runnerObject.GetComponent<ColonistActivityRunner>();
            SetPrivateField(runner, "currentFacility", facility);
            SetRunnerState(runner, binding, activityActive: true, exitInProgress: false);
            SetHunger(50f);

            stats.SimulationTick(1f);

            Assert.That(stats.Hunger, Is.EqualTo(58f).Within(0.0001f));
        }

        [Test]
        public void ColonistActivityRunnerBindingIsNotActiveBeforeActivation()
        {
            ColonistActivityRunner runner = CreateRunner();
            FacilityActivityBinding sleep = CreateSleepBinding();
            SetRunnerState(runner, sleep, activityActive: false, exitInProgress: false);

            Assert.That(runner.IsActivityActive, Is.False);
            Assert.That(runner.ActiveActivityBinding, Is.Null);
        }

        [Test]
        public void EmptyFacilityActivityBindingWorksForStatsInspection()
        {
            ColonistActivityRunner runner = CreateRunner();
            FacilityActivityBinding empty = new FacilityActivityBinding();
            SetRunnerState(runner, empty, activityActive: false, exitInProgress: false);

            Assert.That(runner.IsActivityActive, Is.False);
            Assert.That(runner.ActiveActivityBinding, Is.Null);
        }

        [Test]
        public void BindingIsNotActiveBeforeActivation()
        {
            ColonistActivityRunner runner = CreateRunner();
            FacilityActivityBinding sleep = CreateSleepBinding();
            SetRunnerState(runner, sleep, activityActive: false, exitInProgress: false);

            Assert.That(runner.IsActivityActive, Is.False);
            Assert.That(runner.ActiveActivityBinding, Is.Null);
        }

        [Test]
        public void ActiveSleepIsExposed()
        {
            ColonistActivityRunner runner = CreateRunner();
            FacilityActivityBinding sleep = CreateSleepBinding();
            SetRunnerState(runner, sleep, activityActive: true, exitInProgress: false);

            Assert.That(runner.IsActivityActive, Is.True);
            Assert.That(runner.ActiveActivityBinding, Is.SameAs(sleep));
            Assert.That(runner.ActiveActivityId, Is.EqualTo("Sleep"));
        }

        [Test]
        public void ExitingSleepIsNotExposedAsActive()
        {
            ColonistActivityRunner runner = CreateRunner();
            FacilityActivityBinding sleep = CreateSleepBinding();
            SetRunnerState(runner, sleep, activityActive: true, exitInProgress: true);

            Assert.That(runner.CurrentActivityId, Is.EqualTo("Sleep"));
            Assert.That(runner.IsActivityActive, Is.False);
            Assert.That(runner.ActiveActivityBinding, Is.Null);
            Assert.That(runner.ActiveActivityId, Is.Null);
        }

        [Test]
        public void ActiveSleepUsesAuthoredFatigueRate()
        {
            CreateRunnerAndStats();
            FacilityActivityBinding sleep = CreateSleepBinding();
            SetRunnerState(runnerObject.GetComponent<ColonistActivityRunner>(), sleep,
                activityActive: true, exitInProgress: false);
            SetFatigue(50f);

            stats.SimulationTick(1f);

            Assert.That(stats.Fatigue, Is.EqualTo(40f).Within(0.0001f));
        }

        [Test]
        public void SleepEntryUsesBaselineFatigueRate()
        {
            CreateRunnerAndStats();
            FacilityActivityBinding sleep = CreateSleepBinding();
            SetRunnerState(runnerObject.GetComponent<ColonistActivityRunner>(), sleep,
                activityActive: false, exitInProgress: false);
            SetFatigue(50f);

            stats.SimulationTick(1f);

            Assert.That(stats.Fatigue, Is.EqualTo(55f).Within(0.0001f));
        }

        [Test]
        public void SleepExitUsesBaselineFatigueRateImmediately()
        {
            CreateRunnerAndStats();
            FacilityActivityBinding sleep = CreateSleepBinding();
            SetRunnerState(runnerObject.GetComponent<ColonistActivityRunner>(), sleep,
                activityActive: true, exitInProgress: true);
            SetFatigue(50f);

            stats.SimulationTick(1f);

            Assert.That(stats.Fatigue, Is.EqualTo(55f).Within(0.0001f));
        }

        private void ActivateOffDutyActivity(
            float stimulationRecovery,
            float relaxationRecovery,
            bool activityActive,
            bool exitInProgress,
            bool activityEnabled = true)
        {
            ColonistActivityRunner runner = runnerObject.GetComponent<ColonistActivityRunner>();
            FacilityActivityBinding binding = CreateOffDutyFacility(
                stimulationRecovery,
                relaxationRecovery,
                activityEnabled);
            SetPrivateField(
                runner,
                "currentFacility",
                offDutyFacilityObject.GetComponent<InteractableFacility>());
            SetRunnerState(runner, binding, activityActive, exitInProgress);
        }

        private FacilityActivityBinding CreateOffDutyFacility(
            float stimulationRecovery,
            float relaxationRecovery,
            bool activityEnabled)
        {
            offDutyFacilityObject = new GameObject("Recreation Facility Test");
            InteractableFacility facility =
                offDutyFacilityObject.AddComponent<InteractableFacility>();
            Transform approach = new GameObject("Recreation Approach").transform;
            approach.SetParent(offDutyFacilityObject.transform, false);
            FacilityActivityBinding facilityBinding = new FacilityActivityBinding();
            SetPrivateField(facilityBinding, "activityId", "play");
            SetPrivateField(facilityBinding, "reservationGroup", "Play01");
            SetPrivateField(facilityBinding, "externallyRequestable", true);
            SetPrivateField(facilityBinding, "approachAnchor", approach);
            SetPrivateField(facility, "activities", new[] { facilityBinding });

            OffDutyComponent provider = offDutyFacilityObject.AddComponent<OffDutyComponent>();
            OffDutyActivityBinding activity = new OffDutyActivityBinding();
            SetPrivateField(activity, "activityId", "play");
            SetPrivateField(activity, "plannedDurationGameHours", 1f);
            SetPrivateField(activity, "enabled", activityEnabled);
            SetPrivateField(activity, "cooldownGameHours", 12f);
            SetPrivateField(activity, "cooldownKey", "play");
            SetPrivateField(activity, "stimulationRecoveryPerGameHour", stimulationRecovery);
            SetPrivateField(activity, "relaxationRecoveryPerGameHour", relaxationRecovery);
            SetPrivateField(provider, "activities", new[] { activity });
            return facilityBinding;
        }

        private static int CountEntries(string eventKey)
        {
            if (SimulationLogManager.Instance == null)
                return 0;

            int count = 0;
            IReadOnlyList<SimulationLogEntry> entries = SimulationLogManager.Instance.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index] != null &&
                    string.Equals(
                        entries[index].EventKey,
                        eventKey,
                        System.StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private ColonistActivityRunner CreateRunner()
        {
            runnerObject = new GameObject("Colonist Activity Runner Test");
            return runnerObject.AddComponent<ColonistActivityRunner>();
        }

        private void CreateRunnerAndStats()
        {
            runnerObject = new GameObject("Colonist Activity Stats Test");
            ColonistActivityRunner runner =
                runnerObject.AddComponent<ColonistActivityRunner>();
            stats = runnerObject.AddComponent<ColonistStatsComponent>();
            SetPrivateField(stats, "activityRunner", runner);
        }

        private static FacilityActivityBinding CreateSleepBinding()
        {
            FacilityActivityBinding binding = new FacilityActivityBinding();
            SetPrivateField(binding, "activityId", "Sleep");
            SetPrivateField(binding, "overridesFatigueRate", true);
            SetPrivateField(binding, "fatiguePerGameHour", -10f);
            return binding;
        }

        private InteractableFacility CreateFoodFacility(
            out FoodServiceComponent service,
            out FacilityActivityBinding binding)
        {
            foodFacilityObject = new GameObject("Food Facility Test");
            InteractableFacility facility =
                foodFacilityObject.AddComponent<InteractableFacility>();
            Transform approach = new GameObject("Food Approach").transform;
            approach.SetParent(foodFacilityObject.transform, false);
            binding = new FacilityActivityBinding();
            SetPrivateField(binding, "activityId", "Eat");
            SetPrivateField(binding, "reservationGroup", "Eat01");
            SetPrivateField(binding, "externallyRequestable", true);
            SetPrivateField(binding, "approachAnchor", approach);
            SetPrivateField(facility, "activities", new[] { binding });
            service = foodFacilityObject.AddComponent<FoodServiceComponent>();
            SetPrivateField(service, "facility", facility);
            SetPrivateField(service, "eatActivityId", "Eat");
            SetPrivateField(service, "hungerRecoveryPerGameHour", 60f);
            return facility;
        }

        private static void SetRunnerState(
            ColonistActivityRunner runner,
            FacilityActivityBinding binding,
            bool activityActive,
            bool exitInProgress)
        {
            SetPrivateField(runner, "currentBinding", binding);
            SetPrivateField(runner, "activityActive", activityActive);
            SetPrivateField(runner, "exitInProgress", exitInProgress);
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

        private void SetFatigue(float value)
        {
            FieldInfo fatigue = typeof(ColonistStatsComponent).GetField(
                "fatigue", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(fatigue, Is.Not.Null);
            fatigue.SetValue(stats, value);
        }

        private void SetHunger(float value)
        {
            FieldInfo hunger = typeof(ColonistStatsComponent).GetField(
                "hunger", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(hunger, Is.Not.Null);
            hunger.SetValue(stats, value);
        }

        private void SetStimulationNeed(float value)
        {
            FieldInfo field = typeof(ColonistStatsComponent).GetField(
                "stimulationNeed", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(stats, value);
        }

        private void SetRelaxationNeed(float value)
        {
            FieldInfo field = typeof(ColonistStatsComponent).GetField(
                "relaxationNeed", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(stats, value);
        }
    }
}
