using Colony.Interactions;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Game-side composition root for a physical colonist actor.
    ///
    /// The interaction package owns navigation and facility playback. This
    /// component is the small game-specific bridge that keeps logical transit
    /// state on ColonistAgent without making the reusable package know about it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ColonistAgent))]
    [RequireComponent(typeof(ColonistStatusComponent))]
    [RequireComponent(typeof(ColonistMotor))]
    [RequireComponent(typeof(ColonistActivityRunner))]
    public sealed class ColonistActor : MonoBehaviour
    {
        [SerializeField] private ColonistAgent colonist;
        [SerializeField] private ColonistMotor motor;
        [SerializeField] private ColonistActivityRunner activityRunner;

        private bool logicalMovePending;

        public ColonistAgent Colonist => colonist;
        public ColonistMotor Motor => motor;
        public ColonistActivityRunner Activities => activityRunner;

        private void Reset()
        {
            CacheReferences();
        }

        private void OnValidate()
        {
            CacheReferences();
        }

        private void Awake()
        {
            CacheReferences();

            if (motor != null)
            {
                motor.Arrived += HandleArrived;
                motor.MoveFailed += HandleMoveFailed;
            }
        }

        private void OnDestroy()
        {
            if (motor != null)
            {
                motor.Arrived -= HandleArrived;
                motor.MoveFailed -= HandleMoveFailed;
            }
        }

        /// <summary>
        /// Starts a physical move and commits ColonistAgent.currentLocation only
        /// after the motor reports arrival.
        /// </summary>
        public bool MoveTo(LocationAnchor destination, string transitKind = "walking")
        {
            if (!CacheReferences() || destination == null || logicalMovePending)
                return false;

            if (!colonist.BeginTransit(colonist.currentLocation, destination, transitKind))
                return false;

            if (!motor.MoveTo(destination.transform))
            {
                colonist.CancelTransit();
                return false;
            }

            logicalMovePending = true;
            return true;
        }

        public void StopMovement()
        {
            if (motor != null)
                motor.Stop();

            CancelLogicalMove();
        }

        public bool RequestActivity(InteractableFacility facility, string activityId)
        {
            return CacheReferences() && activityRunner.RequestActivity(facility, activityId);
        }

        public bool RequestSequence(InteractableFacility facility, string sequenceId)
        {
            return CacheReferences() && activityRunner.RequestSequence(facility, sequenceId);
        }

        private void HandleArrived()
        {
            if (!logicalMovePending)
                return;

            logicalMovePending = false;
            colonist.CompleteTransit();
        }

        private void HandleMoveFailed(string reason)
        {
            if (!logicalMovePending)
                return;

            CancelLogicalMove();
        }

        private void CancelLogicalMove()
        {
            if (!logicalMovePending)
                return;

            logicalMovePending = false;
            colonist.CancelTransit();
        }

        private bool CacheReferences()
        {
            if (colonist == null)
                colonist = GetComponent<ColonistAgent>();
            if (motor == null)
                motor = GetComponent<ColonistMotor>();
            if (activityRunner == null)
                activityRunner = GetComponent<ColonistActivityRunner>();

            return colonist != null && motor != null && activityRunner != null;
        }
    }
}
