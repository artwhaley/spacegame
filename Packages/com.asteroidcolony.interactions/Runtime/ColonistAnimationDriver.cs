using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Colony.Interactions
{
    [RequireComponent(typeof(Animator))]
    public sealed class ColonistAnimationDriver : MonoBehaviour
    {
        private const int BaseLayer = 0;
        private const string LocomotionState = "Locomotion";
        private const string ActionAState = "ActionA";
        private const string ActionBState = "ActionB";
        private const string ActionAPlaceholder = "ActionA Placeholder";
        private const string ActionBPlaceholder = "ActionB Placeholder";
        private const float DefaultLocomotionBlendDuration = 0.15f;

        [SerializeField] private Animator animator;

        private AnimatorOverrideController overrideController;
        private AnimationClip actionAPlaceholder;
        private AnimationClip actionBPlaceholder;
        private Coroutine playbackCoroutine;
        private int playbackVersion;
        private float activeSegmentBlendDuration = DefaultLocomotionBlendDuration;

        public AnimationSegment CurrentSegment { get; private set; }
        public bool CurrentSegmentIsLoop { get; private set; }

        public bool IsPlaying { get; private set; }
        public string ActiveSlot { get; private set; }

        public event Action<string> StatusChanged;
        public event Action ActiveStarted;
        public event Action SequenceCompleted;

        // Fired when a finite activity has finished its own active steps, so the
        // runner can leave the facility on its own.
        public event Action ActivityBodyCompleted;
        public event Action<AnimationSegment> SegmentStarted;
        public event Action<float> LocomotionTransitionStarted;

        public bool TryGetCurrentSegmentProgress(out float normalizedProgress)
        {
            normalizedProgress = 0f;
            if (animator == null || CurrentSegment == null || string.IsNullOrEmpty(ActiveSlot))
            {
                return false;
            }

            int stateHash = Animator.StringToHash(ActiveSlot);
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(BaseLayer);
            if (state.shortNameHash != stateHash || animator.IsInTransition(BaseLayer))
            {
                return false;
            }

            if (CurrentSegment.Speed < 0f)
            {
                normalizedProgress = 1f - Mathf.Clamp01(
                    state.normalizedTime / Mathf.Max(CurrentSegment.Clip.length, 0.0001f));
            }
            else if (CurrentSegmentIsLoop)
            {
                normalizedProgress = state.normalizedTime - Mathf.Floor(state.normalizedTime);
            }
            else
            {
                normalizedProgress = Mathf.Clamp01(state.normalizedTime);
            }

            return true;
        }

        private void Awake()
        {
            Initialize();
        }

        private void Update()
        {
            ApplyPresentationSpeed();
        }

        public bool PlaySequence(IReadOnlyList<AnimationSegment> segments)
        {
            if (!Initialize())
            {
                return false;
            }

            if (!TryCollectSteps(segments, "action", out List<AnimationSegment> playableSegments))
            {
                return false;
            }

            // Preserve the blend of the activity that is currently being
            // stopped. An empty exit sequence uses it for the handoff to
            // locomotion after StopPlayback clears the old routine.
            float fallbackBlendDuration = activeSegmentBlendDuration;

            // An empty sequence is a legal "this activity has no exit animation".
            // The routine still returns to locomotion so the exit can complete.
            StopPlayback(false);
            int version = ++playbackVersion;
            IsPlaying = true;
            playbackCoroutine = StartCoroutine(
                PlaySequenceRoutine(playableSegments, fallbackBlendDuration, version));
            return true;
        }

        public bool PlayTwoClipDemo(AnimationClip first, AnimationClip second)
        {
            if (first == null || second == null)
            {
                return Fail("The action demo requires two animation clips.");
            }

            AnimationSegment[] segments =
            {
                new AnimationSegment(first, 1f, 0.25f),
                new AnimationSegment(second, 1f, 0.25f)
            };

            return PlaySequence(segments);
        }

        // Plays one whole activity body. Every list is optional:
        // Sustained: entry steps, then hold the loop until the caller stops it
        //            (no loop: hold with no animation).
        // Finite:    entry steps, then play the active steps once and report
        //            ActivityBodyCompleted (no active steps: play the loop once;
        //            no loop either: finish immediately).
        public bool PlayActivity(
            IReadOnlyList<AnimationSegment> entrySteps,
            AnimationSegment loopSegment,
            IReadOnlyList<AnimationSegment> activeSteps,
            ActivityCompletionMode completionMode)
        {
            if (!Initialize())
            {
                return false;
            }

            if (!TryCollectSteps(entrySteps, "entry", out List<AnimationSegment> playableEntry))
            {
                return false;
            }

            bool sustained = completionMode == ActivityCompletionMode.Sustained;
            AnimationSegment loop = loopSegment != null && loopSegment.Clip != null
                ? loopSegment
                : null;

            List<AnimationSegment> bodySteps = new List<AnimationSegment>();
            if (sustained)
            {
                if (loop != null && !IsValidSpeed(loop.Speed))
                {
                    return Fail("The activity loop needs a finite, nonzero speed.");
                }
            }
            else
            {
                if (!TryCollectSteps(activeSteps, "active", out bodySteps))
                {
                    return false;
                }

                if (bodySteps.Count == 0 && loop != null)
                {
                    if (!IsValidSpeed(loop.Speed))
                    {
                        return Fail("The activity loop needs a finite, nonzero speed.");
                    }

                    bodySteps.Add(loop);
                }
            }

            StopPlayback(false);
            activeSegmentBlendDuration = DefaultLocomotionBlendDuration;
            int version = ++playbackVersion;
            IsPlaying = true;
            playbackCoroutine = StartCoroutine(
                PlayActivityRoutine(playableEntry, loop, bodySteps, sustained, version));
            return true;
        }

        private bool TryCollectSteps(
            IReadOnlyList<AnimationSegment> steps,
            string label,
            out List<AnimationSegment> collected)
        {
            collected = new List<AnimationSegment>();
            if (steps == null)
            {
                return true;
            }

            for (int index = 0; index < steps.Count; index++)
            {
                AnimationSegment segment = steps[index];
                if (segment == null || segment.Clip == null)
                {
                    // A spare, unconfigured row must not fail the whole activity.
                    continue;
                }

                if (!IsValidSpeed(segment.Speed))
                {
                    return Fail($"Each {label} step needs a finite, nonzero speed.");
                }

                collected.Add(segment);
            }

            return true;
        }

        public void StopPlayback(bool returnToLocomotion)
        {
            playbackVersion++;

            if (playbackCoroutine != null)
            {
                StopCoroutine(playbackCoroutine);
                playbackCoroutine = null;
            }

            IsPlaying = false;
            ActiveSlot = null;
            CurrentSegment = null;
            CurrentSegmentIsLoop = false;

            if (!Initialize())
            {
                return;
            }

            ApplyPresentationSpeed();
            if (returnToLocomotion)
            {
                animator.CrossFadeInFixedTime(
                    Animator.StringToHash(LocomotionState),
                    activeSegmentBlendDuration,
                    BaseLayer,
                    0f);
            }
        }

        // Sequence routines use Animator's normalized loop time instead of
        // guessing from a clip-length timer.  This is only meaningful while a
        // sustained loop is the active action state.
        public bool TryGetCompletedLoopCount(out int completedLoops)
        {
            completedLoops = 0;

            if (!IsPlaying || animator == null || string.IsNullOrEmpty(ActiveSlot))
            {
                return false;
            }

            int stateHash = Animator.StringToHash(ActiveSlot);
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(BaseLayer);
            if (state.shortNameHash != stateHash || animator.IsInTransition(BaseLayer))
            {
                return false;
            }

            completedLoops = Mathf.Max(0, Mathf.FloorToInt(state.normalizedTime));
            return true;
        }

        private bool Initialize()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return Fail("The animation driver requires an Animator with a controller.");
            }

            if (overrideController != null)
            {
                return true;
            }

            RuntimeAnimatorController sourceController = animator.runtimeAnimatorController;
            AnimatorOverrideController existingOverride =
                sourceController as AnimatorOverrideController;
            if (existingOverride != null)
            {
                sourceController = existingOverride.runtimeAnimatorController;
            }

            if (sourceController == null)
            {
                return Fail("The Animator controller could not be resolved.");
            }

            actionAPlaceholder = FindClip(sourceController, ActionAPlaceholder);
            actionBPlaceholder = FindClip(sourceController, ActionBPlaceholder);
            if (actionAPlaceholder == null || actionBPlaceholder == null)
            {
                return Fail("The shared controller is missing ActionA or ActionB placeholders.");
            }

            overrideController = new AnimatorOverrideController(sourceController);
            animator.runtimeAnimatorController = overrideController;
            return true;
        }

        private IEnumerator PlaySequenceRoutine(
            List<AnimationSegment> segments,
            float fallbackBlendDuration,
            int version)
        {
            StatusChanged?.Invoke(segments.Count == 0
                ? "No exit animation; returning to locomotion."
                : $"Action sequence started ({segments.Count} segments).");

            for (int index = 0; index < segments.Count; index++)
            {
                int slot = GetInactiveSlot();
                yield return PlaySegmentRoutine(segments[index], slot, version);

                if (version != playbackVersion)
                {
                    yield break;
                }
            }

            yield return ReturnToLocomotionRoutine(
                segments.Count == 0 ? fallbackBlendDuration : DefaultLocomotionBlendDuration,
                version);
            if (version != playbackVersion)
            {
                yield break;
            }

            ActiveSlot = null;
            IsPlaying = false;
            CurrentSegment = null;
            CurrentSegmentIsLoop = false;
            playbackCoroutine = null;
            StatusChanged?.Invoke("Action sequence complete.");
            SequenceCompleted?.Invoke();
        }

        private IEnumerator PlayActivityRoutine(
            List<AnimationSegment> entrySteps,
            AnimationSegment loopSegment,
            List<AnimationSegment> bodySteps,
            bool sustained,
            int version)
        {
            StatusChanged?.Invoke($"Activity entry started ({entrySteps.Count} segments).");

            for (int index = 0; index < entrySteps.Count; index++)
            {
                int slot = GetInactiveSlot();
                yield return PlaySegmentRoutine(entrySteps[index], slot, version);

                if (version != playbackVersion)
                {
                    yield break;
                }
            }

            if (sustained && loopSegment != null)
            {
                yield return HoldLoopRoutine(loopSegment, version);
                yield break;
            }

            playbackCoroutine = null;
            StatusChanged?.Invoke("Activity is active.");
            // The callback can start exit playback; finish our bookkeeping first.
            ActiveStarted?.Invoke();

            if (version != playbackVersion)
            {
                yield break;
            }

            if (sustained)
            {
                // No loop to hold; the activity simply stays active until stopped.
                IsPlaying = false;
                yield break;
            }

            for (int index = 0; index < bodySteps.Count; index++)
            {
                int slot = GetInactiveSlot();
                yield return PlaySegmentRoutine(bodySteps[index], slot, version);

                if (version != playbackVersion)
                {
                    yield break;
                }
            }

            IsPlaying = false;
            StatusChanged?.Invoke("Activity finished its active steps.");
            ActivityBodyCompleted?.Invoke();
        }

        private IEnumerator HoldLoopRoutine(AnimationSegment loopSegment, int version)
        {
            int loopSlot = GetInactiveSlot();
            AnimationClip placeholder = loopSlot == 0 ? actionAPlaceholder : actionBPlaceholder;
            string stateName = loopSlot == 0 ? ActionAState : ActionBState;
            int stateHash = Animator.StringToHash(stateName);
            AnimationClip loopClip = loopSegment.Clip;

            ApplyOverride(placeholder, loopClip);
            ActiveSlot = stateName;
            CurrentSegment = loopSegment;
            CurrentSegmentIsLoop = true;
            activeSegmentBlendDuration = loopSegment.BlendDuration;
            ApplyPresentationSpeed();
            animator.SetFloat(
                loopSlot == 0 ? "ActionASpeed" : "ActionBSpeed",
                loopSegment.Speed);
            animator.CrossFadeInFixedTime(
                stateHash,
                loopSegment.BlendDuration,
                BaseLayer,
                loopSegment.Speed < 0f ? loopClip.length : 0f);
            SegmentStarted?.Invoke(loopSegment);
            StatusChanged?.Invoke($"Playing {loopClip.name} loop in {stateName}.");

            float timeout = Mathf.Max(10f, loopClip.length + 5f);
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                if (version != playbackVersion)
                {
                    yield break;
                }

                AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(BaseLayer);
                if (currentState.shortNameHash == stateHash && !animator.IsInTransition(BaseLayer))
                {
                    StatusChanged?.Invoke("Activity is active.");
                    playbackCoroutine = null;
                    // The callback can start exit playback; finish our bookkeeping first.
                    ActiveStarted?.Invoke();
                    yield break;
                }

                elapsed += PresentationTime.DeltaTime;
                yield return null;
            }

            Fail($"Animator never entered the {loopClip.name} loop.");
            playbackCoroutine = null;
        }

        private IEnumerator PlaySegmentRoutine(
            AnimationSegment segment,
            int slot,
            int version)
        {
            AnimationClip playbackClip = segment.Clip;
            AnimationClip placeholder = slot == 0 ? actionAPlaceholder : actionBPlaceholder;
            string stateName = slot == 0 ? ActionAState : ActionBState;
            int stateHash = Animator.StringToHash(stateName);

            ApplyOverride(placeholder, playbackClip);
            ActiveSlot = stateName;
            CurrentSegment = segment;
            CurrentSegmentIsLoop = false;
            activeSegmentBlendDuration = segment.BlendDuration;
            ApplyPresentationSpeed();
            animator.SetFloat(slot == 0 ? "ActionASpeed" : "ActionBSpeed", segment.Speed);
            animator.CrossFadeInFixedTime(
                stateHash,
                segment.BlendDuration,
                BaseLayer,
                segment.Speed < 0f ? playbackClip.length : 0f);
            SegmentStarted?.Invoke(segment);
            StatusChanged?.Invoke($"Playing {playbackClip.name} in {stateName}.");

            float timeout = Mathf.Max(10f, playbackClip.length / Mathf.Abs(segment.Speed) + segment.BlendDuration + 5f);
            float elapsed = 0f;
            bool enteredState = false;
            while (elapsed < timeout)
            {
                if (version != playbackVersion)
                {
                    yield break;
                }

                AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(BaseLayer);
                if (currentState.shortNameHash == stateHash && !animator.IsInTransition(BaseLayer))
                {
                    enteredState = true;
                    break;
                }

                elapsed += PresentationTime.DeltaTime;
                yield return null;
            }

            if (!enteredState)
            {
                Fail($"Animator never entered {stateName}.");
                playbackCoroutine = null;
                yield break;
            }

            elapsed = 0f;
            while (elapsed < timeout)
            {
                if (version != playbackVersion)
                {
                    yield break;
                }

                AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(BaseLayer);
                if (currentState.shortNameHash == stateHash &&
                    (segment.Speed < 0f ? currentState.normalizedTime <= 0f : currentState.normalizedTime >= 1f) &&
                    !animator.IsInTransition(BaseLayer))
                {
                    StatusChanged?.Invoke($"Completed {playbackClip.name} in {stateName}.");
                    yield break;
                }

                elapsed += PresentationTime.DeltaTime;
                yield return null;
            }

            Fail($"Animator did not complete {playbackClip.name} in {stateName}.");
            playbackCoroutine = null;
        }

        private IEnumerator ReturnToLocomotionRoutine(
            float blendDuration,
            int version)
        {
            int locomotionHash = Animator.StringToHash(LocomotionState);
            blendDuration = Mathf.Max(0f, blendDuration);

            ApplyPresentationSpeed();
            animator.CrossFadeInFixedTime(
                locomotionHash,
                blendDuration,
                BaseLayer,
                0f);
            LocomotionTransitionStarted?.Invoke(blendDuration);

            float elapsed = 0f;
            while (elapsed < 2f)
            {
                if (version != playbackVersion)
                {
                    yield break;
                }

                AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(BaseLayer);
                if (currentState.shortNameHash == locomotionHash &&
                    !animator.IsInTransition(BaseLayer))
                {
                    yield break;
                }

                elapsed += PresentationTime.DeltaTime;
                yield return null;
            }
        }

        private int GetInactiveSlot()
        {
            AnimatorStateInfo state = animator.IsInTransition(BaseLayer)
                ? animator.GetNextAnimatorStateInfo(BaseLayer)
                : animator.GetCurrentAnimatorStateInfo(BaseLayer);
            return state.shortNameHash == Animator.StringToHash(ActionAState) ? 1 : 0;
        }

        private void ApplyPresentationSpeed()
        {
            if (animator != null)
                animator.speed = PresentationTime.SpeedFactor;
        }

        private static bool IsValidSpeed(float speed)
        {
            return !float.IsNaN(speed) && !float.IsInfinity(speed) && Mathf.Abs(speed) >= 0.01f;
        }

        private void ApplyOverride(AnimationClip placeholder, AnimationClip replacement)
        {
            List<KeyValuePair<AnimationClip, AnimationClip>> overrides =
                new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(overrides);

            for (int index = 0; index < overrides.Count; index++)
            {
                if (overrides[index].Key == placeholder)
                {
                    overrides[index] =
                        new KeyValuePair<AnimationClip, AnimationClip>(placeholder, replacement);
                    overrideController.ApplyOverrides(overrides);
                    return;
                }
            }

            Fail($"The override list does not contain {placeholder.name}.");
        }

        private static AnimationClip FindClip(
            RuntimeAnimatorController controller,
            string clipName)
        {
            AnimationClip[] clips = controller.animationClips;
            for (int index = 0; index < clips.Length; index++)
            {
                if (clips[index] != null &&
                    string.Equals(clips[index].name, clipName, StringComparison.Ordinal))
                {
                    return clips[index];
                }
            }

            return null;
        }

        private bool Fail(string reason)
        {
            playbackVersion++;

            if (playbackCoroutine != null)
            {
                StopCoroutine(playbackCoroutine);
                playbackCoroutine = null;
            }

            IsPlaying = false;
            ActiveSlot = null;
            CurrentSegment = null;
            CurrentSegmentIsLoop = false;
            Debug.LogWarning($"{nameof(ColonistAnimationDriver)} on {name}: {reason}", this);
            StatusChanged?.Invoke($"Action failed: {reason}");
            return false;
        }
    }
}
