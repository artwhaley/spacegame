using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Renders one smoke burst for each actual RCS pulse reported by the voyage.
    /// Marker local +Z is exhaust; force on the Shuttle is in the opposite direction.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ShuttleVoyageComponent))]
    public sealed class ShuttleRcsSmokeController : MonoBehaviour
    {
        [Header("Authored setup")]
        [SerializeField] private ShuttleVoyageComponent voyage;
        [SerializeField] private Transform nozzleRoot;
        [SerializeField] private Material smokeMaterial;
        [SerializeField] private ShuttleRcsVfxProfile vfxProfile;

        private readonly List<Nozzle> nozzles = new List<Nozzle>();
        private readonly List<ShuttleRcsPulseEvent> pendingPulses = new List<ShuttleRcsPulseEvent>();
        private Material appliedMaterial;

        private sealed class Nozzle
        {
            public Transform marker;
            public ParticleSystem particles;
            public ParticleSystemRenderer renderer;
        }

        public ShuttleVoyageComponent Voyage => voyage;
        public ShuttleRcsVfxProfile VfxProfile => vfxProfile;
        public int NozzleCount => nozzles.Count;

        private void Awake()
        {
            if (voyage == null)
                voyage = GetComponent<ShuttleVoyageComponent>();
            if (nozzleRoot == null)
                nozzleRoot = transform.Find("RCS");
            BuildNozzleEmitters();
        }

        private void LateUpdate()
        {
            if (smokeMaterial != appliedMaterial)
                ApplyMaterial();
            if (voyage == null)
                return;

            pendingPulses.Clear();
            voyage.DrainRcsPulseEvents(pendingPulses);
            if (vfxProfile == null || smokeMaterial == null)
                return;

            for (int i = 0; i < pendingPulses.Count; i++)
                RenderPulse(pendingPulses[i]);
        }

        [ContextMenu("Preview RCS Burst (All Jets)")]
        public void PreviewBurst()
        {
            if (!Application.isPlaying || vfxProfile == null || smokeMaterial == null)
                return;
            for (int i = 0; i < nozzles.Count; i++)
                EmitBurst(nozzles[i], 1f);
        }

        private void BuildNozzleEmitters()
        {
            if (nozzleRoot == null)
            {
                Debug.LogError($"{nameof(ShuttleRcsSmokeController)} on {name} needs an RCS nozzle root.", this);
                return;
            }

            for (int i = 0; i < nozzleRoot.childCount; i++)
            {
                Transform marker = nozzleRoot.GetChild(i);
                GameObject emitter = new GameObject($"{marker.name}_Smoke");
                emitter.transform.SetParent(marker, false);
                ParticleSystem particles = emitter.AddComponent<ParticleSystem>();
                ConfigureEmitter(particles);
                ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.alignment = ParticleSystemRenderSpace.View;
                renderer.minParticleSize = 0f;
                renderer.maxParticleSize = 1f;
                nozzles.Add(new Nozzle { marker = marker, particles = particles, renderer = renderer });
            }

            ApplyMaterial();
            if (nozzles.Count == 0)
                Debug.LogError($"{nameof(ShuttleRcsSmokeController)} on {name} found no nozzle markers under RCS.", this);
            if (smokeMaterial == null)
                Debug.LogError($"{nameof(ShuttleRcsSmokeController)} on {name} needs an HDRP smoke material.", this);
        }

        private static void ConfigureEmitter(ParticleSystem particles)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 256;
            main.startLifetime = 1f;
            main.startSpeed = 0f;
            main.startSize = 1f;
            main.startColor = Color.white;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;

            Gradient fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(fade);

            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            particles.Play(true);
        }

        private void ApplyMaterial()
        {
            appliedMaterial = smokeMaterial;
            for (int i = 0; i < nozzles.Count; i++)
            {
                nozzles[i].renderer.sharedMaterial = smokeMaterial;
                nozzles[i].renderer.enabled = smokeMaterial != null;
            }
        }

        private void RenderPulse(ShuttleRcsPulseEvent pulse)
        {
            Vector3 linearDirection = pulse.localLinearAcceleration.sqrMagnitude > 0.000001f
                ? pulse.localLinearAcceleration.normalized : Vector3.zero;
            Vector3 angularDirection = pulse.localAngularAcceleration.sqrMagnitude > 0.000001f
                ? pulse.localAngularAcceleration.normalized : Vector3.zero;
            float minimumAlignment = Mathf.Clamp01(vfxProfile.minimumDirectionAlignment);

            for (int i = 0; i < nozzles.Count; i++)
            {
                Nozzle nozzle = nozzles[i];
                Vector3 thrustDirection = -transform.InverseTransformDirection(nozzle.marker.forward).normalized;
                float alignment = float.NegativeInfinity;
                if (linearDirection.sqrMagnitude > 0f)
                    alignment = Vector3.Dot(thrustDirection, linearDirection);
                if (angularDirection.sqrMagnitude > 0f)
                {
                    Vector3 leverArm = transform.InverseTransformPoint(nozzle.marker.position);
                    Vector3 torque = Vector3.Cross(leverArm, thrustDirection);
                    if (torque.sqrMagnitude > 0.000001f)
                        alignment = Mathf.Max(alignment,
                            Vector3.Dot(torque.normalized, angularDirection));
                }
                if (alignment >= minimumAlignment)
                    EmitBurst(nozzle, pulse.strength);
            }
        }

        private void EmitBurst(Nozzle nozzle, float strength)
        {
            float size = Mathf.Max(0.001f, vfxProfile.puffSize);
            float variation = Mathf.Clamp(vfxProfile.puffSizeVariation, 0f, 0.9f);
            float lifetime = Mathf.Max(0.01f, vfxProfile.puffLifetime);
            float speed = Mathf.Max(0f, vfxProfile.exhaustSpeed);
            float spread = Mathf.Tan(Mathf.Clamp(vfxProfile.spreadDegrees, 0f, 89f) * Mathf.Deg2Rad);
            Vector3 outward = nozzle.marker.forward;
            Vector3 origin = nozzle.marker.position + outward *
                (Mathf.Max(0f, vfxProfile.nozzleExitOffset) + size * 0.5f);
            float visualStrength = Mathf.Clamp01(strength);
            Color tint = new Color(1f, 1f, 1f,
                Mathf.Clamp01(vfxProfile.puffOpacity * Mathf.Lerp(0.5f, 1f, visualStrength)));

            int count = Mathf.Clamp(Mathf.RoundToInt(vfxProfile.particlesPerBurst *
                Mathf.Lerp(0.35f, 1f, visualStrength)), 1, 64);
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = (outward + Random.insideUnitSphere * spread).normalized;
                ParticleSystem.EmitParams puff = new ParticleSystem.EmitParams
                {
                    position = origin + outward * Random.Range(0f, speed * lifetime * 0.85f)
                        + Random.insideUnitSphere * size * 0.1f,
                    velocity = direction * speed * Random.Range(0.8f, 1.2f),
                    startSize = size * Random.Range(1f - variation, 1f + variation),
                    startLifetime = lifetime * Random.Range(0.85f, 1.15f),
                    startColor = tint
                };
                nozzle.particles.Emit(puff, 1);
            }
        }
    }
}
