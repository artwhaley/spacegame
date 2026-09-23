using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class DockingPortTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null)
                    Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        [Test]
        public void RootPoseSolverAlignsOffsetProbeAtArbitraryThreeDimensionalPose()
        {
            GameObject root = CreateObject("Shuttle root");
            root.transform.SetPositionAndRotation(new Vector3(4f, -2f, 9f), Quaternion.Euler(-13f, 72f, 29f));
            Transform probe = CreateChild(root.transform, "probe",
                new Vector3(2f, -1f, 3f), Quaternion.Euler(17f, 81f, -33f));
            Transform target = CreateObject("dock node").transform;
            target.SetPositionAndRotation(new Vector3(-12f, 7f, 25f), Quaternion.Euler(41f, -72f, 13f));

            Assert.That(DockingPoseUtility.TrySolveRootPose(root.transform, probe, target,
                out Vector3 solvedPosition, out Quaternion solvedRotation), Is.True);

            root.transform.SetPositionAndRotation(solvedPosition, solvedRotation);

            Assert.That(Vector3.Distance(probe.position, target.position), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(probe.rotation, target.rotation), Is.LessThan(0.001f));
        }

        [Test]
        public void PortCannotBeReservedByTwoShuttles()
        {
            DockingPortComponent port = CreatePort("port");
            ShuttleDockingProbeComponent first = CreateProbe("first Shuttle");
            ShuttleDockingProbeComponent second = CreateProbe("second Shuttle");

            Assert.That(port.TryReserve(first), Is.True);
            Assert.That(port.TryReserve(second, out string reason), Is.False);

            Assert.That(port.State, Is.EqualTo(DockingPortState.Reserved));
            Assert.That(port.ReservationHolder, Is.EqualTo(first));
            Assert.That(reason, Does.Contain("reserved"));
        }

        [Test]
        public void ReservationHolderCanOccupyAndWrongShuttleCannot()
        {
            DockingPortComponent port = CreatePort("port");
            ShuttleDockingProbeComponent holder = CreateProbe("holder");
            ShuttleDockingProbeComponent wrongShuttle = CreateProbe("wrong Shuttle");
            Assert.That(port.TryReserve(holder), Is.True);

            Assert.That(port.TryOccupy(wrongShuttle, out string reason), Is.False);
            Assert.That(reason, Does.Contain("does not hold"));
            Assert.That(port.TryOccupy(holder), Is.True);
            Assert.That(port.State, Is.EqualTo(DockingPortState.Occupied));
            Assert.That(port.Occupant, Is.EqualTo(holder));
            Assert.That(port.TryOccupy(wrongShuttle), Is.False);
        }

        [Test]
        public void ReleaseReturnsReservedOrOccupiedPortToFree()
        {
            DockingPortComponent port = CreatePort("port");
            ShuttleDockingProbeComponent holder = CreateProbe("holder");
            ShuttleDockingProbeComponent wrongShuttle = CreateProbe("wrong Shuttle");
            Assert.That(port.TryReserve(holder), Is.True);

            Assert.That(port.Release(wrongShuttle), Is.False);
            Assert.That(port.Release(holder), Is.True);
            Assert.That(port.State, Is.EqualTo(DockingPortState.Free));
            Assert.That(port.ReservationHolder, Is.Null);

            Assert.That(port.TryReserve(holder), Is.True);
            Assert.That(port.TryOccupy(holder), Is.True);
            Assert.That(port.Release(holder), Is.True);
            Assert.That(port.State, Is.EqualTo(DockingPortState.Free));
            Assert.That(port.Occupant, Is.Null);
        }

        [Test]
        public void ClosedPortRejectsReservationUntilReopened()
        {
            DockingPortComponent port = CreatePort("port");
            ShuttleDockingProbeComponent shuttle = CreateProbe("Shuttle");

            Assert.That(port.Close(), Is.True);
            Assert.That(port.Close(), Is.True);
            Assert.That(port.TryReserve(shuttle, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("port is closed"));
            Assert.That(port.Open(), Is.True);
            Assert.That(port.Open(), Is.True);
            Assert.That(port.TryReserve(shuttle), Is.True);
        }

        [Test]
        public void OddAuthoredPortOrientationIsPreserved()
        {
            DockingPortComponent port = CreatePort("odd-angle port");
            ShuttleDockingProbeComponent shuttle = CreateProbe("Shuttle");
            Quaternion authored = Quaternion.Euler(47f, 128f, -31f);
            port.DockingNode.rotation = authored;

            Assert.That(port.ValidateConfiguration(out string reason), Is.True, reason);
            Assert.That(port.TryReserve(shuttle), Is.True);
            Assert.That(Quaternion.Angle(port.DockingNode.rotation, authored), Is.LessThan(0.001f));
        }

        [Test]
        public void MissingNodeOrInvalidCaptureToleranceReportsAReason()
        {
            DockingPortComponent port = CreatePort("port");
            port.ApproachNode = null;

            Assert.That(port.ValidateConfiguration(out string missingNodeReason), Is.False);
            Assert.That(missingNodeReason, Does.Contain("node references"));

            port = CreatePort("second port");
            port.capturePositionTolerance = float.NaN;
            Assert.That(port.ValidateConfiguration(out string toleranceReason), Is.False);
            Assert.That(toleranceReason, Does.Contain("capture tolerances"));
        }

        private DockingPortComponent CreatePort(string name)
        {
            GameObject root = CreateObject(name);
            Transform docking = CreateChild(root.transform, "nodeDocking", Vector3.zero, Quaternion.identity);
            Transform approach = CreateChild(root.transform, "nodeApproach", Vector3.forward * 5f, Quaternion.identity);
            Transform clearance = CreateChild(root.transform, "nodeClearance", Vector3.forward * 10f, Quaternion.identity);
            DockingPortComponent port = root.AddComponent<DockingPortComponent>();
            port.DockingNode = docking;
            port.ApproachNode = approach;
            port.ClearanceNode = clearance;
            return port;
        }

        private ShuttleDockingProbeComponent CreateProbe(string name)
        {
            GameObject root = CreateObject(name);
            Transform probeTransform = CreateChild(root.transform, "node_docking", Vector3.back * 2f,
                Quaternion.Euler(0f, 180f, 0f));
            ShuttleDockingProbeComponent probe = root.AddComponent<ShuttleDockingProbeComponent>();
            probe.ProbeTransform = probeTransform;
            return probe;
        }

        private GameObject CreateObject(string name)
        {
            GameObject value = new GameObject(name);
            created.Add(value);
            return value;
        }

        private static Transform CreateChild(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            child.transform.localRotation = rotation;
            return child.transform;
        }
    }
}
