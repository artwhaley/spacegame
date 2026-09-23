using System;
using System.Collections.Generic;
using Colony.Interactions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AsteroidColony.Editor
{
    /// <summary>
    /// Idempotently adds the P4b logistics fixture to the existing modular scene
    /// without re-arranging the modules already authored there.
    /// </summary>
    public static class P4bModularSceneAuthoring
    {
        private const string ScenePath = "Assets/bobandfriends_modular.unity";
        private const string AirlockPath = "Assets/Prefabs/Airlock.prefab";
        private const string PorterRolePath = "Assets/GameData/Jobs/Porter.asset";
        private const string FoodPath = "Assets/GameData/Resources/Food.asset";
        private const string FarmRecipePath = "Assets/GameData/Recipes/SimpleFarmFood.asset";
        private const string AliceName = "Alice";
        private const string DanaName = "Dana";

        [MenuItem("Colony/Logistics/P4b/Configure Modular Fixture")]
        public static void ConfigureModularFixture()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
            {
                Debug.LogError("Open " + ScenePath + " before configuring the P4b fixture.");
                return;
            }

            try
            {
                ConfigureLoadedSceneAndSave(scene);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public static void ConfigureLoadedSceneAndSave(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
                throw new InvalidOperationException("Expected the loaded modular scene at " + ScenePath + ".");

            Configure(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save " + ScenePath + ".");
            AssetDatabase.SaveAssets();
            Debug.Log("P4b modular logistics fixture authored. Run the P4b validation and Play Mode acceptance.");
        }

        [MenuItem("Colony/Logistics/P4b/Validate Modular Fixture")]
        public static void ValidateModularFixture()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
            {
                Debug.LogError("Open " + ScenePath + " before validating the P4b fixture.");
                return;
            }

            int errors = Validate(scene);
            if (errors == 0)
                Debug.Log("P4b fixture authoring passed. Play Mode traversal and inventory movement still require human acceptance.");
            else
                Debug.LogError("P4b fixture validation found " + errors + " error(s).");
        }

        private static void Configure(Scene scene)
        {
            GameObject command = RequireModule(scene, "Assets/Prefabs/CommandCenter.prefab");
            GameObject cafeteria = RequireModule(scene, "Assets/Prefabs/Cafeteria.prefab");
            GameObject disco = RequireModule(scene, "Assets/Prefabs/Disco.prefab");
            GameObject farm = RequireModule(scene, "Assets/Prefabs/Farm.prefab");
            GameObject airlock = EnsureAirlock(scene, farm, command, cafeteria, disco);

            ResourceDefinition food = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(FoodPath);
            if (food == null)
                throw new InvalidOperationException("Could not load Food resource at " + FoodPath + ".");

            InventoryComponent farmInventory = EnsureComponent<InventoryComponent>(farm);
            InventoryComponent cafeteriaInventory = EnsureComponent<InventoryComponent>(cafeteria);
            InventoryComponent depotInventory = EnsureComponent<InventoryComponent>(airlock);
            RequireCapacity(farmInventory, food, 100f);
            RequireCapacity(cafeteriaInventory, food, 15f);
            RequireCapacity(depotInventory, food, 100f);

            InventoryComponent oldStore = FindNamedInventory(scene, "Station Food Store");
            if (oldStore != null)
            {
                float oldFood = oldStore.GetOnHand(food);
                if (oldFood > 0f)
                {
                    float accepted = cafeteriaInventory.Add(food, oldFood);
                    float removed = oldStore.Remove(food, accepted);
                    if (Mathf.Abs(accepted - removed) > 0.0001f)
                        throw new InvalidOperationException("Could not conserve the legacy Food stock during migration.");
                }
            }

            ResourceConverterComponent converter = RequireComponent<ResourceConverterComponent>(farm);
            converter.inventory = farmInventory;
            RecipeDefinition farmRecipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(FarmRecipePath);
            if (farmRecipe == null || !farmRecipe.IsValid)
                throw new InvalidOperationException("The Farm's whole-unit Food recipe is missing or invalid.");
            converter.availableRecipes = new List<RecipeDefinition> { farmRecipe };
            converter.activeRecipe = farmRecipe;
            EditorUtility.SetDirty(converter);

            FoodServiceComponent foodService = RequireComponent<FoodServiceComponent>(cafeteria);
            SerializedObject foodServiceData = new SerializedObject(foodService);
            foodServiceData.Update();
            SerializedProperty inventoryProperty = foodServiceData.FindProperty("foodInventory");
            SerializedProperty resourceProperty = foodServiceData.FindProperty("foodResource");
            if (inventoryProperty == null || resourceProperty == null)
                throw new InvalidOperationException("FoodServiceComponent is missing inventory/resource bindings.");
            inventoryProperty.objectReferenceValue = cafeteriaInventory;
            resourceProperty.objectReferenceValue = food;
            foodServiceData.ApplyModifiedPropertiesWithoutUndo();
            foodService.RefreshStaticBindingMetadata();
            EditorUtility.SetDirty(foodService);

            if (oldStore != null)
            {
                oldStore.gameObject.name = "Station Food Store (Inert)";
                oldStore.SetCapacity(food, oldStore.GetOnHand(food));
                EditorUtility.SetDirty(oldStore);
                EditorUtility.SetDirty(oldStore.gameObject);
            }

            LogisticsStockComponent farmStock = EnsureComponent<LogisticsStockComponent>(farm);
            farmStock.SetBindings(farmInventory, GetFreightAnchor(farm));
            farmStock.ConfigurePolicy(food, LogisticsStockRole.Producer, 0f, false, 0f, 0f, 1f);
            EditorUtility.SetDirty(farmStock);

            LogisticsStockComponent cafeteriaStock = EnsureComponent<LogisticsStockComponent>(cafeteria);
            cafeteriaStock.SetBindings(cafeteriaInventory, GetFreightAnchor(cafeteria));
            cafeteriaStock.ConfigurePolicy(food, LogisticsStockRole.Consumer, 15f, true, 8f, 3f, 1f);
            EditorUtility.SetDirty(cafeteriaStock);

            LogisticsStockComponent depotStock = EnsureComponent<LogisticsStockComponent>(airlock);
            depotStock.SetBindings(depotInventory, GetFreightAnchor(airlock));
            depotStock.ConfigurePolicy(food, LogisticsStockRole.Depot, 0f, false, 0f, 0f, 1f);
            EditorUtility.SetDirty(depotStock);

            JobRoleDefinition porterRole = EnsurePorterRole();
            Transform dutyAnchor = GetFreightAnchor(airlock);
            WorkplaceComponent porterWorkplace = EnsureComponent<WorkplaceComponent>(airlock);
            porterWorkplace.ConfigureMobileDuty(porterRole, 1, dutyAnchor);
            EditorUtility.SetDirty(porterWorkplace);

            ColonistIdentity alice = RequireColonist(scene, AliceName);
            ColonistIdentity dana = RequireColonist(scene, DanaName);
            WorkforceManager workforce = RequireSceneComponent<WorkforceManager>(scene);
            if (!workforce.TryGetAssignment(alice, out WorkAssignment aliceAssignment) ||
                aliceAssignment.Shift == null || !aliceAssignment.Shift.IsConfigured)
                throw new InvalidOperationException("Alice needs a configured Cafeteria shift to seed the P4b Porter shift.");

            WorkAssignmentResult assignmentResult = workforce.Assign(
                dana,
                porterWorkplace,
                porterRole,
                new DailyShiftWindow(aliceAssignment.Shift.StartHour, aliceAssignment.Shift.EndHour));
            if (assignmentResult != WorkAssignmentResult.Applied)
                throw new InvalidOperationException("Could not assign Dana to Porter duty: " + assignmentResult + ".");
            EditorUtility.SetDirty(workforce);

            WalkingFreightCarrierComponent danaCarrier = EnsureComponent<WalkingFreightCarrierComponent>(dana.gameObject);
            danaCarrier.Configure(10f, false);
            RequireCapacity(danaCarrier.CargoInventory, food, 10f);
            EditorUtility.SetDirty(danaCarrier);

            WorkplaceComponent cafeteriaWorkplace = RequireComponent<WorkplaceComponent>(cafeteria);
            WalkingFreightCarrierComponent aliceCarrier = EnsureComponent<WalkingFreightCarrierComponent>(alice.gameObject);
            aliceCarrier.Configure(5f, true, cafeteriaWorkplace);
            RequireCapacity(aliceCarrier.CargoInventory, food, 5f);
            EditorUtility.SetDirty(aliceCarrier);

            FreightLogisticsManager freightManager = FindSceneComponent<FreightLogisticsManager>(scene);
            if (freightManager == null)
            {
                GameObject managerObject = new GameObject("FreightLogisticsManager");
                SceneManager.MoveGameObjectToScene(managerObject, scene);
                Undo.RegisterCreatedObjectUndo(managerObject, "Create P4b freight authority");
                freightManager = managerObject.AddComponent<FreightLogisticsManager>();
            }
            EnsureComponent<SupplyChainDebugLog>(freightManager.gameObject);

            InventoryComponent legacyStore = FindNamedInventory(scene, "Station Food Store (Inert)");
            if (legacyStore != null && legacyStore.GetOnHand(food) > 0f)
                throw new InvalidOperationException("The inert legacy inventory still holds Food.");

            EditorUtility.SetDirty(farmInventory);
            EditorUtility.SetDirty(cafeteriaInventory);
            EditorUtility.SetDirty(depotInventory);
            EditorUtility.SetDirty(airlock);
            int errors = Validate(scene);
            if (errors != 0)
                throw new InvalidOperationException("P4b authoring finished with " + errors + " validation error(s).");
        }

        private static GameObject EnsureAirlock(
            Scene scene,
            GameObject farm,
            GameObject command,
            GameObject cafeteria,
            GameObject disco)
        {
            GameObject existing = FindModule(scene, AirlockPath);
            if (existing != null)
                return existing;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AirlockPath);
            if (prefab == null)
                throw new InvalidOperationException("Could not load " + AirlockPath + ".");
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
                throw new InvalidOperationException("Could not instantiate " + AirlockPath + ".");
            Undo.RegisterCreatedObjectUndo(instance, "Add Airlock logistics depot");
            instance.name = prefab.name;

            ModuleConnectionPoint farmPoint = FindUnmatchedPoint(farm, command, cafeteria, disco);
            ModuleConnectionPoint airlockPoint = FirstConnection(instance);
            SnapModule(instance, airlockPoint, farmPoint);
            return instance;
        }

        private static ModuleConnectionPoint FindUnmatchedPoint(
            GameObject farm,
            params GameObject[] otherModules)
        {
            ModuleConnectionPoint[] points = farm.GetComponentsInChildren<ModuleConnectionPoint>(true);
            for (int i = 0; i < points.Length; i++)
            {
                bool matched = false;
                for (int m = 0; m < otherModules.Length && !matched; m++)
                {
                    ModuleConnectionPoint[] candidates =
                        otherModules[m].GetComponentsInChildren<ModuleConnectionPoint>(true);
                    for (int j = 0; j < candidates.Length; j++)
                    {
                        float maximum = Mathf.Min(points[i].MaxPartnerDistance, candidates[j].MaxPartnerDistance);
                        if ((points[i].transform.position - candidates[j].transform.position).sqrMagnitude <= maximum * maximum)
                        {
                            matched = true;
                            break;
                        }
                    }
                }
                if (!matched)
                    return points[i];
            }
            throw new InvalidOperationException("Farm has no unmatched connection point for the Airlock.");
        }

        private static void SnapModule(
            GameObject moving,
            ModuleConnectionPoint movingPoint,
            ModuleConnectionPoint fixedPoint)
        {
            if (movingPoint == null || fixedPoint == null)
                throw new InvalidOperationException("Cannot snap Airlock; a connection point is missing.");
            Undo.RecordObject(moving.transform, "Connect Airlock to Farm");
            Quaternion desired = Quaternion.LookRotation(-fixedPoint.transform.forward, fixedPoint.transform.up);
            Quaternion delta = desired * Quaternion.Inverse(movingPoint.transform.rotation);
            moving.transform.rotation = delta * moving.transform.rotation;
            moving.transform.position += fixedPoint.transform.position - movingPoint.transform.position;
            EditorUtility.SetDirty(moving.transform);
        }

        private static JobRoleDefinition EnsurePorterRole()
        {
            JobRoleDefinition role = AssetDatabase.LoadAssetAtPath<JobRoleDefinition>(PorterRolePath);
            if (role != null)
                return role;

            role = ScriptableObject.CreateInstance<JobRoleDefinition>();
            SerializedObject serialized = new SerializedObject(role);
            serialized.FindProperty("stableId").stringValue = "porter";
            serialized.FindProperty("displayName").stringValue = "Porter";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(role, PorterRolePath);
            EditorUtility.SetDirty(role);
            return role;
        }

        private static Transform GetFreightAnchor(GameObject module)
        {
            ModuleConnectionPoint point = FirstConnection(module);
            return point != null && point.WalkAnchor != null
                ? point.WalkAnchor
                : module.transform;
        }

        private static ModuleConnectionPoint FirstConnection(GameObject module)
        {
            ModuleConnectionPoint[] points = module.GetComponentsInChildren<ModuleConnectionPoint>(true);
            if (points.Length == 0)
                throw new InvalidOperationException(module.name + " has no ModuleConnectionPoint.");
            Array.Sort(points, (a, b) => string.CompareOrdinal(a.GetStableHierarchyKey(), b.GetStableHierarchyKey()));
            return points[0];
        }

        private static int Validate(Scene scene)
        {
            int errors = 0;
            GameObject farm = FindModule(scene, "Assets/Prefabs/Farm.prefab");
            GameObject cafeteria = FindModule(scene, "Assets/Prefabs/Cafeteria.prefab");
            GameObject airlock = FindModule(scene, AirlockPath);
            ResourceDefinition food = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(FoodPath);
            if (farm == null || cafeteria == null || airlock == null || food == null)
                return 1;

            InventoryComponent farmInventory = farm.GetComponent<InventoryComponent>();
            InventoryComponent cafeteriaInventory = cafeteria.GetComponent<InventoryComponent>();
            if (farmInventory == null || cafeteriaInventory == null ||
                farmInventory == cafeteriaInventory ||
                RequireComponent<ResourceConverterComponent>(farm).inventory != farmInventory ||
                RequireComponent<FoodServiceComponent>(cafeteria).FoodInventory != cafeteriaInventory)
            {
                Debug.LogError("Farm and Cafeteria must use separate local Food inventories.");
                errors++;
            }

            InventoryComponent legacyStore = FindNamedInventory(scene, "Station Food Store (Inert)");
            if (legacyStore != null && legacyStore.GetOnHand(food) > 0f)
            {
                Debug.LogError("The inert legacy inventory still holds Food.");
                errors++;
            }

            FoodServiceComponent foodService = cafeteria.GetComponent<FoodServiceComponent>();
            if (foodService == null || !foodService.RequiresStaff ||
                foodService.SelfServicePolicy != FoodSelfServicePolicy.AssignedWorkers ||
                foodService.FoodResource != food ||
                !Mathf.Approximately(foodService.HungerRecoveryPerMeal, 90f) ||
                !Mathf.Approximately(foodService.MealDurationGameHours, 0.25f))
            {
                Debug.LogError("Cafeteria needs staffed access and definition-driven 90-point, 15-minute Food meals.");
                errors++;
            }

            ResourceConverterComponent farmConverter = farm.GetComponent<ResourceConverterComponent>();
            if (farmConverter == null || farmConverter.activeRecipe == null ||
                farmConverter.activeRecipe.stableId != "simple-farm-food" ||
                farmConverter.activeRecipe.executionMode != RecipeExecutionMode.Batch ||
                !Mathf.Approximately(farmConverter.activeRecipe.durationHours, 1f))
            {
                Debug.LogError("Farm must batch one whole Food per staffed game-hour.");
                errors++;
            }

            if (farmInventory == null || farmInventory.GetCapacity(food) < 100f ||
                cafeteriaInventory == null || cafeteriaInventory.GetCapacity(food) != 15f)
            {
                Debug.LogError("Farm/Cafeteria Food inventory capacities are not configured.");
                errors++;
            }

            LogisticsStockComponent farmPolicy = farm.GetComponent<LogisticsStockComponent>();
            LogisticsStockComponent cafeteriaPolicy = cafeteria.GetComponent<LogisticsStockComponent>();
            if (farmPolicy == null || !farmPolicy.TryGetPolicy(food, out LogisticsStockPolicyEntry farmEntry) ||
                farmEntry.role != LogisticsStockRole.Producer ||
                cafeteriaPolicy == null || !cafeteriaPolicy.TryGetPolicy(food, out LogisticsStockPolicyEntry cafeEntry) ||
                cafeEntry.role != LogisticsStockRole.Consumer || cafeEntry.reorderThreshold != 8f ||
                cafeEntry.emergencyThreshold != 3f || cafeEntry.minimumPickup != 1f ||
                !cafeEntry.targetFull)
            {
                Debug.LogError("Farm/Cafeteria logistics stock policies are incomplete.");
                errors++;
            }

            WorkplaceComponent porterWorkplace = airlock.GetComponent<WorkplaceComponent>();
            if (porterWorkplace == null || porterWorkplace.ExecutionMode != WorkplaceExecutionMode.MobileDuty ||
                porterWorkplace.DutyAnchor == null ||
                FindSceneComponent<FreightLogisticsManager>(scene) == null ||
                FindSceneComponent<SupplyChainDebugLog>(scene) == null)
            {
                Debug.LogError("Airlock mobile Porter workplace or FreightLogisticsManager is missing.");
                errors++;
            }

            ColonistIdentity alice = FindColonist(scene, AliceName);
            ColonistIdentity dana = FindColonist(scene, DanaName);
            if (alice == null || dana == null ||
                alice.GetComponent<WalkingFreightCarrierComponent>() == null ||
                dana.GetComponent<WalkingFreightCarrierComponent>() == null)
            {
                Debug.LogError("Alice/Dana walking carrier configuration is missing.");
                errors++;
            }
            return errors;
        }

        private static T EnsureComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(gameObject);
        }

        private static void RequireCapacity(
            InventoryComponent inventory,
            ResourceDefinition resource,
            float capacity)
        {
            if (inventory == null || !inventory.SetCapacity(resource, capacity))
                throw new InvalidOperationException(
                    "Could not set " + (resource != null ? resource.name : "resource") +
                    " capacity on " + (inventory != null ? inventory.name : "a missing inventory") +
                    " without discarding existing stock.");
        }

        private static T RequireComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject != null ? gameObject.GetComponent<T>() : null;
            if (component == null)
                throw new InvalidOperationException(gameObject.name + " has no " + typeof(T).Name + ".");
            return component;
        }

        private static GameObject RequireModule(Scene scene, string path)
        {
            GameObject module = FindModule(scene, path);
            if (module == null)
                throw new InvalidOperationException("Missing module instance " + path + ".");
            return module;
        }

        private static GameObject FindModule(Scene scene, string path)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                UnityEngine.Object source = PrefabUtility.GetCorrespondingObjectFromSource(roots[i]);
                if (source != null && AssetDatabase.GetAssetPath(source) == path)
                    return roots[i];
            }
            return null;
        }

        private static InventoryComponent FindNamedInventory(Scene scene, string objectName)
        {
            InventoryComponent[] all = Resources.FindObjectsOfTypeAll<InventoryComponent>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && !EditorUtility.IsPersistent(all[i]) &&
                    all[i].gameObject.scene == scene && all[i].gameObject.name == objectName)
                    return all[i];
            }
            return null;
        }

        private static T FindSceneComponent<T>(Scene scene) where T : Component
        {
            T[] all = Resources.FindObjectsOfTypeAll<T>();
            T result = null;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || EditorUtility.IsPersistent(all[i]) || all[i].gameObject.scene != scene)
                    continue;
                if (result != null)
                    return null;
                result = all[i];
            }
            return result;
        }

        private static ColonistIdentity RequireColonist(Scene scene, string displayName)
        {
            ColonistIdentity colonist = FindColonist(scene, displayName);
            if (colonist == null)
                throw new InvalidOperationException("Could not uniquely find colonist " + displayName + ".");
            return colonist;
        }

        private static ColonistIdentity FindColonist(Scene scene, string displayName)
        {
            ColonistIdentity[] all = Resources.FindObjectsOfTypeAll<ColonistIdentity>();
            ColonistIdentity result = null;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || EditorUtility.IsPersistent(all[i]) || all[i].gameObject.scene != scene ||
                    all[i].DisplayName != displayName)
                    continue;
                if (result != null)
                    return null;
                result = all[i];
            }
            return result;
        }

        private static T RequireSceneComponent<T>(Scene scene) where T : Component
        {
            T component = FindSceneComponent<T>(scene);
            if (component == null)
                throw new InvalidOperationException("Missing or duplicate " + typeof(T).Name + ".");
            return component;
        }
    }
}
