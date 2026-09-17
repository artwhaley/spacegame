using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class PilotDutyTests
    {
        private StaffingTestHarness harness;
        private SimulationManager clock;
        private StaffingManager staffing;
        private ContractManager contracts;
        private LocationAnchor home;
        private WorkerClassDefinition pilotClass;
        private StaffingRoleDefinition pilotRole;
        private ShiftPatternDefinition pattern;
        private StaffingComponent crewStaffing;
        private ShipComponent ship;

        [SetUp]
        public void SetUp()
        {
            harness = new StaffingTestHarness();
            clock = harness.New("Simulation").AddComponent<SimulationManager>();
            staffing = harness.New("Staffing").AddComponent<StaffingManager>();
            contracts = harness.New("Contracts").AddComponent<ContractManager>();
            home = harness.New("Command Post").AddComponent<LocationAnchor>();
            pilotClass = harness.Class("Pilot");
            pattern = harness.Pattern(("A", 0f, 8f), ("B", 8f, 8f), ("C", 16f, 8f));
            pilotRole = harness.Asset<StaffingRoleDefinition>();
            pilotRole.displayName = "Pilot";
            pilotRole.requiredClass = pilotClass;
            pilotRole.minimumActiveForOperation = 1;
            pilotRole.maximumAssignedPerShift = 1;

            GameObject shipObject = harness.New("Shuttle");
            LocationAnchor anchor = shipObject.AddComponent<LocationAnchor>();
            ship = shipObject.AddComponent<ShipComponent>();
            shipObject.AddComponent<ShipMovementComponent>().movementSpeed = 10f;
            crewStaffing = shipObject.AddComponent<StaffingComponent>();
            crewStaffing.workplaceLocation = anchor;
            crewStaffing.shiftPattern = pattern;
            crewStaffing.offeredRoles.Add(pilotRole);
            ship.crewStaffing = crewStaffing;
            ship.operatingRole = pilotRole;
            ship.crewChangeBase = home;
            ship.initialDock = home;
            ship.EnsureInitialized();
            shipObject.AddComponent<ShipCrewDutyComponent>();
        }

        [TearDown]
        public void TearDown() => harness.Dispose();

        [Test]
        public void AssignedPilotsShareOneShuttleAcrossTwoShifts()
        {
            ColonistAgent a = harness.Colonist("Pilot A", home, pilotClass);
            ColonistAgent b = harness.Colonist("Pilot B", home, pilotClass);
            Assert.That(staffing.Assign(a, crewStaffing, pilotRole, "A"), Is.EqualTo(AssignmentResult.Applied));
            Assert.That(staffing.Assign(b, crewStaffing, pilotRole, "B"), Is.EqualTo(AssignmentResult.Applied));

            SetGameHour(0f);
            staffing.SimulationTick(0f);
            Assert.That(ship.ResponsiblePilot, Is.EqualTo(a));
            Assert.That(a.currentEmployment.workplace, Is.EqualTo(crewStaffing));
            Assert.That(b.currentEmployment.workplace, Is.EqualTo(crewStaffing));

            SetGameHour(8f);
            staffing.SimulationTick(0f);
            ship.GetComponent<ShipCrewDutyComponent>().SimulationTick(0f);
            Assert.That(ship.ResponsiblePilot, Is.Null);
            Assert.That(a.currentEmployment.workplace, Is.EqualTo(crewStaffing));

            SetGameHour(9f);
            staffing.SimulationTick(0f);
            Assert.That(ship.ResponsiblePilot, Is.EqualTo(b));
            Assert.That(a.activity, Is.EqualTo(ColonistActivity.Sleeping).Or.EqualTo(ColonistActivity.Resting));
        }

        [Test]
        public void AtBasePilotBoardsDirectlyWithoutSelfCommute()
        {
            ColonistAgent pilot = harness.Colonist("Pilot", home, pilotClass);
            Assert.That(staffing.Assign(pilot, crewStaffing, pilotRole, "A"), Is.EqualTo(AssignmentResult.Applied));

            SetGameHour(0f);
            staffing.SimulationTick(0f);

            Assert.That(ship.ResponsiblePilot, Is.EqualTo(pilot));
            Assert.That(pilot.Status.CurrentDutyState, Is.EqualTo(ColonistDutyState.AcceptingNewWork));
            Assert.That(contracts.Contracts.Count, Is.EqualTo(0));
        }

        [Test]
        public void PilotWithExistingPassengerTripCannotBoardUntilTripCompletes()
        {
            ColonistAgent pilot = harness.Colonist("Pilot", home, pilotClass);
            LocationAnchor destination = harness.New("Destination").AddComponent<LocationAnchor>();
            Assert.That(staffing.Assign(pilot, crewStaffing, pilotRole, "A"), Is.EqualTo(AssignmentResult.Applied));
            Assert.That(contracts.CreatePassengerContract(home, destination,
                new System.Collections.Generic.List<ColonistAgent> { pilot }), Is.Not.Null);

            SetGameHour(0f);
            staffing.SimulationTick(0f);

            Assert.That(ship.ResponsiblePilot, Is.Null);
            Assert.That(pilot.currentEmployment.workplace, Is.EqualTo(crewStaffing));
        }

        [Test]
        public void SameShiftCapacityIsOneButOtherShiftIsAvailable()
        {
            ColonistAgent a = harness.Colonist("Pilot A", home, pilotClass);
            ColonistAgent b = harness.Colonist("Pilot B", home, pilotClass);
            Assert.That(staffing.Assign(a, crewStaffing, pilotRole, "A"), Is.EqualTo(AssignmentResult.Applied));
            Assert.That(staffing.Assign(b, crewStaffing, pilotRole, "A"), Is.EqualTo(AssignmentResult.RejectedAtCapacity));
            Assert.That(staffing.Assign(b, crewStaffing, pilotRole, "B"), Is.EqualTo(AssignmentResult.Applied));
        }

        [Test]
        public void DailyPilotShiftDoesNotRestartAtSixteenHours()
        {
            ColonistAgent pilot = harness.Colonist("Pilot", home, pilotClass);
            Assert.That(staffing.Assign(pilot, crewStaffing, pilotRole, "A"), Is.EqualTo(AssignmentResult.Applied));

            SetGameHour(0f);
            staffing.SimulationTick(0f);
            Assert.That(ship.ResponsiblePilot, Is.EqualTo(pilot));

            SetGameHour(8f);
            staffing.SimulationTick(0f);
            ship.GetComponent<ShipCrewDutyComponent>().SimulationTick(0f);
            Assert.That(ship.ResponsiblePilot, Is.Null);

            SetGameHour(16f);
            staffing.SimulationTick(0f);
            ship.GetComponent<ShipCrewDutyComponent>().SimulationTick(0f);
            Assert.That(ship.ResponsiblePilot, Is.Null);

            SetGameHour(24f);
            staffing.SimulationTick(0f);
            ship.GetComponent<ShipCrewDutyComponent>().SimulationTick(0f);
            Assert.That(ship.ResponsiblePilot, Is.EqualTo(pilot));
        }

        [Test]
        public void ExplicitCShiftPilotBoardsDuringSixteenToTwentyFour()
        {
            ColonistAgent pilot = harness.Colonist("Pilot C", home, pilotClass);
            Assert.That(staffing.Assign(pilot, crewStaffing, pilotRole, "C"), Is.EqualTo(AssignmentResult.Applied));

            SetGameHour(16f);
            staffing.SimulationTick(0f);

            Assert.That(ship.ResponsiblePilot, Is.EqualTo(pilot));
        }

        [Test]
        public void ScheduleTableIncludesPilotDailyOffDutyWindow()
        {
            ColonistAgent pilot = harness.Colonist("Pilot", home, pilotClass);
            Assert.That(staffing.Assign(pilot, crewStaffing, pilotRole, "A"), Is.EqualTo(AssignmentResult.Applied));

            string table = staffing.BuildDailyScheduleTable(17f);

            Assert.That(table, Does.Contain("Pilot"));
            Assert.That(table, Does.Contain("00:00-08:00"));
            Assert.That(table, Does.Contain("08:00-24:00"));
        }

        [Test]
        public void DifferentHomeCannotJoinExistingCrewBase()
        {
            ColonistAgent a = harness.Colonist("Pilot A", home, pilotClass);
            LocationAnchor otherHome = harness.New("Other Habitat").AddComponent<LocationAnchor>();
            ColonistAgent b = harness.Colonist("Pilot B", otherHome, pilotClass);
            Assert.That(staffing.Assign(a, crewStaffing, pilotRole, "A"), Is.EqualTo(AssignmentResult.Applied));
            Assert.That(staffing.Assign(b, crewStaffing, pilotRole, "B"), Is.EqualTo(AssignmentResult.RejectedCrewBaseMismatch));
            Assert.That(b.currentEmployment, Is.Null);
        }

        [Test]
        public void PilotFatigueUsesActiveLeaseAndStopsAtExhaustion()
        {
            ColonistAgent pilot = harness.Colonist("Pilot", home, pilotClass);
            Assert.That(staffing.Assign(pilot, crewStaffing, pilotRole, "A"), Is.EqualTo(AssignmentResult.Applied));
            SetGameHour(0f);
            staffing.SimulationTick(0f);
            pilot.GetComponent<ColonistStatusComponent>().AdjustFatigue(0.8f);
            staffing.SimulationTick(1f);
            Assert.That(pilot.GetComponent<ColonistStatusComponent>().Fatigue, Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(ship.ReleaseRequested, Is.True);
            Assert.That(ship.IsOperationallyCrewed, Is.False);
            Assert.That(pilot.Status.CurrentDutyState, Is.EqualTo(ColonistDutyState.ReturningHome));
            Assert.That(pilot.currentEmployment.workplace, Is.EqualTo(crewStaffing));
        }

        [Test]
        public void ReleaseStillReturnsPilotWhenShipIsOutOfService()
        {
            ColonistAgent pilot = harness.Colonist("Pilot", home, pilotClass);
            Assert.That(staffing.Assign(pilot, crewStaffing, pilotRole, "A"), Is.EqualTo(AssignmentResult.Applied));
            SetGameHour(0f);
            staffing.SimulationTick(0f);
            Assert.That(ship.ResponsiblePilot, Is.EqualTo(pilot));

            // Taking a ship out of service must not strand the already-boarded
            // pilot. Release is the handoff boundary and must still be processed
            // while operational work is disabled.
            ship.operationalEnabled = false;
            ship.RequestRelease(DutyEndReason.WorkplaceUnavailable);
            ShipCrewDutyComponent crew = ship.GetComponent<ShipCrewDutyComponent>();
            crew.RequestRelease(DutyEndReason.WorkplaceUnavailable);
            staffing.SimulationTick(0f);
            Assert.That(pilot.Status.CurrentDutyState, Is.EqualTo(ColonistDutyState.ReturningHome));
            Assert.That(ship.TryClaimMovement(ShipMovementOwner.Transport), Is.True);
            crew.SimulationTick(0f);

            Assert.That(ship.ResponsiblePilot, Is.Null);
            Assert.That(pilot.currentLocation, Is.EqualTo(home));
            Assert.That(ship.ReleaseRequested, Is.False);
            staffing.SimulationTick(0f);
            Assert.That(pilot.Status.CurrentDutyState, Is.EqualTo(ColonistDutyState.Blocked));
        }

        [Test]
        public void UnassignKeepsResponsiblePilotUntilSafeRelease()
        {
            ColonistAgent pilot = harness.Colonist("Pilot", home, pilotClass);
            staffing.Assign(pilot, crewStaffing, pilotRole, "A");
            SetGameHour(0f);
            staffing.SimulationTick(0f);
            Assert.That(ship.ResponsiblePilot, Is.EqualTo(pilot));
            staffing.Unassign(pilot);
            Assert.That(pilot.currentEmployment, Is.Null);
            Assert.That(ship.ResponsiblePilot, Is.EqualTo(pilot));
            Assert.That(ship.ReleaseRequested, Is.True);
        }

        private void SetGameHour(float hour)
        {
            System.Reflection.FieldInfo field = typeof(SimulationManager).GetField(
                "currentGameHour", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field.SetValue(clock, hour);
        }
    }
}
