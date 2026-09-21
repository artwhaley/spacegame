using System;
using UnityEngine;
using UnityEngine.AI;

namespace Colony.Interactions
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class ColonistMotor : MonoBehaviour
    {
        private const string SpeedParameter = "Speed";
        private const string TurnParameter = "Turn";

        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Animator animator;
        [SerializeField, Min(0.01f)] private float arrivalEpsilon = 0.1f;
        [SerializeField, Min(0f)] private float animatorDampTime = 0.1f;
        [SerializeField, Min(0.01f)] private float destinationSampleDistance = 1f;
        [SerializeField, Min(1f)] private float activityFacingSpeed = 360f;
        [SerializeField, Range(10f, 180f)] private float turnBlendAngle = 70f;

        private NavMeshPath calculatedPath;
        private bool hasDestination;
        private bool activityMotionActive;
        private bool placementBlendActive;
        private bool holdPlacementRootAfterBlend;
        private Vector3 placementStartPosition;
        private Quaternion placementStartRotation;
        private Vector3 placementTargetPosition;
        private Quaternion placementTargetRotation;
        private float placementBlendDuration;
        private float placementBlendElapsed;
        private int placementBlendFrame = -1;
        private bool navigationReturnTargetValid;
        private Vector3 navigationReturnPosition;
        private Quaternion navigationReturnRotation;
        private bool activityFacingActive;
        private Quaternion activityFacingTarget;
        private bool hasTurnParameter;

        public bool IsNavigating { get; private set; }
        public bool IsInActivityMotion => activityMotionActive;

        public event Action Arrived;
        public event Action<string> MoveFailed;
        public event Action PlacementBlendCompleted;
        public event Action ActivityFacingAligned;

        private void Awake()
        {
            calculatedPath = new NavMeshPath();

            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            // This motor applies animation deltas explicitly in OnAnimatorMove.
            // Prefab-specific automatic root motion must not compete with it.
            if (animator != null)
            {
                animator.applyRootMotion = false;
                hasTurnParameter = HasFloatParameter(animator, TurnParameter);
            }

            if (agent != null)
            {
                agent.updatePosition = true;
                ConfigureNavigationRotation();
            }
        }

        private void Update()
        {
            UpdateLocomotionAnimation();

            if (!hasDestination || agent == null || agent.pathPending)
            {
                return;
            }

            if (agent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                Fail("The NavMesh path is not complete.");
                return;
            }

            Vector3 planarVelocity = agent.velocity;
            planarVelocity.y = 0f;

            float arrivalDistance = Mathf.Max(agent.stoppingDistance, arrivalEpsilon);
            if (agent.remainingDistance <= arrivalDistance && planarVelocity.sqrMagnitude <= 0.01f)
            {
                CompleteArrival();
            }
        }

        public bool MoveTo(Transform target)
        {
            if (target == null)
            {
                return Fail("MoveTo received a null target.");
            }

            return MoveTo(target.position);
        }

        public bool MoveTo(Vector3 destination)
        {
            if (agent == null || !agent.isActiveAndEnabled)
            {
                return Fail("MoveTo requires an enabled NavMeshAgent.");
            }

            if (!agent.isOnNavMesh)
            {
                return Fail("MoveTo requires the actor to be on a NavMesh.");
            }

            if (!NavMesh.SamplePosition(
                    destination,
                    out NavMeshHit hit,
                    destinationSampleDistance,
                    agent.areaMask))
            {
                return Fail("The destination is not near a usable NavMesh area.");
            }

            if (!agent.CalculatePath(hit.position, calculatedPath) ||
                calculatedPath.status != NavMeshPathStatus.PathComplete)
            {
                return Fail("The destination does not have a complete NavMesh path.");
            }

            if (!agent.SetDestination(hit.position))
            {
                return Fail("The NavMeshAgent rejected the destination.");
            }

            hasDestination = true;
            IsNavigating = true;
            agent.isStopped = false;
            return true;
        }

        public void Stop()
        {
            hasDestination = false;
            IsNavigating = false;

            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }

            SetAnimatorSpeed(0f);
            SetAnimatorTurn(0f);
        }

        public bool BeginActivityMotion()
        {
            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            {
                return Fail("Activity motion requires an enabled NavMeshAgent on the NavMesh.");
            }

            hasDestination = false;
            IsNavigating = false;
            agent.isStopped = true;
            agent.ResetPath();
            agent.updatePosition = false;
            agent.updateRotation = false;
            CancelPlacementBlend();
            holdPlacementRootAfterBlend = false;
            activityFacingActive = false;
            navigationReturnTargetValid = false;
            activityMotionActive = true;
            SetAnimatorSpeed(0f);
            SetAnimatorTurn(0f);
            return true;
        }

        public bool BeginActivityFacingAlignment(Quaternion targetRotation)
        {
            if (!activityMotionActive)
            {
                return Fail("Activity facing requires active activity motion.");
            }

            activityFacingTarget = Quaternion.Euler(
                0f,
                targetRotation.eulerAngles.y,
                0f);

            // Complete alignment after Animator evaluation, including when
            // already facing the target. Entry must not start inside Update.
            activityFacingActive = true;
            return true;
        }

        public bool BeginPlacementBlend(
            Transform animationAnchor,
            AnimationSegment segment,
            bool holdRootAfterBlend = false)
        {
            if (animationAnchor == null || segment == null)
            {
                return Fail("Activity placement requires an animation anchor and animation segment.");
            }

            segment.GetWorldPlacement(
                animationAnchor,
                out Vector3 targetPosition,
                out Quaternion targetRotation);

            return BeginPlacementBlend(
                targetPosition,
                targetRotation,
                segment.BlendDuration,
                holdRootAfterBlend);
        }

        public bool BeginReturnToNavigationBlend(Transform exitAnchor, float blendDuration)
        {
            if (exitAnchor == null)
            {
                return Fail("Returning from activity motion requires an exit anchor.");
            }

            if (!NavMesh.SamplePosition(
                    exitAnchor.position,
                    out NavMeshHit hit,
                    destinationSampleDistance,
                    agent.areaMask))
            {
                return Fail("The exit anchor is not on a usable NavMesh area.");
            }

            navigationReturnPosition = hit.position;
            navigationReturnRotation = exitAnchor.rotation;
            navigationReturnTargetValid = true;
            holdPlacementRootAfterBlend = false;
            return BeginPlacementBlend(
                navigationReturnPosition,
                navigationReturnRotation,
                blendDuration);
        }

        public bool EndActivityMotion()
        {
            if (agent == null || !agent.isActiveAndEnabled)
            {
                return Fail("Returning from activity motion requires an enabled NavMeshAgent.");
            }

            if (!navigationReturnTargetValid)
            {
                return Fail("Returning from activity motion requires a prepared navigation target.");
            }

            activityMotionActive = false;
            activityFacingActive = false;
            CancelPlacementBlend();
            holdPlacementRootAfterBlend = false;
            if (!agent.Warp(navigationReturnPosition))
            {
                activityMotionActive = true;
                return Fail("The NavMeshAgent could not reattach at the exit anchor.");
            }

            transform.SetPositionAndRotation(
                navigationReturnPosition,
                navigationReturnRotation);
            agent.updatePosition = true;
            ConfigureNavigationRotation();
            agent.isStopped = true;
            agent.ResetPath();
            navigationReturnTargetValid = false;
            SetAnimatorSpeed(0f);
            return true;
        }

        // Aborts activity motion without an exit animation: gives the root back to the
        // NavMeshAgent and puts the actor on the closest usable NavMesh position, so a
        // later MoveTo can navigate normally instead of moving invisibly.
        public bool AbortActivityMotion()
        {
            if (!activityMotionActive)
            {
                return true;
            }

            CancelPlacementBlend();
            activityFacingActive = false;
            holdPlacementRootAfterBlend = false;
            navigationReturnTargetValid = false;
            hasDestination = false;
            IsNavigating = false;

            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            {
                return Fail("Aborting activity motion requires an enabled NavMeshAgent on the NavMesh.");
            }

            Vector3 resumePosition;
            if (NavMesh.SamplePosition(
                    transform.position,
                    out NavMeshHit hit,
                    destinationSampleDistance,
                    agent.areaMask))
            {
                resumePosition = hit.position;
            }
            else
            {
                // Nowhere walkable nearby; fall back to where the agent last stood.
                resumePosition = agent.nextPosition;
            }

            Quaternion resumeRotation = transform.rotation;
            if (!agent.Warp(resumePosition))
            {
                activityMotionActive = true;
                return Fail("The NavMeshAgent could not reattach after the failed activity.");
            }

            activityMotionActive = false;
            transform.SetPositionAndRotation(resumePosition, resumeRotation);
            agent.updatePosition = true;
            ConfigureNavigationRotation();
            agent.isStopped = true;
            agent.ResetPath();
            SetAnimatorSpeed(0f);
            SetAnimatorTurn(0f);
            return true;
        }

        private void OnAnimatorMove()
        {
            if (!activityMotionActive || animator == null || activityFacingActive)
            {
                return;
            }

            if (placementBlendActive)
            {
                // Placement owns the actor root during alignment. Applying the
                // animation delta here as well would stack authored root travel
                // on top of the fitted world-space placement.
                AdvancePlacementBlend();
            }
            else if (!holdPlacementRootAfterBlend)
            {
                transform.SetPositionAndRotation(
                    transform.position + animator.deltaPosition,
                    animator.deltaRotation * transform.rotation);
            }
        }

        private void LateUpdate()
        {
            if (activityFacingActive)
            {
                AdvanceActivityFacing();
                return;
            }

            if (activityMotionActive && placementBlendActive &&
                placementBlendFrame != Time.frameCount)
            {
                AdvancePlacementBlend();
            }
        }

        private void CompleteArrival()
        {
            hasDestination = false;
            IsNavigating = false;
            agent.isStopped = true;
            agent.ResetPath();
            SetAnimatorSpeed(0f);
            Arrived?.Invoke();
        }

        private bool Fail(string reason)
        {
            hasDestination = false;
            IsNavigating = false;
            SetAnimatorSpeed(0f);
            SetAnimatorTurn(0f);
            Debug.LogWarning($"{nameof(ColonistMotor)} on {name}: {reason}", this);
            MoveFailed?.Invoke(reason);
            return false;
        }

        private void UpdateLocomotionAnimation()
        {
            if (animator == null || agent == null)
            {
                return;
            }

            Vector3 planarVelocity = agent.velocity;
            planarVelocity.y = 0f;

            float configuredWalkSpeed = Mathf.Max(agent.speed, 0.01f);
            float normalizedSpeed = Mathf.Clamp01(planarVelocity.magnitude / configuredWalkSpeed);
            SetAnimatorSpeed(normalizedSpeed);

            if (!hasTurnParameter || activityMotionActive || !hasDestination || agent.isStopped)
            {
                SetAnimatorTurn(0f);
                return;
            }

            Vector3 desiredVelocity = agent.desiredVelocity;
            desiredVelocity.y = 0f;
            if (desiredVelocity.sqrMagnitude <= 0.0001f)
            {
                SetAnimatorTurn(0f);
                return;
            }

            float signedAngle = Vector3.SignedAngle(
                transform.forward,
                desiredVelocity.normalized,
                Vector3.up);
            float normalizedTurn = Mathf.Clamp(
                signedAngle / Mathf.Max(turnBlendAngle, 1f),
                -1f,
                1f);
            SetAnimatorTurn(normalizedTurn);

            Quaternion desiredRotation = Quaternion.LookRotation(
                desiredVelocity.normalized,
                Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                desiredRotation,
                Mathf.Max(agent.angularSpeed, 1f) * Time.deltaTime);
        }

        private void SetAnimatorSpeed(float speed)
        {
            if (animator != null)
            {
                animator.SetFloat(SpeedParameter, speed, animatorDampTime, Time.deltaTime);
            }
        }

        private void SetAnimatorTurn(float turn)
        {
            if (animator != null && hasTurnParameter)
            {
                animator.SetFloat(TurnParameter, turn, animatorDampTime, Time.deltaTime);
            }
        }

        private void ConfigureNavigationRotation()
        {
            if (agent != null)
            {
                // A controller with a Turn parameter owns the visual turn blend;
                // the motor rotates the actor toward the NavMesh steering vector.
                // Older controllers retain Unity NavMeshAgent rotation.
                agent.updateRotation = !hasTurnParameter;
            }
        }

        private static bool HasFloatParameter(Animator targetAnimator, string parameterName)
        {
            AnimatorControllerParameter[] parameters = targetAnimator.parameters;
            for (int index = 0; index < parameters.Length; index++)
            {
                AnimatorControllerParameter parameter = parameters[index];
                if (parameter.name == parameterName &&
                    parameter.type == AnimatorControllerParameterType.Float)
                {
                    return true;
                }
            }

            return false;
        }

        private bool BeginPlacementBlend(
            Vector3 targetPosition,
            Quaternion targetRotation,
            float blendDuration,
            bool holdRootAfterBlend = false)
        {
            if (!activityMotionActive)
            {
                return Fail("Activity placement requires activity motion to be active.");
            }

            placementStartPosition = transform.position;
            placementStartRotation = transform.rotation;
            placementTargetPosition = targetPosition;
            placementTargetRotation = targetRotation;
            placementBlendDuration = Mathf.Max(0f, blendDuration);
            placementBlendElapsed = 0f;
            placementBlendFrame = -1;
            holdPlacementRootAfterBlend = holdRootAfterBlend;
            placementBlendActive = placementBlendDuration > 0.0001f;

            if (!placementBlendActive)
            {
                ApplyPlacementCorrection(1f);
                PlacementBlendCompleted?.Invoke();
            }

            return true;
        }

        private void AdvancePlacementBlend()
        {
            if (!placementBlendActive || placementBlendFrame == Time.frameCount)
            {
                return;
            }

            placementBlendFrame = Time.frameCount;
            placementBlendElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(
                placementBlendElapsed / placementBlendDuration);
            ApplyPlacementCorrection(progress);

            if (progress >= 1f)
            {
                placementBlendActive = false;
                PlacementBlendCompleted?.Invoke();
            }
        }

        private void ApplyPlacementCorrection(float progress)
        {
            transform.SetPositionAndRotation(
                Vector3.Lerp(placementStartPosition, placementTargetPosition, progress),
                Quaternion.Slerp(placementStartRotation, placementTargetRotation, progress));
        }

        private void CancelPlacementBlend()
        {
            placementBlendActive = false;
            placementBlendElapsed = 0f;
            placementBlendFrame = -1;
        }

        private void AdvanceActivityFacing()
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                activityFacingTarget,
                activityFacingSpeed * Time.deltaTime);

            if (Quaternion.Angle(transform.rotation, activityFacingTarget) > 0.1f)
            {
                return;
            }

            transform.rotation = activityFacingTarget;
            if (animator != null && animator.isHuman)
            {
                // Seed the action from the completed world-space turn, rather
                // than the Animator's root orientation from the previous pose.
                animator.rootRotation = activityFacingTarget;
            }
            activityFacingActive = false;
            ActivityFacingAligned?.Invoke();
        }
    }
}
