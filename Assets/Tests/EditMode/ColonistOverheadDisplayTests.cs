using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class ColonistOverheadDisplayTests
    {
        private GameObject colonistObject;

        [TearDown]
        public void TearDown()
        {
            if (colonistObject != null)
                Object.DestroyImmediate(colonistObject);
        }

        [Test]
        public void SleepyIconFollowsStatsAndNameIsDisplayed()
        {
            colonistObject = new GameObject("Colonist");
            ColonistStatsComponent stats = colonistObject.AddComponent<ColonistStatsComponent>();

            GameObject overheadObject = new GameObject("OverheadDisplay");
            overheadObject.transform.SetParent(colonistObject.transform, false);
            GameObject nameObject = new GameObject("Name");
            nameObject.transform.SetParent(overheadObject.transform, false);
            TextMeshPro nameText = nameObject.AddComponent<TextMeshPro>();
            GameObject stackObject = new GameObject("StatusIcons");
            stackObject.transform.SetParent(overheadObject.transform, false);
            GameObject sleepyIcon = new GameObject("SleepyZ");
            sleepyIcon.transform.SetParent(stackObject.transform, false);
            sleepyIcon.SetActive(false);

            ColonistOverheadDisplay display =
                overheadObject.AddComponent<ColonistOverheadDisplay>();
            SetPrivateField(display, "stats", stats);
            SetPrivateField(display, "nameText", nameText);
            SetPrivateField(display, "iconStack", stackObject.transform);
            SetPrivateField(display, "sleepyIcon", sleepyIcon);
            SetPrivateField(display, "displayName", "Bob");

            SetPrivateField(stats, "fatigue", 69f);
            InvokePrivate(display, "RefreshPresentation");
            Assert.That(nameText.text, Is.EqualTo("Bob"));
            Assert.That(sleepyIcon.activeSelf, Is.False);

            SetPrivateField(stats, "fatigue", 70f);
            InvokePrivate(display, "RefreshPresentation");
            Assert.That(sleepyIcon.activeSelf, Is.True);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing private method {methodName}.");
            method.Invoke(target, null);
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
