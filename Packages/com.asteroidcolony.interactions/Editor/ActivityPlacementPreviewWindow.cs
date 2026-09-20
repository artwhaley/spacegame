using System.Collections.Generic;
using Colony.Interactions;
using UnityEditor;
using UnityEngine;

public sealed class ActivityPlacementPreviewWindow : EditorWindow
{
    private enum SegmentKind { Entry, Loop, Active, Exit }

    private sealed class SegmentChoice
    {
        public SegmentKind Kind;
        public int Index;
        public string Label;
        public AnimationSegment Segment;
    }

    [SerializeField] private InteractableFacility facility;
    [SerializeField] private Animator previewActor;
    [SerializeField] private int activityIndex;
    [SerializeField] private int segmentIndex;
    [SerializeField] private float previewTime;
    [SerializeField] private bool editPlacement = true;

    private GameObject previewRoot;
    private GameObject previewAvatar;
    private Animator previewSource;
    private bool ownsAnimationMode;

    public static void Open(InteractableFacility targetFacility, string activityId)
    {
        ActivityPlacementPreviewWindow window =
            GetWindow<ActivityPlacementPreviewWindow>("Activity Placement Preview");
        window.facility = targetFacility;
        window.activityIndex = FindActivityIndex(targetFacility, activityId);
        window.segmentIndex = 0;
        window.previewTime = 0f;

        if (window.previewActor == null)
        {
            ColonistMotor motor = Object.FindAnyObjectByType<ColonistMotor>();
            if (motor != null)
            {
                window.previewActor = motor.GetComponent<Animator>();
            }
        }

        window.Show();
        window.Focus();
        window.RefreshPreview();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += HandleSceneGui;
        EditorApplication.update += RefreshPreview;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= HandleSceneGui;
        EditorApplication.update -= RefreshPreview;
        DestroyPreview();
    }

    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        facility = (InteractableFacility)EditorGUILayout.ObjectField(
            "Facility", facility, typeof(InteractableFacility), true);
        if (EditorGUI.EndChangeCheck())
        {
            activityIndex = 0;
            segmentIndex = 0;
            previewTime = 0f;
        }

        if (!TryGetBinding(out FacilityActivityBinding binding))
        {
            EditorGUILayout.HelpBox("Choose a facility that has at least one activity binding.", MessageType.Info);
            return;
        }

        DrawActivityPicker();
        previewActor = (Animator)EditorGUILayout.ObjectField(
            "Preview Actor", previewActor, typeof(Animator), true);

        List<SegmentChoice> choices = BuildSegmentChoices(binding);
        if (choices.Count == 0)
        {
            EditorGUILayout.HelpBox("This activity has no playable animation segments.", MessageType.Warning);
            return;
        }

        segmentIndex = Mathf.Clamp(segmentIndex, 0, choices.Count - 1);
        string[] labels = new string[choices.Count];
        for (int index = 0; index < choices.Count; index++)
        {
            labels[index] = choices[index].Label;
        }

        EditorGUI.BeginChangeCheck();
        segmentIndex = EditorGUILayout.Popup("Preview Segment", segmentIndex, labels);
        if (EditorGUI.EndChangeCheck())
        {
            previewTime = 0f;
        }

        SegmentChoice choice = choices[segmentIndex];
        AnimationClip clip = choice.Segment.Clip;
        EditorGUILayout.ObjectField("Clip", clip, typeof(AnimationClip), false);
        previewTime = EditorGUILayout.Slider("Time", previewTime, 0f, Mathf.Max(clip.length, 0.01f));
        editPlacement = EditorGUILayout.Toggle("Show Placement Handle", editPlacement);
        EditorGUILayout.HelpBox(
            "The selected segment's placement and contact data live on the facility. The preview actor is a temporary hidden clone.",
            MessageType.None);

        if (GUILayout.Button("Frame Preview in Scene"))
        {
            FramePreview();
        }

        RefreshPreview();
    }

    private void DrawActivityPicker()
    {
        IReadOnlyList<FacilityActivityBinding> activities = facility.Activities;
        activityIndex = Mathf.Clamp(activityIndex, 0, activities.Count - 1);
        string[] labels = new string[activities.Count];
        for (int index = 0; index < activities.Count; index++)
        {
            labels[index] = string.IsNullOrWhiteSpace(activities[index].ActivityId)
                ? $"Activity {index}"
                : activities[index].ActivityId;
        }

        EditorGUI.BeginChangeCheck();
        activityIndex = EditorGUILayout.Popup("Activity", activityIndex, labels);
        if (EditorGUI.EndChangeCheck())
        {
            segmentIndex = 0;
            previewTime = 0f;
        }
    }

    private void HandleSceneGui(SceneView sceneView)
    {
        if (!editPlacement || !TryGetBinding(out FacilityActivityBinding binding) ||
            !TryGetCurrentChoice(binding, out SegmentChoice choice))
        {
            return;
        }

        Transform placementAnchor = choice.Segment.ResolvePlacementAnchor(binding);
        if (placementAnchor == null)
        {
            return;
        }

        choice.Segment.GetWorldPlacement(placementAnchor, out Vector3 position, out Quaternion rotation);
        Handles.color = new Color(0.3f, 0.9f, 1f, 1f);
        Handles.Label(position + Vector3.up * 0.12f, "Selected clip placement");

        EditorGUI.BeginChangeCheck();
        Vector3 adjustedPosition = Handles.PositionHandle(position, rotation);
        Quaternion adjustedRotation = Handles.RotationHandle(rotation, adjustedPosition);
        if (EditorGUI.EndChangeCheck())
        {
            SetPlacement(
                choice,
                placementAnchor.InverseTransformPoint(adjustedPosition),
                (Quaternion.Inverse(placementAnchor.rotation) * adjustedRotation).eulerAngles);
            RefreshPreview();
        }
    }

    private void RefreshPreview()
    {
        if (Application.isPlaying || !TryGetBinding(out FacilityActivityBinding binding) ||
            !TryGetCurrentChoice(binding, out SegmentChoice choice) ||
            choice.Segment.Clip == null || previewActor == null)
        {
            DestroyPreview();
            return;
        }

        EnsurePreview();
        if (previewRoot == null || previewAvatar == null)
        {
            return;
        }

        Transform placementAnchor = choice.Segment.ResolvePlacementAnchor(binding);
        if (placementAnchor == null)
        {
            DestroyPreview();
            return;
        }

        choice.Segment.GetWorldPlacement(placementAnchor, out Vector3 position, out Quaternion rotation);
        previewRoot.transform.SetPositionAndRotation(position, rotation);

        if (!AnimationMode.InAnimationMode())
        {
            AnimationMode.StartAnimationMode();
            ownsAnimationMode = true;
        }

        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(
            previewAvatar,
            choice.Segment.Clip,
            Mathf.Clamp(previewTime, 0f, choice.Segment.Clip.length));
        AnimationMode.EndSampling();
        previewAvatar.transform.localPosition = Vector3.zero;
        previewAvatar.transform.localRotation = Quaternion.identity;
        SceneView.RepaintAll();
    }

    private void EnsurePreview()
    {
        if (previewAvatar != null && previewSource == previewActor)
        {
            return;
        }

        DestroyPreview();
        previewRoot = new GameObject("Activity Placement Preview") { hideFlags = HideFlags.HideAndDontSave };
        previewAvatar = Instantiate(previewActor.gameObject, previewRoot.transform);
        previewAvatar.name = "Preview Avatar";
        previewAvatar.transform.localPosition = Vector3.zero;
        previewAvatar.transform.localRotation = Quaternion.identity;
        previewAvatar.transform.localScale = previewActor.transform.lossyScale;
        previewSource = previewActor;

        foreach (Transform transform in previewAvatar.GetComponentsInChildren<Transform>(true))
        {
            transform.gameObject.hideFlags = HideFlags.HideAndDontSave;
        }

        foreach (Behaviour behaviour in previewAvatar.GetComponentsInChildren<Behaviour>(true))
        {
            behaviour.hideFlags = HideFlags.HideAndDontSave;
            if (!(behaviour is Animator))
            {
                behaviour.enabled = false;
            }
        }
    }

    private void DestroyPreview()
    {
        if (previewRoot != null)
        {
            DestroyImmediate(previewRoot);
        }

        previewRoot = null;
        previewAvatar = null;
        previewSource = null;
        if (ownsAnimationMode && AnimationMode.InAnimationMode())
        {
            AnimationMode.StopAnimationMode();
        }

        ownsAnimationMode = false;
    }

    private void FramePreview()
    {
        if (previewRoot == null)
        {
            return;
        }

        Selection.activeGameObject = previewRoot;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    private bool TryGetBinding(out FacilityActivityBinding binding)
    {
        binding = null;
        if (facility == null || facility.Activities == null || facility.Activities.Count == 0)
        {
            return false;
        }

        activityIndex = Mathf.Clamp(activityIndex, 0, facility.Activities.Count - 1);
        binding = facility.Activities[activityIndex];
        return binding != null;
    }

    private bool TryGetCurrentChoice(FacilityActivityBinding binding, out SegmentChoice choice)
    {
        List<SegmentChoice> choices = BuildSegmentChoices(binding);
        if (choices.Count == 0)
        {
            choice = null;
            return false;
        }

        segmentIndex = Mathf.Clamp(segmentIndex, 0, choices.Count - 1);
        choice = choices[segmentIndex];
        return true;
    }

    private static List<SegmentChoice> BuildSegmentChoices(FacilityActivityBinding binding)
    {
        List<SegmentChoice> choices = new List<SegmentChoice>();
        if (binding == null)
        {
            return choices;
        }

        AddSegments(choices, binding.EntrySteps, SegmentKind.Entry, "Entry");
        if (binding.LoopSegment != null && binding.LoopSegment.Clip != null)
        {
            choices.Add(new SegmentChoice
            {
                Kind = SegmentKind.Loop,
                Index = 0,
                Label = $"Loop: {binding.LoopSegment.Clip.name}",
                Segment = binding.LoopSegment
            });
        }

        AddSegments(choices, binding.ActiveSteps, SegmentKind.Active, "Active");
        AddSegments(choices, binding.ExitSteps, SegmentKind.Exit, "Exit");
        return choices;
    }

    private static void AddSegments(
        List<SegmentChoice> choices,
        AnimationSegment[] segments,
        SegmentKind kind,
        string prefix)
    {
        if (segments == null)
        {
            return;
        }

        for (int index = 0; index < segments.Length; index++)
        {
            AnimationSegment segment = segments[index];
            if (segment == null || segment.Clip == null)
            {
                continue;
            }

            choices.Add(new SegmentChoice
            {
                Kind = kind,
                Index = index,
                Label = $"{prefix} {index + 1}: {segment.Clip.name}",
                Segment = segment
            });
        }
    }

    private static int FindActivityIndex(InteractableFacility targetFacility, string activityId)
    {
        if (targetFacility == null || targetFacility.Activities == null)
        {
            return 0;
        }

        for (int index = 0; index < targetFacility.Activities.Count; index++)
        {
            if (targetFacility.Activities[index] != null &&
                targetFacility.Activities[index].ActivityId == activityId)
            {
                return index;
            }
        }

        return 0;
    }

    private void SetPlacement(SegmentChoice choice, Vector3 localPosition, Vector3 localEulerAngles)
    {
        SerializedObject serializedFacility = new SerializedObject(facility);
        SerializedProperty segmentProperty = GetSegmentProperty(serializedFacility, activityIndex, choice);
        if (segmentProperty == null)
        {
            return;
        }

        Undo.RecordObject(facility, "Adjust Activity Clip Placement");
        segmentProperty.FindPropertyRelative("placementPositionOffset").vector3Value = localPosition;
        segmentProperty.FindPropertyRelative("placementEulerOffset").vector3Value = localEulerAngles;
        serializedFacility.ApplyModifiedProperties();
        EditorUtility.SetDirty(facility);
    }

    private static SerializedProperty GetSegmentProperty(
        SerializedObject serializedFacility,
        int activityIndex,
        SegmentChoice choice)
    {
        SerializedProperty activity = serializedFacility.FindProperty("activities")
            .GetArrayElementAtIndex(activityIndex);

        switch (choice.Kind)
        {
            case SegmentKind.Entry:
                return activity.FindPropertyRelative("entrySteps").GetArrayElementAtIndex(choice.Index);
            case SegmentKind.Loop:
                return activity.FindPropertyRelative("loopStep");
            case SegmentKind.Active:
                return activity.FindPropertyRelative("activeSteps").GetArrayElementAtIndex(choice.Index);
            case SegmentKind.Exit:
                return activity.FindPropertyRelative("exitSteps").GetArrayElementAtIndex(choice.Index);
            default:
                return null;
        }
    }
}
