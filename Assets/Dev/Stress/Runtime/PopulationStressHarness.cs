using System;
using AsteroidColony;
using Colony.Interactions;
using UnityEngine;

namespace AsteroidColony.Stress
{
    public enum StressScenario
    {
        PairedSmallFixture,
        ProductionPopulation,
        StaggeredPopulation,
        FoodContention,
        RecreationContention,
        FailureRecovery
    }

    /// <summary>
    /// Manual-run controller for the generated stress scene. It changes only
    /// the simulation speed and observation lifecycle; scenario construction is
    /// performed by the editor builder so the runtime remains production-like.
    /// </summary>
    public sealed class PopulationStressHarness : MonoBehaviour
    {
        [SerializeField] private SimulationManager simulationManager;
        [SerializeField] private StressTelemetry telemetry;
        [SerializeField] private PopulationStressMonitor monitor;
        [SerializeField] private StressScenario scenario = StressScenario.PairedSmallFixture;
        [SerializeField, Range(1f, 1000f)] private float speedMultiplier = 1000f;
        [SerializeField, Min(1f)] private float runDurationSimulationSeconds = 86400f;
        [SerializeField] private bool autoStart;
        [SerializeField] private string exportDirectory;

        private bool running;
        private double startSimulationSeconds;
        private double startRequestedSimulationSeconds;
        private double startPaceShortfallSeconds;
        private long startAdmissionLimitHitCount;
        private StressTelemetrySnapshot lastSnapshot;
        private bool profileApplied;

        public bool IsRunning => running;
        public StressTelemetrySnapshot LastSnapshot => lastSnapshot;
        public StressScenario Scenario => scenario;

        private void Awake()
        {
            if (simulationManager == null)
                simulationManager = SimulationManager.Instance;
            if (telemetry == null)
                telemetry = StressTelemetry.Instance;
            if (monitor == null)
                monitor = GetComponent<PopulationStressMonitor>();

            // The generated lab must be configured before the first logical tick.
            // BeginRun is the explicit start gate for every stress scenario.
            if (simulationManager != null)
                simulationManager.paused = true;
        }

        private void Start()
        {
            if (autoStart)
                BeginRun();
        }

        private void Update()
        {
            if (!running || simulationManager == null)
                return;

            if (simulationManager.SimulationStopReached ||
                simulationManager.CurrentSimulationSeconds - startSimulationSeconds >= runDurationSimulationSeconds)
                EndRun();
        }

        [ContextMenu("Begin Stress Run")]
        public void BeginRun()
        {
            if (running)
                return;
            if (simulationManager == null)
                simulationManager = SimulationManager.Instance;
            if (telemetry == null)
                telemetry = StressTelemetry.Instance;
            if (monitor == null)
                monitor = GetComponent<PopulationStressMonitor>();
            if (simulationManager == null || telemetry == null)
            {
                Debug.LogError("Stress harness requires SimulationManager and StressTelemetry.", this);
                return;
            }

            simulationManager.SetSpeedMultiplier(speedMultiplier);
            simulationManager.paused = true;
            ApplyScenarioProfile();
            telemetry.ResetRun(BuildRunId());
            telemetry.RecordActivityEvent(
                StressEventKind.RunStarted,
                0,
                0,
                string.Empty,
                scenario.ToString(),
                simulationManager.CurrentTick,
                simulationManager.CurrentSimulationSeconds);
            monitor?.BeginObservation();
            startSimulationSeconds = simulationManager.CurrentSimulationSeconds;
            startRequestedSimulationSeconds = simulationManager.RequestedSimulationSeconds;
            startPaceShortfallSeconds = simulationManager.PaceShortfallSeconds;
            startAdmissionLimitHitCount = simulationManager.AdmissionLimitHitCount;
            simulationManager.ArmSimulationStop(
                startSimulationSeconds + runDurationSimulationSeconds);
            running = true;
            simulationManager.paused = false;
        }

        private void ApplyScenarioProfile()
        {
            if (profileApplied)
                return;

            if (monitor == null || monitor.Actors == null || monitor.Actors.Count == 0)
            {
                Debug.LogWarning("Stress scenario profile could not find any configured actors.", this);
                profileApplied = true;
                return;
            }

            bool staggered = scenario == StressScenario.StaggeredPopulation;
            bool foodContention = scenario == StressScenario.FoodContention;
            bool recreationContention = scenario == StressScenario.RecreationContention;

            for (int actorIndex = 0; actorIndex < monitor.Actors.Count; actorIndex++)
            {
                ColonistActivityRunner runner = monitor.Actors[actorIndex];
                if (runner == null)
                    continue;

                ColonistStatsComponent stats = runner.GetComponent<ColonistStatsComponent>();
                if (stats == null)
                    continue;

                ResetNeed(stats);
                if (staggered)
                {
                    ApplyStaggeredNeeds(stats, actorIndex);
                }
                else if (foodContention)
                {
                    stats.AdjustHunger(90f);
                }
                else if (recreationContention)
                {
                    stats.AdjustStimulationNeed(70f);
                }
                else
                {
                    ApplySynchronizedNeeds(stats, actorIndex);
                }
            }

            ApplyShiftProfile(staggered);
            profileApplied = true;
        }

        private static void ResetNeed(ColonistStatsComponent stats)
        {
            stats.AdjustHunger(-stats.Hunger);
            stats.AdjustFatigue(-stats.Fatigue);
            stats.AdjustStimulationNeed(-stats.StimulationNeed);
            stats.AdjustRelaxationNeed(-stats.RelaxationNeed);
        }

        private static void ApplySynchronizedNeeds(
            ColonistStatsComponent stats,
            int actorIndex)
        {
            if (actorIndex >= 120 && actorIndex < 170)
                stats.AdjustHunger(90f);
            else if (actorIndex >= 170 && actorIndex < 190)
                stats.AdjustFatigue(75f);
            else if (actorIndex >= 190)
                stats.AdjustStimulationNeed(70f);
            else
            {
                stats.AdjustHunger(15f);
                stats.AdjustFatigue(10f);
            }
        }

        private static void ApplyStaggeredNeeds(
            ColonistStatsComponent stats,
            int actorIndex)
        {
            // These are deterministic, bounded phase offsets rather than random
            // values, so repeated runs can still be compared meaningfully.
            float hunger = 5f + ((actorIndex * 37 + 11) % 8000) / 100f;
            float fatigue = 5f + ((actorIndex * 53 + 19) % 8000) / 100f;
            float stimulation = ((actorIndex * 71 + 7) % 8500) / 100f;
            float relaxation = ((actorIndex * 29 + 31) % 7000) / 100f;

            // Seed a small critical-hunger cohort without making the test another
            // synchronized food wall: their phases are distributed by index.
            if (actorIndex % 11 == 0)
                hunger = 90f + (actorIndex % 10);

            stats.AdjustHunger(hunger);
            stats.AdjustFatigue(fatigue);
            stats.AdjustStimulationNeed(stimulation);
            stats.AdjustRelaxationNeed(relaxation);
        }

        private void ApplyShiftProfile(bool staggered)
        {
            WorkforceManager workforce = WorkforceManager.Instance;
            if (workforce == null)
                return;

            WorkAssignment[] assignments = new WorkAssignment[workforce.Assignments.Count];
            for (int index = 0; index < assignments.Length; index++)
                assignments[index] = workforce.Assignments[index];

            for (int index = 0; index < assignments.Length; index++)
            {
                WorkAssignment assignment = assignments[index];
                if (assignment == null || !assignment.IsConfigured)
                    continue;

                float startHour = staggered
                    ? Mathf.Repeat(8f + index * 0.37f, SimulationTime.HoursPerDay)
                    : 8f;
                float endHour = Mathf.Repeat(startHour + 8f, SimulationTime.HoursPerDay);
                workforce.Assign(
                    assignment.Colonist,
                    assignment.Workplace,
                    assignment.Role,
                    new DailyShiftWindow(startHour, endHour));
            }
        }

        [ContextMenu("End Stress Run and Export")]
        public void EndRun()
        {
            if (!running)
                return;

            if (simulationManager != null)
            {
                simulationManager.paused = true;
                simulationManager.ClearSimulationStop();
            }
            monitor?.EndObservation();
            telemetry?.RecordActivityEvent(
                StressEventKind.RunCompleted,
                0,
                0,
                string.Empty,
                scenario.ToString(),
                simulationManager != null ? simulationManager.CurrentTick : 0L,
                simulationManager != null ? simulationManager.CurrentSimulationSeconds : 0d);
            lastSnapshot = telemetry != null ? telemetry.CaptureSnapshot() : null;
            running = false;

            if (lastSnapshot != null)
            {
                StressRunManifest manifest = BuildManifest(lastSnapshot);
                string path = StressRunExporter.Export(exportDirectory, manifest, lastSnapshot);
                Debug.Log("High-speed stress run exported to " + path, this);
            }
        }

        public void SetSpeedMultiplier(float value)
        {
            speedMultiplier = Mathf.Clamp(value, 1f, 1000f);
            simulationManager?.SetSpeedMultiplier(speedMultiplier);
        }

        public StressTelemetrySnapshot CaptureSnapshot()
        {
            lastSnapshot = telemetry != null ? telemetry.CaptureSnapshot() : null;
            return lastSnapshot;
        }

        private StressRunManifest BuildManifest(StressTelemetrySnapshot snapshot)
        {
            return new StressRunManifest
            {
                RunId = snapshot.RunId,
                Scenario = scenario.ToString(),
                SourceScene = gameObject.scene.name,
                ObservationMode = telemetry != null ? telemetry.Mode.ToString() : StressObservationMode.Disabled.ToString(),
                Population = monitor != null ? monitor.Actors.Count : 0,
                SpeedMultiplier = Mathf.RoundToInt(speedMultiplier),
                PresentationSpeedFactor = simulationManager != null ? simulationManager.PresentationSpeedFactor : 0f,
                LogicalStepSimulationSeconds = simulationManager != null ? simulationManager.LogicalStepSimulationSeconds : 0f,
                MaxLogicalStepsPerFrame = simulationManager != null ? simulationManager.MaxLogicalStepsPerFrame : 0,
                EventCapacity = snapshot.Events != null ? snapshot.Events.Length : 0,
                FailureCapacity = snapshot.Failures != null ? snapshot.Failures.Length : 0,
                UnityVersion = Application.unityVersion,
                Digest = StressTelemetry.DigestText(snapshot.Digest),
                RequestedSimulationSeconds = simulationManager != null
                    ? simulationManager.RequestedSimulationSeconds - startRequestedSimulationSeconds
                    : 0d,
                CommittedSimulationSeconds = snapshot.ElapsedSimulationSeconds,
                PaceShortfallSeconds = simulationManager != null
                    ? simulationManager.PaceShortfallSeconds - startPaceShortfallSeconds
                    : 0d,
                MaximumObservedDebtSeconds = simulationManager != null
                    ? simulationManager.MaximumObservedDebtSeconds
                    : 0d,
                AdmissionLimitHitCount = simulationManager != null
                    ? simulationManager.AdmissionLimitHitCount - startAdmissionLimitHitCount
                    : 0L
            };
        }

        private string BuildRunId()
        {
            return scenario.ToString() + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
