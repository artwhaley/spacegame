using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    /// <summary>T04: employment assignment, shift scheduling, and physical commutes.</summary>
    public class StaffingManagerTests
    {
        private StaffingTestHarness harness;
        private SimulationManager clock;
        private ContractManager contracts;
        private StaffingManager manager;
        private ShiftPatternDefinition pattern;
        private FacilityEffectDefinition productionRate;
        private LocationAnchor home;
        private WorkerClassDefinition farmClass;
        private StaffingComponent farm;
        private StaffingRoleDefinition farmOperator;

        private const float OnDutyHour = 1f;
        private const float OffDutyHour = 9f;

        [SetUp]
        public void SetUp()
        {
            harness = new StaffingTestHarness();
            clock = harness.New("Simulation").AddComponent<SimulationManager>();
            contracts = harness.New("Contracts").AddComponent<ContractManager>();
            manager = harness.New("Staffing Manager").AddComponent<StaffingManager>();
            pattern = harness.Pattern(("A", 0f, 8f), ("B", 8f, 8f));
            productionRate = harness.Effect("Production Rate");
            home = harness.New("Command Post").AddComponent<LocationAnchor>();
            home.displayName = "Command Post";
            farmClass = harness.Class("Farm Technician");
            farmOperator = MakeRole(farmClass, 1, 3, productionRate, 0f, 0.65f, 1f, 1.2f);
            farm = harness.Facility("Farm", null, pattern, farmOperator);
        }

        [TearDown]
        public void TearDown()
        {
            harness.Dispose();
        }

        [Test]
        public void WrongClassAssignmentIsRejected()
        {
            ColonistAgent outsider = harness.Colonist("Medic", home);

            AssignmentResult result = manager.Assign(outsider, farm, farmOperator, "A");
            Assert.That(result, Is.EqualTo(AssignmentResult.RejectedMissingClass));
            Assert.That(outsider.currentEmployment, Is.Null);
        }

        [Test]
        public void UnknownShiftIsRejected()
        {
            ColonistAgent farmer = harness.Colonist("Farmer", home, farmClass);

            AssignmentResult result = manager.Assign(farmer, farm, farmOperator, "Z");
            Assert.That(result, Is.EqualTo(AssignmentResult.RejectedShiftUnknown));
        }

        [Test]
        public void RoleNotOfferedIsRejected()
        {
            StaffingRoleDefinition otherRole = MakeRole(farmClass, 1, 3, productionRate, 0f, 1f);
            ColonistAgent farmer = harness.Colonist("Farmer", home, farmClass);

            Assert.That(manager.Assign(farmer, farm, otherRole, "A"),
                Is.EqualTo(AssignmentResult.RejectedRoleNotOffered));
        }

        [Test]
        public void CapacityIsEnforcedPerRoleAndShift()
        {
            for (int i = 0; i < 3; i++)
                Assert.That(manager.Assign(harness.Colonist($"Farmer {i}", home, farmClass), farm, farmOperator, "A"),
                    Is.EqualTo(AssignmentResult.Applied));

            ColonistAgent extra = harness.Colonist("Farmer 3", home, farmClass);
            Assert.That(manager.Assign(extra, farm, farmOperator, "A"),
                Is.EqualTo(AssignmentResult.RejectedAtCapacity));
            Assert.That(extra.currentEmployment, Is.Null);
        }

        [Test]
        public void AtHomeOffShiftWorkerRests()
        {
            harness.SetGameHour(clock, OffDutyHour);
            ColonistAgent farmer = AssignFarmer("Farmer", "A");

            manager.SimulationTick(1f);
            Assert.That(farmer.activity, Is.EqualTo(ColonistActivity.Resting));
            Assert.That(farmer.Status.CurrentDutyState, Is.EqualTo(ColonistDutyState.ReleasedResting));
            Assert.That(PassengerContractCount(), Is.EqualTo(0));
        }

        [Test]
        public void OnDutyAtHomeWorkerWaitsAndRequestsCommute()
        {
            ColonistAgent farmer = AssignFarmer("Farmer", "A");
            harness.SetGameHour(clock, OnDutyHour);

            manager.SimulationTick(1f);

            Assert.That(farmer.activity, Is.EqualTo(ColonistActivity.WaitingForTransport));
            Assert.That(farmer.Status.CurrentDutyState, Is.EqualTo(ColonistDutyState.ScheduledShift));
            Assert.That(PassengerContractCount(), Is.EqualTo(1));
            TransportContract contract = FirstPassengerContract();
            Assert.That(contract.sourceLocation, Is.EqualTo(home));
            Assert.That(contract.destinationLocation, Is.EqualTo(farm.workplaceLocation));
            Assert.That(contract.passengers.Count, Is.EqualTo(1));
        }

        [Test]
        public void OnDutyAtWorkplaceWorkerBecomesWorking()
        {
            harness.SetGameHour(clock, OnDutyHour);
            ColonistAgent farmer = AssignFarmerAtWork("Farmer", "A");

            manager.SimulationTick(1f);

            Assert.That(farmer.activity, Is.EqualTo(ColonistActivity.Working));
            Assert.That(farmer.Status.CurrentDutyState, Is.EqualTo(ColonistDutyState.AcceptingNewWork));
            farm.RefreshDebugRows(OnDutyHour);
            Assert.That(farm.DebugRows[0].activeQualified, Is.EqualTo(1));
        }

        [Test]
        public void OffDutyAtWorkplaceWorkerRequestsReturn()
        {
            harness.SetGameHour(clock, OffDutyHour);
            ColonistAgent farmer = AssignFarmerAtWork("Farmer", "A");

            manager.SimulationTick(1f);

            Assert.That(farmer.activity, Is.EqualTo(ColonistActivity.WaitingForTransport));
            Assert.That(farmer.Status.CurrentDutyState, Is.EqualTo(ColonistDutyState.ReturningHome));
            TransportContract contract = FirstPassengerContract();
            Assert.That(contract.sourceLocation, Is.EqualTo(farm.workplaceLocation));
            Assert.That(contract.destinationLocation, Is.EqualTo(home));
        }

        [Test]
        public void LateArrivalWorksOnlyTheRemainingShift()
        {
            ColonistAgent farmer = AssignFarmer("Farmer", "A");
            harness.SetGameHour(clock, OnDutyHour);
            manager.SimulationTick(1f);
            Assert.That(farmer.activity, Is.EqualTo(ColonistActivity.WaitingForTransport));
            Assert.That(farmer.currentLocation, Is.EqualTo(home));
        }

        [Test]
        public void ArrivingAfterShiftEndedDoesNotGrantWork()
        {
            harness.SetGameHour(clock, OffDutyHour);
            ColonistAgent farmer = AssignFarmerAtWork("Farmer", "A");

            manager.SimulationTick(1f);

            Assert.That(farmer.activity, Is.Not.EqualTo(ColonistActivity.Working));
            Assert.That(FirstPassengerContract().destinationLocation, Is.EqualTo(home));
        }

        [Test]
        public void ReassignmentWhileWorkingAppliesImmediately()
        {
            StaffingComponent secondFarm = harness.Facility("Farm 2", null, pattern, farmOperator);
            harness.SetGameHour(clock, OnDutyHour);
            ColonistAgent farmer = AssignFarmerAtWork("Farmer", "A");
            manager.SimulationTick(0f);
            Assert.That(farmer.activity, Is.EqualTo(ColonistActivity.Working));

            AssignmentResult result = manager.Assign(farmer, secondFarm, farmOperator, "A");

            Assert.That(result, Is.EqualTo(AssignmentResult.Applied));
            Assert.That(farmer.currentEmployment.workplace, Is.EqualTo(secondFarm));
            Assert.That(farmer.activity, Is.EqualTo(ColonistActivity.WaitingForTransport));
        }

        [Test]
        public void ReassignmentDoesNotTeleportWorker()
        {
            StaffingComponent secondFarm = harness.Facility("Farm 2", null, pattern, farmOperator);
            harness.SetGameHour(clock, OnDutyHour);
            ColonistAgent farmer = AssignFarmerAtWork("Farmer", "A");
            manager.Assign(farmer, secondFarm, farmOperator, "A");

            Assert.That(farmer.currentEmployment.workplace, Is.EqualTo(secondFarm));
            Assert.That(farmer.currentLocation, Is.EqualTo(farm.workplaceLocation));
        }

        [Test]
        public void UnassignWhileAwayClearsImmediately()
        {
            harness.SetGameHour(clock, OnDutyHour);
            ColonistAgent farmer = AssignFarmerAtWork("Farmer", "A");

            Assert.That(manager.Unassign(farmer), Is.EqualTo(AssignmentResult.Applied));
            Assert.That(farmer.currentEmployment, Is.Null);
        }

        [Test]
        public void TwoWorkersSameRouteAreGroupedIntoOneContract()
        {
            harness.SetGameHour(clock, OffDutyHour);
            AssignFarmer("Farmer 1", "A");
            AssignFarmer("Farmer 2", "A");
            harness.SetGameHour(clock, OnDutyHour);

            manager.SimulationTick(1f);

            Assert.That(PassengerContractCount(), Is.EqualTo(1));
            Assert.That(FirstPassengerContract().passengers.Count, Is.EqualTo(2));
        }

        [Test]
        public void OppositeShiftsUseDifferentCommuteWindows()
        {
            ColonistAgent shiftA = AssignFarmer("Farmer A", "A");
            ColonistAgent shiftB = AssignFarmer("Farmer B", "B");

            harness.SetGameHour(clock, OnDutyHour);
            manager.SimulationTick(1f);
            Assert.That(shiftA.activity, Is.EqualTo(ColonistActivity.WaitingForTransport));
            Assert.That(shiftB.activity, Is.EqualTo(ColonistActivity.Resting));
            Assert.That(PassengerContractCount(), Is.EqualTo(1));

            harness.SetGameHour(clock, OffDutyHour);
            manager.SimulationTick(1f);
            Assert.That(shiftB.activity, Is.EqualTo(ColonistActivity.WaitingForTransport));
            Assert.That(ContractedPassenger(shiftB), Is.True);
            // The first flight remains immutable until a real vehicle unloads it.
            Assert.That(ContractedPassenger(shiftA), Is.True);
        }

        [Test]
        public void FatigueAtNinetyPercentStopsWorkImmediately()
        {
            harness.SetGameHour(clock, OnDutyHour);
            ColonistAgent farmer = AssignFarmerAtWork("Exhausted Farmer", "A");
            ColonistStatusComponent status = farmer.GetComponent<ColonistStatusComponent>();
            status.AdjustFatigue(0.8f);

            manager.SimulationTick(1f);

            Assert.That(status.Fatigue, Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(status.IsExhausted, Is.True);
            Assert.That(farmer.activity, Is.EqualTo(ColonistActivity.WaitingForTransport));
            Assert.That(farm.GetActiveQualifiedWorkers(farmOperator, "A").Count, Is.EqualTo(0));
            Assert.That(status.DutyHistory[status.DutyHistory.Count - 1].endReason,
                Is.EqualTo(DutyEndReason.Exhausted));
        }

        [Test]
        public void DailyScheduleTableIncludesAssignedAndUnassignedColonists()
        {
            ColonistAgent assigned = AssignFarmer("Zed Farmer", "A");
            ColonistAgent unassigned = harness.Colonist("Ada Resident", home);

            string table = manager.BuildDailyScheduleTable(17f);

            Assert.That(table, Does.Contain("Colonist | Home | Workplace | Role | Shift | Work | Off duty | Now | Duty state"));
            Assert.That(table, Does.Contain("Ada Resident"));
            Assert.That(table, Does.Contain("Zed Farmer"));
            Assert.That(table, Does.Contain("00:00-08:00"));
            Assert.That(table, Does.Contain("08:00-24:00"));
            Assert.That(assigned.currentEmployment, Is.Not.Null);
            Assert.That(unassigned.currentEmployment, Is.Null);
        }

        // ---------------- helpers ----------------

        private ColonistAgent AssignFarmer(string name, string shiftId)
        {
            ColonistAgent farmer = harness.Colonist(name, home, farmClass);
            manager.Assign(farmer, farm, farmOperator, shiftId);
            return farmer;
        }

        private ColonistAgent AssignFarmerAtWork(string name, string shiftId)
        {
            ColonistAgent farmer = harness.Colonist(name, home, farmClass);
            farmer.currentLocation = farm.workplaceLocation;
            Assert.That(manager.Assign(farmer, farm, farmOperator, shiftId), Is.EqualTo(AssignmentResult.Applied));
            return farmer;
        }

        private int PassengerContractCount()
        {
            int count = 0;
            IReadOnlyList<TransportContract> all = contracts.Contracts;
            for (int i = 0; i < all.Count; i++)
                if (all[i].type == TransportContractType.Passenger)
                    count++;
            return count;
        }

        private TransportContract FirstPassengerContract()
        {
            IReadOnlyList<TransportContract> all = contracts.Contracts;
            for (int i = 0; i < all.Count; i++)
                if (all[i].type == TransportContractType.Passenger)
                    return all[i];
            return null;
        }

        private bool ContractedPassenger(ColonistAgent colonist)
        {
            IReadOnlyList<TransportContract> all = contracts.Contracts;
            for (int i = 0; i < all.Count; i++)
                if (all[i].type == TransportContractType.Passenger && all[i].passengers.Contains(colonist))
                    return true;
            return false;
        }

        private StaffingRoleDefinition MakeRole(
            WorkerClassDefinition requiredClass, int minimum, int maximum,
            FacilityEffectDefinition effect, params float[] curve)
        {
            StaffingRoleDefinition role = harness.Asset<StaffingRoleDefinition>();
            role.displayName = "Farm Operator";
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
