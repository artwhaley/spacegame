using System.Collections.Generic;
using Colony.Interactions;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Implemented by systems that advance with the simulation clock.</summary>
    public interface ISimulationTickable
    {
        void SimulationTick(float deltaGameHours);
    }

    /// <summary>Optional ordering contract for tickables that must see earlier state changes.</summary>
    public interface ISimulationTickPriority
    {
        int SimulationTickPriority { get; }
    }

    /// <summary>
    /// Central simulation clock. Owns game time and ticks registered ISimulationTickable
    /// objects. Components register their desired participation independently of
    /// manager creation order; there are no scene scans during a tick.
    /// </summary>
    public class SimulationManager : MonoBehaviour, IPresentationSpeedSource
    {
        private const float SecondsPerGameHour = 3600f;
        private const float MinSpeedMultiplier = 1f;
        private const float MaxSpeedMultiplier = 1000f;
        private const float DefaultSpeedMultiplier = 10f;

        public static SimulationManager Instance { get; private set; }

        private static readonly HashSet<ISimulationTickable> desiredTickables =
            new HashSet<ISimulationTickable>();
        private static readonly Dictionary<ISimulationTickable, long> registrationOrdinals =
            new Dictionary<ISimulationTickable, long>();
        private static long nextRegistrationOrdinal;

        [Header("Control")]
        public bool paused;
        [Range(MinSpeedMultiplier, MaxSpeedMultiplier)]
        public float speedMultiplier = DefaultSpeedMultiplier;

        // Retained so older scenes deserialize cleanly. Simulation time is now
        // derived directly from real seconds and speedMultiplier.
        [HideInInspector]
        public float gameHoursPerRealSecond = 1f;
        public float tickIntervalSeconds = 0.1f;

        [Header("Runtime State")]
        [SerializeField] private float currentGameHour;
        [SerializeField] private long currentTick;

        private readonly List<ISimulationTickable> tickables = new List<ISimulationTickable>();
        private readonly HashSet<ISimulationTickable> pendingAdds = new HashSet<ISimulationTickable>();
        private readonly HashSet<ISimulationTickable> pendingRemoves = new HashSet<ISimulationTickable>();
        private float accumulator;
        private bool ticking;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one active SimulationManager is supported.", this);
                enabled = false;
                return;
            }
            Instance = this;
            PresentationTime.RegisterSource(this);
            ReadinessHistory.BeginSession();
            PruneDesiredTickables();
            AdoptDesiredTickables();
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;
            PresentationTime.UnregisterSource(this);
            Instance = null;
            tickables.Clear();
            pendingAdds.Clear();
            pendingRemoves.Clear();
        }

        public float CurrentGameHour => currentGameHour;
        public long CurrentTick => currentTick;
        public int CurrentDayIndex => SimulationTime.DayIndexAt(currentGameHour);
        public int CurrentDayNumber => SimulationTime.DayNumberAt(currentGameHour);
        public float CurrentHourOfDay => SimulationTime.HourOfDayAt(currentGameHour);

        public float PresentationSpeedFactor
        {
            get
            {
                if (paused)
                    return 0f;

                return EffectiveSpeedMultiplier;
            }
        }

        /// <summary>UI-safe pause command; does not mutate any simulation state.</summary>
        public void SetPaused(bool value) => paused = value;

        /// <summary>UI-safe speed command with the same authoring bounds as the Inspector.</summary>
        public void SetSpeedMultiplier(float value) => speedMultiplier = ClampSpeedMultiplier(value);

        public void TogglePaused() => paused = !paused;

        public static void RegisterTickable(ISimulationTickable tickable)
        {
            if (!IsLiveEnabled(tickable))
                return;
            desiredTickables.Add(tickable);
            if (!registrationOrdinals.ContainsKey(tickable))
                registrationOrdinals[tickable] = nextRegistrationOrdinal++;
            if (Instance != null)
                Instance.QueueAdd(tickable);
        }

        public static void UnregisterTickable(ISimulationTickable tickable)
        {
            if (tickable == null)
                return;
            desiredTickables.Remove(tickable);
            registrationOrdinals.Remove(tickable);
            if (Instance != null)
                Instance.QueueRemove(tickable);
        }

        // Compatibility entry points for existing callers; components use the static API.
        public void Register(ISimulationTickable tickable)
        {
            RegisterTickable(tickable);
        }

        public void Unregister(ISimulationTickable tickable)
        {
            UnregisterTickable(tickable);
        }

        private void Update()
        {
            if (paused)
                return;

            float interval = Mathf.Max(0.0001f, tickIntervalSeconds);
            accumulator += Time.unscaledDeltaTime;
            while (accumulator >= interval)
            {
                accumulator -= interval;
                AdvanceTick(interval);
            }
        }

        private void OnValidate()
        {
            if (float.IsNaN(currentGameHour) || float.IsInfinity(currentGameHour))
                currentGameHour = 0f;
            currentGameHour = Mathf.Max(0f, currentGameHour);

            speedMultiplier = ClampSpeedMultiplier(speedMultiplier);

            if (float.IsNaN(tickIntervalSeconds) || float.IsInfinity(tickIntervalSeconds))
                tickIntervalSeconds = 0.1f;
            tickIntervalSeconds = Mathf.Max(0.0001f, tickIntervalSeconds);
        }

        private void AdvanceTick(float realDeltaSeconds)
        {
            float deltaGameHours = realDeltaSeconds * EffectiveSpeedMultiplier / SecondsPerGameHour;
            currentGameHour += deltaGameHours;
            currentTick++;

            ticking = true;
            ISimulationTickable[] snapshot = tickables.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                ISimulationTickable tickable = snapshot[i];
                if (pendingRemoves.Contains(tickable) || !IsLiveEnabled(tickable))
                    continue;
                tickable.SimulationTick(deltaGameHours);
            }
            ticking = false;
            FlushPending();
        }

        private float EffectiveSpeedMultiplier => ClampSpeedMultiplier(speedMultiplier);

        private static float ClampSpeedMultiplier(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return DefaultSpeedMultiplier;
            return Mathf.Clamp(value, MinSpeedMultiplier, MaxSpeedMultiplier);
        }

        private void AdoptDesiredTickables()
        {
            ISimulationTickable[] desired = new ISimulationTickable[desiredTickables.Count];
            desiredTickables.CopyTo(desired);
            for (int i = 0; i < desired.Length; i++)
                if (IsLiveEnabled(desired[i]))
                    QueueAdd(desired[i]);
            FlushPending();
        }

        private void QueueAdd(ISimulationTickable tickable)
        {
            if (!IsLiveEnabled(tickable))
                return;
            pendingRemoves.Remove(tickable);
            if (ticking)
            {
                pendingAdds.Add(tickable);
                return;
            }
            if (!tickables.Contains(tickable))
            {
                tickables.Add(tickable);
                SortTickables();
                SimulationLog.Log($"Registered tickable: {tickable.GetType().Name}");
            }
        }

        private void QueueRemove(ISimulationTickable tickable)
        {
            pendingAdds.Remove(tickable);
            if (ticking)
            {
                pendingRemoves.Add(tickable);
                return;
            }
            tickables.Remove(tickable);
        }

        private void FlushPending()
        {
            PruneDesiredTickables();
            if (pendingRemoves.Count > 0)
            {
                foreach (ISimulationTickable tickable in pendingRemoves)
                    tickables.Remove(tickable);
                pendingRemoves.Clear();
            }
            if (pendingAdds.Count > 0)
            {
                foreach (ISimulationTickable tickable in pendingAdds)
                    if (IsLiveEnabled(tickable) && !tickables.Contains(tickable))
                        tickables.Add(tickable);
                pendingAdds.Clear();
            }
            for (int i = tickables.Count - 1; i >= 0; i--)
                if (!IsLiveEnabled(tickables[i]) || !desiredTickables.Contains(tickables[i]))
                    tickables.RemoveAt(i);
            SortTickables();
        }

        private static void PruneDesiredTickables()
        {
            List<ISimulationTickable> stale = null;
            foreach (ISimulationTickable tickable in desiredTickables)
            {
                if (IsLiveEnabled(tickable))
                    continue;
                if (stale == null)
                    stale = new List<ISimulationTickable>();
                stale.Add(tickable);
            }

            if (stale == null)
                return;
            for (int i = 0; i < stale.Count; i++)
            {
                desiredTickables.Remove(stale[i]);
                registrationOrdinals.Remove(stale[i]);
            }
        }

        private void SortTickables()
        {
            tickables.Sort(CompareTickables);
        }

        private static int CompareTickables(ISimulationTickable left, ISimulationTickable right)
        {
            int leftPriority = left is ISimulationTickPriority leftOrdered
                ? leftOrdered.SimulationTickPriority : 1000;
            int rightPriority = right is ISimulationTickPriority rightOrdered
                ? rightOrdered.SimulationTickPriority : 1000;
            int priority = leftPriority.CompareTo(rightPriority);
            if (priority != 0)
                return priority;
            long leftOrdinal = registrationOrdinals.TryGetValue(left, out long lo) ? lo : long.MaxValue;
            long rightOrdinal = registrationOrdinals.TryGetValue(right, out long ro) ? ro : long.MaxValue;
            return leftOrdinal.CompareTo(rightOrdinal);
        }

        private static bool IsLiveEnabled(ISimulationTickable tickable)
        {
            if (tickable == null)
                return false;
            Behaviour behaviour = tickable as Behaviour;
            return behaviour == null || behaviour.isActiveAndEnabled;
        }
    }
}
