using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    public class ShuttleVoyageTests
    {
        private readonly List<GameObject> created = new List<GameObject>();
        private ShuttleFlightProfile profile;

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<ShuttleFlightProfile>();
            profile.mainAcceleration = 8f;
            profile.rcsAcceleration = 2f;
            profile.maxCruiseSpeed = 25f;
            profile.approachMaxSpeed = 3f;
            profile.finalDockMaxSpeed = 0.5f;
            profile.angularAcceleration = 60f;
            profile.maxAngularSpeed = 90f;
            profile.positionTolerance = 0.15f;
            profile.velocityTolerance = 0.1f;
            profile.angleTolerance = 1f;
            profile.captureAngularSpeedTolerance = 2f;
            profile.integrationSubstepSeconds = 0.1f;
            profile.brakingSafetyMargin = 5f;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null)
                    Object.DestroyImmediate(created[i]);
            created.Clear();
            if (profile != null)
                Object.DestroyImmediate(profile);
        }

        [Test]
        public void InvalidRequestDoesNotMoveOrReserveEitherPort()
        {
            VoyageRig rig = CreateRig();
            Vector3 startPosition = rig.root.transform.position;
            Quaternion startRotation = rig.root.transform.rotation;

            Assert.That(rig.voyage.TryRequestVoyage(rig.portA, out string samePortReason), Is.False);
            Assert.That(samePortReason, Does.Contain("differ"));
            Assert.That(rig.voyage.TryRequestVoyage(null, out string missingReason), Is.False);
            Assert.That(missingReason, Does.Contain("missing destination"));

            Assert.That(rig.voyage.Phase, Is.EqualTo(ShuttleVoyagePhase.Docked));
            Assert.That(rig.portA.State, Is.EqualTo(DockingPortState.Occupied));
            Assert.That(rig.portB.State, Is.EqualTo(DockingPortState.Free));
            Assert.That(Vector3.Distance(rig.root.transform.position, startPosition), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(rig.root.transform.rotation, startRotation), Is.LessThan(0.0001f));
        }

        [Test]
        public void VoyageReservesBeforeUndockingAndRejectsASecondRequest()
        {
            VoyageRig rig = CreateRig();
            Assert.That(rig.voyage.TryRequestVoyage(rig.portB, out string reason), Is.True, reason);

            Assert.That(rig.voyage.Phase, Is.EqualTo(ShuttleVoyagePhase.Undocking));
            Assert.That(rig.portA.State, Is.EqualTo(DockingPortState.Occupied));
            Assert.That(rig.portB.State, Is.EqualTo(DockingPortState.Reserved));
            Assert.That(rig.portB.ReservationHolder, Is.EqualTo(rig.probe));
            Assert.That(rig.voyage.TryRequestVoyage(rig.portA, out string secondReason), Is.False);
            Assert.That(secondReason, Does.Contain("not safely docked"));
        }

        [Test]
        public void LongOddAngleVoyageUsesCruisePointsFlipAndPhysicalBrakingBeforeCapture()
        {
            VoyageRig rig = CreateRig();
            FlightRoute route = CreateCruiseRoute(rig.portA, rig.portB);
            Assert.That(rig.voyage.TryRequestVoyage(rig.portB, route, out string requestReason),
                Is.True, requestReason);

            HashSet<ShuttleVoyagePhase> phases = new HashSet<ShuttleVoyagePhase>();
            bool passedFirstCruiseAtSpeed = false;
            bool flippedWithoutMainThrust = false;
            bool sawMainBurn = false;
            float brakingStartSpeed = -1f;
            float speedAfterBraking = -1f;
            float finalDockStartDistance = -1f;
            float closestFinalDockDistance = float.PositiveInfinity;
            float largestDockingTurnAngle = 0f;

            for (int tick = 0; tick < 12000; tick++)
            {
                rig.voyage.SimulationTick(0.25f / 3600f);
                ShuttleVoyagePhase current = rig.voyage.Phase;
                phases.Add(current);
                passedFirstCruiseAtSpeed |= rig.voyage.CurrentWaypointIndex >= 2 &&
                    rig.voyage.Speed > profile.approachMaxSpeed;
                sawMainBurn |= rig.voyage.MainEngineFiring;
                flippedWithoutMainThrust |= current == ShuttleVoyagePhase.FlipForBraking &&
                    !rig.voyage.MainEngineFiring && rig.voyage.Speed > profile.approachMaxSpeed;
                if (current == ShuttleVoyagePhase.CruiseBraking && brakingStartSpeed < 0f)
                    brakingStartSpeed = rig.voyage.Speed;
                if (current == ShuttleVoyagePhase.Approach && brakingStartSpeed >= 0f && speedAfterBraking < 0f)
                    speedAfterBraking = rig.voyage.Speed;
                if (current == ShuttleVoyagePhase.DockingTurn)
                    largestDockingTurnAngle = Mathf.Max(largestDockingTurnAngle, rig.voyage.AngularErrorDegrees);
                if (current == ShuttleVoyagePhase.FinalDocking)
                {
                    float distance = Vector3.Distance(rig.probe.ProbeTransform.position,
                        rig.portB.DockingNode.position);
                    if (finalDockStartDistance < 0f)
                        finalDockStartDistance = distance;
                    closestFinalDockDistance = Mathf.Min(closestFinalDockDistance, distance);
                }

                if (current == ShuttleVoyagePhase.Docked && rig.voyage.CurrentDock == rig.portB)
                    break;
            }

            Assert.That(rig.voyage.Phase, Is.EqualTo(ShuttleVoyagePhase.Docked));
            Assert.That(rig.voyage.CurrentDock, Is.EqualTo(rig.portB));
            Assert.That(rig.portA.State, Is.EqualTo(DockingPortState.Free));
            Assert.That(rig.portB.State, Is.EqualTo(DockingPortState.Occupied));
            Assert.That(rig.portB.Occupant, Is.EqualTo(rig.probe));
            Assert.That(phases.Contains(ShuttleVoyagePhase.CruiseAccelerating), Is.True);
            Assert.That(phases.Contains(ShuttleVoyagePhase.CruiseCoasting), Is.True);
            Assert.That(phases.Contains(ShuttleVoyagePhase.FlipForBraking), Is.True);
            Assert.That(phases.Contains(ShuttleVoyagePhase.CruiseBraking), Is.True);
            Assert.That(phases.Contains(ShuttleVoyagePhase.Approach), Is.True);
            Assert.That(phases.Contains(ShuttleVoyagePhase.DockingTurn), Is.True);
            Assert.That(phases.Contains(ShuttleVoyagePhase.FinalDocking), Is.True);
            Assert.That(phases.Contains(ShuttleVoyagePhase.Captured), Is.True);
            Assert.That(passedFirstCruiseAtSpeed, Is.True, "Cruise waypoints must not require a full stop.");
            Assert.That(flippedWithoutMainThrust, Is.True, "The turn should be visible while the shuttle coasts.");
            Assert.That(sawMainBurn, Is.True);
            Assert.That(brakingStartSpeed, Is.GreaterThan(profile.approachMaxSpeed));
            Assert.That(speedAfterBraking, Is.LessThan(brakingStartSpeed));
            Assert.That(largestDockingTurnAngle, Is.GreaterThan(10f));
            Assert.That(closestFinalDockDistance, Is.LessThan(finalDockStartDistance - 1f));
            Assert.That(Vector3.Distance(rig.probe.ProbeTransform.position, rig.portB.DockingNode.position),
                Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(rig.probe.ProbeTransform.rotation,
                    DockingPoseUtility.GetMatingProbeRotation(rig.portB.DockingNode.rotation)),
                Is.LessThan(0.001f));
            Assert.That(rig.voyage.FlightState.velocity.magnitude, Is.LessThan(0.001f));
            Assert.That(rig.voyage.FlightState.angularVelocity.magnitude, Is.LessThan(0.001f));
        }

        [Test]
        public void FiveAlternatingVoyagesLeaveNoProbePoseDriftOrPortReservations()
        {
            VoyageRig rig = CreateRig();
            DockingPortComponent target = rig.portB;
            for (int trip = 0; trip < 5; trip++)
            {
                Assert.That(rig.voyage.TryRequestVoyage(target, out string reason), Is.True, reason);
                for (int tick = 0; tick < 12000; tick++)
                {
                    rig.voyage.SimulationTick(0.25f / 3600f);
                    if (rig.voyage.Phase == ShuttleVoyagePhase.Docked && rig.voyage.CurrentDock == target)
                        break;
                }

                Assert.That(rig.voyage.Phase, Is.EqualTo(ShuttleVoyagePhase.Docked));
                Assert.That(rig.voyage.CurrentDock, Is.EqualTo(target));
                Assert.That(target.State, Is.EqualTo(DockingPortState.Occupied));
                Assert.That(Vector3.Distance(rig.probe.ProbeTransform.position, target.DockingNode.position),
                    Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(rig.probe.ProbeTransform.rotation,
                        DockingPoseUtility.GetMatingProbeRotation(target.DockingNode.rotation)),
                    Is.LessThan(0.001f));

                target = target == rig.portA ? rig.portB : rig.portA;
            }

            Assert.That(rig.portA.State == DockingPortState.Occupied || rig.portB.State == DockingPortState.Occupied,
                Is.True);
            Assert.That(rig.portA.State == DockingPortState.Reserved || rig.portB.State == DockingPortState.Reserved,
                Is.False);
        }

        [Test]
        public void PortCaptureToleranceRejectsExcessSpeedAndOrientationError()
        {
            DockingPortComponent port = CreatePort("capture port", new Vector3(2f, -3f, 4f),
                Quaternion.Euler(27f, 63f, -14f), Quaternion.identity);
            Quaternion correctRotation = DockingPoseUtility.GetMatingProbeRotation(port.DockingNode.rotation);

            Assert.That(port.IsWithinCaptureTolerance(port.DockingNode.position, correctRotation,
                Vector3.right, 0f, out string speedReason), Is.False);
            Assert.That(speedReason, Does.Contain("relative speed"));

            Assert.That(port.IsWithinCaptureTolerance(port.DockingNode.position,
                correctRotation * Quaternion.Euler(0f, 8f, 0f), Vector3.zero, 0f,
                out string angleReason), Is.False);
            Assert.That(angleReason, Does.Contain("angle error"));
        }

        private FlightRoute CreateCruiseRoute(DockingPortComponent origin, DockingPortComponent destination)
        {
            Vector3 start = origin.ClearanceNode.position;
            Vector3 end = destination.ApproachNode.position;
            Vector3 first = Vector3.Lerp(start, end, 0.32f);
            Vector3 second = Vector3.Lerp(start, end, 0.68f);
            FlightRoute result = new FlightRoute();
            result.AddWaypoint(FlightWaypoint.Clearance(start, null, 1f, 1f));
            result.AddWaypoint(FlightWaypoint.Cruise(first, 5f));
            result.AddWaypoint(FlightWaypoint.Cruise(second, 5f));
            result.AddWaypoint(FlightWaypoint.Approach(end,
                DockingPoseUtility.GetMatingProbeRotation(destination.ApproachNode.rotation), 1f, 1f));
            return result;
        }

        private VoyageRig CreateRig()
        {
            DockingPortComponent portA = CreatePort("Port A", Vector3.zero, Quaternion.identity,
                Quaternion.identity);
            DockingPortComponent portB = CreatePort("Port B", new Vector3(18f, 12f, 220f),
                Quaternion.Euler(24f, 71f, -19f), Quaternion.Euler(18f, 38f, -12f));

            GameObject root = CreateObject("Shuttle root");
            Transform probeNode = CreateChild(root.transform, "node_docking", Vector3.back * 3f,
                Quaternion.Euler(0f, 180f, 0f));
            ShuttleDockingProbeComponent probe = root.AddComponent<ShuttleDockingProbeComponent>();
            probe.ProbeTransform = probeNode;
            ShuttleVoyageComponent voyage = root.AddComponent<ShuttleVoyageComponent>();
            voyage.Profile = profile;
            voyage.Probe = probe;
            voyage.CurrentDock = portA;
            Assert.That(DockingPoseUtility.TryGetProbePoseRelativeToRoot(root.transform, probeNode,
                out Vector3 probeLocalPosition, out Quaternion probeLocalRotation), Is.True);
            Assert.That(DockingPoseUtility.TrySolveRootPose(probeLocalPosition, probeLocalRotation,
                portA.DockingNode.position,
                DockingPoseUtility.GetMatingProbeRotation(portA.DockingNode.rotation),
                out Vector3 rootPosition, out Quaternion rootRotation), Is.True);
            root.transform.SetPositionAndRotation(rootPosition, rootRotation);
            Assert.That(portA.TryReserve(probe), Is.True);
            Assert.That(portA.TryOccupy(probe), Is.True);
            return new VoyageRig(root, probe, voyage, portA, portB);
        }

        private DockingPortComponent CreatePort(string name, Vector3 position,
            Quaternion rootRotation, Quaternion approachLocalRotation)
        {
            GameObject root = CreateObject(name);
            root.transform.SetPositionAndRotation(position, rootRotation);
            Transform docking = CreateChild(root.transform, "nodeDocking", Vector3.zero, Quaternion.identity);
            Transform approach = CreateChild(root.transform, "nodeApproach", Vector3.forward * 15f,
                approachLocalRotation);
            Transform clearance = CreateChild(root.transform, "nodeClearance", Vector3.forward * 10f,
                Quaternion.identity);
            DockingPortComponent port = root.AddComponent<DockingPortComponent>();
            port.DockingNode = docking;
            port.ApproachNode = approach;
            port.ClearanceNode = clearance;
            port.capturePositionTolerance = 0.25f;
            port.captureRelativeSpeedTolerance = 0.25f;
            port.captureAngleToleranceDegrees = 2f;
            port.captureAngularSpeedTolerance = 3f;
            return port;
        }

        private GameObject CreateObject(string name)
        {
            GameObject value = new GameObject(name);
            created.Add(value);
            return value;
        }

        private static Transform CreateChild(Transform parent, string name, Vector3 localPosition,
            Quaternion localRotation)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = localRotation;
            return child.transform;
        }

        private sealed class VoyageRig
        {
            public readonly GameObject root;
            public readonly ShuttleDockingProbeComponent probe;
            public readonly ShuttleVoyageComponent voyage;
            public readonly DockingPortComponent portA;
            public readonly DockingPortComponent portB;

            public VoyageRig(GameObject root, ShuttleDockingProbeComponent probe,
                ShuttleVoyageComponent voyage, DockingPortComponent portA, DockingPortComponent portB)
            {
                this.root = root;
                this.probe = probe;
                this.voyage = voyage;
                this.portA = portA;
                this.portB = portB;
            }
        }
    }
}
