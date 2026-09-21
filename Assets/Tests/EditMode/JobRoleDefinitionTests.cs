using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class JobRoleDefinitionTests
    {
        private JobRoleDefinition role;

        [TearDown]
        public void TearDown()
        {
            if (role != null)
                Object.DestroyImmediate(role);
        }

        [Test]
        public void StableIdMakesRoleConfigured()
        {
            role = ScriptableObject.CreateInstance<JobRoleDefinition>();
            SetPrivateField(role, "stableId", "farmer");

            Assert.That(role.IsConfigured, Is.True);
        }

        [Test]
        public void BlankStableIdMakesRoleUnconfigured()
        {
            role = ScriptableObject.CreateInstance<JobRoleDefinition>();
            SetPrivateField(role, "stableId", " ");

            Assert.That(role.IsConfigured, Is.False);
        }

        [Test]
        public void DisplayNameUsesAuthoredName()
        {
            role = ScriptableObject.CreateInstance<JobRoleDefinition>();
            SetPrivateField(role, "stableId", "farmer");
            SetPrivateField(role, "displayName", "Farmer");

            Assert.That(role.DisplayName, Is.EqualTo("Farmer"));
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
