using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Inspector-visible rolling list of recent simulation events. Every entry is also
    /// sent to Debug.Log. Prototype-grade logging; intentionally not a telemetry framework.
    /// </summary>
    public class SimulationLog : MonoBehaviour
    {
        public static SimulationLog Instance { get; private set; }

        public int capacity = 100;
        public SimulationManager simulationManager;

        [SerializeField] private List<string> entries = new List<string>();

        public IReadOnlyList<string> Entries => entries;

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>Simple static logging entry point used across the prototype.</summary>
        public static void Log(string message)
        {
            if (Instance != null)
                Instance.AddEntry(message);
            else
                Debug.Log(message);
        }

        private void AddEntry(string message)
        {
            float hour = simulationManager != null ? simulationManager.CurrentGameHour : 0f;
            string formatted = $"[{hour:00.00}h] {message}";
            entries.Add(formatted);
            if (entries.Count > capacity)
                entries.RemoveAt(0);
            Debug.Log(formatted);
        }
    }
}