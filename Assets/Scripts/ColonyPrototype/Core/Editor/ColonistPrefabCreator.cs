using System;
using System.Linq;
using Colony.Interactions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace AsteroidColony
{
    /// <summary>
    /// Creates the first real colonist actor without modifying the existing
    /// prototype scene. Locomotion is wired from the imported idle, walk, and
    /// turn clips; the named action placeholders remain for facility activities.
    /// </summary>
    public static class ColonistPrefabCreator
    {
        private const string SourceCharacterPath =
            "Assets/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Male_01.prefab";
        private const string ControllerPath =
            "Assets/Animations/Colonists/ColonistHumanoid.controller";
        private const string PrefabPath =
            "Assets/Prefabs/Colonists/Colonist_Synty_Male_01.prefab";

        private const string IdleClipPath =
            "Assets/Animations/Colonists/Mixamo_POLYGON_Guy_Naked@Idle.fbx";
        private const string PreferredWalkClipPath =
            "Assets/Animations/Colonists/Mixamo_POLYGON_Guy_Naked@Walking.fbx";
        private const string FallbackWalkClipPath =
            "Assets/Animations/Colonists/Mixamo_POLYGON_Guy_Naked@Walking (1).fbx";
        private const string LeftTurnClipPath =
            "Assets/Animations/Colonists/Left Turn.fbx";
        private const string RightTurnClipPath =
            "Assets/Animations/Colonists/Right Turn.fbx";

        [MenuItem("Colony/People/Create Synty Colonist Base")]
        public static void CreateSyntyColonistBase()
        {
            EnsureFolderPath("Assets/Animations/Colonists");
            EnsureFolderPath("Assets/Prefabs/Colonists");

            AnimatorController controller = CreateOrLoadController();
            GameObject prefab = CreateColonistPrefab(controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (prefab != null)
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
        }

        [MenuItem("Colony/People/Wire Synty Colonist Animations")]
        public static void WireSyntyColonistAnimations()
        {
            EnsureFolderPath("Assets/Animations/Colonists");
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError(
                    $"Could not find the colonist controller at {ControllerPath}. " +
                    "Create the Synty colonist base first.");
                return;
            }

            if (WireLocomotionAnimations(controller))
            {
                AssetDatabase.SaveAssets();
                Selection.activeObject = controller;
                EditorGUIUtility.PingObject(controller);
                Debug.Log(
                    $"Wired colonist locomotion animations into {ControllerPath}. " +
                    "The motor will drive Speed and signed Turn at runtime.");
            }
        }

        private static AnimatorController CreateOrLoadController()
        {
            AnimatorController existing =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null)
            {
                WireLocomotionAnimations(existing);
                return existing;
            }

            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AddFloatParameter(controller, "Speed");
            AddFloatParameter(controller, "Turn");
            AddFloatParameter(controller, "ActionASpeed");
            AddFloatParameter(controller, "ActionBSpeed");

            AnimationClip idle = CreateControllerClip(controller, "Idle Placeholder");
            AnimationClip walk = CreateControllerClip(controller, "Walk Placeholder");
            AnimationClip leftTurn = CreateControllerClip(controller, "Left Turn Placeholder");
            AnimationClip rightTurn = CreateControllerClip(controller, "Right Turn Placeholder");
            AnimationClip actionA = CreateControllerClip(controller, "ActionA Placeholder");
            AnimationClip actionB = CreateControllerClip(controller, "ActionB Placeholder");

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            BlendTree locomotionTree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "Speed",
                blendParameterY = "Turn",
                useAutomaticThresholds = false
            };
            locomotionTree.AddChild(idle, Vector2.zero);
            locomotionTree.AddChild(walk, Vector2.right);
            locomotionTree.AddChild(leftTurn, Vector2.down);
            locomotionTree.AddChild(rightTurn, Vector2.up);
            AssetDatabase.AddObjectToAsset(locomotionTree, controller);

            AnimatorState locomotion = stateMachine.AddState("Locomotion");
            locomotion.motion = locomotionTree;
            stateMachine.defaultState = locomotion;

            AnimatorState actionAState = stateMachine.AddState("ActionA");
            actionAState.motion = actionA;
            AnimatorState actionBState = stateMachine.AddState("ActionB");
            actionBState.motion = actionB;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static bool WireLocomotionAnimations(AnimatorController controller)
        {
            ConfigureHumanoidAnimationImport(LeftTurnClipPath);
            ConfigureHumanoidAnimationImport(RightTurnClipPath);

            AnimationClip idle = LoadClip(
                IdleClipPath,
                "Idle",
                "mixamo.com");
            AnimationClip walk = LoadClip(
                PreferredWalkClipPath,
                "Walking",
                "mixamo.com");
            if (walk == null)
            {
                walk = LoadClip(
                    FallbackWalkClipPath,
                    "Walking",
                    "mixamo.com");
            }

            AnimationClip leftTurn = LoadClip(
                LeftTurnClipPath,
                "Left Turn",
                "mixamo.com");
            AnimationClip rightTurn = LoadClip(
                RightTurnClipPath,
                "Right Turn",
                "mixamo.com");

            if (idle == null || walk == null || leftTurn == null || rightTurn == null)
            {
                Debug.LogWarning(
                    "Colonist locomotion could not be fully wired. " +
                    $"Idle={(idle != null ? idle.name : "missing")}, " +
                    $"Walk={(walk != null ? walk.name : "missing")}, " +
                    $"LeftTurn={(leftTurn != null ? leftTurn.name : "missing")}, " +
                    $"RightTurn={(rightTurn != null ? rightTurn.name : "missing")}. " +
                    "The controller was left unchanged.");
                return false;
            }

            AddFloatParameter(controller, "Turn");

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState locomotion = FindState(stateMachine, "Locomotion");
            if (locomotion == null)
            {
                locomotion = stateMachine.AddState("Locomotion");
                stateMachine.defaultState = locomotion;
            }

            BlendTree locomotionTree = locomotion.motion as BlendTree;
            if (locomotionTree == null)
            {
                locomotionTree = new BlendTree { name = "Locomotion" };
                AssetDatabase.AddObjectToAsset(locomotionTree, controller);
                locomotion.motion = locomotionTree;
            }

            locomotionTree.blendType = BlendTreeType.FreeformDirectional2D;
            locomotionTree.blendParameter = "Speed";
            locomotionTree.blendParameterY = "Turn";
            locomotionTree.useAutomaticThresholds = false;
            locomotionTree.children = Array.Empty<ChildMotion>();
            locomotionTree.AddChild(idle, Vector2.zero);
            locomotionTree.AddChild(walk, Vector2.right);
            locomotionTree.AddChild(leftTurn, Vector2.down);
            locomotionTree.AddChild(rightTurn, Vector2.up);

            EditorUtility.SetDirty(controller);
            Debug.Log(
                $"Selected locomotion clips: idle={idle.name}, walk={walk.name}, " +
                $"left={leftTurn.name}, right={rightTurn.name}.");
            return true;
        }

        private static void ConfigureHumanoidAnimationImport(string assetPath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null ||
                (importer.animationType == ModelImporterAnimationType.Human &&
                 importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel))
            {
                return;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.SaveAndReimport();
            Debug.Log($"Configured {assetPath} as a Humanoid animation from its own source rig.");
        }

        private static AnimatorState FindState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            for (int index = 0; index < stateMachine.states.Length; index++)
            {
                AnimatorState state = stateMachine.states[index].state;
                if (state != null && state.name == stateName)
                {
                    return state;
                }
            }

            return null;
        }

        private static AnimationClip LoadClip(
            string assetPath,
            params string[] preferredNames)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            AnimationClip[] clips = assets
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                .ToArray();

            for (int nameIndex = 0; nameIndex < preferredNames.Length; nameIndex++)
            {
                string preferredName = preferredNames[nameIndex];
                AnimationClip matchingClip = clips.FirstOrDefault(
                    clip => clip.name == preferredName);
                if (matchingClip != null)
                {
                    return matchingClip;
                }
            }

            return clips.Length == 1 ? clips[0] : null;
        }

        private static GameObject CreateColonistPrefab(AnimatorController controller)
        {
            GameObject source =
                AssetDatabase.LoadAssetAtPath<GameObject>(SourceCharacterPath);
            if (source == null)
            {
                Debug.LogError($"Could not find the Synty source prefab at {SourceCharacterPath}.");
                return null;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                Debug.Log($"Colonist prefab already exists at {PrefabPath}; leaving it unchanged.");
                return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            }

            GameObject instance =
                (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.name = "Colonist_Synty_Male_01";
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;

            Animator animator = GetOrAdd<Animator>(instance);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            GetOrAdd<ColonistAgent>(instance);
            GetOrAdd<ColonistStatusComponent>(instance);

            NavMeshAgent navMeshAgent = GetOrAdd<NavMeshAgent>(instance);
            navMeshAgent.radius = 0.35f;
            navMeshAgent.height = 1.8f;
            navMeshAgent.baseOffset = 0f;
            navMeshAgent.speed = 2f;
            navMeshAgent.acceleration = 8f;
            navMeshAgent.angularSpeed = 360f;
            navMeshAgent.stoppingDistance = 0.05f;
            navMeshAgent.updatePosition = true;
            navMeshAgent.updateRotation = true;

            CapsuleCollider capsule = GetOrAdd<CapsuleCollider>(instance);
            capsule.radius = 0.35f;
            capsule.height = 1.8f;
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.direction = 1;

            GetOrAdd<ColonistMotor>(instance);
            GetOrAdd<ColonistAnimationDriver>(instance);
            GetOrAdd<ColonistActivityRunner>(instance);
            GetOrAdd<ColonistActor>(instance);

            GameObject prefab =
                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            UnityEngine.Object.DestroyImmediate(instance);

            Debug.Log($"Created Synty colonist prefab at {PrefabPath}.");
            return prefab;
        }

        private static AnimationClip CreateControllerClip(
            AnimatorController controller,
            string clipName)
        {
            AnimationClip clip = new AnimationClip
            {
                name = clipName,
                frameRate = 60f
            };
            AssetDatabase.AddObjectToAsset(clip, controller);
            return clip;
        }

        private static void AddFloatParameter(AnimatorController controller, string name)
        {
            for (int index = 0; index < controller.parameters.Length; index++)
            {
                if (controller.parameters[index].name == name)
                    return;
            }

            controller.AddParameter(name, AnimatorControllerParameterType.Float);
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void EnsureFolderPath(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }
                current = next;
            }
        }
    }
}
