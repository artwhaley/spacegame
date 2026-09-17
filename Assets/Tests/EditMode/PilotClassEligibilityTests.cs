using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class PilotClassEligibilityTests
    {
        private StaffingTestHarness harness;

        [SetUp]
        public void SetUp() => harness = new StaffingTestHarness();

        [TearDown]
        public void TearDown() => harness.Dispose();

        [Test]
        public void ShipRosterRequiresQualifiedResponsiblePilot()
        {
            WorkerClassDefinition pilotClass = harness.Class("Pilot");
            LocationAnchor home = harness.New("Command Post").AddComponent<LocationAnchor>();
            StaffingRoleDefinition role = harness.Asset<StaffingRoleDefinition>();
            role.displayName = "Pilot";
            role.requiredClass = pilotClass;
            role.minimumActiveForOperation = 1;
            role.maximumAssignedPerShift = 1;
            ShiftPatternDefinition pattern = harness.Pattern(("A", 0f, 8f));
            GameObject shipObject = harness.New("Shuttle");
            LocationAnchor anchor = shipObject.AddComponent<LocationAnchor>();
            ShipComponent ship = shipObject.AddComponent<ShipComponent>();
            StaffingComponent roster = shipObject.AddComponent<StaffingComponent>();
            roster.workplaceLocation = anchor;
            roster.shiftPattern = pattern;
            roster.offeredRoles.Add(role);
            ship.crewStaffing = roster;
            ship.operatingRole = role;
            ship.crewChangeBase = home;
            ship.initialDock = home;
            ship.EnsureInitialized();
            shipObject.AddComponent<ShipCrewDutyComponent>();

            ColonistAgent unqualified = harness.Colonist("Maintenance", home);
            Assert.That(ship.TryBoardResponsiblePilot(unqualified), Is.False);
            Assert.That(ship.IsOperationallyCrewed, Is.False);

            ColonistAgent qualified = harness.Colonist("Pilot", home, pilotClass);
            Assert.That(ship.TryBoardResponsiblePilot(qualified), Is.True);
            Assert.That(ship.HasQualifiedPilot, Is.True);
            qualified.Status.BeginPilotDuty(ship, role, "A", 0f);
            Assert.That(ship.IsOperationallyCrewed, Is.True);
        }

        [Test]
        public void PilotAssignmentRejectsShipWithoutCrewChangeBase()
        {
            StaffingManager manager = harness.New("Staffing Manager").AddComponent<StaffingManager>();
            WorkerClassDefinition pilotClass = harness.Class("Pilot");
            LocationAnchor home = harness.New("Command Post").AddComponent<LocationAnchor>();
            StaffingRoleDefinition role = harness.Asset<StaffingRoleDefinition>();
            role.displayName = "Pilot";
            role.requiredClass = pilotClass;
            ShiftPatternDefinition pattern = harness.Pattern(("A", 0f, 8f));
            GameObject shipObject = harness.New("Shuttle");
            LocationAnchor anchor = shipObject.AddComponent<LocationAnchor>();
            ShipComponent ship = shipObject.AddComponent<ShipComponent>();
            StaffingComponent roster = shipObject.AddComponent<StaffingComponent>();
            roster.workplaceLocation = anchor;
            roster.shiftPattern = pattern;
            roster.offeredRoles.Add(role);
            ship.crewStaffing = roster;
            ship.operatingRole = role;
            ship.initialDock = home;
            ship.EnsureInitialized();
            ColonistAgent pilot = harness.Colonist("Pilot", home, pilotClass);

            Assert.That(manager.Assign(pilot, roster, role, "A"),
                Is.EqualTo(AssignmentResult.RejectedMissingCrewBase));
            Assert.That(manager.LastDiagnostic, Does.Contain("missing").IgnoreCase);
        }
    }
}
