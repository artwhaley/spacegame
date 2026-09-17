using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// A component that publishes operational blockers and effect multiplier
    /// contributions for one facility. Staffing is one provider; maintenance,
    /// upgrades, power quality, or hazards may implement the same interface later.
    /// </summary>
    public interface IFacilityPerformanceProvider
    {
        void PublishPerformance(FacilityPerformanceSnapshot snapshot, float absoluteGameHour);
    }

    /// <summary>
    /// Mutable accumulator used while evaluating providers. Multipliers default to
    /// 1.0 and aggregate multiplicatively; any blocker makes the facility
    /// non-operational.
    /// </summary>
    public class FacilityPerformanceSnapshot
    {
        private readonly Dictionary<FacilityEffectDefinition, float> multipliers =
            new Dictionary<FacilityEffectDefinition, float>();
        private readonly List<string> blockReasons = new List<string>();

        public bool Operational { get; private set; }
        public IReadOnlyList<string> BlockReasons => blockReasons;

        public FacilityPerformanceSnapshot()
        {
            Reset();
        }

        public void Reset()
        {
            multipliers.Clear();
            blockReasons.Clear();
            Operational = true;
        }

        /// <summary>Records a reason the facility cannot operate.</summary>
        public void AddBlocker(string reason)
        {
            Operational = false;
            if (!string.IsNullOrEmpty(reason) && !blockReasons.Contains(reason))
                blockReasons.Add(reason);
        }

        /// <summary>Multiplies the current contribution for one effect channel.</summary>
        public void Multiply(FacilityEffectDefinition effect, float multiplier)
        {
            if (effect == null || float.IsNaN(multiplier) || float.IsInfinity(multiplier))
                return;

            float current;
            float value = Mathf.Max(0f, multiplier);
            multipliers[effect] = multipliers.TryGetValue(effect, out current) ? current * value : value;
        }

        /// <summary>Aggregated multiplier for a channel; 1.0 when no provider contributed.</summary>
        public float GetMultiplier(FacilityEffectDefinition effect)
        {
            if (effect == null)
                return 1f;
            float value;
            return multipliers.TryGetValue(effect, out value) ? value : 1f;
        }
    }

    /// <summary>
    /// Aggregates every local <see cref="IFacilityPerformanceProvider"/> into one
    /// operational flag and a set of effect multipliers. It never depends on
    /// staffing directly, and providers are evaluated synchronously on demand so
    /// consumers avoid tick-order dependence.
    /// </summary>
    [DisallowMultipleComponent]
    public class FacilityPerformanceComponent : MonoBehaviour
    {
        [Tooltip("Extra providers beyond the components on this GameObject.")]
        public List<MonoBehaviour> additionalProviders = new List<MonoBehaviour>();

        private readonly List<IFacilityPerformanceProvider> providers = new List<IFacilityPerformanceProvider>();
        private readonly FacilityPerformanceSnapshot snapshot = new FacilityPerformanceSnapshot();

        public bool IsOperational
        {
            get
            {
                Evaluate();
                return snapshot.Operational;
            }
        }

        public IReadOnlyList<string> BlockReasons
        {
            get
            {
                Evaluate();
                return snapshot.BlockReasons;
            }
        }

        public string BlockSummary
        {
            get
            {
                Evaluate();
                if (snapshot.BlockReasons.Count == 0)
                    return string.Empty;
                return string.Join("; ", snapshot.BlockReasons);
            }
        }

        public float GetMultiplier(FacilityEffectDefinition effect)
        {
            Evaluate();
            return snapshot.GetMultiplier(effect);
        }

        public void Evaluate()
        {
            EvaluateAt(CurrentGameHour());
        }

        public void EvaluateAt(float absoluteGameHour)
        {
            RebuildProviders();
            snapshot.Reset();
            for (int i = 0; i < providers.Count; i++)
            {
                IFacilityPerformanceProvider provider = providers[i];
                if (provider != null)
                    provider.PublishPerformance(snapshot, absoluteGameHour);
            }
        }

        public void RebuildProviders()
        {
            providers.Clear();
            MonoBehaviour[] local = GetComponents<MonoBehaviour>();
            for (int i = 0; i < local.Length; i++)
                AddProvider(local[i] as IFacilityPerformanceProvider);
            if (additionalProviders != null)
                for (int i = 0; i < additionalProviders.Count; i++)
                    AddProvider(additionalProviders[i] as IFacilityPerformanceProvider);
        }

        private void AddProvider(IFacilityPerformanceProvider provider)
        {
            MonoBehaviour behaviour = provider as MonoBehaviour;
            if (provider != null && (behaviour == null || behaviour.isActiveAndEnabled) && !providers.Contains(provider))
                providers.Add(provider);
        }


        private static float CurrentGameHour()
        {
            return SimulationManager.Instance != null ? SimulationManager.Instance.CurrentGameHour : 0f;
        }
    }
}
