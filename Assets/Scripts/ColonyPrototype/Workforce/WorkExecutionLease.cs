using System;

namespace AsteroidColony
{
    public enum WorkReleaseReason
    {
        EndOfShift,
        CriticalNeed,
        OtherWorkPolicy
    }

    public enum WorkReleaseDisposition
    {
        ReleasedNow,
        Deferred
    }

    /// <summary>Workplace-owned execution decides when an assigned worker is safe to release.</summary>
    public interface IWorkExecutionOwner
    {
        WorkReleaseDisposition RequestRelease(
            WorkExecutionLease lease,
            WorkReleaseReason reason);
    }

    /// <summary>
    /// Generic ownership of a worker's concrete work. It contains no knowledge of cargo,
    /// vehicles, activities, or the reason a workplace may need to defer release.
    /// </summary>
    public sealed class WorkExecutionLease
    {
        private readonly IWorkExecutionOwner owner;
        private bool hasReleaseRequest;
        private WorkReleaseReason lastReleaseReason;
        private WorkReleaseDisposition lastReleaseDisposition;

        internal WorkExecutionLease(
            ColonistIdentity worker,
            WorkplaceComponent workplace,
            IWorkExecutionOwner owner)
        {
            if (worker == null)
                throw new ArgumentNullException(nameof(worker));
            if (workplace == null)
                throw new ArgumentNullException(nameof(workplace));
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            Worker = worker;
            Workplace = workplace;
            this.owner = owner;
            IsActive = true;
        }

        public ColonistIdentity Worker { get; }
        public WorkplaceComponent Workplace { get; }
        public bool IsActive { get; private set; }
        public bool HasPendingReleaseRequest => IsActive && hasReleaseRequest;
        public WorkReleaseReason? PendingReleaseReason => HasPendingReleaseRequest
            ? lastReleaseReason
            : (WorkReleaseReason?)null;

        public WorkReleaseDisposition RequestRelease(WorkReleaseReason reason)
        {
            if (!IsActive)
                return WorkReleaseDisposition.ReleasedNow;

            if (hasReleaseRequest && lastReleaseReason == reason)
                return lastReleaseDisposition;

            hasReleaseRequest = true;
            lastReleaseReason = reason;
            lastReleaseDisposition = owner.RequestRelease(this, reason);

            if (!IsActive || lastReleaseDisposition == WorkReleaseDisposition.ReleasedNow)
            {
                Release();
                return WorkReleaseDisposition.ReleasedNow;
            }

            if (lastReleaseDisposition != WorkReleaseDisposition.Deferred)
                throw new InvalidOperationException("A work owner returned an unknown release disposition.");

            return WorkReleaseDisposition.Deferred;
        }

        /// <summary>Called by the owner when its committed work has reached a safe handoff point.</summary>
        public bool Release()
        {
            if (!IsActive)
                return false;

            IsActive = false;
            hasReleaseRequest = false;
            return true;
        }
    }
}
