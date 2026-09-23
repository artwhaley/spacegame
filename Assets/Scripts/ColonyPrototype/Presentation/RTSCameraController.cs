using UnityEngine;
using UnityEngine.InputSystem;

namespace AsteroidColony
{
    /// <summary>
    /// Drives a fixed-pitch RTS camera from a yaw-only focus rig.
    /// Place the controlled camera as a direct child of this GameObject.
    /// </summary>
    public sealed class RTSCameraController : MonoBehaviour
    {
        [Header("Rig References")]
        [Tooltip("The camera transform, assigned to a direct child of this yaw-only rig.")]
        [SerializeField] private Transform cameraTransform;

        [Tooltip("World object the V key recenters the rig over.")]
        [SerializeField] private GameObject recenterTarget;

        [Header("Fixed Camera Angle")]
        [Tooltip("Fixed downward camera pitch in degrees. The rig itself only rotates around world up.")]
        [SerializeField, Range(15f, 70f)] private float pitchDegrees = 40f;

        [Tooltip("Camera distance from the focus rig when the scene starts.")]
        [SerializeField, Min(0.1f)] private float initialZoomDistance = 18f;

        [Tooltip("Closest camera distance from the focus rig.")]
        [SerializeField, Min(0.1f)] private float minZoomDistance = 8f;

        [Tooltip("Farthest camera distance from the focus rig.")]
        [SerializeField, Min(0.1f)] private float maxZoomDistance = 36f;

        [Header("Pan")]
        [Tooltip("World units per second at the reference zoom distance.")]
        [SerializeField, Min(0f)] private float panSpeedAtReferenceZoom = 12f;

        [Tooltip("Zoom distance at which the pan speed above is used.")]
        [SerializeField, Min(0.1f)] private float referenceZoomDistance = 18f;

        [Tooltip("Scale pan speed and acceleration with zoom distance.")]
        [SerializeField] private bool scalePanSpeedWithZoom = true;

        [Tooltip("Lowest pan speed multiplier when zoomed in.")]
        [SerializeField, Min(0f)] private float minPanSpeedMultiplier = 0.65f;

        [Tooltip("Highest pan speed multiplier when zoomed out.")]
        [SerializeField, Min(0f)] private float maxPanSpeedMultiplier = 1.7f;

        [Tooltip("Pan acceleration in world units per second squared at reference zoom.")]
        [SerializeField, Min(0f)] private float panAcceleration = 90f;

        [Tooltip("Pan deceleration in world units per second squared at reference zoom.")]
        [SerializeField, Min(0f)] private float panDeceleration = 120f;

        [Header("Rotation")]
        [Tooltip("Continuous yaw rotation speed while Q or E is held, in degrees per second.")]
        [SerializeField, Min(0f)] private float rotationSpeed = 110f;

        [Tooltip("How quickly rotation reaches the requested speed.")]
        [SerializeField, Min(0f)] private float rotationAcceleration = 1200f;

        [Tooltip("How quickly rotation stops after Q or E is released.")]
        [SerializeField, Min(0f)] private float rotationDeceleration = 1600f;

        [Header("Zoom and Recenter")]
        [Tooltip("Zoom distance change per second while R or F is held.")]
        [SerializeField, Min(0f)] private float zoomSpeed = 18f;

        [Tooltip("Smooth time for zoom distance. Set to zero for immediate zoom.")]
        [SerializeField, Min(0f)] private float zoomSmoothTime = 0.12f;

        [Tooltip("Smooth time used when V moves the focus back to the recenter target.")]
        [SerializeField, Min(0.01f)] private float recenterSmoothTime = 0.25f;

        private float focusHeight;
        private float yawDegrees;
        private float rotationVelocity;
        private float targetZoomDistance;
        private float currentZoomDistance;
        private float zoomVelocity;
        private Vector3 panVelocity;
        private Vector3 recenterVelocity;
        private bool isRecentering;

        private void Awake()
        {
            if (cameraTransform == null)
            {
                Camera childCamera = GetComponentInChildren<Camera>();
                if (childCamera != null)
                    cameraTransform = childCamera.transform;
            }

            if (cameraTransform == null || cameraTransform.parent != transform)
            {
                Debug.LogError(
                    "RTSCameraController requires a Camera on a direct child of its rig.",
                    this);
                enabled = false;
                return;
            }

            minZoomDistance = Mathf.Max(0.1f, minZoomDistance);
            maxZoomDistance = Mathf.Max(minZoomDistance, maxZoomDistance);
            focusHeight = transform.position.y;
            yawDegrees = transform.eulerAngles.y;
            targetZoomDistance = Mathf.Clamp(initialZoomDistance, minZoomDistance, maxZoomDistance);
            currentZoomDistance = targetZoomDistance;

            transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            ApplyCameraPose();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            Keyboard keyboard = Keyboard.current;
            float horizontalInput = 0f;
            float verticalInput = 0f;
            int rotationInput = 0;
            int zoomInput = 0;
            bool recenterPressed = false;

            if (keyboard != null)
            {
                horizontalInput =
                    (keyboard.dKey.isPressed ? 1f : 0f) -
                    (keyboard.aKey.isPressed ? 1f : 0f);
                verticalInput =
                    (keyboard.wKey.isPressed ? 1f : 0f) -
                    (keyboard.sKey.isPressed ? 1f : 0f);
                rotationInput =
                    (keyboard.eKey.isPressed ? 1 : 0) -
                    (keyboard.qKey.isPressed ? 1 : 0);
                zoomInput =
                    (keyboard.fKey.isPressed ? 1 : 0) -
                    (keyboard.rKey.isPressed ? 1 : 0);
                recenterPressed = keyboard.vKey.wasPressedThisFrame;
            }

            UpdateRotation(rotationInput, deltaTime);
            UpdatePanning(horizontalInput, verticalInput, recenterPressed, deltaTime);
            UpdateZoom(zoomInput, deltaTime);
        }

        private void LateUpdate()
        {
            if (cameraTransform != null)
                ApplyCameraPose();
        }

        private void UpdateRotation(int rotationInput, float deltaTime)
        {
            float requestedRotationVelocity = rotationInput * rotationSpeed;
            float acceleration = rotationInput == 0 ? rotationDeceleration : rotationAcceleration;
            rotationVelocity = Mathf.MoveTowards(
                rotationVelocity,
                requestedRotationVelocity,
                acceleration * deltaTime);

            yawDegrees = Mathf.Repeat(yawDegrees + rotationVelocity * deltaTime, 360f);
            transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
        }

        private void UpdatePanning(
            float horizontalInput,
            float verticalInput,
            bool recenterPressed,
            float deltaTime)
        {
            Vector3 inputDirection = new Vector3(horizontalInput, 0f, verticalInput);
            inputDirection = Quaternion.Euler(0f, yawDegrees, 0f) * inputDirection;
            if (inputDirection.sqrMagnitude > 1f)
                inputDirection.Normalize();

            if (recenterPressed && recenterTarget != null)
            {
                isRecentering = true;
                panVelocity = Vector3.zero;
                recenterVelocity = Vector3.zero;
            }
            else if (isRecentering && inputDirection.sqrMagnitude > 0f)
            {
                isRecentering = false;
                recenterVelocity = Vector3.zero;
            }

            if (isRecentering && recenterTarget != null)
            {
                Vector3 targetPosition = recenterTarget.transform.position;
                targetPosition.y = focusHeight;

                Vector3 nextPosition = Vector3.SmoothDamp(
                    transform.position,
                    targetPosition,
                    ref recenterVelocity,
                    Mathf.Max(0.01f, recenterSmoothTime),
                    Mathf.Infinity,
                    deltaTime);
                nextPosition.y = focusHeight;
                transform.position = nextPosition;

                if ((targetPosition - nextPosition).sqrMagnitude < 0.0025f)
                {
                    transform.position = targetPosition;
                    recenterVelocity = Vector3.zero;
                    isRecentering = false;
                }

                panVelocity = Vector3.zero;
                return;
            }

            float zoomScale = GetPanSpeedScale();
            Vector3 requestedVelocity =
                inputDirection * (panSpeedAtReferenceZoom * zoomScale);
            float acceleration = inputDirection.sqrMagnitude > 0f
                ? panAcceleration
                : panDeceleration;
            panVelocity = Vector3.MoveTowards(
                panVelocity,
                requestedVelocity,
                acceleration * zoomScale * deltaTime);

            Vector3 position = transform.position;
            position.x += panVelocity.x * deltaTime;
            position.z += panVelocity.z * deltaTime;
            position.y = focusHeight;
            transform.position = position;
        }

        private void UpdateZoom(int zoomInput, float deltaTime)
        {
            targetZoomDistance = Mathf.Clamp(
                targetZoomDistance + (zoomInput * zoomSpeed * deltaTime),
                minZoomDistance,
                maxZoomDistance);

            if (zoomSmoothTime <= 0f)
            {
                currentZoomDistance = targetZoomDistance;
                zoomVelocity = 0f;
            }
            else
            {
                currentZoomDistance = Mathf.SmoothDamp(
                    currentZoomDistance,
                    targetZoomDistance,
                    ref zoomVelocity,
                    zoomSmoothTime,
                    Mathf.Infinity,
                    deltaTime);
            }
        }

        private float GetPanSpeedScale()
        {
            if (!scalePanSpeedWithZoom)
                return 1f;

            float referenceDistance = Mathf.Max(0.1f, referenceZoomDistance);
            return Mathf.Clamp(
                currentZoomDistance / referenceDistance,
                minPanSpeedMultiplier,
                maxPanSpeedMultiplier);
        }

        private void ApplyCameraPose()
        {
            float pitchRadians = pitchDegrees * Mathf.Deg2Rad;
            cameraTransform.localPosition = new Vector3(
                0f,
                Mathf.Sin(pitchRadians) * currentZoomDistance,
                -Mathf.Cos(pitchRadians) * currentZoomDistance);
            cameraTransform.localRotation = Quaternion.Euler(pitchDegrees, 0f, 0f);
        }

        private void OnValidate()
        {
            minZoomDistance = Mathf.Max(0.1f, minZoomDistance);
            maxZoomDistance = Mathf.Max(minZoomDistance, maxZoomDistance);
            initialZoomDistance = Mathf.Clamp(
                initialZoomDistance,
                minZoomDistance,
                maxZoomDistance);
            referenceZoomDistance = Mathf.Max(0.1f, referenceZoomDistance);
            minPanSpeedMultiplier = Mathf.Max(0f, minPanSpeedMultiplier);
            maxPanSpeedMultiplier = Mathf.Max(minPanSpeedMultiplier, maxPanSpeedMultiplier);
        }
    }
}
