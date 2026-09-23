using System;
using System.Collections.Generic;
using Colony.Interactions;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AsteroidColony.Editor
{
    /// <summary>
    /// One-shot/idempotent scene authoring for the Stack 4 Bob-and-Friends modular fixture.
    ///
    /// This deliberately leaves Assets/bobandfriends.unity untouched. The legacy semantic
    /// objects inside the cloned modular scene are used as migration sources the first time
    /// this command runs, then replaced by the real module prefabs.
    /// </summary>
    public static class BobAndFriendsModularSceneAuthoring
    {
        private const string ModularScenePath = "Assets/bobandfriends_modular.unity";
        private const string LegacyScenePath = "Assets/bobandfriends.unity";

        private const string CommandCenterPath = "Assets/Prefabs/CommandCenter.prefab";
        private const string CafeteriaPath = "Assets/Prefabs/Cafeteria.prefab";
        private const string DiscoPath = "Assets/Prefabs/Disco.prefab";
        private const string FarmPath = "Assets/Prefabs/Farm.prefab";

        private const string BobName = "Bob";
        private const string AliceName = "Alice";
        private const string CharlieName = "Charlie";
        private const string DanaName = "Dana";

        private const string Bed01 = "Bed01";
        private const string Bed02 = "Bed02";
        private const string Bed03 = "Bed03";
        private const string Bed04 = "Bed04";
        private const string Command01 = "Command01";
        private const string Serve01 = "Serve01";
        private const string Eat01 = "Eat01";
        private const string Farmwork01 = "Farmwork01";
        private const string Dance01 = "Dance01";
        private const string Dance02 = "Dance02";

        [MenuItem("Colony/Navigation/Build Bob And Friends Modular Scene")]
        public static void BuildModularScene()
        {
            Scene scene;
            if (!TryGetModularScene(out scene))
                return;

            try
            {
                LegacySources legacy = CaptureLegacySources(scene);
                ModuleSet modules = EnsureModules(scene);

                if (legacy.HasLegacyFixture)
                    MigrateLegacySemantics(scene, legacy, modules);
                else
                    EnsureAlreadyMigratedSemantics(scene, modules);

                ArrangeModules(modules);
                RemoveNonModuleNavMeshSurfaces(scene, modules);

                if (legacy.HasLegacyFixture)
                    RemoveLegacyFixture(legacy);

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("Unity failed to save " + ModularScenePath + ".");

                int errors = ValidateScene(scene, true);
                if (errors == 0)
                {
                    Debug.Log(
                        "Bob-and-Friends modular scene authored successfully. " +
                        "Open Scene view/NavMesh visualization, then enter Play Mode for runtime link and traversal acceptance.");
                }
                else
                {
                    Debug.LogError(
                        "Bob-and-Friends modular scene was saved, but validation found " +
                        errors + " error(s). Fix them before Play Mode acceptance.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem("Colony/Navigation/Validate Bob And Friends Modular Scene")]
        public static void ValidateModularScene()
        {
            Scene scene;
            if (!TryGetModularScene(out scene))
                return;

            int errors = ValidateScene(scene, true);
            if (errors == 0)
            {
                Debug.Log(
                    "Bob-and-Friends modular scene validation passed. " +
                    "This validates authoring only; Play Mode traversal remains a human acceptance step.");
            }
            else
            {
                Debug.LogError(
                    "Bob-and-Friends modular scene validation found " +
                    errors + " error(s). See the preceding messages.");
            }
        }

        private static bool TryGetModularScene(out Scene scene)
        {
            scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("No loaded active scene. Open " + ModularScenePath + " first.");
                return false;
            }

            if (string.Equals(scene.path, LegacyScenePath, StringComparison.Ordinal))
            {
                Debug.LogError(
                    "Refusing to author the legacy Bob-and-Friends scene. Open " +
                    ModularScenePath + " instead.");
                return false;
            }

            if (!string.Equals(scene.path, ModularScenePath, StringComparison.Ordinal))
            {
                Debug.LogError(
                    "This command only operates on " + ModularScenePath +
                    ". Current active scene: " + scene.path);
                return false;
            }

            return true;
        }

        private static LegacySources CaptureLegacySources(Scene scene)
        {
            GameObject commandPod = FindRoot(scene, "CommandPod");
            GameObject commandModule = FindRoot(scene, "CommandModule");

            if (commandPod == null)
            {
                if (commandModule != null)
                {
                    throw new InvalidOperationException(
                        "Found legacy CommandModule but not legacy CommandPod. " +
                        "The modular scene appears partially migrated; restore/reconcile it before rerunning the builder.");
                }

                return new LegacySources();
            }

            GameObject cafeteria = FindDescendant(commandPod.transform, "Cafeteria");
            GameObject farm = FindDescendant(commandPod.transform, "Farm");
            GameObject recreation = FindDescendant(commandPod.transform, "Recreation");

            if (cafeteria == null || farm == null || recreation == null)
            {
                throw new InvalidOperationException(
                    "Legacy CommandPod is present but one or more expected child facilities are missing: " +
                    "Cafeteria, Farm, Recreation.");
            }

            LegacySources result = new LegacySources
            {
                CommandPod = commandPod,
                CommandModule = commandModule,
                CommandFacility = RequireComponent<InteractableFacility>(commandPod, "legacy CommandPod"),
                CommandWorkplace = RequireComponent<WorkplaceComponent>(commandPod, "legacy CommandPod"),
                CafeteriaFacility = RequireComponent<InteractableFacility>(cafeteria, "legacy Cafeteria"),
                CafeteriaWorkplace = RequireComponent<WorkplaceComponent>(cafeteria, "legacy Cafeteria"),
                FoodService = RequireComponent<FoodServiceComponent>(cafeteria, "legacy Cafeteria"),
                FarmFacility = RequireComponent<InteractableFacility>(farm, "legacy Farm"),
                FarmWorkplace = RequireComponent<WorkplaceComponent>(farm, "legacy Farm"),
                FarmPerformance = RequireComponent<FacilityPerformanceComponent>(farm, "legacy Farm"),
                FarmWorkforcePerformance = RequireComponent<WorkforcePerformanceProvider>(farm, "legacy Farm"),
                FarmConverter = RequireComponent<ResourceConverterComponent>(farm, "legacy Farm"),
                RecreationFacility = RequireComponent<InteractableFacility>(recreation, "legacy Recreation"),
                OffDuty = RequireComponent<OffDutyComponent>(recreation, "legacy Recreation")
            };

            return result;
        }

        private static ModuleSet EnsureModules(Scene scene)
        {
            return new ModuleSet
            {
                CommandCenter = EnsureModule(scene, CommandCenterPath),
                Cafeteria = EnsureModule(scene, CafeteriaPath),
                Disco = EnsureModule(scene, DiscoPath),
                Farm = EnsureModule(scene, FarmPath)
            };
        }

        private static GameObject EnsureModule(Scene scene, string prefabPath)
        {
            List<GameObject> existing = FindPrefabRoots(scene, prefabPath);
            if (existing.Count > 1)
            {
                throw new InvalidOperationException(
                    "Found " + existing.Count + " instances of " + prefabPath +
                    " in " + ModularScenePath + ". Resolve duplicates before rerunning.");
            }

            if (existing.Count == 1)
                return existing[0];

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                throw new InvalidOperationException("Could not load module prefab " + prefabPath + ".");

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
                throw new InvalidOperationException("Could not instantiate module prefab " + prefabPath + ".");

            Undo.RegisterCreatedObjectUndo(instance, "Create modular Bob-and-Friends module");
            instance.name = prefab.name;
            return instance;
        }

        private static void MigrateLegacySemantics(
            Scene scene,
            LegacySources legacy,
            ModuleSet modules)
        {
            WorkforceManager workforce = RequireSceneComponent<WorkforceManager>(scene);
            FoodManager foodManager = RequireSceneComponent<FoodManager>(scene);
            OffDutyManager offDutyManager = RequireSceneComponent<OffDutyManager>(scene);
            InventoryComponent stationFoodStore = RequireNamedSceneComponent<InventoryComponent>(
                scene,
                "Station Food Store");

            ColonistIdentity bob = RequireColonist(scene, BobName);
            ColonistIdentity alice = RequireColonist(scene, AliceName);
            ColonistIdentity charlie = RequireColonist(scene, CharlieName);
            ColonistIdentity dana = RequireColonist(scene, DanaName);

            JobRoleDefinition farmerRole = RequireSingleRole(legacy.FarmWorkplace, "legacy Farm");
            JobRoleDefinition cafeteriaRole = RequireSingleRole(legacy.CafeteriaWorkplace, "legacy Cafeteria");
            JobRoleDefinition commandRole = RequireSingleRole(legacy.CommandWorkplace, "legacy CommandPod");

            int farmerCapacity = GetSingleRoleCapacity(legacy.FarmWorkplace);
            int cafeteriaCapacity = GetSingleRoleCapacity(legacy.CafeteriaWorkplace);
            int commandCapacity = GetSingleRoleCapacity(legacy.CommandWorkplace);

            InteractableFacility commandFacility =
                RequireComponent<InteractableFacility>(modules.CommandCenter, "CommandCenter prefab");
            InteractableFacility cafeteriaFacility =
                RequireComponent<InteractableFacility>(modules.Cafeteria, "Cafeteria prefab");
            InteractableFacility discoFacility =
                RequireComponent<InteractableFacility>(modules.Disco, "Disco prefab");
            InteractableFacility farmFacility =
                RequireComponent<InteractableFacility>(modules.Farm, "Farm prefab");

            RequireActivity(commandFacility, Bed01);
            RequireActivity(commandFacility, Bed02);
            RequireActivity(commandFacility, Bed03);
            RequireActivity(commandFacility, Bed04);
            RequireActivity(commandFacility, Command01);
            RequireActivity(cafeteriaFacility, Serve01);
            RequireActivity(cafeteriaFacility, Eat01);
            RequireActivity(discoFacility, Dance01);
            RequireActivity(discoFacility, Dance02);
            RequireActivity(farmFacility, Farmwork01);

            WorkplaceComponent commandWorkplace =
                EnsureComponent<WorkplaceComponent>(modules.CommandCenter);
            WorkplaceComponent cafeteriaWorkplace =
                EnsureComponent<WorkplaceComponent>(modules.Cafeteria);
            WorkplaceComponent farmWorkplace =
                EnsureComponent<WorkplaceComponent>(modules.Farm);

            ConfigureSingleRoleWorkplace(
                commandWorkplace,
                commandFacility,
                commandRole,
                Command01,
                commandCapacity);
            ConfigureSingleRoleWorkplace(
                cafeteriaWorkplace,
                cafeteriaFacility,
                cafeteriaRole,
                Serve01,
                cafeteriaCapacity);
            ConfigureSingleRoleWorkplace(
                farmWorkplace,
                farmFacility,
                farmerRole,
                Farmwork01,
                farmerCapacity);

            FoodServiceComponent foodService =
                EnsureComponent<FoodServiceComponent>(modules.Cafeteria);
            ConfigureFoodService(
                foodService,
                cafeteriaFacility,
                cafeteriaWorkplace,
                cafeteriaRole,
                stationFoodStore,
                legacy.FoodService);

            OffDutyComponent offDuty =
                EnsureComponent<OffDutyComponent>(modules.Disco);
            ConfigureOffDuty(offDuty, discoFacility, legacy.OffDuty);

            FacilityPerformanceComponent farmPerformance =
                EnsureComponent<FacilityPerformanceComponent>(modules.Farm);
            WorkforcePerformanceProvider farmWorkforcePerformance =
                EnsureComponent<WorkforcePerformanceProvider>(modules.Farm);
            farmWorkforcePerformance.requiredWorkplace = farmWorkplace;
            farmWorkforcePerformance.requiredRole = farmerRole;
            farmWorkforcePerformance.minimumActiveWorkers =
                Mathf.Max(1, legacy.FarmWorkforcePerformance.minimumActiveWorkers);
            EditorUtility.SetDirty(farmWorkforcePerformance);

            ResourceConverterComponent farmConverter =
                EnsureComponent<ResourceConverterComponent>(modules.Farm);
            ConfigureFarmConverter(
                farmConverter,
                farmPerformance,
                stationFoodStore,
                legacy.FarmConverter);

            ConfigureSleepTarget(bob, commandFacility, Bed01);
            ConfigureSleepTarget(alice, commandFacility, Bed02);
            ConfigureSleepTarget(charlie, commandFacility, Bed03);
            ConfigureSleepTarget(dana, commandFacility, Bed04);

            ReassignWorkforce(
                workforce,
                bob,
                farmWorkplace,
                farmerRole,
                "Bob/Farmer");
            ReassignWorkforce(
                workforce,
                alice,
                cafeteriaWorkplace,
                cafeteriaRole,
                "Alice/Cafeteria");
            ReassignWorkforce(
                workforce,
                charlie,
                commandWorkplace,
                commandRole,
                "Charlie/Command");

            SetSingleManagerReference(foodManager, "services", foodService);
            SetSingleManagerReference(offDutyManager, "providers", offDuty);

            foodService.RefreshStaticBindingMetadata();
            offDuty.RefreshStaticBindingMetadata();

            EditorUtility.SetDirty(farmPerformance);
            EditorUtility.SetDirty(farmConverter);
            EditorUtility.SetDirty(workforce);
        }

        private static void EnsureAlreadyMigratedSemantics(Scene scene, ModuleSet modules)
        {
            if (modules.CommandCenter.GetComponent<WorkplaceComponent>() == null ||
                modules.Cafeteria.GetComponent<WorkplaceComponent>() == null ||
                modules.Cafeteria.GetComponent<FoodServiceComponent>() == null ||
                modules.Disco.GetComponent<OffDutyComponent>() == null ||
                modules.Farm.GetComponent<WorkplaceComponent>() == null ||
                modules.Farm.GetComponent<FacilityPerformanceComponent>() == null ||
                modules.Farm.GetComponent<WorkforcePerformanceProvider>() == null ||
                modules.Farm.GetComponent<ResourceConverterComponent>() == null)
            {
                throw new InvalidOperationException(
                    "Legacy migration sources are gone, but modular semantic wiring is incomplete. " +
                    "Restore/reconcile " + ModularScenePath + " before rerunning the builder.");
            }
        }

        private static void ConfigureSingleRoleWorkplace(
            WorkplaceComponent workplace,
            InteractableFacility facility,
            JobRoleDefinition role,
            string activityId,
            int capacity)
        {
            SerializedObject serialized = new SerializedObject(workplace);
            serialized.Update();

            serialized.FindProperty("facility").objectReferenceValue = facility;
            SerializedProperty roles = serialized.FindProperty("roles");
            roles.arraySize = 1;

            SerializedProperty binding = roles.GetArrayElementAtIndex(0);
            binding.FindPropertyRelative("role").objectReferenceValue = role;
            binding.FindPropertyRelative("activityId").stringValue = activityId;
            binding.FindPropertyRelative("maximumConcurrentScheduledWorkers").intValue =
                Mathf.Max(1, capacity);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(workplace);
        }

        private static void ConfigureFoodService(
            FoodServiceComponent destination,
            InteractableFacility facility,
            WorkplaceComponent requiredWorkplace,
            JobRoleDefinition requiredRole,
            InventoryComponent stationFoodStore,
            FoodServiceComponent source)
        {
            if (source == null)
                throw new InvalidOperationException("Legacy Cafeteria has no FoodServiceComponent.");

            SerializedObject serialized = new SerializedObject(destination);
            serialized.Update();

            serialized.FindProperty("facility").objectReferenceValue = facility;
            serialized.FindProperty("eatActivityId").stringValue = Eat01;
            serialized.FindProperty("requiresStaff").boolValue = source.RequiresStaff;
            serialized.FindProperty("requiredWorkplace").objectReferenceValue = requiredWorkplace;
            serialized.FindProperty("requiredRole").objectReferenceValue = requiredRole;
            serialized.FindProperty("minimumActiveWorkers").intValue = source.MinimumActiveWorkers;
            serialized.FindProperty("selfServicePolicy").enumValueIndex =
                (int)source.SelfServicePolicy;
            serialized.FindProperty("inventoryAccountingEnabled").boolValue =
                source.InventoryAccountingEnabled;
            serialized.FindProperty("foodInventory").objectReferenceValue = stationFoodStore;
            serialized.FindProperty("foodResource").objectReferenceValue = source.FoodResource;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            destination.RefreshStaticBindingMetadata();
            EditorUtility.SetDirty(destination);
        }

        private static void ConfigureOffDuty(
            OffDutyComponent destination,
            InteractableFacility facility,
            OffDutyComponent source)
        {
            if (source == null || source.Activities.Count < 2)
            {
                throw new InvalidOperationException(
                    "Legacy Recreation must expose both stimulation and relaxation activities.");
            }

            OffDutyActivityBinding stimulation = FindOffDutyByDrive(source, OffDutyDrive.Stimulation);
            OffDutyActivityBinding relaxation = FindOffDutyByDrive(source, OffDutyDrive.Relaxation);
            if (stimulation == null || relaxation == null)
            {
                throw new InvalidOperationException(
                    "Could not uniquely resolve legacy stimulation and relaxation activity bindings.");
            }

            if (stimulation.RequiresStaff || relaxation.RequiresStaff)
            {
                throw new InvalidOperationException(
                    "The legacy Recreation unexpectedly requires staff. " +
                    "Stack 4 migration has no authored replacement staffing mapping for Disco.");
            }

            SerializedObject serialized = new SerializedObject(destination);
            serialized.Update();
            serialized.FindProperty("facility").objectReferenceValue = facility;

            SerializedProperty activities = serialized.FindProperty("activities");
            activities.arraySize = 2;
            ConfigureOffDutyBinding(
                activities.GetArrayElementAtIndex(0),
                stimulation,
                Dance01);
            ConfigureOffDutyBinding(
                activities.GetArrayElementAtIndex(1),
                relaxation,
                Dance02);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            destination.RefreshStaticBindingMetadata();
            EditorUtility.SetDirty(destination);
        }

        private static void ConfigureOffDutyBinding(
            SerializedProperty destination,
            OffDutyActivityBinding source,
            string activityId)
        {
            destination.FindPropertyRelative("activityId").stringValue = activityId;
            destination.FindPropertyRelative("plannedDurationGameHours").floatValue =
                source.PlannedDurationGameHours;
            destination.FindPropertyRelative("enabled").boolValue = source.Enabled;
            destination.FindPropertyRelative("requiresStaff").boolValue = false;
            destination.FindPropertyRelative("requiredWorkplace").objectReferenceValue = null;
            destination.FindPropertyRelative("requiredRole").objectReferenceValue = null;
            destination.FindPropertyRelative("minimumActiveWorkers").intValue =
                Mathf.Max(1, source.MinimumActiveWorkers);
            destination.FindPropertyRelative("cooldownGameHours").floatValue =
                source.CooldownGameHours;
            destination.FindPropertyRelative("cooldownKey").stringValue =
                source.CooldownKey;
            destination.FindPropertyRelative("stimulationRecoveryPerGameHour").floatValue =
                source.StimulationRecoveryPerGameHour;
            destination.FindPropertyRelative("relaxationRecoveryPerGameHour").floatValue =
                source.RelaxationRecoveryPerGameHour;
        }

        private static OffDutyActivityBinding FindOffDutyByDrive(
            OffDutyComponent component,
            OffDutyDrive drive)
        {
            OffDutyActivityBinding match = null;
            for (int i = 0; i < component.Activities.Count; i++)
            {
                OffDutyActivityBinding candidate = component.Activities[i];
                if (candidate == null || !candidate.Satisfies(drive))
                    continue;

                if (match != null)
                    return null;

                match = candidate;
            }

            return match;
        }

        private static void ConfigureFarmConverter(
            ResourceConverterComponent destination,
            FacilityPerformanceComponent performance,
            InventoryComponent stationFoodStore,
            ResourceConverterComponent source)
        {
            if (source == null)
                throw new InvalidOperationException("Legacy Farm has no ResourceConverterComponent.");

            destination.inventory = stationFoodStore;
            destination.performance = performance;
            destination.productionRateEffect = source.productionRateEffect;
            destination.availableRecipes = source.availableRecipes == null
                ? new List<RecipeDefinition>()
                : new List<RecipeDefinition>(source.availableRecipes);
            destination.activeRecipe = source.activeRecipe;
            destination.operationalEnabled = source.operationalEnabled;
            EditorUtility.SetDirty(destination);
        }

        private static void ConfigureSleepTarget(
            ColonistIdentity colonist,
            InteractableFacility facility,
            string activityId)
        {
            ColonistAssignments assignments =
                colonist.GetComponent<ColonistAssignments>();
            if (assignments == null)
            {
                throw new InvalidOperationException(
                    colonist.DisplayName + " has no ColonistAssignments component.");
            }

            SerializedObject serialized = new SerializedObject(assignments);
            serialized.Update();
            SerializedProperty target = serialized.FindProperty("sleepTarget");
            target.FindPropertyRelative("facility").objectReferenceValue = facility;
            target.FindPropertyRelative("activityId").stringValue = activityId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(assignments);
        }

        private static void ReassignWorkforce(
            WorkforceManager manager,
            ColonistIdentity colonist,
            WorkplaceComponent workplace,
            JobRoleDefinition role,
            string description)
        {
            WorkAssignment current = null;
            for (int i = 0; i < manager.Assignments.Count; i++)
            {
                WorkAssignment candidate = manager.Assignments[i];
                if (candidate != null && candidate.Colonist == colonist)
                {
                    current = candidate;
                    break;
                }
            }

            if (current == null || current.Shift == null || !current.Shift.IsConfigured)
            {
                throw new InvalidOperationException(
                    "Could not preserve the current shift for " + description + ".");
            }

            DailyShiftWindow shift =
                new DailyShiftWindow(current.Shift.StartHour, current.Shift.EndHour);
            WorkAssignmentResult result = manager.Assign(colonist, workplace, role, shift);
            if (result != WorkAssignmentResult.Applied)
            {
                throw new InvalidOperationException(
                    "Failed to migrate " + description + " workforce assignment: " + result + ".");
            }
        }

        private static void SetSingleManagerReference(
            UnityEngine.Object manager,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(manager);
            serialized.Update();
            SerializedProperty list = serialized.FindProperty(propertyName);
            if (list == null || !list.isArray)
            {
                throw new InvalidOperationException(
                    manager.name + " has no serialized list named " + propertyName + ".");
            }

            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
        }

        private static void ArrangeModules(ModuleSet modules)
        {
            SetRootPose(modules.CommandCenter, Vector3.zero, Quaternion.identity);

            ModuleConnectionPoint[] commandPoints = GetSortedPoints(modules.CommandCenter, 2);
            ModuleConnectionPoint[] cafeteriaPoints = GetSortedPoints(modules.Cafeteria, 2);
            ModuleConnectionPoint[] discoPoints = GetSortedPoints(modules.Disco, 1);
            ModuleConnectionPoint[] farmPoints = GetSortedPoints(modules.Farm, 2);

            SnapModule(modules.Disco, discoPoints[0], commandPoints[0]);
            SnapModule(modules.Cafeteria, cafeteriaPoints[0], commandPoints[1]);

            // Moving Cafeteria changed its second point's world pose; use the updated transform.
            cafeteriaPoints = GetSortedPoints(modules.Cafeteria, 2);
            SnapModule(modules.Farm, farmPoints[0], cafeteriaPoints[1]);
        }

        private static void SetRootPose(
            GameObject module,
            Vector3 position,
            Quaternion rotation)
        {
            Undo.RecordObject(module.transform, "Arrange modular Bob-and-Friends modules");
            module.transform.SetPositionAndRotation(position, rotation);
            EditorUtility.SetDirty(module.transform);
        }

        private static void SnapModule(
            GameObject movingModule,
            ModuleConnectionPoint movingPoint,
            ModuleConnectionPoint fixedPoint)
        {
            Undo.RecordObject(movingModule.transform, "Snap modular Bob-and-Friends module");

            Quaternion desiredPointRotation =
                Quaternion.LookRotation(-fixedPoint.transform.forward, fixedPoint.transform.up);
            Quaternion rotationDelta =
                desiredPointRotation * Quaternion.Inverse(movingPoint.transform.rotation);

            movingModule.transform.rotation =
                rotationDelta * movingModule.transform.rotation;

            Vector3 offset =
                fixedPoint.transform.position - movingPoint.transform.position;
            movingModule.transform.position += offset;
            EditorUtility.SetDirty(movingModule.transform);
        }

        private static void RemoveNonModuleNavMeshSurfaces(
            Scene scene,
            ModuleSet modules)
        {
            HashSet<NavMeshSurface> allowed = new HashSet<NavMeshSurface>
            {
                RequireRootSurface(modules.CommandCenter),
                RequireRootSurface(modules.Cafeteria),
                RequireRootSurface(modules.Disco),
                RequireRootSurface(modules.Farm)
            };

            NavMeshSurface[] surfaces = FindSceneComponents<NavMeshSurface>(scene);
            for (int i = 0; i < surfaces.Length; i++)
            {
                NavMeshSurface surface = surfaces[i];
                if (surface == null || allowed.Contains(surface))
                    continue;

                // During first migration this removes the legacy scene-wide surface.
                // On reruns, any unexpected extra surface is not allowed to mask local-nav acceptance.
                Undo.DestroyObjectImmediate(surface);
            }
        }

        private static void RemoveLegacyFixture(LegacySources legacy)
        {
            if (legacy.CommandPod != null)
                Undo.DestroyObjectImmediate(legacy.CommandPod);
            if (legacy.CommandModule != null)
                Undo.DestroyObjectImmediate(legacy.CommandModule);
        }

        private static int ValidateScene(Scene scene, bool logSuccesses)
        {
            int errors = 0;

            ModuleSet modules = new ModuleSet
            {
                CommandCenter = RequireSinglePrefabRootForValidation(
                    scene, CommandCenterPath, ref errors),
                Cafeteria = RequireSinglePrefabRootForValidation(
                    scene, CafeteriaPath, ref errors),
                Disco = RequireSinglePrefabRootForValidation(
                    scene, DiscoPath, ref errors),
                Farm = RequireSinglePrefabRootForValidation(
                    scene, FarmPath, ref errors)
            };

            if (FindRoot(scene, "CommandPod") != null ||
                FindRoot(scene, "CommandModule") != null)
            {
                Debug.LogError(
                    "Legacy CommandPod/CommandModule still exists in the modular scene.");
                errors++;
            }

            if (modules.CommandCenter == null ||
                modules.Cafeteria == null ||
                modules.Disco == null ||
                modules.Farm == null)
            {
                return errors;
            }

            errors += ValidateModuleSurface(modules.CommandCenter, 2);
            errors += ValidateModuleSurface(modules.Cafeteria, 2);
            errors += ValidateModuleSurface(modules.Disco, 1);
            errors += ValidateModuleSurface(modules.Farm, 2);

            NavMeshSurface[] allSurfaces = FindSceneComponents<NavMeshSurface>(scene);
            if (allSurfaces.Length != 4)
            {
                Debug.LogError(
                    "Expected exactly four active scene NavMeshSurfaces (one per module), found " +
                    allSurfaces.Length + ".");
                errors++;
            }

            errors += ValidateSemanticWiring(scene, modules);
            errors += ValidateConnectionLayout(modules);

            if (errors == 0 && logSuccesses)
            {
                Debug.Log(
                    "PASS modular scene authoring: four prefab modules, four local surfaces, " +
                    "seven connection points, three unambiguous candidate pairs, one spare Farm point, " +
                    "and Stack 3 semantic references are wired to the new facilities.");
            }

            return errors;
        }

        private static int ValidateSemanticWiring(Scene scene, ModuleSet modules)
        {
            int errors = 0;

            InteractableFacility commandFacility =
                modules.CommandCenter.GetComponent<InteractableFacility>();
            InteractableFacility cafeteriaFacility =
                modules.Cafeteria.GetComponent<InteractableFacility>();
            InteractableFacility discoFacility =
                modules.Disco.GetComponent<InteractableFacility>();
            InteractableFacility farmFacility =
                modules.Farm.GetComponent<InteractableFacility>();

            WorkplaceComponent commandWorkplace =
                modules.CommandCenter.GetComponent<WorkplaceComponent>();
            WorkplaceComponent cafeteriaWorkplace =
                modules.Cafeteria.GetComponent<WorkplaceComponent>();
            WorkplaceComponent farmWorkplace =
                modules.Farm.GetComponent<WorkplaceComponent>();
            FoodServiceComponent foodService =
                modules.Cafeteria.GetComponent<FoodServiceComponent>();
            OffDutyComponent offDuty =
                modules.Disco.GetComponent<OffDutyComponent>();
            WorkforcePerformanceProvider workforcePerformance =
                modules.Farm.GetComponent<WorkforcePerformanceProvider>();
            ResourceConverterComponent converter =
                modules.Farm.GetComponent<ResourceConverterComponent>();

            errors += RequireActivityForValidation(commandFacility, Bed01);
            errors += RequireActivityForValidation(commandFacility, Bed02);
            errors += RequireActivityForValidation(commandFacility, Bed03);
            errors += RequireActivityForValidation(commandFacility, Bed04);
            errors += RequireActivityForValidation(commandFacility, Command01);
            errors += RequireActivityForValidation(cafeteriaFacility, Serve01);
            errors += RequireActivityForValidation(cafeteriaFacility, Eat01);
            errors += RequireActivityForValidation(discoFacility, Dance01);
            errors += RequireActivityForValidation(discoFacility, Dance02);
            errors += RequireActivityForValidation(farmFacility, Farmwork01);

            if (commandWorkplace == null ||
                !HasSingleRoleActivity(commandWorkplace, Command01))
            {
                Debug.LogError("CommandCenter workplace is not bound to Command01.");
                errors++;
            }

            if (cafeteriaWorkplace == null ||
                !HasSingleRoleActivity(cafeteriaWorkplace, Serve01))
            {
                Debug.LogError("Cafeteria workplace is not bound to Serve01.");
                errors++;
            }

            if (farmWorkplace == null ||
                !HasSingleRoleActivity(farmWorkplace, Farmwork01))
            {
                Debug.LogError("Farm workplace is not bound to Farmwork01.");
                errors++;
            }

            if (foodService == null ||
                foodService.Facility != cafeteriaFacility ||
                !string.Equals(foodService.EatActivityId, Eat01, StringComparison.Ordinal) ||
                foodService.RequiredWorkplace != cafeteriaWorkplace ||
                !foodService.InventoryAccountingEnabled ||
                foodService.FoodInventory == null ||
                foodService.FoodResource == null)
            {
                Debug.LogError("Cafeteria FoodService is not fully wired to Eat01 / Stack 3 inventory.");
                errors++;
            }

            if (offDuty == null ||
                !HasOffDutyActivity(offDuty, Dance01, OffDutyDrive.Stimulation) ||
                !HasOffDutyActivity(offDuty, Dance02, OffDutyDrive.Relaxation))
            {
                Debug.LogError("Disco OffDuty wiring does not provide Dance01 stimulation and Dance02 relaxation.");
                errors++;
            }

            if (workforcePerformance == null ||
                workforcePerformance.requiredWorkplace != farmWorkplace ||
                workforcePerformance.requiredRole == null)
            {
                Debug.LogError("Farm WorkforcePerformanceProvider is not bound to the modular Farm workplace.");
                errors++;
            }

            if (converter == null ||
                converter.inventory == null ||
                converter.performance == null ||
                converter.activeRecipe == null)
            {
                Debug.LogError("Farm ResourceConverter is missing Stack 3 inventory/performance/recipe wiring.");
                errors++;
            }

            errors += ValidateSleepTarget(scene, BobName, commandFacility, Bed01);
            errors += ValidateSleepTarget(scene, AliceName, commandFacility, Bed02);
            errors += ValidateSleepTarget(scene, CharlieName, commandFacility, Bed03);
            errors += ValidateSleepTarget(scene, DanaName, commandFacility, Bed04);

            WorkforceManager workforce = FindSceneComponent<WorkforceManager>(scene);
            if (workforce == null)
            {
                Debug.LogError("No WorkforceManager in modular scene.");
                errors++;
            }
            else
            {
                errors += ValidateWorkAssignment(workforce, BobName, farmWorkplace);
                errors += ValidateWorkAssignment(workforce, AliceName, cafeteriaWorkplace);
                errors += ValidateWorkAssignment(workforce, CharlieName, commandWorkplace);
            }

            return errors;
        }

        private static int ValidateConnectionLayout(ModuleSet modules)
        {
            ModuleConnectionPoint[] all =
            {
                GetSortedPoints(modules.Disco, 1)[0],
                GetSortedPoints(modules.CommandCenter, 2)[0],
                GetSortedPoints(modules.CommandCenter, 2)[1],
                GetSortedPoints(modules.Cafeteria, 2)[0],
                GetSortedPoints(modules.Cafeteria, 2)[1],
                GetSortedPoints(modules.Farm, 2)[0],
                GetSortedPoints(modules.Farm, 2)[1]
            };

            Dictionary<ModuleConnectionPoint, int> candidates =
                new Dictionary<ModuleConnectionPoint, int>();
            for (int i = 0; i < all.Length; i++)
                candidates[all[i]] = 0;

            int pairCount = 0;
            HashSet<string> modulePairs = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < all.Length; i++)
            {
                for (int j = i + 1; j < all.Length; j++)
                {
                    ModuleConnectionPoint a = all[i];
                    ModuleConnectionPoint b = all[j];
                    if (a.OwnerSurface == null ||
                        b.OwnerSurface == null ||
                        a.OwnerSurface == b.OwnerSurface)
                    {
                        continue;
                    }

                    float maxDistance = Mathf.Min(
                        a.MaxPartnerDistance,
                        b.MaxPartnerDistance);
                    if ((a.transform.position - b.transform.position).sqrMagnitude >
                        maxDistance * maxDistance)
                    {
                        continue;
                    }

                    pairCount++;
                    candidates[a]++;
                    candidates[b]++;
                    modulePairs.Add(ModulePairKey(a, b));
                }
            }

            int errors = 0;
            if (pairCount != 3)
            {
                Debug.LogError(
                    "Expected exactly three nearby cross-module connection pairs; found " +
                    pairCount + ".");
                errors++;
            }

            foreach (KeyValuePair<ModuleConnectionPoint, int> pair in candidates)
            {
                if (pair.Value > 1)
                {
                    Debug.LogError(
                        pair.Key.GetStableHierarchyKey() +
                        " has " + pair.Value +
                        " nearby candidates; intended Stack 4 layout must be unambiguous.",
                        pair.Key);
                    errors++;
                }
            }

            int unmatched = 0;
            foreach (KeyValuePair<ModuleConnectionPoint, int> pair in candidates)
                if (pair.Value == 0)
                    unmatched++;

            if (unmatched != 1)
            {
                Debug.LogError(
                    "Expected exactly one intentionally unmatched connection point; found " +
                    unmatched + ".");
                errors++;
            }

            if (!modulePairs.Contains("Cafeteria|CommandCenter") ||
                !modulePairs.Contains("Cafeteria|Farm") ||
                !modulePairs.Contains("CommandCenter|Disco"))
            {
                Debug.LogError(
                    "Connection candidates do not form Disco <-> CommandCenter <-> Cafeteria <-> Farm.");
                errors++;
            }

            ModuleConnectionPoint[] farmPoints = GetSortedPoints(modules.Farm, 2);
            int farmUnmatched = 0;
            for (int i = 0; i < farmPoints.Length; i++)
                if (candidates[farmPoints[i]] == 0)
                    farmUnmatched++;

            if (farmUnmatched != 1)
            {
                Debug.LogError(
                    "Expected the spare unmatched point to belong to Farm.");
                errors++;
            }

            return errors;
        }

        private static string ModulePairKey(
            ModuleConnectionPoint a,
            ModuleConnectionPoint b)
        {
            string aName = a.OwnerSurface.transform.root.name;
            string bName = b.OwnerSurface.transform.root.name;
            return string.CompareOrdinal(aName, bName) <= 0
                ? aName + "|" + bName
                : bName + "|" + aName;
        }

        private static int ValidateModuleSurface(GameObject module, int expectedPointCount)
        {
            int errors = 0;
            NavMeshSurface rootSurface = module.GetComponent<NavMeshSurface>();
            NavMeshSurface[] allSurfaces = module.GetComponentsInChildren<NavMeshSurface>(true);

            if (rootSurface == null)
            {
                Debug.LogError(module.name + " has no root NavMeshSurface.", module);
                errors++;
            }
            else if (rootSurface.navMeshData == null)
            {
                Debug.LogError(module.name + " root NavMeshSurface has no persisted NavMeshData.", rootSurface);
                errors++;
            }

            if (allSurfaces.Length != 1)
            {
                Debug.LogError(
                    module.name + " should own exactly one NavMeshSurface; found " +
                    allSurfaces.Length + ".", module);
                errors++;
            }

            ModuleConnectionPoint[] points =
                module.GetComponentsInChildren<ModuleConnectionPoint>(true);
            if (points.Length != expectedPointCount)
            {
                Debug.LogError(
                    module.name + " should have " + expectedPointCount +
                    " ModuleConnectionPoint(s); found " + points.Length + ".", module);
                errors++;
            }

            for (int i = 0; i < points.Length; i++)
            {
                if (points[i].OwnerSurface != rootSurface ||
                    points[i].WalkAnchor == null)
                {
                    Debug.LogError(
                        module.name + " contains a connection point with invalid surface/WalkAnchor references.",
                        points[i]);
                    errors++;
                }
            }

            return errors;
        }

        private static int ValidateSleepTarget(
            Scene scene,
            string colonistName,
            InteractableFacility facility,
            string activityId)
        {
            ColonistIdentity colonist = FindColonist(scene, colonistName);
            if (colonist == null)
            {
                Debug.LogError("Missing colonist " + colonistName + ".");
                return 1;
            }

            ColonistAssignments assignments =
                colonist.GetComponent<ColonistAssignments>();
            ActivityTarget target;
            if (assignments == null ||
                !assignments.TryGetSleepTarget(out target) ||
                target.Facility != facility ||
                !string.Equals(target.ActivityId, activityId, StringComparison.Ordinal))
            {
                Debug.LogError(
                    colonistName + " sleep target is not " + facility.name + "/" + activityId + ".");
                return 1;
            }

            return 0;
        }

        private static int ValidateWorkAssignment(
            WorkforceManager manager,
            string colonistName,
            WorkplaceComponent expectedWorkplace)
        {
            for (int i = 0; i < manager.Assignments.Count; i++)
            {
                WorkAssignment assignment = manager.Assignments[i];
                if (assignment == null ||
                    assignment.Colonist == null ||
                    !string.Equals(
                        assignment.Colonist.DisplayName,
                        colonistName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (assignment.Workplace == expectedWorkplace)
                    return 0;

                break;
            }

            Debug.LogError(
                colonistName + " is not assigned to expected modular workplace " +
                expectedWorkplace.name + ".");
            return 1;
        }

        private static int RequireActivityForValidation(
            InteractableFacility facility,
            string activityId)
        {
            if (facility != null && facility.TryGetBinding(activityId, out _))
                return 0;

            Debug.LogError(
                (facility != null ? facility.name : "<missing facility>") +
                " does not expose required activity " + activityId + ".");
            return 1;
        }

        private static bool HasSingleRoleActivity(
            WorkplaceComponent workplace,
            string activityId)
        {
            return workplace != null &&
                   workplace.Roles.Count == 1 &&
                   workplace.Roles[0] != null &&
                   string.Equals(
                       workplace.Roles[0].ActivityId,
                       activityId,
                       StringComparison.Ordinal) &&
                   workplace.Roles[0].Role != null;
        }

        private static bool HasOffDutyActivity(
            OffDutyComponent component,
            string activityId,
            OffDutyDrive drive)
        {
            OffDutyActivityBinding activity;
            if (component == null ||
                !component.TryGetActivity(activityId, out activity))
            {
                return false;
            }

            return activity != null && activity.Satisfies(drive);
        }

        private static GameObject RequireSinglePrefabRootForValidation(
            Scene scene,
            string prefabPath,
            ref int errors)
        {
            List<GameObject> matches = FindPrefabRoots(scene, prefabPath);
            if (matches.Count == 1)
                return matches[0];

            Debug.LogError(
                "Expected exactly one " + prefabPath + " instance; found " + matches.Count + ".");
            errors++;
            return null;
        }

        private static void RequireActivity(
            InteractableFacility facility,
            string activityId)
        {
            if (facility == null || !facility.TryGetBinding(activityId, out _))
            {
                throw new InvalidOperationException(
                    (facility != null ? facility.name : "<missing facility>") +
                    " does not contain required activity " + activityId + ".");
            }
        }

        private static JobRoleDefinition RequireSingleRole(
            WorkplaceComponent workplace,
            string description)
        {
            if (workplace == null ||
                workplace.Roles.Count != 1 ||
                workplace.Roles[0] == null ||
                workplace.Roles[0].Role == null)
            {
                throw new InvalidOperationException(
                    description + " must have exactly one configured workplace role.");
            }

            return workplace.Roles[0].Role;
        }

        private static int GetSingleRoleCapacity(WorkplaceComponent workplace)
        {
            return workplace != null &&
                   workplace.Roles.Count == 1 &&
                   workplace.Roles[0] != null
                ? Mathf.Max(1, workplace.Roles[0].MaximumConcurrentScheduledWorkers)
                : 1;
        }

        private static ModuleConnectionPoint[] GetSortedPoints(
            GameObject module,
            int expectedCount)
        {
            ModuleConnectionPoint[] points =
                module.GetComponentsInChildren<ModuleConnectionPoint>(true);
            Array.Sort(
                points,
                (left, right) => string.CompareOrdinal(
                    HierarchyKey(left.transform, module.transform),
                    HierarchyKey(right.transform, module.transform)));

            if (points.Length != expectedCount)
            {
                throw new InvalidOperationException(
                    module.name + " expected " + expectedCount +
                    " ModuleConnectionPoint(s), found " + points.Length + ".");
            }

            return points;
        }

        private static NavMeshSurface RequireRootSurface(GameObject module)
        {
            NavMeshSurface surface = module.GetComponent<NavMeshSurface>();
            if (surface == null)
                throw new InvalidOperationException(module.name + " has no root NavMeshSurface.");
            return surface;
        }

        private static string HierarchyKey(Transform transform, Transform stopAt)
        {
            List<int> indexes = new List<int>();
            Transform current = transform;
            while (current != null)
            {
                indexes.Add(current.GetSiblingIndex());
                if (current == stopAt)
                    break;
                current = current.parent;
            }

            indexes.Reverse();
            return string.Join("/", indexes) + "/" + transform.name;
        }

        private static List<GameObject> FindPrefabRoots(Scene scene, string prefabPath)
        {
            List<GameObject> result = new List<GameObject>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                UnityEngine.Object source =
                    PrefabUtility.GetCorrespondingObjectFromSource(root);
                if (source == null)
                    continue;

                string sourcePath = AssetDatabase.GetAssetPath(source);
                if (string.Equals(sourcePath, prefabPath, StringComparison.Ordinal))
                    result.Add(root);
            }

            return result;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                if (string.Equals(roots[i].name, name, StringComparison.Ordinal))
                    return roots[i];

            return null;
        }

        private static GameObject FindDescendant(Transform parent, string name)
        {
            Transform[] descendants = parent.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                if (descendants[i] != parent &&
                    string.Equals(descendants[i].name, name, StringComparison.Ordinal))
                {
                    return descendants[i].gameObject;
                }
            }

            return null;
        }

        private static ColonistIdentity RequireColonist(Scene scene, string displayName)
        {
            ColonistIdentity colonist = FindColonist(scene, displayName);
            if (colonist == null)
                throw new InvalidOperationException("Missing colonist " + displayName + ".");
            return colonist;
        }

        private static ColonistIdentity FindColonist(Scene scene, string displayName)
        {
            ColonistIdentity[] colonists = FindSceneComponents<ColonistIdentity>(scene);
            ColonistIdentity match = null;
            for (int i = 0; i < colonists.Length; i++)
            {
                if (!string.Equals(
                        colonists[i].DisplayName,
                        displayName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (match != null)
                    return null;

                match = colonists[i];
            }

            return match;
        }

        private static T RequireNamedSceneComponent<T>(
            Scene scene,
            string gameObjectName)
            where T : Component
        {
            T[] components = FindSceneComponents<T>(scene);
            for (int i = 0; i < components.Length; i++)
                if (string.Equals(
                        components[i].gameObject.name,
                        gameObjectName,
                        StringComparison.Ordinal))
                    return components[i];

            throw new InvalidOperationException(
                "Could not find " + typeof(T).Name + " on " + gameObjectName + ".");
        }

        private static T RequireSceneComponent<T>(Scene scene)
            where T : Component
        {
            T component = FindSceneComponent<T>(scene);
            if (component == null)
                throw new InvalidOperationException("Missing or duplicate scene component " + typeof(T).Name + ".");
            return component;
        }

        private static T FindSceneComponent<T>(Scene scene)
            where T : Component
        {
            T[] components = FindSceneComponents<T>(scene);
            return components.Length == 1 ? components[0] : null;
        }

        private static T[] FindSceneComponents<T>(Scene scene)
            where T : Component
        {
            List<T> result = new List<T>();
            T[] all = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < all.Length; i++)
            {
                T component = all[i];
                if (component == null ||
                    EditorUtility.IsPersistent(component) ||
                    component.gameObject.scene != scene)
                {
                    continue;
                }

                result.Add(component);
            }

            return result.ToArray();
        }

        private static T EnsureComponent<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component != null)
                return component;

            return Undo.AddComponent<T>(gameObject);
        }

        private static T RequireComponent<T>(GameObject gameObject, string description)
            where T : Component
        {
            T component = gameObject != null ? gameObject.GetComponent<T>() : null;
            if (component == null)
                throw new InvalidOperationException(description + " has no " + typeof(T).Name + ".");
            return component;
        }

        private sealed class LegacySources
        {
            public GameObject CommandPod;
            public GameObject CommandModule;

            public InteractableFacility CommandFacility;
            public WorkplaceComponent CommandWorkplace;

            public InteractableFacility CafeteriaFacility;
            public WorkplaceComponent CafeteriaWorkplace;
            public FoodServiceComponent FoodService;

            public InteractableFacility FarmFacility;
            public WorkplaceComponent FarmWorkplace;
            public FacilityPerformanceComponent FarmPerformance;
            public WorkforcePerformanceProvider FarmWorkforcePerformance;
            public ResourceConverterComponent FarmConverter;

            public InteractableFacility RecreationFacility;
            public OffDutyComponent OffDuty;

            public bool HasLegacyFixture
            {
                get { return CommandPod != null; }
            }
        }

        private sealed class ModuleSet
        {
            public GameObject CommandCenter;
            public GameObject Cafeteria;
            public GameObject Disco;
            public GameObject Farm;
        }
    }
}
