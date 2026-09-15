using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Implemented by systems that advance with the simulation clock.</summary>
    public interface ISimulationTickable
    {
        void SimulationTick(float deltaGameHours);
    }

    /// <summary>
    /// Central simulation clock. Owns game time and ticks registered ISimulationTickable
    /// objects. Objects register once and are never looked up in the scene per tick.
    /// </summary>
    public class SimulationManager : MonoBehaviour
    {
        public static SimulationManager Instance { get; private set; }

        [Header("Control")]
        public bool paused;
        [Range(0.1f, 10f)]
        public float speedMultiplier = 1f;
        public float gameHoursPerRealSecond = 1f;
        public float tickIntervalSeconds = 0.1f;

        [Header("Runtime State")]
        [SerializeField] private float currentGameHour;
        [SerializeField] private long currentTick;

        private readonly List<ISimulationTickable> tickables = new List<ISimulationTickable>();
        private float accumulator;

        private void Awake()
        {
            Instance = this;
        }

        public float CurrentGameHour => currentGameHour;
        public long CurrentTick => currentTick;

        public void Register(ISimulationTickable tickable)
        {
            if (tickable != null && !tickables.Contains(tickable))
            {
                tickables.Add(tickable);
                SimulationLog.Log($"Registered tickable: {tickable.GetType().Name}");
            }
        }

        public void Unregister(ISimulationTickable tickable)
        {
            if (tickable != null)
                tickables.Remove(tickable);
        }

        private void Update()
        {
            if (paused)
                return;

            accumulator += Time.unscaledDeltaTime;
            while (accumulator >= tickIntervalSeconds)
            {
                accumulator -= tickIntervalSeconds;
                AdvanceTick();
            }
        }

        private void AdvanceTick()
        {
            float deltaGameHours = gameHoursPerRealSecond * speedMultiplier * tickIntervalSeconds;
            currentGameHour += deltaGameHours;
            currentTick++;

            for (int i = 0; i < tickables.Count; i++)
                tickables[i].SimulationTick(deltaGameHours);
        }
    }
}