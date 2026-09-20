using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Colony.Interactions
{
    [Serializable]
    public sealed class ContactRigChannelBinding
    {
        [SerializeField] private string channel;
        [SerializeField] private TwoBoneIKConstraint constraint;
        [SerializeField] private Transform targetProxy;

        public string Channel => channel;
        public TwoBoneIKConstraint Constraint => constraint;
        public Transform TargetProxy => targetProxy;
    }

    [DisallowMultipleComponent]
    public sealed class ContactRigDriver : MonoBehaviour
    {
        [SerializeField] private ColonistAnimationDriver animationDriver;
        [SerializeField] private ContactRigChannelBinding[] channels =
            Array.Empty<ContactRigChannelBinding>();
        [SerializeField, Min(0.01f)] private float defaultReleaseSeconds = 0.15f;

        private FacilityActivityBinding activeBinding;
        private AnimationSegment activeSegment;
        private readonly Dictionary<ContactRigChannelBinding, RuntimeChannelState> states =
            new Dictionary<ContactRigChannelBinding, RuntimeChannelState>();

        private sealed class RuntimeChannelState
        {
            public Transform target;
            public Vector3 position;
            public Quaternion rotation = Quaternion.identity;
            public float weight;
        }

        private void Awake()
        {
            if (animationDriver == null)
            {
                animationDriver = GetComponent<ColonistAnimationDriver>();
            }

            ResetRigWeights();
        }

        private void Update()
        {
            float progress = 0f;
            bool hasProgress = animationDriver != null &&
                               animationDriver.TryGetCurrentSegmentProgress(out progress);

            for (int index = 0; index < channels.Length; index++)
            {
                ContactRigChannelBinding mapping = channels[index];
                if (mapping == null || mapping.Constraint == null || mapping.TargetProxy == null)
                {
                    continue;
                }

                if (!states.TryGetValue(mapping, out RuntimeChannelState state))
                {
                    state = new RuntimeChannelState
                    {
                        position = mapping.TargetProxy.position,
                        rotation = mapping.TargetProxy.rotation
                    };
                    states.Add(mapping, state);
                }

                AnimationContact contact = FindContact(mapping.Channel);
                float desiredWeight = contact == null || !hasProgress
                    ? 0f
                    : contact.EvaluateWeight(progress);
                Transform desiredTarget = contact?.Target;

                if (desiredTarget != null && desiredWeight > 0f)
                {
                    state.target = desiredTarget;
                    float targetBlend = desiredWeight > state.weight
                        ? contact.BlendInSeconds
                        : contact.BlendOutSeconds;
                    float targetLerp = CalculateLerp(targetBlend);
                    state.position = Vector3.Lerp(
                        state.position,
                        desiredTarget.position,
                        targetLerp);
                    state.rotation = Quaternion.Slerp(
                        state.rotation,
                        desiredTarget.rotation,
                        targetLerp);
                    mapping.TargetProxy.SetPositionAndRotation(state.position, state.rotation);
                }

                float weightBlend = desiredWeight > state.weight
                    ? contact?.BlendInSeconds ?? defaultReleaseSeconds
                    : contact?.BlendOutSeconds ?? defaultReleaseSeconds;
                state.weight = Mathf.Lerp(state.weight, desiredWeight, CalculateLerp(weightBlend));
                if (Mathf.Abs(state.weight - desiredWeight) < 0.001f)
                {
                    state.weight = desiredWeight;
                }

                mapping.Constraint.weight = state.weight;
                mapping.Constraint.data.targetPositionWeight = contact?.PositionWeight ?? 1f;
                mapping.Constraint.data.targetRotationWeight = contact?.RotationWeight ?? 1f;
                if (state.weight <= 0.001f && desiredTarget == null)
                {
                    state.target = null;
                }
            }
        }

        public void BeginSegment(FacilityActivityBinding binding, AnimationSegment segment)
        {
            activeBinding = binding;
            activeSegment = segment;
            if (segment == null)
            {
                ClearContacts();
            }
        }

        public void ClearContacts()
        {
            activeBinding = null;
            activeSegment = null;
        }

        public void ResetRigWeights()
        {
            states.Clear();
            for (int index = 0; index < channels.Length; index++)
            {
                ContactRigChannelBinding mapping = channels[index];
                if (mapping?.Constraint != null)
                {
                    mapping.Constraint.weight = 0f;
                    mapping.Constraint.data.targetPositionWeight = 1f;
                    mapping.Constraint.data.targetRotationWeight = 1f;
                }
            }
        }

        private AnimationContact FindContact(string channel)
        {
            if (activeBinding == null || activeSegment == null ||
                string.IsNullOrWhiteSpace(channel) || activeSegment.Contacts == null)
            {
                return null;
            }

            AnimationContact[] contacts = activeSegment.Contacts;
            for (int index = 0; index < contacts.Length; index++)
            {
                AnimationContact contact = contacts[index];
                if (contact != null &&
                    string.Equals(contact.Channel, channel, StringComparison.Ordinal) &&
                    contact.Target != null)
                {
                    return contact;
                }
            }

            return null;
        }

        private static float CalculateLerp(float duration)
        {
            return duration <= 0.0001f
                ? 1f
                : 1f - Mathf.Exp(-Time.deltaTime / duration);
        }
    }
}
