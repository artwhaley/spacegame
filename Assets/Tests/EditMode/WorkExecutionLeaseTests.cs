using AsteroidColony;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    [Category("Core")]
    public sealed class WorkExecutionLeaseTests
    {
        [Test]
        public void DeferredReleaseKeepsOwnershipUntilOwnerReleases()
        {
            ColonistIdentity worker;
            WorkplaceComponent workplace;
            CreateWorkerAndWorkplace(out worker, out workplace,
                out GameObject workerObject, out GameObject workplaceObject);
            try
            {
                FakeOwner owner = new FakeOwner(WorkReleaseDisposition.Deferred);
                WorkExecutionLease lease = new WorkExecutionLease(worker, workplace, owner);

                Assert.That(lease.RequestRelease(WorkReleaseReason.EndOfShift),
                    Is.EqualTo(WorkReleaseDisposition.Deferred));
                Assert.That(lease.IsActive, Is.True);
                Assert.That(lease.HasPendingReleaseRequest, Is.True);
                Assert.That(lease.PendingReleaseReason,
                    Is.EqualTo(WorkReleaseReason.EndOfShift));

                Assert.That(lease.RequestRelease(WorkReleaseReason.EndOfShift),
                    Is.EqualTo(WorkReleaseDisposition.Deferred));
                Assert.That(owner.RequestCount, Is.EqualTo(1));

                Assert.That(lease.Release(), Is.True);
                Assert.That(lease.IsActive, Is.False);
                Assert.That(lease.HasPendingReleaseRequest, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(workerObject);
                Object.DestroyImmediate(workplaceObject);
            }
        }

        [Test]
        public void ReleasedNowClosesLeaseImmediately()
        {
            ColonistIdentity worker;
            WorkplaceComponent workplace;
            CreateWorkerAndWorkplace(out worker, out workplace,
                out GameObject workerObject, out GameObject workplaceObject);
            try
            {
                FakeOwner owner = new FakeOwner(WorkReleaseDisposition.ReleasedNow);
                WorkExecutionLease lease = new WorkExecutionLease(worker, workplace, owner);

                Assert.That(lease.RequestRelease(WorkReleaseReason.CriticalNeed),
                    Is.EqualTo(WorkReleaseDisposition.ReleasedNow));
                Assert.That(lease.IsActive, Is.False);
                Assert.That(lease.HasPendingReleaseRequest, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(workerObject);
                Object.DestroyImmediate(workplaceObject);
            }
        }

        private static void CreateWorkerAndWorkplace(
            out ColonistIdentity worker,
            out WorkplaceComponent workplace,
            out GameObject workerObject,
            out GameObject workplaceObject)
        {
            workerObject = new GameObject("Work execution lease test worker");
            worker = workerObject.AddComponent<ColonistIdentity>();
            workplaceObject = new GameObject("Work execution lease test workplace");
            workplace = workplaceObject.AddComponent<WorkplaceComponent>();
        }

        private sealed class FakeOwner : IWorkExecutionOwner
        {
            private readonly WorkReleaseDisposition disposition;

            public FakeOwner(WorkReleaseDisposition disposition)
            {
                this.disposition = disposition;
            }

            public int RequestCount { get; private set; }

            public WorkReleaseDisposition RequestRelease(
                WorkExecutionLease lease,
                WorkReleaseReason reason)
            {
                RequestCount++;
                return disposition;
            }
        }
    }
}
