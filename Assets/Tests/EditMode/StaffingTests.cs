using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class StaffingTests
    {
        private GameObject workplaceObject;
        private GameObject workerObject;
        private StaffingComponent staffing;
        private WorkerClassDefinition pilot;
        private WorkerClassDefinition farmer;
        private SkillDefinition agriculture;

        [SetUp]
        public void SetUp()
        {
            workplaceObject = new GameObject("Workplace");
            LocationAnchor workplace = workplaceObject.AddComponent<LocationAnchor>();
            staffing = workplaceObject.AddComponent<StaffingComponent>();
            staffing.workplace = workplace;

            workerObject = new GameObject("Worker");
            ColonistAgent worker = workerObject.AddComponent<ColonistAgent>();
            worker.currentLocation = workplace;
            worker.activity = ColonistActivity.Working;
            staffing.assignedWorkers.Add(worker);

            pilot = ScriptableObject.CreateInstance<WorkerClassDefinition>();
            farmer = ScriptableObject.CreateInstance<WorkerClassDefinition>();
            agriculture = ScriptableObject.CreateInstance<SkillDefinition>();
            worker.classes.Add(pilot);
            worker.skills.Add(new SkillRating { skill = agriculture, proficiency = 1f });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(agriculture);
            Object.DestroyImmediate(farmer);
            Object.DestroyImmediate(pilot);
            Object.DestroyImmediate(workerObject);
            Object.DestroyImmediate(workplaceObject);
        }

        [Test]
        public void MultiClassAndSkillEligibilityRemainIndependent()
        {
            ColonistAgent worker = staffing.assignedWorkers[0];
            worker.classes.Add(farmer);
            Assert.That(worker.HasClass(pilot), Is.True);
            Assert.That(worker.HasClass(farmer), Is.True);
            Assert.That(worker.GetSkill(agriculture), Is.EqualTo(1f));

            RecipeStaffingRule rule = new RecipeStaffingRule
            {
                minimumWorkers = 1,
                requiredClass = farmer,
                preferredSkill = agriculture,
                maxPreferredSkillBonus = 0.5f
            };
            Assert.That(rule.CalculateThroughputMultiplier(new List<ColonistAgent> { worker }), Is.EqualTo(1.5f));
        }

        [Test]
        public void SkillDoesNotGrantMissingClass()
        {
            RecipeStaffingRule rule = new RecipeStaffingRule
            {
                minimumWorkers = 1,
                requiredClass = farmer,
                preferredSkill = agriculture,
                maxPreferredSkillBonus = 0.5f
            };
            Assert.That(rule.IsStaffed(new List<ColonistAgent> { staffing.assignedWorkers[0] }), Is.False);
            Assert.That(rule.CalculateThroughputMultiplier(new List<ColonistAgent> { staffing.assignedWorkers[0] }), Is.EqualTo(0f));
        }

        [Test]
        public void StaffingCountsPresentAndWorking()
        {
            Assert.That(staffing.AssignedCount, Is.EqualTo(1));
            Assert.That(staffing.PresentCount, Is.EqualTo(1));
            Assert.That(staffing.WorkingCount, Is.EqualTo(1));
            staffing.assignedWorkers[0].activity = ColonistActivity.Resting;
            Assert.That(staffing.WorkingCount, Is.EqualTo(0));
            Assert.That(staffing.CountWorkingWithClass(pilot), Is.EqualTo(0));
        }
    }
}
