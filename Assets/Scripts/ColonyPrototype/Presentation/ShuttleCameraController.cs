using UnityEngine;
using UnityEngine.InputSystem;

namespace AsteroidColony
{
    /// <summary>
    /// Debug fixture camera for BobInSpace.unity. Follows the Shuttle every frame:
    /// Q/E orbit it, W/S tilt up/down, R/F zoom in/out. Not production grade.
    /// </summary>
    public sealed class ShuttleCameraController : MonoBehaviour
    {
        [Header("Follow target")]
        [Tooltip("Shuttle root to orbit. Left empty, the controller finds the Shuttle in the scene.")]
        [SerializeField] private Transform shuttle;

        [Header("Controls")]
        [Tooltip("Orbit yaw speed while Q or E is held, degrees per second.")]
        [SerializeField] private float orbitDegreesPerSecond = 90f;

        [Tooltip("Tilt speed while W or S is held, degrees per second.")]
        [SerializeField] private float tiltDegreesPerSecond = 60f;

        [Tooltip("Zoom distance change per second while R or F is held.")]
        [SerializeField] private float zoomUnitsPerSecond = 24f;

        [Header("Mouse controls")]
        [SerializeField] private float mouseOrbitDegreesPerPixel = 0.2f;
        [SerializeField] private float mouseZoomUnitsPerScrollUnit = 0.02f;

        [Header("Limits")]
        [SerializeField] private float minDistance = 4f;
        [SerializeField] private float maxDistance = 150f;
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;

        [Header("Starting pose")]
        [SerializeField] private float startYaw = 45f;
        [SerializeField] private float startPitch = 20f;
        [SerializeField] private float startDistance = 20f;

        private float yaw;
        private float pitch;
        private float distance;

        private void Awake()
        {
            ResolveShuttle();
            yaw = startYaw;
            pitch = Mathf.Clamp(startPitch, minPitch, maxPitch);
            distance = Mathf.Clamp(startDistance, minDistance, maxDistance);
        }

        private void LateUpdate()
        {
            if (!ResolveShuttle())
                return;

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                int yawInput = (keyboard.eKey.isPressed ? 1 : 0) - (keyboard.qKey.isPressed ? 1 : 0);
                int pitchInput = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
                int zoomInput = (keyboard.fKey.isPressed ? 1 : 0) - (keyboard.rKey.isPressed ? 1 : 0);

                yaw = Mathf.Repeat(yaw + yawInput * orbitDegreesPerSecond * deltaTime, 360f);
                pitch = Mathf.Clamp(pitch + pitchInput * tiltDegreesPerSecond * deltaTime, minPitch, maxPitch);
                distance = Mathf.Clamp(distance + zoomInput * zoomUnitsPerSecond * deltaTime,
                    minDistance, maxDistance);
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.rightButton.isPressed)
                {
                    Vector2 drag = mouse.delta.ReadValue();
                    yaw = Mathf.Repeat(yaw + drag.x * mouseOrbitDegreesPerPixel, 360f);
                    pitch = Mathf.Clamp(pitch - drag.y * mouseOrbitDegreesPerPixel,
                        minPitch, maxPitch);
                }

                distance = Mathf.Clamp(distance - mouse.scroll.ReadValue().y * mouseZoomUnitsPerScrollUnit,
                    minDistance, maxDistance);
            }

            float yawRadians = yaw * Mathf.Deg2Rad;
            float pitchRadians = pitch * Mathf.Deg2Rad;
            Vector3 orbitOffset = new Vector3(
                Mathf.Sin(yawRadians) * Mathf.Cos(pitchRadians),
                Mathf.Sin(pitchRadians),
                -Mathf.Cos(yawRadians) * Mathf.Cos(pitchRadians)) * distance;

            transform.position = shuttle.position + orbitOffset;
            transform.LookAt(shuttle.position);
        }

        private bool ResolveShuttle()
        {
            if (shuttle != null && shuttle.gameObject.scene.IsValid())
                return true;

            // A prefab asset reference is not the live Shuttle instance in the open scene.
            shuttle = null;
            ShuttleVoyageComponent voyage = FindObjectOfType<ShuttleVoyageComponent>();
            if (voyage != null)
            {
                shuttle = voyage.transform;
                return true;
            }

            GameObject found = GameObject.Find("Shuttle");
            if (found != null)
                shuttle = found.transform;
            return shuttle != null;
        }
    }
}
