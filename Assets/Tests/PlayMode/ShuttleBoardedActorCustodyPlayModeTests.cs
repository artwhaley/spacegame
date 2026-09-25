using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AsteroidColony.Tests
{
    [Category("UnityIntegration")]
    public sealed class ShuttleBoardedActorCustodyPlayModeTests
    {
        private readonly System.Collections.Generic.List<GameObject> roots =
            new System.Collections.Generic.List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = roots.Count - 1; index >= 0; index--)
                if (roots[index] != null)
                    Object.Destroy(roots[index]);
            roots.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PassengerRemainsIndependentHiddenAndSafeWhenShuttleIsDisabled()
        {
            DockingPortComponent port = CreateDockingPort("Home Port");
            DockingPortComponent destinationPort = CreateDockingPort("Destination Port");
            ShuttleTransferEndpoint origin = CreateEndpoint("Origin", "home", port);
            ShuttleTransferEndpoint destination = CreateEndpoint("Destination", "remote", destinationPort);

            GameObject shuttleRoot = Track(new GameObject("Custody Test Shuttle"));
            ShuttleServiceComponent shuttle = shuttleRoot.AddComponent<ShuttleServiceComponent>();
            ShuttleVoyageComponent voyage = shuttleRoot.GetComponent<ShuttleVoyageComponent>();
            voyage.CurrentDock = port;
            Transform passengerAnchor = new GameObject("Passenger Anchor").transform;
            passengerAnchor.SetParent(shuttleRoot.transform, false);
            passengerAnchor.localPosition = new Vector3(0f, 0.5f, 0f);
            shuttle.Configure("custody-test", null, voyage, null, passengerAnchor, 1, 0, null);

            GameObject colonistContainer = Track(new GameObject("Colonist Roots"));
            GameObject actorRoot = new GameObject("Passenger Actor");
            actorRoot.transform.SetParent(colonistContainer.transform, false);
            ColonistIdentity actor = actorRoot.AddComponent<ColonistIdentity>();
            GameObject visibleObject = new GameObject("Visible Mesh");
            visibleObject.transform.SetParent(actorRoot.transform, false);
            MeshRenderer visibleRenderer = visibleObject.AddComponent<MeshRenderer>();
            GameObject preHiddenObject = new GameObject("Pre-hidden Mesh");
            preHiddenObject.transform.SetParent(actorRoot.transform, false);
            MeshRenderer preHiddenRenderer = preHiddenObject.AddComponent<MeshRenderer>();
            preHiddenRenderer.enabled = false;
            GameObject canvasObject = new GameObject("Actor Label");
            canvasObject.transform.SetParent(actorRoot.transform, false);
            Canvas visibleCanvas = canvasObject.AddComponent<Canvas>();

            var trip = new ShuttleTrip("custody-trip", shuttle, origin, destination, 0);
            var request = new ShuttleTransportRequest("custody-request", "custody-correlation",
                ShuttlePayloadType.Passenger, origin, destination, 0, 0);
            request.AddPayload(actor);
            request.AssignedTrip = trip;
            request.IsPayloadReady = true;
            trip.Add(request);

            Assert.That(shuttle.TryAcceptTrip(trip), Is.True);
            Assert.That(shuttle.TryBoardPassenger(request, actor), Is.True);
            Assert.That(actor.transform.parent, Is.SameAs(colonistContainer.transform));
            Assert.That(actorRoot.activeSelf, Is.True);
            Assert.That(visibleRenderer.enabled, Is.False);
            Assert.That(preHiddenRenderer.enabled, Is.False);
            Assert.That(visibleCanvas.enabled, Is.False);

            Vector3 shuttleLocalActorPosition = shuttleRoot.transform.InverseTransformPoint(actor.transform.position);
            shuttleRoot.transform.SetPositionAndRotation(
                new Vector3(8f, 3f, -4f), Quaternion.Euler(0f, 35f, 0f));
            yield return null;
            Vector3 expectedFollowPosition = shuttleRoot.transform.TransformPoint(shuttleLocalActorPosition);
            Assert.That(Vector3.Distance(actor.transform.position, expectedFollowPosition), Is.LessThan(0.001f));
            Assert.That(actor.transform.parent, Is.SameAs(colonistContainer.transform));

            shuttleRoot.SetActive(false);
            Assert.That(actor != null, Is.True);
            Assert.That(actorRoot.activeSelf, Is.True);
            Assert.That(actor.transform.parent, Is.SameAs(colonistContainer.transform));
            Assert.That(visibleRenderer.enabled, Is.True);
            Assert.That(preHiddenRenderer.enabled, Is.False);
            Assert.That(visibleCanvas.enabled, Is.True);
            Assert.That(trip.State, Is.EqualTo(ShuttleTripState.Blocked));
            Assert.That(request.State, Is.EqualTo(ShuttleTransportRequestState.Blocked));

            Object.Destroy(actorRoot);
            yield return null;
            Assert.That(actor == null, Is.True);
        }

        private ShuttleTransferEndpoint CreateEndpoint(
            string objectName, string stableId, DockingPortComponent port)
        {
            GameObject endpointRoot = Track(new GameObject(objectName));
            ShuttleTransferEndpoint endpoint = endpointRoot.AddComponent<ShuttleTransferEndpoint>();
            endpoint.Configure(stableId, port, endpointRoot.transform, null, null);
            return endpoint;
        }

        private DockingPortComponent CreateDockingPort(string objectName)
        {
            GameObject portRoot = Track(new GameObject(objectName));
            portRoot.SetActive(false);
            Transform docking = new GameObject("Node Docking").transform;
            Transform approach = new GameObject("Node Approach").transform;
            Transform clearance = new GameObject("Node Clearance").transform;
            docking.SetParent(portRoot.transform, false);
            approach.SetParent(portRoot.transform, false);
            clearance.SetParent(portRoot.transform, false);
            DockingPortComponent port = portRoot.AddComponent<DockingPortComponent>();
            port.DockingNode = docking;
            port.ApproachNode = approach;
            port.ClearanceNode = clearance;
            portRoot.SetActive(true);
            return port;
        }

        private GameObject Track(GameObject root)
        {
            roots.Add(root);
            return root;
        }
    }
}
