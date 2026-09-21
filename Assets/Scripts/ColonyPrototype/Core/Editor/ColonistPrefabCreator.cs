using System;
using System.Linq;
using Colony.Interactions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using TMPro;

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
        private const string SleepyIconPrefabPath =
            "Assets/PolygonIcons/Prefabs/SM_Icon_Text_Z.prefab";
        private const string BobScenePath = "Assets/Bob.unity";
        private const string OverheadMaterialFolder =
            "Assets/Materials/ColonistOverhead";
        private const string TmpSettingsPath =
            "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string OverheadFontAssetPath =
            "Assets/Materials/ColonistOverhead/ColonistOverheadFont.asset";
        private const string NameMaterialPath =
            "Assets/Materials/ColonistOverhead/ColonistNameGlow.mat";
        private const string SleepyMaterialPath =
            "Assets/Materials/ColonistOverhead/ColonistSleepyGlow.mat";

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

        [MenuItem("Colony/People/Author Bob Overhead Display")]
        public static void AuthorBobOverheadDisplay()
        {
            EnsureFolderPath("Assets/Prefabs/Colonists");
            EnsureFolderPath(OverheadMaterialFolder);
            EnsureTextMeshProSettings();

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Could not find the colonist prefab at {PrefabPath}.");
                return;
            }

            GameObject prefabContents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                EnsureOverheadDisplay(prefabContents, "Colonist");
                PrefabUtility.SaveAsPrefabAsset(prefabContents, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            AuthorBobSceneDisplayName();
            Debug.Log($"Authored the colonist overhead display in {PrefabPath} and {BobScenePath}.");
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

            GetOrAdd<ColonistStatsComponent>(instance);
            GetOrAdd<ColonistAssignments>(instance);
            GetOrAdd<ColonistTargetResolver>(instance);

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
            GetOrAdd<ColonistBrain>(instance);
            EnsureOverheadDisplay(instance, "Colonist");

            GameObject prefab =
                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            UnityEngine.Object.DestroyImmediate(instance);

            Debug.Log($"Created Synty colonist prefab at {PrefabPath}.");
            return prefab;
        }

        private static void EnsureOverheadDisplay(GameObject colonist, string defaultDisplayName)
        {
            EnsureFolderPath(OverheadMaterialFolder);
            TMP_FontAsset overheadFont = EnsureOverheadFontAsset();
            if (overheadFont == null)
                return;

            Material nameMaterial = EnsureNameMaterial(overheadFont);
            Material sleepyMaterial = EnsureSleepyMaterial();

            Transform overheadTransform = GetOrCreateChild(
                colonist.transform,
                "OverheadDisplay");
            overheadTransform.localPosition = new Vector3(0f, 2.25f, 0f);
            overheadTransform.localRotation = Quaternion.identity;
            overheadTransform.localScale = Vector3.one;

            Transform existingNameTransform = overheadTransform.Find("Name");
            bool existingNameUsesTmp = existingNameTransform != null &&
                existingNameTransform.GetComponent<TextMeshPro>() != null;
            Transform nameTransform;
            if (existingNameUsesTmp)
            {
                nameTransform = existingNameTransform;
            }
            else
            {
                if (existingNameTransform != null)
                {
                    existingNameTransform.name = "LegacyName";
                    existingNameTransform.gameObject.SetActive(false);
                }

                GameObject nameObject = new GameObject("Name");
                nameObject.transform.SetParent(overheadTransform, false);
                nameTransform = nameObject.transform;
            }
            nameTransform.localPosition = Vector3.zero;
            nameTransform.localRotation = Quaternion.identity;
            nameTransform.localScale = Vector3.one;

            TextMeshPro nameText = GetOrAdd<TextMeshPro>(nameTransform.gameObject);
            nameText.font = overheadFont;
            nameText.fontSharedMaterial = nameMaterial;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.fontSize = 90f;
            nameText.fontStyle = FontStyles.Bold;
            nameText.textWrappingMode = TextWrappingModes.NoWrap;
            nameText.overflowMode = TextOverflowModes.Overflow;
            nameText.color = Color.white;
            nameText.text = defaultDisplayName;
            // Keep authoring independent of TMP's edit-time mesh rebuild. The
            // rebuild can invalidate prefab-content objects while Unity is
            // importing the asset. The legacy display was 0.2 world units;
            // this fixed scale is the requested one-third presentation size.
            nameTransform.localScale = Vector3.one * 0.04f;

            Transform iconStack = GetOrCreateChild(overheadTransform, "StatusIcons");
            iconStack.localPosition = new Vector3(0f, 0.58f, 0f);
            iconStack.localRotation = Quaternion.identity;
            iconStack.localScale = Vector3.one;

            Transform sleepyTransform = iconStack.Find("SleepyZ");
            if (sleepyTransform == null)
            {
                GameObject sleepyPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(SleepyIconPrefabPath);
                if (sleepyPrefab == null)
                {
                    Debug.LogError(
                        $"Could not find the sleepy icon prefab at {SleepyIconPrefabPath}.");
                    return;
                }

                GameObject sleepyInstance =
                    (GameObject)PrefabUtility.InstantiatePrefab(sleepyPrefab, iconStack);
                sleepyInstance.name = "SleepyZ";
                sleepyTransform = sleepyInstance.transform;
            }

            sleepyTransform.localPosition = Vector3.zero;
            sleepyTransform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            sleepyTransform.localScale = Vector3.one * 1.05f;
            sleepyTransform.gameObject.SetActive(false);
            MeshRenderer sleepyRenderer = sleepyTransform.GetComponent<MeshRenderer>();
            if (sleepyRenderer != null)
                sleepyRenderer.sharedMaterial = sleepyMaterial;

            ColonistOverheadDisplay previousDisplay =
                overheadTransform.GetComponent<ColonistOverheadDisplay>();
            if (previousDisplay != null)
                UnityEngine.Object.DestroyImmediate(previousDisplay);

            ColonistOverheadDisplay display =
                overheadTransform.gameObject.AddComponent<ColonistOverheadDisplay>();
            SerializedObject serializedDisplay = new SerializedObject(display);
            serializedDisplay.FindProperty("stats").objectReferenceValue =
                colonist.GetComponent<ColonistStatsComponent>();
            serializedDisplay.FindProperty("billboardRoot").objectReferenceValue =
                overheadTransform;
            serializedDisplay.FindProperty("nameText").objectReferenceValue =
                nameText;
            serializedDisplay.FindProperty("iconStack").objectReferenceValue =
                iconStack;
            serializedDisplay.FindProperty("sleepyIcon").objectReferenceValue =
                sleepyTransform.gameObject;
            serializedDisplay.FindProperty("displayName").stringValue = defaultDisplayName;
            serializedDisplay.FindProperty("iconSpacing").floatValue = 1.15f;
            serializedDisplay.FindProperty("billboardYawOffset").floatValue = 180f;
            serializedDisplay.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(display);
        }

        private static TMP_FontAsset EnsureOverheadFontAsset()
        {
            EnsureTextMeshProSettings();
            TMP_FontAsset fontAsset =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OverheadFontAssetPath);
            if (fontAsset == null)
            {
                fontAsset = TMP_FontAsset.CreateFontAsset("Arial", "Regular", 90);
                if (fontAsset == null)
                    fontAsset = TMP_FontAsset.CreateFontAsset("Arial", "Normal", 90);
                if (fontAsset == null)
                {
                    Debug.LogError("Could not create the colonist overhead TextMeshPro font asset.");
                    return null;
                }

                fontAsset.name = "ColonistOverheadFont";
                AssetDatabase.CreateAsset(fontAsset, OverheadFontAssetPath);

                if (fontAsset.atlasTextures != null)
                {
                    for (int index = 0; index < fontAsset.atlasTextures.Length; index++)
                    {
                        Texture2D atlas = fontAsset.atlasTextures[index];
                        if (atlas != null && !AssetDatabase.Contains(atlas))
                            AssetDatabase.AddObjectToAsset(atlas, fontAsset);
                    }
                }

                if (fontAsset.material != null && !AssetDatabase.Contains(fontAsset.material))
                    AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            fontAsset.TryAddCharacters(
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 _-", 
                out string missingCharacters);
            if (!string.IsNullOrEmpty(missingCharacters))
                Debug.LogWarning("The colonist overhead font asset could not add every requested glyph.");

            EditorUtility.SetDirty(fontAsset);
            return fontAsset;
        }

        private static void EnsureTextMeshProSettings()
        {
            TMP_Settings settings =
                AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings != null)
                return;

            // The TMP package ships the required settings, shaders, and default
            // font assets together. Import that supported package resource rather
            // than creating an empty settings asset that cannot create a font.
            TMP_PackageResourceImporter.ImportResources(
                importEssentials: true,
                importExamples: false,
                interactive: false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings == null)
                Debug.LogError(
                    "TMP Essential Resources could not be imported, so the colonist overhead display cannot be authored.");
        }

        private static Material EnsureNameMaterial(TMP_FontAsset fontAsset)
        {
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(NameMaterialPath);
            if (material == null)
            {
                material = new Material(fontAsset.material)
                {
                    name = "ColonistNameGlow"
                };
                AssetDatabase.CreateAsset(material, NameMaterialPath);
            }

            material.SetColor(
                ShaderUtilities.ID_FaceColor,
                new Color(0.05f, 1f, 0.12f, 1f));
            material.SetColor(
                ShaderUtilities.ID_OutlineColor,
                new Color(0f, 0.12f, 0.01f, 1f));
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
            material.EnableKeyword(ShaderUtilities.Keyword_Glow);
            material.SetColor(
                ShaderUtilities.ID_GlowColor,
                new Color(0.04f, 1f, 0.08f, 1f));
            material.SetFloat(ShaderUtilities.ID_GlowOffset, 0f);
            material.SetFloat(ShaderUtilities.ID_GlowOuter, 0.65f);
            material.SetFloat(ShaderUtilities.ID_GlowPower, 0.8f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material EnsureSleepyMaterial()
        {
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(SleepyMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("HDRP/Unlit");
                if (shader == null)
                    shader = Shader.Find("Unlit/Color");

                material = new Material(shader)
                {
                    name = "ColonistSleepyGlow"
                };
                AssetDatabase.CreateAsset(material, SleepyMaterialPath);
            }

            Color blue = new Color(0.02f, 0.2f, 1f, 1f);
            if (material.HasProperty("_UnlitColor"))
                material.SetColor("_UnlitColor", blue);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", blue);
            if (material.HasProperty("_EmissiveColor"))
                material.SetColor("_EmissiveColor", new Color(0.04f, 0.4f, 4f, 1f));
            if (material.HasProperty("_EmissiveIntensity"))
                material.SetFloat("_EmissiveIntensity", 4f);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AuthorBobSceneDisplayName()
        {
            Scene scene = EditorSceneManager.OpenScene(
                BobScenePath,
                OpenSceneMode.Single);
            ColonistOverheadDisplay[] displays =
                Resources.FindObjectsOfTypeAll<ColonistOverheadDisplay>();

            for (int index = 0; index < displays.Length; index++)
            {
                ColonistOverheadDisplay display = displays[index];
                if (display == null || display.gameObject.scene != scene)
                    continue;

                SerializedObject serializedDisplay = new SerializedObject(display);
                serializedDisplay.FindProperty("displayName").stringValue = "Bob";
                serializedDisplay.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(display);
                EditorSceneManager.SaveScene(scene);
                return;
            }

            Debug.LogError("Bob scene did not contain a ColonistOverheadDisplay after prefab authoring.");
        }

        private static Transform GetOrCreateChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
                return existing;

            GameObject child = new GameObject(childName);
            child.transform.SetParent(parent, false);
            return child.transform;
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
