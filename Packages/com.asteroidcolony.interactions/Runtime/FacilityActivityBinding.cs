using System;
using UnityEngine;

namespace Colony.Interactions
{
    [Serializable]
    public sealed class FacilityActivityBinding
    {
        [SerializeField] private string activityId;
        [SerializeField] private string reservationGroup;
        [SerializeField] private bool externallyRequestable = true;
        [SerializeField] private ActivityCompletionMode completionMode = ActivityCompletionMode.Sustained;
        [SerializeField] private bool overridesFatigueRate;
        [SerializeField] private float fatiguePerGameHour;

        [SerializeField] private Transform approachAnchor;
        [SerializeField] private Transform animationAnchor;
        [SerializeField] private Transform exitAnchor;
        [SerializeField] private Transform targets;

        [SerializeField] private AnimationSegment[] entrySteps = Array.Empty<AnimationSegment>();
        [SerializeField] private AnimationSegment loopStep;
        [SerializeField] private AnimationSegment[] activeSteps = Array.Empty<AnimationSegment>();
        [SerializeField] private AnimationSegment[] exitSteps = Array.Empty<AnimationSegment>();

        public string ActivityId => activityId;
        public string ReservationGroup => reservationGroup;
        public bool ExternallyRequestable => externallyRequestable;
        public ActivityCompletionMode CompletionMode => completionMode;
        public bool OverridesFatigueRate => overridesFatigueRate;
        public float FatiguePerGameHour => fatiguePerGameHour;
        public Transform ApproachAnchor => approachAnchor;
        public Transform AnimationAnchor => animationAnchor;
        public Transform ExitAnchor => exitAnchor;
        public Transform Targets => targets;
        public AnimationSegment[] EntrySteps => entrySteps;
        public AnimationSegment LoopSegment => loopStep;
        public AnimationClip LoopClip => loopStep?.Clip;
        public AnimationSegment[] ActiveSteps => activeSteps;
        public AnimationSegment[] ExitSteps => exitSteps;
    }
}
