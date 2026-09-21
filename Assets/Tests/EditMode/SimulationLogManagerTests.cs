using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class SimulationLogManagerTests
    {
        private GameObject managerObject;
        private GameObject primaryObject;
        private GameObject targetObject;

        [TearDown]
        public void TearDown()
        {
            if (targetObject != null)
                Object.DestroyImmediate(targetObject);
            if (primaryObject != null)
                Object.DestroyImmediate(primaryObject);
            if (managerObject != null)
                Object.DestroyImmediate(managerObject);
        }

        [Test]
        public void StructuredEntriesPreserveFieldsAndSubjects()
        {
            SimulationLogManager manager = CreateManager();
            primaryObject = new GameObject("Bob");
            targetObject = new GameObject("Cafeteria");

            SimulationLogEntry entry = manager.Record(
                "colonist.decision.eat",
                "Decision",
                "Info",
                primaryObject,
                targetObject,
                new SimulationLogField("reason", "hungry"),
                new SimulationLogField("hunger", 72));

            Assert.That(entry.PrimarySubject.DisplayName, Is.EqualTo("Bob"));
            Assert.That(entry.SecondarySubject.DisplayName, Is.EqualTo("Cafeteria"));
            Assert.That(entry.TryGetField("reason", out string reason), Is.True);
            Assert.That(reason, Is.EqualTo("hungry"));
            Assert.That(entry.EventKey, Is.EqualTo("colonist.decision.eat"));
            Assert.That(entry.SequenceNumber, Is.GreaterThan(0));
        }

        [Test]
        public void EntriesAreSequencedAndBounded()
        {
            SimulationLogManager manager = CreateManager();
            SetPrivateField(manager, "capacity", 2);

            SimulationLogEntry first = manager.Record("first", "System");
            SimulationLogEntry second = manager.Record("second", "System");
            SimulationLogEntry third = manager.Record("third", "System");

            Assert.That(second.SequenceNumber, Is.GreaterThan(first.SequenceNumber));
            Assert.That(third.SequenceNumber, Is.GreaterThan(second.SequenceNumber));
            Assert.That(manager.Entries.Count, Is.EqualTo(2));
            Assert.That(manager.Entries[0].EventKey, Is.EqualTo("second"));
            Assert.That(manager.Entries[1].EventKey, Is.EqualTo("third"));
        }

        [Test]
        public void JsonSerializationUsesStructuredFields()
        {
            SimulationLogManager manager = CreateManager();
            SimulationLogEntry entry = manager.Record(
                "activity.failed",
                "Activity",
                "Warning",
                (Object)null,
                (Object)null,
                new SimulationLogField("reason", "missing_anchor"));

            string json = JsonUtility.ToJson(entry);

            Assert.That(json, Does.Contain("activity.failed"));
            Assert.That(json, Does.Contain("missing_anchor"));
            Assert.That(json, Does.Not.Contain("Detail:"));
        }

        private SimulationLogManager CreateManager()
        {
            managerObject = new GameObject("Simulation Log Manager Test");
            return managerObject.AddComponent<SimulationLogManager>();
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }
    }
}
