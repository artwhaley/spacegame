using System;
using AsteroidColony;
using UnityEngine;

namespace AsteroidColony.Stress
{
    public enum StressScenario
    {
        PairedSmallFixture,
        ProductionPopulation,
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
        private StressTelemetrySnapshot lastSnapshot;

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

            if (simulationManager.CurrentSimulationSeconds - startSimulationSeconds >= runDurationSimulationSeconds)
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
            running = true;
        }

        [ContextMenu("End Stress Run and Export")]
        public void EndRun()
        {
            if (!running)
                return;

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
                Digest = StressTelemetry.DigestText(snapshot.Digest)
            };
        }

        private string BuildRunId()
        {
            return scenario.ToString() + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
