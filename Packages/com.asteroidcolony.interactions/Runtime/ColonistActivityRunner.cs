using System;
using System.Collections;
using UnityEngine;

namespace Colony.Interactions
{
    public enum ActivityPhase
    {
        Idle,
        Reserved,
        Navigating,
        ReadyForEntry,
        Busy,
        Failed
    }

    [RequireComponent(typeof(ColonistMotor))]
    public sealed class ColonistActivityRunner : MonoBehaviour
    {
        [SerializeField] private ColonistMotor motor;

        private InteractableFacility currentFacility;
        private FacilityActivityBinding currentBinding;
        private FacilityReservationToken reservation;
        private ColonistAnimationDriver animationDriver;
        private ContactRigDriver contactRigDriver;
        private bool activityActive;
        private bool stopRequested;
        private bool exitInProgress;
        private bool returnPlacementStarted;
        private bool exitAnimationCompleted;
        private bool exitPlacementCompleted;
        private InteractableFacility pendingFacility;
        private string pendingActivityId;
        private IActivitySequence activeSequence;
        private InteractableFacility sequenceFacility;
        private int sequenceCycleIndex;
        private int sequenceStepIndex;
        private Coroutine sequenceCompletionCoroutine;
        private int sequenceCompletionVersion;

        public ActivityPhase Phase { get; private set; } = ActivityPhase.Idle;
        // CurrentActivityId describes the activity request/lifecycle currently being handled,
        // including reservation, navigation, entry, active, and exit.
        public string CurrentActivityId => currentBinding?.ActivityId;
        // ActiveActivityId and ActiveActivityBinding describe the activity the colonist is
        // genuinely performing right now, after entry has completed and before exit begins.
        public bool IsActivityActive =>
            activityActive &&
            !exitInProgress &&
            currentBinding != null;
        public FacilityActivityBinding ActiveActivityBinding =>
            IsActivityActive ? currentBinding : null;
        public InteractableFacility ActiveFacility =>
            IsActivityActive ? currentFacility : null;
        public string ActiveActivityId => ActiveActivityBinding?.ActivityId;
        public string PendingActivityId => pendingActivityId;
        public bool HasActiveRequest => reservation != null && !reservation.IsReleased;
        public string CurrentReservationGroup =>
            reservation != null && !reservation.IsReleased
                ? reservation.ReservationGroup
                : null;
        public bool HasPendingRequest => pendingFacility != null && !string.IsNullOrEmpty(pendingActivityId);
        public bool HasActiveSequence => activeSequence != null;

        public event Action<string> StatusChanged;

        private void Awake()
        {
            if (motor == null)
            {
                motor = GetComponent<ColonistMotor>();
            }

            animationDriver = GetComponent<ColonistAnimationDriver>();
            if (animationDriver == null)
            {
                animationDriver = gameObject.AddComponent<ColonistAnimationDriver>();
            }

            contactRigDriver = GetComponent<ContactRigDriver>();

            if (motor != null)
            {
                motor.Arrived += HandleArrived;
                motor.MoveFailed += HandleMoveFailed;
                motor.PlacementBlendCompleted += HandlePlacementBlendCompleted;
                motor.ActivityFacingAligned += HandleActivityFacingAligned;
            }

            if (animationDriver != null)
            {
                animationDriver.ActiveStarted += HandleActiveStarted;
                animationDriver.SequenceCompleted += HandleSequenceCompleted;
                animationDriver.ActivityBodyCompleted += HandleActivityBodyCompleted;
                animationDriver.StatusChanged += HandleAnimationStatus;
                animationDriver.SegmentStarted += HandleSegmentStarted;
                animationDriver.LocomotionTransitionStarted +=
                    HandleLocomotionTransitionStarted;
            }
        }

        private void OnDestroy()
        {
            if (motor != null)
            {
                motor.Arrived -= HandleArrived;
                motor.MoveFailed -= HandleMoveFailed;
                motor.PlacementBlendCompleted -= HandlePlacementBlendCompleted;
                motor.ActivityFacingAligned -= HandleActivityFacingAligned;
            }

            if (animationDriver != null)
            {
                animationDriver.ActiveStarted -= HandleActiveStarted;
                animationDriver.SequenceCompleted -= HandleSequenceCompleted;
                animationDriver.ActivityBodyCompleted -= HandleActivityBodyCompleted;
                animationDriver.StatusChanged -= HandleAnimationStatus;
                animationDriver.SegmentStarted -= HandleSegmentStarted;
                animationDriver.LocomotionTransitionStarted -=
                    HandleLocomotionTransitionStarted;
            }

            CancelSequenceIntent();
            ReleaseReservation();
        }

        public bool RequestActivity(InteractableFacility facility, string activityId)
        {
            if (!TryResolveBinding(facility, activityId, out FacilityActivityBinding binding, out string error))
            {
                return false;
            }

            // A direct activity request is an explicit override of an automatic
            // sequence.  Keep the current activity's graceful exit path, but do
            // not let the sequence advance to another step afterward.
            CancelSequenceIntent();

            if (HasActiveRequest)
            {
                if (currentFacility == facility &&
                    currentBinding != null &&
                    string.Equals(currentBinding.ActivityId, activityId, StringComparison.Ordinal) &&
                    !exitInProgress &&
                    !HasPendingRequest)
                {
                    StatusChanged?.Invoke($"Already handling {activityId}.");
                    return true;
                }

                if (HasPendingRequest &&
                    pendingFacility == facility &&
                    string.Equals(pendingActivityId, activityId, StringComparison.Ordinal))
                {
                    StatusChanged?.Invoke($"Already pending {activityId}.");
                    return true;
                }

                if (Phase == ActivityPhase.Reserved ||
                    Phase == ActivityPhase.Navigating ||
                    Phase == ActivityPhase.ReadyForEntry)
                {
                    CancelBeforeEntryForReplacement();
                    return StartActivityRequest(facility, binding);
                }

                if (Phase == ActivityPhase.Busy)
                {
                    SetPendingRequest(facility, activityId);
                    stopRequested = true;

                    if (activityActive)
                    {
                        BeginExit();
                    }
                    else if (!exitInProgress)
                    {
                        StatusChanged?.Invoke(
                            $"Pending {activityId}; finishing {CurrentActivityId} before switching.");
                    }

                    return true;
                }
            }

            if (HasActiveRequest)
            {
                StatusChanged?.Invoke($"Busy: already handling {CurrentActivityId}.");
                return false;
            }

            return StartActivityRequest(facility, binding);
        }

        public bool RequestSequence(
            InteractableFacility facility,
            string sequenceId)
        {
            if (facility == null)
            {
                RejectSequence("No facility was provided for the activity sequence.");
                return false;
            }

            if (!facility.TryGetSequence(
                    sequenceId,
                    out FacilitySequenceBinding sequence))
            {
                RejectSequence(
                    $"The facility has no sequence named {sequenceId}.");
                return false;
            }

            if (!sequence.ExternallyRequestable)
            {
                RejectSequence(
                    $"The sequence {sequence.SequenceId} is not externally requestable.");
                return false;
            }

            return StartSequenceRequest(facility, sequence);
        }

        private bool StartSequenceRequest(
            InteractableFacility facility,
            IActivitySequence sequence)
        {
            if (!TryResolveSequence(
                    facility,
                    sequence,
                    out FacilityActivityBinding firstBinding,
                    out string error))
            {
                RejectSequence(error);
                return false;
            }

            if (HasActiveRequest)
            {
                StatusChanged?.Invoke(
                    $"Busy: already handling {CurrentActivityId}; sequence {sequence.SequenceId} was not started.");
                return false;
            }

            if (!facility.TryAcquire(
                    firstBinding.ReservationGroup,
                    this,
                    out FacilityReservationToken acquiredToken))
            {
                Phase = ActivityPhase.Idle;
                StatusChanged?.Invoke(
                    $"Busy: reservation group {firstBinding.ReservationGroup} is occupied.");
                return false;
            }

            activeSequence = sequence;
            sequenceFacility = facility;
            sequenceCycleIndex = 0;
            sequenceStepIndex = 0;
            currentFacility = facility;
            currentBinding = firstBinding;
            reservation = acquiredToken;
            ResetActivityLifecycle();
            StartSequenceStepNavigation();
            return Phase != ActivityPhase.Failed;
        }

        public void Stop()
        {
            // Stop cancels automatic advancement, then lets the current
            // activity use its normal graceful exit when it is already active.
            CancelSequenceIntent();

            if (Phase == ActivityPhase.Busy)
            {
                if (HasPendingRequest)
                {
                    string cancelledActivity = pendingActivityId;
                    ClearPendingRequest();
                    StatusChanged?.Invoke($"Cancelled pending {cancelledActivity}.");
                }

                if (exitInProgress)
                {
                    StatusChanged?.Invoke("Exit is already in progress.");
                    return;
                }

                stopRequested = true;
                if (activityActive)
                {
                    BeginExit();
                }
                else
                {
                    StatusChanged?.Invoke("Stop requested; finishing entry before exit.");
                }

                return;
            }

            if (motor != null)
            {
                motor.Stop();
            }

            ClearPendingRequest();
            string releasedGroup = reservation?.ReservationGroup;
            ReleaseReservation();
            currentFacility = null;
            currentBinding = null;
            Phase = ActivityPhase.Idle;
            StatusChanged?.Invoke(
                string.IsNullOrEmpty(releasedGroup)
                    ? "Idle."
                    : $"Cancelled and released {releasedGroup}.");
        }

        private void ResetActivityLifecycle()
        {
            activityActive = false;
            stopRequested = false;
            exitInProgress = false;
            returnPlacementStarted = false;
            exitAnimationCompleted = false;
            exitPlacementCompleted = false;
        }

        private void StartSequenceStepNavigation()
        {
            if (activeSequence == null || sequenceFacility == null || reservation == null)
            {
                Fail("The activity sequence lost its facility reservation.");
                return;
            }

            ActivitySequenceStep step = activeSequence.Steps[sequenceStepIndex];
            if (!sequenceFacility.TryGetBinding(
                    step.ActivityId,
                    out FacilityActivityBinding binding))
            {
                Fail($"The sequence member {step.ActivityId} is no longer available.");
                return;
            }

            currentFacility = sequenceFacility;
            currentBinding = binding;
            ResetActivityLifecycle();
            Phase = ActivityPhase.Reserved;
            string cycleDescription = activeSequence.IsInfinite
                ? "infinite"
                : $"{sequenceCycleIndex + 1}/{activeSequence.CycleCount}";
            StatusChanged?.Invoke(
                $"Sequence {activeSequence.SequenceId}: cycle {cycleDescription}, " +
                $"starting {binding.ActivityId} " +
                $"(holding {reservation.ReservationGroup}).");

            if (motor == null)
            {
                Fail("The actor could not start navigation for the activity sequence.");
                return;
            }

            if (!motor.MoveTo(binding.ApproachAnchor))
            {
                if (Phase != ActivityPhase.Failed)
                {
                    Fail("The actor could not start sequence navigation to the activity approach.");
                }

                return;
            }

            Phase = ActivityPhase.Navigating;
            StatusChanged?.Invoke($"Navigating to sequence activity {binding.ActivityId}.");
        }

        private bool TryResolveSequence(
            InteractableFacility facility,
            IActivitySequence sequence,
            out FacilityActivityBinding firstBinding,
            out string error)
        {
            firstBinding = null;
            error = null;

            if (facility == null)
            {
                error = "No facility was provided for the activity sequence.";
                return false;
            }

            if (sequence == null)
            {
                error = "No activity sequence was provided.";
                return false;
            }

            ActivitySequenceStep[] steps = sequence.Steps;
            if (steps == null || steps.Length == 0)
            {
                error = $"Sequence {sequence.SequenceId} has no steps.";
                return false;
            }

            string reservationGroup = null;
            for (int index = 0; index < steps.Length; index++)
            {
                ActivitySequenceStep step = steps[index];
                if (step == null || string.IsNullOrWhiteSpace(step.ActivityId))
                {
                    error = $"Sequence {sequence.SequenceId} contains an empty activity step at index {index}.";
                    return false;
                }

                if (!facility.TryGetBinding(
                        step.ActivityId,
                        out FacilityActivityBinding binding))
                {
                    error =
                        $"Sequence {sequence.SequenceId} references missing activity {step.ActivityId}.";
                    return false;
                }

                if (binding.ApproachAnchor == null ||
                    binding.ExitAnchor == null)
                {
                    error =
                        $"Sequence activity {step.ActivityId} needs an approach anchor and exit anchor.";
                    return false;
                }

                if (binding.CompletionMode != ActivityCompletionMode.Sustained)
                {
                    error =
                        $"Sequence activity {step.ActivityId} must be Sustained for this sequence runner.";
                    return false;
                }

                AnimationSegment loopSegment = binding.LoopSegment;
                if (step.DwellSeconds > 0f)
                {
                    if (loopSegment != null &&
                        loopSegment.Clip != null &&
                        loopSegment.Speed <= 0f)
                    {
                        error =
                            $"Sequence step {step.ActivityId} needs a positive loop speed " +
                            "when Dwell Seconds is used.";
                        return false;
                    }
                }
                else
                {
                    if (loopSegment == null || loopSegment.Clip == null)
                    {
                        error = $"Sequence step {step.ActivityId} needs a loop clip for Loop Count.";
                        return false;
                    }

                    if (loopSegment.Speed <= 0f)
                    {
                        error =
                            $"Sequence step {step.ActivityId} needs a positive loop speed for Loop Count.";
                        return false;
                    }
                }

                if (firstBinding == null)
                {
                    firstBinding = binding;
                    reservationGroup = binding.ReservationGroup;
                }
                else if (!string.Equals(
                             reservationGroup,
                             binding.ReservationGroup,
                             StringComparison.Ordinal))
                {
                    error =
                        $"Sequence {sequence.SequenceId} crosses reservation groups; all members must use " +
                        $"{reservationGroup}.";
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(reservationGroup))
            {
                error = $"Sequence {sequence.SequenceId} has no reservation group.";
                return false;
            }

            return true;
        }

        private void RejectSequence(string error)
        {
            if (HasActiveRequest)
            {
                StatusChanged?.Invoke($"Sequence rejected: {error}");
                return;
            }

            Phase = ActivityPhase.Failed;
            StatusChanged?.Invoke($"Failed: {error}");
        }

        private void HandleArrived()
        {
            if (Phase != ActivityPhase.Navigating || currentBinding == null)
            {
                return;
            }

            Phase = ActivityPhase.ReadyForEntry;
            StatusChanged?.Invoke(
                $"Arrived at {currentBinding.ActivityId} approach. Aligning to animation anchor.");

            if (motor == null)
            {
                Fail($"The activity {currentBinding.ActivityId} has no motor.");
                return;
            }

            if (animationDriver == null)
            {
                Fail("The actor has no animation driver.");
                return;
            }

            if (!motor.BeginActivityMotion())
            {
                Fail("The actor could not begin activity motion.");
                return;
            }

            Phase = ActivityPhase.Busy;

            // Entry, loop, active and exit contents are all optional. Facing and
            // placement only exist when something actually plays.
            if (!TryGetFirstPlayableSegment(out AnimationSegment firstSegment))
            {
                StatusChanged?.Invoke(
                    $"{currentBinding.ActivityId} has no animation; using the approach position.");
                StartActivityBody();
                return;
            }

            StatusChanged?.Invoke(
                $"Turning to the {currentBinding.ActivityId} entry facing.");

            if (!TryGetEntryFacing(firstSegment, out Quaternion entryFacing))
            {
                Fail("The activity has no valid placement for its first animation.");
                return;
            }

            if (!motor.BeginActivityFacingAlignment(entryFacing))
            {
                Fail("The actor could not align to the activity entry facing.");
            }
        }

        private bool TryGetFirstPlayableSegment(out AnimationSegment segment)
        {
            segment = null;
            FacilityActivityBinding definition = currentBinding;
            if (definition == null)
            {
                return false;
            }

            // Mirrors the driver's playback order closely enough for facing:
            // entry steps, then the loop, then the one-shot active steps.
            if (TryGetFirstPlayableStep(definition.EntrySteps, out segment))
            {
                return true;
            }

            AnimationSegment loopSegment = definition.LoopSegment;
            if (loopSegment != null && loopSegment.Clip != null)
            {
                segment = loopSegment;
                return true;
            }

            return TryGetFirstPlayableStep(definition.ActiveSteps, out segment);
        }

        private static bool TryGetFirstPlayableStep(
            AnimationSegment[] steps,
            out AnimationSegment segment)
        {
            segment = null;
            if (steps == null)
            {
                return false;
            }

            for (int index = 0; index < steps.Length; index++)
            {
                if (steps[index]?.Clip != null)
                {
                    segment = steps[index];
                    return true;
                }
            }

            return false;
        }

        private bool TryGetEntryFacing(
            AnimationSegment firstSegment,
            out Quaternion entryFacing)
        {
            entryFacing = Quaternion.identity;
            if (firstSegment == null || currentBinding == null)
            {
                return false;
            }

            Transform placementAnchor = firstSegment.ResolvePlacementAnchor(currentBinding);
            if (placementAnchor == null)
            {
                return false;
            }

            firstSegment.GetWorldPlacement(
                placementAnchor,
                out _,
                out entryFacing);
            return true;
        }

        private void HandleActivityFacingAligned()
        {
            if (Phase != ActivityPhase.Busy || currentBinding == null ||
                animationDriver == null || exitInProgress)
            {
                return;
            }

            StatusChanged?.Invoke(
                $"Entry facing aligned; blending into {currentBinding.ActivityId} placement.");
            StartActivityBody();
        }

        private void StartActivityBody()
        {
            if (Phase != ActivityPhase.Busy || currentBinding == null ||
                animationDriver == null || exitInProgress)
            {
                return;
            }

            FacilityActivityBinding definition = currentBinding;
            if (definition == null)
            {
                Fail($"The activity {currentBinding.ActivityId} has no embedded recipe.");
                return;
            }

            if (!animationDriver.PlayActivity(
                    definition.EntrySteps,
                    definition.LoopSegment,
                    definition.ActiveSteps,
                    definition.CompletionMode))
            {
                Fail("The activity animation could not start.");
                return;
            }

            StatusChanged?.Invoke($"Playing {currentBinding.ActivityId}.");
        }

        private void HandleActivityBodyCompleted()
        {
            if (Phase != ActivityPhase.Busy || currentBinding == null || exitInProgress)
            {
                return;
            }

            StatusChanged?.Invoke(
                $"{currentBinding.ActivityId} finished its active steps; leaving the station.");
            BeginExit();
        }

        private void HandleActiveStarted()
        {
            if (Phase == ActivityPhase.Busy && currentBinding != null)
            {
                activityActive = true;
                StatusChanged?.Invoke($"Active: {currentBinding.ActivityId}.");

                if (stopRequested)
                {
                    BeginExit();
                }
                else if (activeSequence != null)
                {
                    StartSequenceCompletionTimer();
                }
            }
        }

        private void StartSequenceCompletionTimer()
        {
            StopSequenceCompletionTimer();

            if (activeSequence == null || currentBinding == null || !activityActive)
            {
                return;
            }

            ActivitySequenceStep step = activeSequence.Steps[sequenceStepIndex];
            int version = sequenceCompletionVersion;
            if (step.DwellSeconds <= 0f)
            {
                sequenceCompletionCoroutine = StartCoroutine(
                    WaitForSequenceLoops(step.LoopCount, version));
            }
            else
            {
                sequenceCompletionCoroutine = StartCoroutine(
                    WaitForSequenceDwell(step.DwellSeconds, version));
            }
        }

        private IEnumerator WaitForSequenceDwell(float duration, int version)
        {
            float elapsed = 0f;
            bool completedFirstLoop = false;
            bool hasLoop = currentBinding != null &&
                           currentBinding.LoopSegment != null &&
                           currentBinding.LoopSegment.Clip != null;

            while (IsSequenceCompletionCurrent(version))
            {
                if (hasLoop &&
                    animationDriver != null &&
                    animationDriver.TryGetCompletedLoopCount(out int completedLoops))
                {
                    completedFirstLoop |= completedLoops >= 1;
                }

                elapsed += PresentationTime.DeltaTime;
                if (elapsed >= duration && (!hasLoop || completedFirstLoop))
                {
                    CompleteSequenceStep();
                    yield break;
                }

                yield return null;
            }
        }

        private IEnumerator WaitForSequenceLoops(int loopCount, int version)
        {
            while (IsSequenceCompletionCurrent(version))
            {
                if (animationDriver != null &&
                    animationDriver.TryGetCompletedLoopCount(out int completedLoops) &&
                    completedLoops >= loopCount)
                {
                    CompleteSequenceStep();
                    yield break;
                }

                yield return null;
            }
        }

        private bool IsSequenceCompletionCurrent(int version)
        {
            return version == sequenceCompletionVersion &&
                   activeSequence != null &&
                   Phase == ActivityPhase.Busy &&
                   activityActive &&
                   !exitInProgress;
        }

        private void CompleteSequenceStep()
        {
            if (activeSequence == null || currentBinding == null || exitInProgress)
            {
                return;
            }

            StopSequenceCompletionTimer();
            stopRequested = true;
            StatusChanged?.Invoke(
                $"Sequence step complete: leaving {currentBinding.ActivityId}.");
            BeginExit();
        }

        private void BeginExit()
        {
            if (exitInProgress || currentBinding == null || animationDriver == null)
            {
                return;
            }

            StopSequenceCompletionTimer();
            exitInProgress = true;
            returnPlacementStarted = false;
            exitAnimationCompleted = false;
            exitPlacementCompleted = false;
            StatusChanged?.Invoke($"Exiting {currentBinding.ActivityId}.");

            if (!animationDriver.PlaySequence(currentBinding.ExitSteps))
            {
                Fail("The activity exit animation could not start.");
            }
        }

        private void HandleSequenceCompleted()
        {
            if (Phase != ActivityPhase.Busy || !exitInProgress || currentBinding == null)
            {
                return;
            }

            exitAnimationCompleted = true;
            CompleteExitWhenReady();
        }

        private void HandleSegmentStarted(AnimationSegment segment)
        {
            if (Phase != ActivityPhase.Busy || currentBinding == null || motor == null)
            {
                return;
            }

            contactRigDriver?.BeginSegment(currentBinding, segment);

            Transform placementAnchor = segment.ResolvePlacementAnchor(currentBinding);

            if (!motor.BeginPlacementBlend(
                    placementAnchor,
                    segment,
                    holdRootAfterBlend: exitInProgress))
            {
                Fail("The actor could not blend to the animation placement.");
            }
        }

        private void HandleLocomotionTransitionStarted(float blendDuration)
        {
            if (Phase != ActivityPhase.Busy || !exitInProgress ||
                currentBinding == null || motor == null)
            {
                return;
            }

            contactRigDriver?.ClearContacts();

            if (currentBinding.ExitAnchor == null)
            {
                Fail("The activity has no exit anchor for navigation recovery.");
                return;
            }

            returnPlacementStarted = true;
            if (!motor.BeginReturnToNavigationBlend(
                    currentBinding.ExitAnchor,
                    blendDuration))
            {
                Fail("The actor could not blend back to the navigation exit.");
            }
        }

        private void HandlePlacementBlendCompleted()
        {
            if (Phase == ActivityPhase.Busy && exitInProgress &&
                returnPlacementStarted)
            {
                exitPlacementCompleted = true;
                CompleteExitWhenReady();
            }
        }

        private void CompleteExitWhenReady()
        {
            if (!exitAnimationCompleted || !exitPlacementCompleted ||
                currentBinding == null || motor == null)
            {
                return;
            }

            if (!motor.EndActivityMotion())
            {
                Fail("The actor could not reattach to the NavMesh at the exit placement.");
                return;
            }

            string completedActivity = currentBinding.ActivityId;
            if (activeSequence != null &&
                sequenceFacility != null &&
                ReferenceEquals(sequenceFacility, currentFacility) &&
                !HasPendingRequest)
            {
                AdvanceSequenceAfterExit(completedActivity);
                return;
            }

            InteractableFacility nextFacility = pendingFacility;
            string nextActivityId = pendingActivityId;
            ClearPendingRequest();
            string releasedGroup = reservation?.ReservationGroup;
            ReleaseReservation();
            currentFacility = null;
            currentBinding = null;
            activityActive = false;
            stopRequested = false;
            exitInProgress = false;
            returnPlacementStarted = false;
            exitAnimationCompleted = false;
            exitPlacementCompleted = false;
            Phase = ActivityPhase.Idle;
            StatusChanged?.Invoke(
                string.IsNullOrEmpty(releasedGroup)
                    ? $"Exited {completedActivity} and returned to navigation."
                    : $"Exited {completedActivity}, cleared the station, and released {releasedGroup}.");

            if (nextFacility != null && !string.IsNullOrEmpty(nextActivityId))
            {
                StatusChanged?.Invoke($"Starting pending {nextActivityId}.");
                RequestActivity(nextFacility, nextActivityId);
            }
        }

        private void AdvanceSequenceAfterExit(string completedActivity)
        {
            IActivitySequence sequence = activeSequence;
            InteractableFacility facility = sequenceFacility;
            if (sequence == null || facility == null)
            {
                Fail("The activity sequence lost its facility during exit.");
                return;
            }

            StopSequenceCompletionTimer();
            sequenceStepIndex++;
            if (sequenceStepIndex >= sequence.Steps.Length)
            {
                sequenceStepIndex = 0;
                sequenceCycleIndex++;
            }

            if (!sequence.IsInfinite && sequenceCycleIndex >= sequence.CycleCount)
            {
                string releasedGroup = reservation?.ReservationGroup;
                ReleaseReservation();
                activeSequence = null;
                sequenceFacility = null;
                currentFacility = null;
                currentBinding = null;
                ResetActivityLifecycle();
                Phase = ActivityPhase.Idle;
                StatusChanged?.Invoke(
                    string.IsNullOrEmpty(releasedGroup)
                        ? $"Sequence {sequence.SequenceId} completed after {sequence.CycleCount} cycles."
                        : $"Sequence {sequence.SequenceId} completed after {sequence.CycleCount} cycles; " +
                          $"cleared the station and released {releasedGroup}.");
                return;
            }

            currentFacility = facility;
            currentBinding = null;
            ResetActivityLifecycle();
            StatusChanged?.Invoke(
                $"Sequence advancing after {completedActivity}; retaining " +
                $"{reservation.ReservationGroup}.");
            StartSequenceStepNavigation();
        }

        private void HandleAnimationStatus(string status)
        {
            if (Phase == ActivityPhase.Busy && status.StartsWith("Action failed:", StringComparison.Ordinal))
            {
                Fail(status);
            }
        }

        private void HandleMoveFailed(string reason)
        {
            if (Phase == ActivityPhase.Navigating || Phase == ActivityPhase.Reserved)
            {
                Fail(reason);
            }
        }

        private bool Fail(string reason)
        {
            CancelSequenceIntent();

            contactRigDriver?.ClearContacts();

            if (animationDriver != null)
            {
                // A failed activity must not leave its animation running.
                animationDriver.StopPlayback(true);
            }

            if (motor != null)
            {
                // Failing mid-activity leaves the NavMeshAgent detached and the root
                // owned by the Animator. Without this the next request silently
                // "navigates" without moving and then blends straight to its anchor.
                motor.AbortActivityMotion();
            }

            ClearPendingRequest();
            ReleaseReservation();
            currentFacility = null;
            currentBinding = null;
            activityActive = false;
            stopRequested = false;
            exitInProgress = false;
            returnPlacementStarted = false;
            exitAnimationCompleted = false;
            exitPlacementCompleted = false;
            Phase = ActivityPhase.Failed;
            StatusChanged?.Invoke($"Failed: {reason}");
            return false;
        }

        private bool TryResolveBinding(
            InteractableFacility facility,
            string activityId,
            out FacilityActivityBinding binding,
            out string error)
        {
            binding = null;
            error = null;

            if (facility == null)
            {
                error = "No facility was provided.";
            }
            else if (!facility.TryGetBinding(activityId, out binding))
            {
                error = $"The facility has no activity named {activityId}.";
            }
            else if (!binding.ExternallyRequestable)
            {
                error = $"The activity {activityId} is not externally requestable.";
            }
            else if (binding.ApproachAnchor == null)
            {
                error = $"The activity {activityId} is missing its embedded recipe or approach anchor.";
            }
            else if (string.IsNullOrWhiteSpace(binding.ReservationGroup))
            {
                error = $"The activity {activityId} has no reservation group.";
            }

            if (error == null)
            {
                return true;
            }

            if (HasActiveRequest)
            {
                StatusChanged?.Invoke($"Request rejected: {error}");
                return false;
            }

            Fail(error);
            return false;
        }

        private bool StartActivityRequest(
            InteractableFacility facility,
            FacilityActivityBinding binding)
        {
            ClearPendingRequest();

            if (!facility.TryAcquire(
                    binding.ReservationGroup,
                    this,
                    out FacilityReservationToken acquiredToken))
            {
                Phase = ActivityPhase.Idle;
                StatusChanged?.Invoke($"Busy: reservation group {binding.ReservationGroup} is occupied.");
                return false;
            }

            currentFacility = facility;
            currentBinding = binding;
            reservation = acquiredToken;
            activityActive = false;
            stopRequested = false;
            exitInProgress = false;
            returnPlacementStarted = false;
            exitAnimationCompleted = false;
            exitPlacementCompleted = false;
            Phase = ActivityPhase.Reserved;
            StatusChanged?.Invoke($"Reserved {binding.ReservationGroup}.");

            if (motor == null)
            {
                return Fail("The actor could not start navigation to the activity approach.");
            }

            if (!motor.MoveTo(binding.ApproachAnchor))
            {
                return Phase == ActivityPhase.Failed
                    ? false
                    : Fail("The actor could not start navigation to the activity approach.");
            }

            Phase = ActivityPhase.Navigating;
            StatusChanged?.Invoke($"Navigating to {binding.ActivityId}.");
            return true;
        }

        private void SetPendingRequest(InteractableFacility facility, string activityId)
        {
            bool replaced = HasPendingRequest;
            pendingFacility = facility;
            pendingActivityId = activityId;
            StatusChanged?.Invoke(
                replaced
                    ? $"Pending request replaced with {activityId}."
                    : $"Pending request: {activityId}.");
        }

        private void CancelBeforeEntryForReplacement()
        {
            CancelSequenceIntent();

            if (motor != null)
            {
                motor.Stop();
            }

            string releasedGroup = reservation?.ReservationGroup;
            ReleaseReservation();
            currentFacility = null;
            currentBinding = null;
            activityActive = false;
            stopRequested = false;
            exitInProgress = false;
            Phase = ActivityPhase.Idle;
            StatusChanged?.Invoke(
                string.IsNullOrEmpty(releasedGroup)
                    ? "Cancelled current request for the newer request."
                    : $"Cancelled current request and released {releasedGroup}.");
        }

        private void StopSequenceCompletionTimer()
        {
            sequenceCompletionVersion++;
            if (sequenceCompletionCoroutine != null)
            {
                StopCoroutine(sequenceCompletionCoroutine);
                sequenceCompletionCoroutine = null;
            }
        }

        private void CancelSequenceIntent()
        {
            StopSequenceCompletionTimer();
            activeSequence = null;
            sequenceFacility = null;
            sequenceCycleIndex = 0;
            sequenceStepIndex = 0;
        }

        private void ClearPendingRequest()
        {
            pendingFacility = null;
            pendingActivityId = null;
        }

        private void ReleaseReservation()
        {
            contactRigDriver?.ClearContacts();
            if (reservation != null && currentFacility != null)
            {
                currentFacility.Release(reservation);
            }

            reservation = null;
        }
    }
}
