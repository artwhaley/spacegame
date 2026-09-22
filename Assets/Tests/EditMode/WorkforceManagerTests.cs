using System.Collections.Generic;
using System.Reflection;
using Colony.Interactions;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    [Category("Core")]
    public class WorkforceManagerTests
    {
        private readonly List<GameObject> sceneObjects = new List<GameObject>();
        private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        private WorkforceManager manager;

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
        }

        [Test]
        public void UnassignedColonistHasNoAssignment()
        {
            manager = CreateManager();
            ColonistIdentity colonist = CreateColonist("Unassigned");

            Assert.That(manager.TryGetAssignment(colonist, out WorkAssignment assignment), Is.False);
            Assert.That(assignment, Is.Null);
        }

        [Test]
        public void AssignValidRecordAndQueryExactData()
        {
            manager = CreateManager();
            JobRoleDefinition role = CreateRole("farmer");
            WorkplaceComponent workplace = CreateWorkplace(role, 1);
            ColonistIdentity colonist = CreateColonist("Bob");
            DailyShiftWindow shift = new DailyShiftWindow(8f, 14f);

            Assert.That(
                manager.Assign(colonist, workplace, role, shift),
                Is.EqualTo(WorkAssignmentResult.Applied));
            Assert.That(manager.TryGetAssignment(colonist, out WorkAssignment assignment), Is.True);
            Assert.That(assignment.Colonist, Is.SameAs(colonist));
            Assert.That(assignment.Workplace, Is.SameAs(workplace));
            Assert.That(assignment.Role, Is.SameAs(role));
            Assert.That(assignment.Shift, Is.SameAs(shift));
        }

        [Test]
        public void ReassigningColonistReplacesTheOnlyAssignment()
        {
            manager = CreateManager();
            JobRoleDefinition role = CreateRole("farmer");
            WorkplaceComponent workplace = CreateWorkplace(role, 1);
            ColonistIdentity colonist = CreateColonist("Bob");
            DailyShiftWindow firstShift = new DailyShiftWindow(8f, 14f);
            DailyShiftWindow replacementShift = new DailyShiftWindow(12f, 18f);

            Assert.That(manager.Assign(colonist, workplace, role, firstShift), Is.EqualTo(WorkAssignmentResult.Applied));
            Assert.That(manager.Assign(colonist, workplace, role, replacementShift), Is.EqualTo(WorkAssignmentResult.Applied));
            Assert.That(manager.Assignments.Count, Is.EqualTo(1));
            Assert.That(manager.TryGetAssignment(colonist, out WorkAssignment assignment), Is.True);
            Assert.That(assignment.Shift, Is.SameAs(replacementShift));
        }

        [Test]
        public void UnassignRemovesAssignment()
        {
            manager = CreateManager();
            JobRoleDefinition role = CreateRole("farmer");
            WorkplaceComponent workplace = CreateWorkplace(role, 1);
            ColonistIdentity colonist = CreateColonist("Bob");
            Assert.That(manager.Assign(colonist, workplace, role, new DailyShiftWindow(8f, 14f)), Is.EqualTo(WorkAssignmentResult.Applied));

            Assert.That(manager.Unassign(colonist), Is.EqualTo(WorkAssignmentResult.Applied));
            Assert.That(manager.TryGetAssignment(colonist, out _), Is.False);
        }

        [Test]
        public void InvalidReplacementPreservesPreviousAssignment()
        {
            manager = CreateManager();
            JobRoleDefinition role = CreateRole("farmer");
            WorkplaceComponent workplace = CreateWorkplace(role, 1);
            ColonistIdentity colonist = CreateColonist("Bob");
            DailyShiftWindow original = new DailyShiftWindow(8f, 14f);
            Assert.That(manager.Assign(colonist, workplace, role, original), Is.EqualTo(WorkAssignmentResult.Applied));

            Assert.That(
                manager.Assign(colonist, workplace, null, new DailyShiftWindow(10f, 16f)),
                Is.EqualTo(WorkAssignmentResult.RejectedMissingRole));
            Assert.That(manager.TryGetAssignment(colonist, out WorkAssignment assignment), Is.True);
            Assert.That(assignment.Shift, Is.SameAs(original));
        }

        [Test]
        public void TwoDifferentColonistsMayHaveAssignments()
        {
            manager = CreateManager();
            JobRoleDefinition role = CreateRole("farmer");
            WorkplaceComponent workplace = CreateWorkplace(role, 2);
            ColonistIdentity first = CreateColonist("Bob");
            ColonistIdentity second = CreateColonist("Alice");

            Assert.That(manager.Assign(first, workplace, role, new DailyShiftWindow(8f, 14f)), Is.EqualTo(WorkAssignmentResult.Applied));
            Assert.That(manager.Assign(second, workplace, role, new DailyShiftWindow(8f, 14f)), Is.EqualTo(WorkAssignmentResult.Applied));
            Assert.That(manager.Assignments.Count, Is.EqualTo(2));
        }

        [Test]
        public void RoleNotOfferedIsRejected()
        {
            manager = CreateManager();
            JobRoleDefinition offeredRole = CreateRole("farmer");
            JobRoleDefinition unofferedRole = CreateRole("doctor");
            WorkplaceComponent workplace = CreateWorkplace(offeredRole, 1);
            ColonistIdentity colonist = CreateColonist("Bob");

            Assert.That(
                manager.Assign(colonist, workplace, unofferedRole, new DailyShiftWindow(8f, 14f)),
                Is.EqualTo(WorkAssignmentResult.RejectedRoleNotOffered));
        }

        [Test]
        public void CapacityOneRejectsOverlappingShift()
        {
            manager = CreateManager();
            JobRoleDefinition role = CreateRole("farmer");
            WorkplaceComponent workplace = CreateWorkplace(role, 1);
            ColonistIdentity first = CreateColonist("Bob");
            ColonistIdentity second = CreateColonist("Alice");
            Assert.That(manager.Assign(first, workplace, role, new DailyShiftWindow(8f, 14f)), Is.EqualTo(WorkAssignmentResult.Applied));

            Assert.That(
                manager.Assign(second, workplace, role, new DailyShiftWindow(10f, 16f)),
                Is.EqualTo(WorkAssignmentResult.RejectedAtScheduledCapacity));
        }

        [Test]
        public void CapacityOneAcceptsTouchingBoundary()
        {
            manager = CreateManager();
            JobRoleDefinition role = CreateRole("farmer");
            WorkplaceComponent workplace = CreateWorkplace(role, 1);
            ColonistIdentity first = CreateColonist("Bob");
            ColonistIdentity second = CreateColonist("Alice");
            Assert.That(manager.Assign(first, workplace, role, new DailyShiftWindow(8f, 14f)), Is.EqualTo(WorkAssignmentResult.Applied));

            Assert.That(
                manager.Assign(second, workplace, role, new DailyShiftWindow(14f, 20f)),
                Is.EqualTo(WorkAssignmentResult.Applied));
        }

        [Test]
        public void CapacityTwoDoesNotOverrejectDifferentOverlapPeriods()
        {
            manager = CreateManager();
            JobRoleDefinition role = CreateRole("farmer");
            WorkplaceComponent workplace = CreateWorkplace(role, 2);
            ColonistIdentity bob = CreateColonist("Bob");
            ColonistIdentity alice = CreateColonist("Alice");
            ColonistIdentity carol = CreateColonist("Carol");

            Assert.That(manager.Assign(bob, workplace, role, new DailyShiftWindow(8f, 16f)), Is.EqualTo(WorkAssignmentResult.Applied));
            Assert.That(manager.Assign(alice, workplace, role, new DailyShiftWindow(8f, 10f)), Is.EqualTo(WorkAssignmentResult.Applied));
            Assert.That(manager.Assign(carol, workplace, role, new DailyShiftWindow(14f, 16f)), Is.EqualTo(WorkAssignmentResult.Applied));
        }

        [Test]
        public void CapacityTwoRejectsActualThirdSimultaneousWorker()
        {
            manager = CreateManager();
            JobRoleDefinition role = CreateRole("farmer");
            WorkplaceComponent workplace = CreateWorkplace(role, 2);
            ColonistIdentity bob = CreateColonist("Bob");
            ColonistIdentity alice = CreateColonist("Alice");
            ColonistIdentity carol = CreateColonist("Carol");

            Assert.That(manager.Assign(bob, workplace, role, new DailyShiftWindow(8f, 16f)), Is.EqualTo(WorkAssignmentResult.Applied));
            Assert.That(manager.Assign(alice, workplace, role, new DailyShiftWindow(9f, 15f)), Is.EqualTo(WorkAssignmentResult.Applied));
            Assert.That(
                manager.Assign(carol, workplace, role, new DailyShiftWindow(10f, 12f)),
                Is.EqualTo(WorkAssignmentResult.RejectedAtScheduledCapacity));
        }

        [Test]
        public void OvernightOverlapIsRejectedAtCapacityOne()
        {
            manager = CreateManager();
            JobRoleDefinition role = CreateRole("farmer");
            WorkplaceComponent workplace = CreateWorkplace(role, 1);
            ColonistIdentity first = CreateColonist("Bob");
            ColonistIdentity second = CreateColonist("Alice");
            Assert.That(manager.Assign(first, workplace, role, new DailyShiftWindow(22f, 6f)), Is.EqualTo(WorkAssignmentResult.Applied));

            Assert.That(
                manager.Assign(second, workplace, role, new DailyShiftWindow(5f, 10f)),
                Is.EqualTo(WorkAssignmentResult.RejectedAtScheduledCapacity));
        }

        [Test]
        public void OvernightBoundaryAdjacencyIsAccepted()
        {
            manager = CreateManager();
            JobRoleDefinition role = CreateRole("farmer");
            WorkplaceComponent workplace = CreateWorkplace(role, 1);
            ColonistIdentity first = CreateColonist("Bob");
            ColonistIdentity second = CreateColonist("Alice");
            Assert.That(manager.Assign(first, workplace, role, new DailyShiftWindow(22f, 6f)), Is.EqualTo(WorkAssignmentResult.Applied));

            Assert.That(
                manager.Assign(second, workplace, role, new DailyShiftWindow(6f, 12f)),
                Is.EqualTo(WorkAssignmentResult.Applied));
        }

        [Test]
        public void EditingOwnAssignmentExcludesOldRecordFromCapacity()
        {
            manager = CreateManager();
            JobRoleDefinition role = CreateRole("farmer");
            WorkplaceComponent workplace = CreateWorkplace(role, 1);
            ColonistIdentity bob = CreateColonist("Bob");
            Assert.That(manager.Assign(bob, workplace, role, new DailyShiftWindow(8f, 14f)), Is.EqualTo(WorkAssignmentResult.Applied));

            Assert.That(
                manager.Assign(bob, workplace, role, new DailyShiftWindow(9f, 15f)),
                Is.EqualTo(WorkAssignmentResult.Applied));
        }

        [Test]
        public void FailedCapacityReplacementPreservesPreviousAssignment()
        {
            manager = CreateManager();
            JobRoleDefinition role = CreateRole("farmer");
            WorkplaceComponent workplace = CreateWorkplace(role, 1);
            ColonistIdentity bob = CreateColonist("Bob");
            ColonistIdentity alice = CreateColonist("Alice");
            Assert.That(manager.Assign(alice, workplace, role, new DailyShiftWindow(14f, 20f)), Is.EqualTo(WorkAssignmentResult.Applied));
            DailyShiftWindow original = new DailyShiftWindow(8f, 14f);
            Assert.That(manager.Assign(bob, workplace, role, original), Is.EqualTo(WorkAssignmentResult.Applied));

            Assert.That(
                manager.Assign(bob, workplace, role, new DailyShiftWindow(10f, 16f)),
                Is.EqualTo(WorkAssignmentResult.RejectedAtScheduledCapacity));
            Assert.That(manager.TryGetAssignment(bob, out WorkAssignment assignment), Is.True);
            Assert.That(assignment.Shift, Is.SameAs(original));
        }

        [Test]
        public void UnemployedColonistHasNoShiftOccurrences()
        {
            manager = CreateManager();
            ColonistIdentity colonist = CreateColonist("Unassigned");

            Assert.That(manager.TryGetCurrentShift(colonist, 10f, out _), Is.False);
            Assert.That(manager.TryGetNextShift(colonist, 10f, out _), Is.False);
            Assert.That(manager.TryGetCurrentOrNextShift(colonist, 10f, out _), Is.False);
        }

        [Test]
        public void OrdinaryShiftReturnsCurrentAndNextAbsoluteOccurrences()
        {
            manager = CreateManager();
            JobRoleDefinition role = CreateRole("farmer");
            WorkplaceComponent workplace = CreateWorkplace(role, 1);
            ColonistIdentity colonist = CreateColonist("Bob");
            Assert.That(manager.Assign(colonist, workplace, role, new DailyShiftWindow(8f, 14f)), Is.EqualTo(WorkAssignmentResult.Applied));

            Assert.That(manager.TryGetNextShift(colonist, 7f, out ScheduledWorkOccurrence nextBefore), Is.True);
            AssertOccurrence(nextBefore, 8f, 14f);
            Assert.That(manager.TryGetCurrentShift(colonist, 8f, out ScheduledWorkOccurrence currentAtStart), Is.True);
            AssertOccurrence(currentAtStart, 8f, 14f);
            Assert.That(manager.TryGetCurrentShift(colonist, 10f, out ScheduledWorkOccurrence current), Is.True);
            AssertOccurrence(current, 8f, 14f);
            Assert.That(manager.TryGetNextShift(colonist, 10f, out ScheduledWorkOccurrence nextWhileWorking), Is.True);
            AssertOccurrence(nextWhileWorking, 32f, 38f);
            Assert.That(manager.TryGetCurrentShift(colonist, 14f, out _), Is.False);
            Assert.That(manager.TryGetNextShift(colonist, 15f, out ScheduledWorkOccurrence nextAfter), Is.True);
            AssertOccurrence(nextAfter, 32f, 38f);
        }

        [Test]
        public void OccurrencesContinueAcrossLaterDays()
        {
            manager = CreateManager();
            JobRoleDefinition role = CreateRole("farmer");
            WorkplaceComponent workplace = CreateWorkplace(role, 1);
            ColonistIdentity colonist = CreateColonist("Bob");
            Assert.That(manager.Assign(colonist, workplace, role, new DailyShiftWindow(8f, 14f)), Is.EqualTo(WorkAssignmentResult.Applied));

            Assert.That(manager.TryGetNextShift(colonist, 31f, out ScheduledWorkOccurrence next), Is.True);
            AssertOccurrence(next, 32f, 38f);
            Assert.That(manager.TryGetCurrentShift(colonist, 34f, out ScheduledWorkOccurrence current), Is.True);
            AssertOccurrence(current, 32f, 38f);
        }

        [Test]
        public void OvernightOccurrencesUsePreviousStartDayWhenActiveAfterMidnight()
        {
            manager = CreateManager();
            JobRoleDefinition role = CreateRole("farmer");
            WorkplaceComponent workplace = CreateWorkplace(role, 1);
            ColonistIdentity colonist = CreateColonist("Bob");
            Assert.That(manager.Assign(colonist, workplace, role, new DailyShiftWindow(22f, 6f)), Is.EqualTo(WorkAssignmentResult.Applied));

            Assert.That(manager.TryGetNextShift(colonist, 21f, out ScheduledWorkOccurrence nextBefore), Is.True);
            AssertOccurrence(nextBefore, 22f, 30f);
            Assert.That(manager.TryGetCurrentShift(colonist, 22f, out ScheduledWorkOccurrence currentAtStart), Is.True);
            AssertOccurrence(currentAtStart, 22f, 30f);
            Assert.That(manager.TryGetCurrentShift(colonist, 26f, out ScheduledWorkOccurrence currentAfterMidnight), Is.True);
            AssertOccurrence(currentAfterMidnight, 22f, 30f);
            Assert.That(manager.TryGetCurrentShift(colonist, 30f, out _), Is.False);
            Assert.That(manager.TryGetNextShift(colonist, 30f, out ScheduledWorkOccurrence nextAfter), Is.True);
            AssertOccurrence(nextAfter, 46f, 54f);
            Assert.That(manager.TryGetNextShift(colonist, 31f, out ScheduledWorkOccurrence nextAtSeven), Is.True);
            AssertOccurrence(nextAtSeven, 46f, 54f);
        }

        [Test]
        public void NextOccurrenceReportsTimeUntilStart()
        {
            manager = CreateManager();
            JobRoleDefinition role = CreateRole("farmer");
            WorkplaceComponent workplace = CreateWorkplace(role, 1);
            ColonistIdentity colonist = CreateColonist("Bob");
            Assert.That(manager.Assign(colonist, workplace, role, new DailyShiftWindow(8f, 14f)), Is.EqualTo(WorkAssignmentResult.Applied));

            Assert.That(manager.TryGetNextShift(colonist, 6.8333333f, out ScheduledWorkOccurrence next), Is.True);
            Assert.That(next.TimeUntilStart(6.8333333f), Is.EqualTo(1.1666667f).Within(0.0001f));
        }

        [Test]
        public void OccurrencesUseRequestedColonistsOwnAssignment()
        {
            manager = CreateManager();
            JobRoleDefinition role = CreateRole("farmer");
            WorkplaceComponent workplace = CreateWorkplace(role, 2);
            ColonistIdentity bob = CreateColonist("Bob");
            ColonistIdentity alice = CreateColonist("Alice");
            Assert.That(manager.Assign(bob, workplace, role, new DailyShiftWindow(8f, 14f)), Is.EqualTo(WorkAssignmentResult.Applied));
            Assert.That(manager.Assign(alice, workplace, role, new DailyShiftWindow(16f, 20f)), Is.EqualTo(WorkAssignmentResult.Applied));

            Assert.That(manager.TryGetCurrentShift(bob, 10f, out ScheduledWorkOccurrence bobCurrent), Is.True);
            AssertOccurrence(bobCurrent, 8f, 14f);
            Assert.That(manager.TryGetNextShift(alice, 10f, out ScheduledWorkOccurrence aliceNext), Is.True);
            AssertOccurrence(aliceNext, 16f, 20f);
        }

        private WorkforceManager CreateManager()
        {
            GameObject managerObject = CreateObject("WorkforceManager");
            return managerObject.AddComponent<WorkforceManager>();
        }

        private ColonistIdentity CreateColonist(string name)
        {
            return CreateObject(name).AddComponent<ColonistIdentity>();
        }

        private WorkplaceComponent CreateWorkplace(
            JobRoleDefinition offeredRole,
            int capacity)
        {
            GameObject workplaceObject = CreateObject("Workplace");
            InteractableFacility facility =
                workplaceObject.AddComponent<InteractableFacility>();
            FacilityActivityBinding activity = new FacilityActivityBinding();
            SetPrivateField(activity, "activityId", "Farm");
            SetPrivateField(facility, "activities", new[] { activity });

            WorkplaceRoleBinding roleBinding = new WorkplaceRoleBinding();
            SetPrivateField(roleBinding, "role", offeredRole);
            SetPrivateField(roleBinding, "activityId", "Farm");
            SetPrivateField(roleBinding, "maximumConcurrentScheduledWorkers", capacity);
            WorkplaceComponent workplace = workplaceObject.AddComponent<WorkplaceComponent>();
            SetPrivateField(workplace, "roles", new[] { roleBinding });
            return workplace;
        }

        private GameObject CreateObject(string name)
        {
            GameObject created = new GameObject(name);
            sceneObjects.Add(created);
            return created;
        }

        private JobRoleDefinition CreateRole(string stableId)
        {
            JobRoleDefinition role = ScriptableObject.CreateInstance<JobRoleDefinition>();
            SetPrivateField(role, "stableId", stableId);
            SetPrivateField(role, "displayName", stableId);
            assets.Add(role);
            return role;
        }

        private static void AssertOccurrence(
            ScheduledWorkOccurrence occurrence,
            float expectedStart,
            float expectedEnd)
        {
            Assert.That(occurrence, Is.Not.Null);
            Assert.That(occurrence.StartGameHour, Is.EqualTo(expectedStart));
            Assert.That(occurrence.EndGameHour, Is.EqualTo(expectedEnd));
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
