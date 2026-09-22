using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AsteroidColony.Tests
{
    public class RuntimeLifecyclePlayModeTests
    {
        private GameObject probeObject;
        private GameObject secondaryObject;
        private GameObject managerObject;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (probeObject != null)
                Object.Destroy(probeObject);
            if (secondaryObject != null)
                Object.Destroy(secondaryObject);
            if (managerObject != null)
                Object.Destroy(managerObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ComponentCreatedBeforeManagerIsDiscoveredAndCanPause()
        {
            probeObject = new GameObject("Lifecycle Probe");
            LifecycleProbe probe = probeObject.AddComponent<LifecycleProbe>();
            yield return null;

            managerObject = new GameObject("Simulation Manager");
            SimulationManager manager = managerObject.AddComponent<SimulationManager>();
            manager.tickIntervalSeconds = 0.001f;
            yield return new WaitForSeconds(0.05f);
            Assert.That(probe.tickCount, Is.GreaterThan(0));

            probeObject.SetActive(false);
            int stoppedAt = probe.tickCount;
            yield return new WaitForSeconds(0.05f);
            Assert.That(probe.tickCount, Is.EqualTo(stoppedAt));

            probeObject.SetActive(true);
            yield return new WaitForSeconds(0.05f);
            Assert.That(probe.tickCount, Is.GreaterThan(stoppedAt));
        }

        [UnityTest]
        public IEnumerator ComponentCreatedAfterManagerIsDiscoveredAndManagerReplacementResumes()
        {
            managerObject = new GameObject("Simulation Manager");
            SimulationManager manager = managerObject.AddComponent<SimulationManager>();
            manager.tickIntervalSeconds = 0.001f;

            probeObject = new GameObject("Lifecycle Probe");
            LifecycleProbe probe = probeObject.AddComponent<LifecycleProbe>();
            yield return new WaitForSeconds(0.05f);
            Assert.That(probe.tickCount, Is.GreaterThan(0));

            int beforeReplacement = probe.tickCount;
            Object.Destroy(managerObject);
            managerObject = null;
            yield return null;

            managerObject = new GameObject("Replacement Simulation Manager");
            SimulationManager replacement = managerObject.AddComponent<SimulationManager>();
            replacement.tickIntervalSeconds = 0.001f;
            yield return new WaitForSeconds(0.05f);
            Assert.That(probe.tickCount, Is.GreaterThan(beforeReplacement));
        }

        [UnityTest]
        public IEnumerator TickPriorityRunsLowerNumbersFirst()
        {
            OrderingProbe.Calls.Clear();
            GameObject lowObject = new GameObject("Low Priority Probe");
            GameObject highObject = new GameObject("High Priority Probe");
            probeObject = lowObject;
            secondaryObject = highObject;
            highObject.AddComponent<OrderingProbe>().priority = 200;
            lowObject.AddComponent<OrderingProbe>().priority = 100;

            managerObject = new GameObject("Simulation Manager");
            SimulationManager manager = managerObject.AddComponent<SimulationManager>();
            manager.tickIntervalSeconds = 0.001f;
            yield return new WaitForSeconds(0.05f);

            Assert.That(OrderingProbe.Calls.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(OrderingProbe.Calls[0], Is.EqualTo("Low Priority Probe"));
            Assert.That(OrderingProbe.Calls[1], Is.EqualTo("High Priority Probe"));
        }

        [UnityTest]
        public IEnumerator SimulationClockCrossesMultipleMidnightsWithoutResetting()
        {
            managerObject = new GameObject("Simulation Manager");
            SimulationManager manager = managerObject.AddComponent<SimulationManager>();
            manager.speedMultiplier = 10f;

            AdvanceTick(manager, 5f * 3600f);

            Assert.That(manager.CurrentGameHour, Is.GreaterThan(48f));
            Assert.That(manager.CurrentDayNumber, Is.GreaterThanOrEqualTo(3));
            Assert.That(manager.CurrentHourOfDay, Is.GreaterThanOrEqualTo(0f));
            Assert.That(manager.CurrentHourOfDay, Is.LessThan(24f));

            yield return null;
        }

        private static void AdvanceTick(SimulationManager simulationManager, float realDeltaSeconds)
        {
            MethodInfo method = typeof(SimulationManager).GetMethod(
                "AdvanceTick",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(simulationManager, new object[] { realDeltaSeconds });
        }

        private sealed class LifecycleProbe : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
        {
            public int tickCount;
            public int SimulationTickPriority => 1000;

            private void OnEnable()
            {
                SimulationManager.RegisterTickable(this);
            }

            private void OnDisable()
            {
                SimulationManager.UnregisterTickable(this);
            }

            public void SimulationTick(float deltaGameHours)
            {
                tickCount++;
            }
        }

        private sealed class OrderingProbe : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
        {
            public static readonly List<string> Calls = new List<string>();
            public int priority;

            public int SimulationTickPriority => priority;

            private void OnEnable()
            {
                SimulationManager.RegisterTickable(this);
            }

            private void OnDisable()
            {
                SimulationManager.UnregisterTickable(this);
            }

            public void SimulationTick(float deltaGameHours)
            {
                Calls.Add(name);
            }
        }
    }
}
