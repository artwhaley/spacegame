using System;
using UnityEngine;

namespace Colony.Interactions
{
    public enum ActivityPlacementReference
    {
        AnimationAnchor,
        ExitAnchor,
        ApproachAnchor,
        CustomAnchor
    }

    [Serializable]
    public sealed class AnimationSegment
    {
        [SerializeField] private AnimationClip clip;
        [SerializeField, Tooltip("Positive plays forward; negative starts at the end and plays backward.")] private float speed = 1f;
        [SerializeField, Min(0f)] private float blendDuration = 1f;
        [SerializeField] private ActivityPlacementReference placementReference = ActivityPlacementReference.AnimationAnchor;
        [SerializeField] private Transform customPlacementAnchor;
        [SerializeField] private Vector3 placementPositionOffset;
        [SerializeField] private Vector3 placementEulerOffset;
        [SerializeField] private AnimationContact[] contacts = Array.Empty<AnimationContact>();

        public AnimationSegment()
        {
            speed = 1f;
            blendDuration = 1f;
        }

        public AnimationSegment(AnimationClip clip, float speed = 1f, float blendDuration = 1f)
        {
            this.clip = clip;
            this.speed = speed;
            this.blendDuration = Mathf.Max(0f, blendDuration);
            placementReference = ActivityPlacementReference.AnimationAnchor;
            contacts = Array.Empty<AnimationContact>();
        }

        public AnimationClip Clip => clip;
        public float Speed => speed;
        public float BlendDuration => blendDuration;
        public ActivityPlacementReference PlacementReference => placementReference;
        public Transform CustomPlacementAnchor => customPlacementAnchor;
        public Vector3 PlacementPositionOffset => placementPositionOffset;
        public Vector3 PlacementEulerOffset => placementEulerOffset;
        public AnimationContact[] Contacts => contacts;

        public Transform ResolvePlacementAnchor(FacilityActivityBinding binding)
        {
            if (binding == null)
            {
                return null;
            }

            switch (placementReference)
            {
                case ActivityPlacementReference.ExitAnchor:
                    return binding.ExitAnchor;
                case ActivityPlacementReference.ApproachAnchor:
                    return binding.ApproachAnchor;
                case ActivityPlacementReference.CustomAnchor:
                    return customPlacementAnchor;
                default:
                    return binding.AnimationAnchor;
            }
        }

        public void GetWorldPlacement(Transform anchor, out Vector3 position, out Quaternion rotation)
        {
            if (anchor == null)
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                return;
            }

            position = anchor.TransformPoint(placementPositionOffset);
            rotation = anchor.rotation * Quaternion.Euler(placementEulerOffset);
        }

        public void GetWorldPlacement(FacilityActivityBinding binding, out Vector3 position, out Quaternion rotation)
        {
            GetWorldPlacement(ResolvePlacementAnchor(binding), out position, out rotation);
        }
    }
}
