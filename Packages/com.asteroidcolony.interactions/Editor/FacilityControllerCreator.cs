using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public sealed class FacilityControllerCreator : EditorWindow
{
    private AnimationClip idleClip;
    private AnimationClip walkClip;
    private AnimationClip actionAClip;
    private AnimationClip actionBClip;

    [MenuItem("Colony/Interactions/Create Compatible Colonist Controller")]
    private static void OpenWindow()
    {
        GetWindow<FacilityControllerCreator>("Colonist Controller Creator");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Choose the four source clips. The generated controller exposes Speed, ActionASpeed, and ActionBSpeed and contains the runtime state names required by ColonistAnimationDriver.",
            MessageType.Info);
        idleClip = (AnimationClip)EditorGUILayout.ObjectField("Idle", idleClip, typeof(AnimationClip), false);
        walkClip = (AnimationClip)EditorGUILayout.ObjectField("Walk", walkClip, typeof(AnimationClip), false);
        actionAClip = (AnimationClip)EditorGUILayout.ObjectField("Action A", actionAClip, typeof(AnimationClip), false);
        actionBClip = (AnimationClip)EditorGUILayout.ObjectField("Action B", actionBClip, typeof(AnimationClip), false);

        using (new EditorGUI.DisabledScope(idleClip == null || walkClip == null))
        {
            if (GUILayout.Button("Create Controller"))
            {
                CreateController();
            }
        }
    }

    private void CreateController()
    {
        string controllerPath = EditorUtility.SaveFilePanelInProject(
            "Create Colonist Controller",
            "ColonistActivity",
            "controller",
            "Choose where to save the controller.");
        if (string.IsNullOrEmpty(controllerPath))
        {
            return;
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("ActionASpeed", AnimatorControllerParameterType.Float);
        controller.AddParameter("ActionBSpeed", AnimatorControllerParameterType.Float);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        BlendTree locomotionTree = new BlendTree
        {
            name = "Locomotion",
            blendType = BlendTreeType.Simple1D,
            blendParameter = "Speed",
            useAutomaticThresholds = false
        };
        locomotionTree.AddChild(idleClip, 0f);
        locomotionTree.AddChild(walkClip, 1f);
        AssetDatabase.AddObjectToAsset(locomotionTree, controller);

        AnimatorState locomotion = stateMachine.AddState("Locomotion");
        locomotion.motion = locomotionTree;
        stateMachine.defaultState = locomotion;

        AnimationClip placeholderA = CloneOrCreatePlaceholder(actionAClip, "ActionA Placeholder", controller);
        AnimationClip placeholderB = CloneOrCreatePlaceholder(actionBClip, "ActionB Placeholder", controller);
        AnimatorState actionA = stateMachine.AddState("ActionA");
        AnimatorState actionB = stateMachine.AddState("ActionB");
        actionA.motion = placeholderA;
        actionB.motion = placeholderB;
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = controller;
        EditorGUIUtility.PingObject(controller);
        Debug.Log($"Created compatible colonist controller at {controllerPath}.");
    }

    private static AnimationClip CloneOrCreatePlaceholder(
        AnimationClip source,
        string name,
        AnimatorController controller)
    {
        AnimationClip clip = source == null
            ? new AnimationClip()
            : Object.Instantiate(source);
        clip.name = name;
        AssetDatabase.AddObjectToAsset(clip, controller);
        return clip;
    }
}
