using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Visible Play Mode controls for manually dispatching the Shuttle between authored berths.</summary>
    public sealed class ShuttleFlightDebugPanel : MonoBehaviour
    {
        [SerializeField] private ShuttleVoyageComponent shuttle;
        [SerializeField] private DockingPortComponent portA;
        [SerializeField] private DockingPortComponent portB;

        private string lastMessage = "Click the opposite port to dispatch the Shuttle.";

        private void Awake()
        {
            if (shuttle == null)
            {
                ShuttleVoyageComponent voyage = FindObjectOfType<ShuttleVoyageComponent>();
                if (voyage != null)
                    shuttle = voyage;
            }
        }

        private void OnGUI()
        {
            if (shuttle == null)
                return;

            GUILayout.BeginArea(new Rect(14f, 14f, 380f, 220f), "Shuttle Flight", GUI.skin.window);
            GUILayout.Label($"Phase: {shuttle.Phase}    Speed: {shuttle.Speed:0.0} m/s");
            GUILayout.Label($"Current berth: {(shuttle.CurrentDock != null ? shuttle.CurrentDock.name : "In flight")}");
            GUILayout.Label($"Port A: {(portA != null ? portA.State.ToString() : "missing")}    Port B: {(portB != null ? portB.State.ToString() : "missing")}");

            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && shuttle.Phase == ShuttleVoyagePhase.Docked &&
                portA != null && portA != shuttle.CurrentDock;
            if (GUILayout.Button("Fly to Port A"))
                RequestVoyage(portA);

            GUI.enabled = previousEnabled && shuttle.Phase == ShuttleVoyagePhase.Docked &&
                portB != null && portB != shuttle.CurrentDock;
            if (GUILayout.Button("Fly to Port B"))
                RequestVoyage(portB);
            GUI.enabled = previousEnabled;

            GUILayout.Label(lastMessage, GUILayout.Height(32f));
            if (shuttle.Phase == ShuttleVoyagePhase.Blocked && !string.IsNullOrEmpty(shuttle.BlockReason))
                GUILayout.Label($"Blocked: {shuttle.BlockReason}");
            GUILayout.Label("Camera: right-drag to orbit, wheel to zoom; Q/E orbit, W/S tilt, R/F zoom.");
            GUILayout.EndArea();
        }

        private void RequestVoyage(DockingPortComponent destination)
        {
            if (shuttle.TryRequestVoyage(destination, out string reason))
                lastMessage = $"{destination.name}: {reason}";
            else
                lastMessage = $"Request rejected: {reason}";
        }
    }
}
