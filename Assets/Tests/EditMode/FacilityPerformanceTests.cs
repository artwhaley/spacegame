using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    /// <summary>Test-only provider used to prove facility effects aggregate across providers.</summary>
    public class TestEffectProvider : MonoBehaviour, IFacilityPerformanceProvider
    {
        public FacilityEffectDefinition effect;
        public float multiplier = 1f;
        public string blocker;

        public void PublishPerformance(FacilityPerformanceSnapshot snapshot, float absoluteGameHour)
        {
            if (!string.IsNullOrEmpty(blocker))
                snapshot.AddBlocker(blocker);
            snapshot.Multiply(effect, multiplier);
        }
    }

    /// <summary>T03: staffing roles, count curves, and facility performance.</summary>
    [Category("Core")]
    public class FacilityPerformanceTests
    {
        private StaffingTestHarness harness;
        private SimulationManager clock;
        private StaffingManager manager;
        private ShiftPatternDefinition pattern;
        private FacilityEffectDefinition productionRate;
        private LocationAnchor home;
        private LocationAnchor workplaceLocation;
        private WorkerClassDefinition farmClass;
        private StaffingComponent farm;
        private FacilityPerformanceComponent performance;
        private StaffingRoleDefinition farmOperator;

        private const float OnDutyHour = 1f;   // Shift A
        private const float OffDutyHour = 9f;  // Shift B

        [SetUp]
        public void SetUp()
        {
            harness = new StaffingTestHarness();
            clock = harness.New("Simulation").AddComponent<SimulationManager>();
            manager = harness.New("Staffing Manager").AddComponent<StaffingManager>();
            pattern = harness.Pattern(("A", 0f, 8f), ("B", 8f, 8f));
            productionRate = harness.Effect("Production Rate");
            home = harness.New("Command Post").AddComponent<LocationAnchor>();
            home.displayName = "Command Post";
            workplaceLocation = harness.New("Farm").AddComponent<LocationAnchor>();
            workplaceLocation.displayName = "Farm";
            farmClass = harness.Class("Farm Technician");
            farmOperator = MakeRole(farmClass, 1, 3, productionRate, 0f, 0.65f, 1f, 1.2f);
            farm = harness.Facility("Farm", workplaceLocation, pattern, farmOperator);
            performance = farm.GetComponent<FacilityPerformanceComponent>();
        }

        [TearDown]
        public void TearDown()
        {
            harness.Dispose();
        }

        [Test]
        public void ZeroActiveWorkersBlocksFacility()
        {
            Assert.That(performance.IsOperational, Is.False);
            Assert.That(performance.GetMultiplier(productionRate), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(performance.BlockReasons.Count, Is.GreaterThan(0));
        }

        [Test]
        public void ActiveWorkerCountSelectsCurveValue()
        {
            Assert.That(MultiplierWith(0), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(MultiplierWith(1), Is.EqualTo(0.65f).Within(0.0001f));
            Assert.That(MultiplierWith(2), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(MultiplierWith(3), Is.EqualTo(1.2f).Within(0.0001f));
        }

        [Test]
        public void ActiveCountAboveCurveLengthClampsToFinalValue()
        {
            Assert.That(MultiplierWith(4), Is.EqualTo(1.2f).Within(0.0001f));
            Assert.That(performance.IsOperational, Is.True);
        }

        [Test]
        public void ResultDependsOnActiveCountNotWorkerIdentity()
        {
            List<ColonistAgent> workers = MakeActiveWorkers(3);
            Assert.That(performance.GetMultiplier(productionRate), Is.EqualTo(1.2f).Within(0.0001f));

            // Removing the first worker leaves two active regardless of which slot
            // they occupied; there is no slot-sliding behavior.
            workers[0].activity = ColonistActivity.Resting;
            Assert.That(performance.GetMultiplier(productionRate), Is.EqualTo(1f).Within(0.0001f));

            workers[1].activity = ColonistActivity.Resting;
            Assert.That(performance.GetMultiplier(productionRate), Is.EqualTo(0.65f).Within(0.0001f));
        }

        [Test]
        public void OptionalRoleWithNoActiveWorkersDoesNotBlock()
        {
            FacilityEffectDefinition speed = harness.Effect("Treatment Speed");
            StaffingRoleDefinition nurse = MakeRole(harness.Class("Nurse"), 0, 3, speed, 0.5f, 1f);
            StaffingComponent clinic = harness.Facility("Clinic", null, pattern, nurse);
            FacilityPerformanceComponent clinicPerformance = clinic.GetComponent<FacilityPerformanceComponent>();

            Assert.That(clinicPerformance.IsOperational, Is.True);
            Assert.That(clinicPerformance.GetMultiplier(speed), Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void DoctorAndNurseEffectChannelsRemainIndependent()
        {
            FacilityEffectDefinition outcome = harness.Effect("Treatment Outcome");
            FacilityEffectDefinition speed = harness.Effect("Treatment Speed");
            WorkerClassDefinition doctorClass = harness.Class("Doctor");
            WorkerClassDefinition nurseClass = harness.Class("Nurse");
            StaffingRoleDefinition doctor = MakeRole(doctorClass, 1, 3, outcome, 1f, 2f);
            StaffingRoleDefinition nurse = MakeRole(nurseClass, 0, 4, speed, 1f, 1.5f, 2f);
            StaffingComponent clinic = harness.Facility("Clinic", null, pattern, doctor, nurse);
            FacilityPerformanceComponent clinicPerformance = clinic.GetComponent<FacilityPerformanceComponent>();

            ColonistAgent doctorWorker = ActiveWorker(clinic, doctor, "A");
            Assert.That(clinicPerformance.GetMultiplier(outcome), Is.EqualTo(2f).Within(0.0001f));
            Assert.That(clinicPerformance.GetMultiplier(speed), Is.EqualTo(1f).Within(0.0001f));

            ActiveWorker(clinic, nurse, "A");
            Assert.That(clinicPerformance.GetMultiplier(outcome), Is.EqualTo(2f).Within(0.0001f));
            Assert.That(clinicPerformance.GetMultiplier(speed), Is.EqualTo(1.5f).Within(0.0001f));

            doctorWorker.currentEmployment = null;
            Assert.That(clinicPerformance.IsOperational, Is.False);
            // The Nurse is never reclassified as a Doctor.
            Assert.That(clinicPerformance.GetMultiplier(outcome), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(clinicPerformance.GetMultiplier(speed), Is.EqualTo(1.5f).Within(0.0001f));
        }

        [Test]
        public void SkillBonusScalesBaseContribution()
        {
            SkillDefinition agriculture = harness.Skill("Agriculture");
            StaffingRoleDefinition role = MakeRole(farmClass, 1, 3, productionRate, 0f, 0.65f);
            role.effects[0].bonusSkill = agriculture;
            role.effects[0].maxSkillBonusFraction = 0.2f;
            StaffingComponent workplace = harness.Facility("Greenhouse", null, pattern, role);
            FacilityPerformanceComponent facilityPerformance = workplace.GetComponent<FacilityPerformanceComponent>();

            ColonistAgent worker = ActiveWorker(workplace, role, "A");
            worker.skills.Add(new SkillRating { skill = agriculture, proficiency = 0.5f });

            Assert.That(facilityPerformance.GetMultiplier(productionRate), Is.EqualTo(0.715f).Within(0.0001f));
        }

        [Test]
        public void SkilledWrongClassWorkerContributesNothing()
        {
            SkillDefinition agriculture = harness.Skill("Agriculture");
            StaffingRoleDefinition role = MakeRole(farmClass, 1, 3, productionRate, 0f, 0.65f, 1f);
            role.effects[0].bonusSkill = agriculture;
            role.effects[0].maxSkillBonusFraction = 0.2f;
            StaffingComponent workplace = harness.Facility("Greenhouse", null, pattern, role);
            FacilityPerformanceComponent facilityPerformance = workplace.GetComponent<FacilityPerformanceComponent>();

            ColonistAgent skilledOutsider = harness.Colonist("Specialist", home);
            skilledOutsider.skills.Add(new SkillRating { skill = agriculture, proficiency = 1f });
            skilledOutsider.currentLocation = workplace.workplaceLocation;
            skilledOutsider.activity = ColonistActivity.Working;
            skilledOutsider.currentEmployment = new EmploymentAssignment(workplace, role, "A");
            manager.RegisterColonist(skilledOutsider);

            Assert.That(facilityPerformance.IsOperational, Is.False);
            Assert.That(facilityPerformance.GetMultiplier(productionRate), Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void MultipleProvidersMultiplyTheSameEffect()
        {
            MakeActiveWorkers(3);
            Assert.That(performance.GetMultiplier(productionRate), Is.EqualTo(1.2f).Within(0.0001f));

            TestEffectProvider upgrade = farm.gameObject.AddComponent<TestEffectProvider>();
            upgrade.effect = productionRate;
            upgrade.multiplier = 0.8f;

            Assert.That(performance.GetMultiplier(productionRate), Is.EqualTo(0.96f).Within(0.0001f));
        }

        [Test]
        public void DisabledAndReenabledProviderChangesNextEvaluation()
        {
            MakeActiveWorkers(3);
            TestEffectProvider upgrade = farm.gameObject.AddComponent<TestEffectProvider>();
            upgrade.effect = productionRate;
            upgrade.multiplier = 0.8f;

            Assert.That(performance.GetMultiplier(productionRate), Is.EqualTo(0.96f).Within(0.0001f));
            upgrade.enabled = false;
            Assert.That(performance.GetMultiplier(productionRate), Is.EqualTo(1.2f).Within(0.0001f));
            upgrade.enabled = true;
            Assert.That(performance.GetMultiplier(productionRate), Is.EqualTo(0.96f).Within(0.0001f));
        }

        [Test]
        public void ProviderBlockerMakesFacilityNonOperational()
        {
            MakeActiveWorkers(2);
            Assert.That(performance.IsOperational, Is.True);

            TestEffectProvider hazard = farm.gameObject.AddComponent<TestEffectProvider>();
            hazard.blocker = "Hull breach";

            Assert.That(performance.IsOperational, Is.False);
            Assert.That(performance.BlockSummary, Does.Contain("Hull breach"));
        }

        [Test]
        public void AutomatedFacilityWithoutProvidersIsOperationalAtOne()
        {
            FacilityPerformanceComponent automated =
                harness.New("Automated Processor").AddComponent<FacilityPerformanceComponent>();

            Assert.That(automated.IsOperational, Is.True);
            Assert.That(automated.GetMultiplier(productionRate), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void OnlyPresentOnDutyQualifiedWorkersAreActive()
        {
            ColonistAgent atHome = harness.Colonist("Home Farmer", home, farmClass);
            atHome.currentEmployment = new EmploymentAssignment(farm, farmOperator, "A");
            manager.RegisterColonist(atHome);
            Assert.That(farm.Query(farmOperator, "A", StaffingWorkerStage.Present, OnDutyHour).Count, Is.EqualTo(0));

            ColonistAgent offDuty = ActiveWorker(farm, farmOperator, "B");
            Assert.That(farm.Query(farmOperator, null, StaffingWorkerStage.Working, OnDutyHour).Count, Is.EqualTo(0));
            Assert.That(farm.Query(farmOperator, null, StaffingWorkerStage.Working, OffDutyHour).Count, Is.EqualTo(1));

            offDuty.currentEmployment = null;
            Assert.That(performance.IsOperational, Is.False);
        }

        [Test]
        public void ConcentratedShiftsGivePeakCoverageForHalfTheCycle()
        {
            ActiveWorker(farm, farmOperator, "A");
            ActiveWorker(farm, farmOperator, "A");

            harness.SetGameHour(clock, OnDutyHour);
            Assert.That(performance.IsOperational, Is.True);
            Assert.That(performance.GetMultiplier(productionRate), Is.EqualTo(1f).Within(0.0001f));

            harness.SetGameHour(clock, OffDutyHour);
            Assert.That(performance.IsOperational, Is.False);
            Assert.That(performance.GetMultiplier(productionRate), Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void StaggeredTwoShiftsLeaveTheThirdDailyWindowUncovered()
        {
            ActiveWorker(farm, farmOperator, "A");
            ActiveWorker(farm, farmOperator, "B");

            // 16h x 0.65 = 10.4 equivalent full-output hours beats the concentrated
            // 8h x 1.0 = 8, but the third daily window remains uncovered.
            harness.SetGameHour(clock, OnDutyHour);
            Assert.That(performance.GetMultiplier(productionRate), Is.EqualTo(0.65f).Within(0.0001f));
            Assert.That(performance.IsOperational, Is.True);

            harness.SetGameHour(clock, OffDutyHour);
            Assert.That(performance.GetMultiplier(productionRate), Is.EqualTo(0.65f).Within(0.0001f));
            Assert.That(performance.IsOperational, Is.True);

            harness.SetGameHour(clock, 17f);
            Assert.That(performance.IsOperational, Is.False);
        }

        [Test]
        public void ThreeDailyShiftsCanProvideFullDayCoverage()
        {
            StaffingComponent dailyFarm = harness.Facility(
                "Daily Farm", null, harness.Pattern(("A", 0f, 8f), ("B", 8f, 8f), ("C", 16f, 8f)), farmOperator);
            FacilityPerformanceComponent dailyPerformance = dailyFarm.GetComponent<FacilityPerformanceComponent>();
            ActiveWorker(dailyFarm, farmOperator, "A");
            ActiveWorker(dailyFarm, farmOperator, "B");
            ActiveWorker(dailyFarm, farmOperator, "C");

            harness.SetGameHour(clock, 1f);
            Assert.That(dailyPerformance.IsOperational, Is.True);
            harness.SetGameHour(clock, 9f);
            Assert.That(dailyPerformance.IsOperational, Is.True);
            harness.SetGameHour(clock, 17f);
            Assert.That(dailyPerformance.IsOperational, Is.True);
        }

        // ---------------- helpers ----------------

        private float MultiplierWith(int activeWorkers)
        {
            MakeActiveWorkers(activeWorkers);
            return performance.GetMultiplier(productionRate);
        }

        private List<ColonistAgent> MakeActiveWorkers(int count)
        {
            List<ColonistAgent> workers = new List<ColonistAgent>();
            for (int i = 0; i < count; i++)
                workers.Add(ActiveWorker(farm, farmOperator, "A"));
            return workers;
        }

        private ColonistAgent ActiveWorker(
            StaffingComponent workplace, StaffingRoleDefinition role, string shiftId)
        {
            ColonistAgent colonist = harness.Colonist($"Worker {workplace.Query(role, null, StaffingWorkerStage.Assigned, OnDutyHour).Count}", home);
            if (role.requiredClass != null)
                colonist.classes.Add(role.requiredClass);
            colonist.currentLocation = workplace.workplaceLocation;
            colonist.activity = ColonistActivity.Working;
            colonist.currentEmployment = new EmploymentAssignment(workplace, role, shiftId);
            manager.RegisterColonist(colonist);
            return colonist;
        }

        private StaffingRoleDefinition MakeRole(
            WorkerClassDefinition requiredClass, int minimum, int maximum,
            FacilityEffectDefinition effect, params float[] curve)
        {
            StaffingRoleDefinition role = harness.Asset<StaffingRoleDefinition>();
            role.displayName = "Role";
            role.requiredClass = requiredClass;
            role.minimumActiveForOperation = minimum;
            role.maximumAssignedPerShift = maximum;
            role.effects.Add(new StaffingEffectRule
            {
                effect = effect,
                multiplierByActiveCount = new List<float>(curve)
            });
            return role;
        }
    }
}
