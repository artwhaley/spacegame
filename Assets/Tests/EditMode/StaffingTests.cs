using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    /// <summary>T01/T02: classes, skills, shift patterns, and the employment record.</summary>
    public class StaffingTests
    {
        private StaffingTestHarness harness;

        [SetUp]
        public void SetUp()
        {
            harness = new StaffingTestHarness();
        }

        [TearDown]
        public void TearDown()
        {
            harness.Dispose();
        }

        [Test]
        public void ColonistCanHoldPilotAndFarmTechnicianSimultaneously()
        {
            WorkerClassDefinition pilot = harness.Class("Pilot");
            WorkerClassDefinition farmer = harness.Class("Farm Technician");
            LocationAnchor home = harness.New("Command Post").AddComponent<LocationAnchor>();
            ColonistAgent colonist = harness.Colonist("Ripley", home, pilot, farmer);

            Assert.That(colonist.HasClass(pilot), Is.True);
            Assert.That(colonist.HasClass(farmer), Is.True);
        }

        [Test]
        public void HasClassIsTrueOnlyWhenClassIsExplicitlyHeld()
        {
            WorkerClassDefinition pilot = harness.Class("Pilot");
            WorkerClassDefinition farmer = harness.Class("Farm Technician");
            LocationAnchor home = harness.New("Command Post").AddComponent<LocationAnchor>();
            ColonistAgent colonist = harness.Colonist("Hicks", home, pilot);

            Assert.That(colonist.HasClass(pilot), Is.True);
            Assert.That(colonist.HasClass(farmer), Is.False);
            Assert.That(colonist.HasClass(null), Is.False);
        }

        [Test]
        public void AgricultureSkillDoesNotGrantFarmTechnicianClass()
        {
            SkillDefinition agriculture = harness.Skill("Agriculture");
            WorkerClassDefinition farmer = harness.Class("Farm Technician");
            LocationAnchor home = harness.New("Command Post").AddComponent<LocationAnchor>();
            ColonistAgent colonist = harness.Colonist("Bishop", home);
            colonist.skills.Add(new SkillRating { skill = agriculture, proficiency = 1f });

            Assert.That(colonist.GetSkill(agriculture), Is.EqualTo(1f));
            Assert.That(colonist.HasClass(farmer), Is.False);
        }

        [Test]
        public void MissingSkillReturnsZero()
        {
            SkillDefinition agriculture = harness.Skill("Agriculture");
            SkillDefinition technical = harness.Skill("Technical");
            LocationAnchor home = harness.New("Command Post").AddComponent<LocationAnchor>();
            ColonistAgent colonist = harness.Colonist("Vasquez", home);
            colonist.skills.Add(new SkillRating { skill = agriculture, proficiency = 0.4f });

            Assert.That(colonist.GetSkill(agriculture), Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(colonist.GetSkill(technical), Is.EqualTo(0f));
            Assert.That(colonist.GetSkill(null), Is.EqualTo(0f));
        }

        [Test]
        public void ProficiencyClampsToUnitRange()
        {
            SkillDefinition agriculture = harness.Skill("Agriculture");
            LocationAnchor home = harness.New("Command Post").AddComponent<LocationAnchor>();
            ColonistAgent colonist = harness.Colonist("Hudson", home);
            colonist.skills.Add(new SkillRating { skill = agriculture, proficiency = 3f });

            Assert.That(colonist.GetSkill(agriculture), Is.EqualTo(1f));
        }

        [Test]
        public void DuplicateAndNullDefinitionsAreReported()
        {
            WorkerClassDefinition farmer = harness.Class("Farm Technician");
            SkillDefinition agriculture = harness.Skill("Agriculture");
            ColonistAgent colonist = new GameObject("Test Colonist").AddComponent<ColonistAgent>();
            colonist.classes.Add(farmer);
            colonist.classes.Add(farmer);
            colonist.classes.Add(null);
            colonist.skills.Add(new SkillRating { skill = agriculture, proficiency = 0.5f });
            colonist.skills.Add(new SkillRating { skill = agriculture, proficiency = 0.2f });
            colonist.skills.Add(new SkillRating { skill = null, proficiency = 0.1f });

            var errors = new List<string>();
            colonist.CollectAuthoringErrors(errors);

            Assert.That(errors.Count, Is.EqualTo(4));
            Object.DestroyImmediate(colonist.gameObject);
        }

        [Test]
        public void ShiftAIsActiveForDailyWindow()
        {
            ShiftPatternDefinition pattern = harness.Pattern(("A", 0f, 8f), ("B", 8f, 8f));

            Assert.That(pattern.IsShiftActive("A", 0f), Is.True);
            Assert.That(pattern.IsShiftActive("A", 7.99f), Is.True);
            Assert.That(pattern.IsShiftActive("A", 8f), Is.False);
            Assert.That(pattern.IsShiftActive("A", 16f), Is.False);
            Assert.That(pattern.IsShiftActive("A", 24f), Is.True);
        }

        [Test]
        public void ShiftBIsActiveForDailyWindow()
        {
            ShiftPatternDefinition pattern = harness.Pattern(("A", 0f, 8f), ("B", 8f, 8f));

            Assert.That(pattern.IsShiftActive("B", 8f), Is.True);
            Assert.That(pattern.IsShiftActive("B", 15.99f), Is.True);
            Assert.That(pattern.IsShiftActive("B", 16f), Is.False);
            Assert.That(pattern.IsShiftActive("B", 24f), Is.False);
            Assert.That(pattern.IsShiftActive("B", 32f), Is.True);
        }

        [Test]
        public void ShiftCClosesTheDailyThreeWindowPattern()
        {
            ShiftPatternDefinition pattern = harness.Pattern(
                ("A", 0f, 8f), ("B", 8f, 8f), ("C", 16f, 8f));

            Assert.That(pattern.IsShiftActive("C", 16f), Is.True);
            Assert.That(pattern.IsShiftActive("C", 23.99f), Is.True);
            Assert.That(pattern.IsShiftActive("C", 24f), Is.False);
            Assert.That(pattern.IsShiftActive("C", 40f), Is.True);
        }

        [Test]
        public void UnknownShiftIsNeverActive()
        {
            ShiftPatternDefinition pattern = harness.Pattern(("A", 0f, 8f));

            Assert.That(pattern.HasShift("A"), Is.True);
            Assert.That(pattern.HasShift("missing"), Is.False);
            Assert.That(pattern.IsShiftActive("missing", 1f), Is.False);
        }

        [Test]
        public void WraparoundShiftSpansMidnight()
        {
            ShiftPatternDefinition pattern = harness.Pattern(("Night", 20f, 8f));

            Assert.That(pattern.IsShiftActive("Night", 20f), Is.True);
            Assert.That(pattern.IsShiftActive("Night", 23.99f), Is.True);
            Assert.That(pattern.IsShiftActive("Night", 24f), Is.True);
            Assert.That(pattern.IsShiftActive("Night", 3.99f), Is.True);
            Assert.That(pattern.IsShiftActive("Night", 4f), Is.False);
        }

        [Test]
        public void ShiftPatternValidationRejectsBadContent()
        {
            ShiftPatternDefinition invalid = harness.Asset<ShiftPatternDefinition>();
            invalid.stableId = "bad";
            invalid.shifts.Add(new ShiftDefinition { shiftId = "A", startHour = 24f, durationHours = 8f });

            Assert.That(invalid.Validate(out _), Is.False);

            ShiftPatternDefinition invalidDuration = harness.Asset<ShiftPatternDefinition>();
            invalidDuration.stableId = "bad-duration";
            invalidDuration.shifts.Add(new ShiftDefinition { shiftId = "A", startHour = 0f, durationHours = 25f });
            Assert.That(invalidDuration.Validate(out _), Is.False);

            ShiftPatternDefinition duplicate = harness.Pattern(("A", 0f, 8f), ("A", 8f, 8f));
            Assert.That(duplicate.Validate(out _), Is.False);
        }

        [Test]
        public void StaffingRoleRequiresAWorkerClass()
        {
            StaffingRoleDefinition role = harness.Asset<StaffingRoleDefinition>();
            role.stableId = "doctor-role";
            role.displayName = "Doctor";
            role.maximumAssignedPerShift = 1;
            Assert.That(role.Validate(out string error), Is.False);
            Assert.That(error, Does.Contain("requiredClass"));
        }

        [Test]
        public void EmploymentAssignmentCarriesWorkplaceRoleAndShiftOnly()
        {
            ShiftPatternDefinition pattern = harness.Pattern(("A", 0f, 8f), ("B", 8f, 8f));
            StaffingRoleDefinition role = harness.Asset<StaffingRoleDefinition>();
            role.displayName = "Farm Operator";
            LocationAnchor workplaceLocation = harness.New("Farm").AddComponent<LocationAnchor>();
            StaffingComponent workplace = harness.Facility("Farm", workplaceLocation, pattern, role);

            EmploymentAssignment assignment = new EmploymentAssignment(workplace, role, "A");

            Assert.That(assignment.IsAssigned, Is.True);
            Assert.That(assignment.workplace, Is.EqualTo(workplace));
            Assert.That(assignment.role, Is.EqualTo(role));
            Assert.That(assignment.shiftId, Is.EqualTo("A"));
            Assert.That(new EmploymentAssignment().IsAssigned, Is.False);
        }

        [Test]
        public void ColonistStoresOneCurrentAssignment()
        {
            ShiftPatternDefinition pattern = harness.Pattern(("A", 0f, 8f), ("B", 8f, 8f));
            StaffingRoleDefinition role = harness.Asset<StaffingRoleDefinition>();
            LocationAnchor workplaceLocation = harness.New("Farm").AddComponent<LocationAnchor>();
            StaffingComponent workplace = harness.Facility("Farm", workplaceLocation, pattern, role);
            LocationAnchor home = harness.New("Command Post").AddComponent<LocationAnchor>();
            ColonistAgent colonist = harness.Colonist("Parker", home);

            colonist.currentEmployment = new EmploymentAssignment(workplace, role, "A");
            // Replacing the current record is the only supported employment mutation;
            // the manager validates and applies it immediately.
            colonist.currentEmployment = new EmploymentAssignment(workplace, role, "B");
            Assert.That(colonist.currentEmployment.shiftId, Is.EqualTo("B"));
            Assert.That(colonist.IsEmployed, Is.True);
        }
    }
}
