using UnityEngine;
using TMPro;

namespace AsteroidColony
{
    [DisallowMultipleComponent]
    public sealed class ColonistOverheadDisplay : MonoBehaviour
    {
        [SerializeField]
        private ColonistStatsComponent stats;

        [SerializeField]
        private Transform billboardRoot;

        [SerializeField]
        private TextMeshPro nameText;

        [SerializeField]
        private Transform iconStack;

        [SerializeField]
        private GameObject sleepyIcon;

        [SerializeField]
        private GameObject hungryIcon;

        [SerializeField]
        private ColonistIdentity identity;

        [SerializeField, Min(0f)]
        private float iconSpacing = 0.25f;

        [SerializeField]
        private float billboardYawOffset = 180f;

        private Camera cachedCamera;

        private void Awake()
        {
            ResolveReferences();
            RefreshPresentation();
        }

        private void LateUpdate()
        {
            RefreshPresentation();
            BillboardToCamera();
        }

        private void ResolveReferences()
        {
            if (stats == null)
                stats = GetComponentInParent<ColonistStatsComponent>();

            if (identity == null)
                identity = GetComponentInParent<ColonistIdentity>();

            if (billboardRoot == null)
                billboardRoot = transform;

            if (nameText == null)
                nameText = GetComponentInChildren<TextMeshPro>(true);

            if (iconStack == null)
            {
                Transform candidate = transform.Find("StatusIcons");
                if (candidate != null)
                    iconStack = candidate;
            }

            if (sleepyIcon == null && iconStack != null)
            {
                Transform candidate = iconStack.Find("SleepyZ");
                if (candidate != null)
                    sleepyIcon = candidate.gameObject;
            }

            if (hungryIcon == null && iconStack != null)
            {
                Transform candidate = iconStack.Find("HungryFood");
                if (candidate != null)
                    hungryIcon = candidate.gameObject;
            }
        }

        private void RefreshPresentation()
        {
            if (nameText != null)
                nameText.text = identity != null ? identity.DisplayName : "Colonist";

            if (sleepyIcon != null)
            {
                sleepyIcon.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                sleepyIcon.SetActive(
                    stats != null &&
                    stats.IsSleepy);
            }

            if (hungryIcon != null)
            {
                hungryIcon.SetActive(
                    stats != null &&
                    stats.IsHungry);
            }

            RefreshIconLayout();
        }

        private void RefreshIconLayout()
        {
            if (iconStack == null)
                return;

            int activeIndex = 0;
            for (int index = 0; index < iconStack.childCount; index++)
            {
                Transform child = iconStack.GetChild(index);
                if (!child.gameObject.activeSelf)
                    continue;

                child.localPosition = Vector3.right * (activeIndex * iconSpacing);
                activeIndex++;
            }
        }

        private void BillboardToCamera()
        {
            if (billboardRoot == null)
                return;

            if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
                cachedCamera = Camera.main;

            if (cachedCamera == null)
                return;

            Vector3 toCamera = cachedCamera.transform.position - billboardRoot.position;
            if (toCamera.sqrMagnitude <= 0.0001f)
                return;

            Quaternion cameraFacingRotation =
                Quaternion.LookRotation(toCamera, Vector3.up);
            billboardRoot.rotation = cameraFacingRotation *
                Quaternion.Euler(0f, billboardYawOffset, 0f);
        }
    }
}
