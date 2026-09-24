using System;
using System.Collections.Generic;
using AsteroidColony;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AsteroidColony.Editor
{
    /// <summary>Authors and validates the B1 routing/shuttle fixture on the existing modular scene.</summary>
    public static class B1ModularSceneAuthoring
    {
        private const string ScenePath = "Assets/bobandfriends_modular.unity";
        private const string AirlockPath = "Assets/Prefabs/Airlock.prefab";
        private const string ShuttleBasePath = "Assets/Prefabs/ShuttleBase.prefab";
        private const string PilotRolePath = "Assets/GameData/Jobs/Pilot.asset";
        private const string PorterRolePath = "Assets/GameData/Jobs/Porter.asset";
        private const string FoodPath = "Assets/GameData/Resources/Food.asset";

        [MenuItem("Colony/Logistics/B1/Configure Modular Fixture")]
        public static void ConfigureModularFixture()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!IsTargetScene(scene))
            {
                Debug.LogError("Open " + ScenePath + " before configuring the B1 fixture.");
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Stop Play Mode before configuring the B1 fixture; Play Mode scene edits will be discarded.");
                return;
            }
            Debug.Log("Configuring B1 modular fixture in " + scene.path + ".");
            try { ConfigureLoadedSceneAndSave(scene); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        public static void ConfigureLoadedSceneAndSave(Scene scene)
        {
            if (!IsTargetScene(scene))
                throw new InvalidOperationException("Expected the loaded modular scene at " + ScenePath + ".");

            // P4b owns the Farm/Cafeteria/Airlock/Porter setup. Reuse it, then add B1.
            P4bModularSceneAuthoring.ConfigureLoadedSceneAndSave(scene);
            Configure(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save " + ScenePath + ".");
            AssetDatabase.SaveAssets();
            Debug.Log("B1 routing and Shuttle Base fixture authored. Run B1 validation, then Play Mode acceptance.");
        }

        [MenuItem("Colony/Logistics/B1/Validate Modular Fixture")]
        public static void ValidateModularFixture()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!IsTargetScene(scene))
            {
                Debug.LogError("Open " + ScenePath + " before validating the B1 fixture.");
                return;
            }
            int errors = Validate(scene);
            if (errors == 0)
                Debug.Log("B1 authoring passed. NavMesh routing and movement still require human Play Mode acceptance.");
            else
                Debug.LogError("B1 fixture validation found " + errors + " error(s).");
        }

        private static void Configure(Scene scene)
        {
            GameObject airlock = RequireModule(scene, AirlockPath);
            GameObject shuttleBase = EnsureShuttleBase(scene, airlock);
            ResourceDefinition food = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(FoodPath);
            if (food == null) throw new InvalidOperationException("Missing Food resource: " + FoodPath);

            JobRoleDefinition pilot = EnsurePilotRole();
            Transform dutyAnchor = FindChild(shuttleBase.transform, "nodeApproach");
            if (dutyAnchor == null) dutyAnchor = shuttleBase.transform;
            WorkplaceComponent workplace = EnsureComponent<WorkplaceComponent>(shuttleBase);
            workplace.ConfigureMobileDuty(pilot, 1, dutyAnchor);

            InventoryComponent inventory = EnsureComponent<InventoryComponent>(shuttleBase);
            RequireCapacity(inventory, food, 100f);
            LogisticsStockComponent stock = EnsureComponent<LogisticsStockComponent>(shuttleBase);
            stock.SetBindings(inventory, dutyAnchor);
            stock.ConfigurePolicy(food, LogisticsStockRole.Depot, 0f, false, 0f, 0f, 1f);

            DockingPortComponent port = EnsureComponent<DockingPortComponent>(shuttleBase);
            port.DockingNode = FindChild(shuttleBase.transform, "nodeDocking");
            port.ApproachNode = FindChild(shuttleBase.transform, "nodeApproach");
            port.ClearanceNode = FindChild(shuttleBase.transform, "nodeClearance");
            ShuttleBaseComponent baseComponent = EnsureComponent<ShuttleBaseComponent>(shuttleBase);
            baseComponent.Configure(port, workplace, inventory, stock, dutyAnchor);
            if (PrefabUtility.IsPartOfPrefabInstance(shuttleBase))
                PrefabUtility.ApplyPrefabInstance(shuttleBase, InteractionMode.AutomatedAction);

            EnsurePersonnelRoutingInfrastructure(scene);
            EnsureUniqueSceneComponent<FreightLogisticsManager>(scene, "FreightLogisticsManager");
            ColonistIdentity[] colonists = FindColonists(scene);
            for (int i = 0; i < colonists.Length; i++)
                EnsureComponent<PersonnelRouteRunner>(colonists[i].gameObject);

            ColonistIdentity charlie = RequireColonist(scene, "Charlie");
            WorkforceManager workforce = RequireUniqueSceneComponent<WorkforceManager>(scene);
            if (!workforce.TryGetAssignment(charlie, out WorkAssignment old) ||
                old.Shift == null || !old.Shift.IsConfigured)
                throw new InvalidOperationException("Charlie needs an existing configured shift to preserve for Pilot duty.");
            WorkAssignmentResult result = workforce.Assign(
                charlie, workplace, pilot,
                new DailyShiftWindow(old.Shift.StartHour, old.Shift.EndHour));
            if (result != WorkAssignmentResult.Applied)
                throw new InvalidOperationException("Could not assign Charlie to Pilot: " + result + ".");

            EditorUtility.SetDirty(workplace);
            EditorUtility.SetDirty(inventory);
            EditorUtility.SetDirty(stock);
            EditorUtility.SetDirty(port);
            EditorUtility.SetDirty(baseComponent);
            EditorUtility.SetDirty(workforce);
            int errors = Validate(scene);
            if (errors != 0)
                throw new InvalidOperationException("B1 authoring finished with " + errors + " validation error(s).");
        }

        private static GameObject EnsureShuttleBase(Scene scene, GameObject airlock)
        {
            List<GameObject> found = FindModules(scene, ShuttleBasePath);
            if (found.Count > 1)
                throw new InvalidOperationException("Scene contains multiple Shuttle Base prefab instances; resolve duplicates before authoring.");
            if (found.Count == 1) return found[0];

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AirlockPath);
            if (prefab == null) throw new InvalidOperationException("Missing reusable Airlock prefab: " + AirlockPath);
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                throw new InvalidOperationException("Missing Assets/Prefabs folder.");

            // The Shuttle Base is a prefab variant of Airlock so its authored port and
            // connection geometry remain reusable. The source prefab is not modified.
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (instance == null) throw new InvalidOperationException("Could not instantiate Airlock as Shuttle Base.");
            Undo.RegisterCreatedObjectUndo(instance, "Create Shuttle Base variant");
            instance.name = "Shuttle Base";
            AlignToFreeConnection(instance, airlock, scene);
            GameObject variant = PrefabUtility.SaveAsPrefabAssetAndConnect(
                instance, ShuttleBasePath, InteractionMode.AutomatedAction);
            if (variant == null) throw new InvalidOperationException("Could not save Shuttle Base prefab variant.");
            return instance;
        }

        private static void AlignToFreeConnection(GameObject moving, GameObject preferred, Scene scene)
        {
            ModuleConnectionPoint[] movingPoints = moving.GetComponentsInChildren<ModuleConnectionPoint>(true);
            if (movingPoints.Length == 0) throw new InvalidOperationException("Airlock prefab has no module connection points.");
            Array.Sort(movingPoints, (a, b) => string.CompareOrdinal(a.GetStableHierarchyKey(), b.GetStableHierarchyKey()));

            ModuleConnectionPoint target = FindFreePoint(preferred, moving, scene);
            if (target == null)
            {
                GameObject[] roots = scene.GetRootGameObjects();
                Array.Sort(roots, (a, b) => string.CompareOrdinal(a.name, b.name));
                for (int i = 0; i < roots.Length && target == null; i++)
                {
                    if (roots[i] == moving) continue;
                    target = FindFreePoint(roots[i], moving, scene);
                }
            }
            if (target == null) throw new InvalidOperationException("Could not find an unmatched module connector for Shuttle Base.");

            ModuleConnectionPoint source = movingPoints[0];
            Quaternion desired = Quaternion.LookRotation(-target.transform.forward, target.transform.up);
            moving.transform.rotation = desired * Quaternion.Inverse(source.transform.rotation) * moving.transform.rotation;
            moving.transform.position += target.transform.position - source.transform.position;
            EditorUtility.SetDirty(moving.transform);
        }

        private static ModuleConnectionPoint FindFreePoint(GameObject root, GameObject moving, Scene scene)
        {
            ModuleConnectionPoint[] points = root.GetComponentsInChildren<ModuleConnectionPoint>(true);
            Array.Sort(points, (a, b) => string.CompareOrdinal(a.GetStableHierarchyKey(), b.GetStableHierarchyKey()));
            for (int i = 0; i < points.Length; i++)
            {
                bool paired = false;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length && !paired; r++)
                {
                    ModuleConnectionPoint[] others = roots[r].GetComponentsInChildren<ModuleConnectionPoint>(true);
                    for (int j = 0; j < others.Length; j++)
                    {
                        if (others[j] == points[i] || others[j].transform.root.gameObject == moving) continue;
                        float max = Mathf.Min(points[i].MaxPartnerDistance, others[j].MaxPartnerDistance);
                        if ((points[i].transform.position - others[j].transform.position).sqrMagnitude <= max * max)
                        { paired = true; break; }
                    }
                }
                if (!paired) return points[i];
            }
            return null;
        }

        private static int Validate(Scene scene)
        {
            int errors = 0;
            if (CountSceneComponents<PersonnelRoutingManager>(scene) != 1 ||
                CountSceneComponents<PedestrianRouteProvider>(scene) != 1 ||
                CountSceneComponents<FreightLogisticsManager>(scene) != 1)
            { Debug.LogError("B1 requires exactly one PersonnelRoutingManager, PedestrianRouteProvider, and FreightLogisticsManager."); errors++; }

            GameObject farm = FindModule(scene, "Assets/Prefabs/Farm.prefab");
            GameObject cafeteria = FindModule(scene, "Assets/Prefabs/Cafeteria.prefab");
            GameObject airlock = FindModule(scene, AirlockPath);
            ColonistIdentity dana = FindColonist(scene, "Dana");
            JobRoleDefinition porter = AssetDatabase.LoadAssetAtPath<JobRoleDefinition>(PorterRolePath);
            WorkforceManager fixtureWorkforce = FindUniqueSceneComponent<WorkforceManager>(scene);
            FreightLogisticsManager freightManager = FindUniqueSceneComponent<FreightLogisticsManager>(scene);
            WorkplaceComponent porterWorkplace = airlock != null ? airlock.GetComponent<WorkplaceComponent>() : null;
            WalkingFreightWorkService porterService = airlock != null
                ? airlock.GetComponent<WalkingFreightWorkService>() : null;
            if (freightManager == null || porter == null || porterService == null ||
                porterService.Workplace != porterWorkplace || !porterService.RoutineFreightEnabled ||
                porterService.RoutineRole != porter ||
                !Mathf.Approximately(porterService.RoutineCapacityPerWorker, 10f))
            { Debug.LogError("Farm Airlock needs a configured routine Porter freight service."); errors++; }
            if (farm == null || cafeteria == null || airlock == null)
            { Debug.LogError("B1 requires Farm, Cafeteria, and Airlock prefab instances."); errors++; }
            else
            {
                InventoryComponent farmInventory = farm.GetComponent<InventoryComponent>();
                InventoryComponent cafeteriaInventory = cafeteria.GetComponent<InventoryComponent>();
                if (farmInventory == null || cafeteriaInventory == null ||
                    farmInventory == cafeteriaInventory)
                { Debug.LogError("Farm and Cafeteria need separate inventories."); errors++; }
                if (!HasDepotPolicy(airlock, AssetDatabase.LoadAssetAtPath<ResourceDefinition>(FoodPath)))
                { Debug.LogError("Airlock needs a Food Depot stock policy."); errors++; }
                if (dana == null || porter == null || fixtureWorkforce == null || porterWorkplace == null ||
                    !fixtureWorkforce.TryGetAssignment(dana, out WorkAssignment danaAssignment) ||
                    danaAssignment.Role != porter || danaAssignment.Workplace != porterWorkplace)
                { Debug.LogError("Dana needs an Airlock Porter assignment."); errors++; }
            }

            List<GameObject> shuttleBases = FindModules(scene, ShuttleBasePath);
            GameObject shuttleBase = shuttleBases.Count == 1 ? shuttleBases[0] : null;
            if (shuttleBases.Count != 1)
            { Debug.LogError("B1 requires exactly one Shuttle Base prefab instance."); errors++; }
            JobRoleDefinition pilot = AssetDatabase.LoadAssetAtPath<JobRoleDefinition>(PilotRolePath);
            if (shuttleBase == null || pilot == null)
            {
                if (pilot == null)
                { Debug.LogError("Pilot role asset is missing."); errors++; }
                return errors;
            }

            ShuttleBaseComponent fixture = shuttleBase.GetComponent<ShuttleBaseComponent>();
            DockingPortComponent port = shuttleBase.GetComponent<DockingPortComponent>();
            WorkplaceComponent workplace = shuttleBase.GetComponent<WorkplaceComponent>();
            InventoryComponent inventory = shuttleBase.GetComponent<InventoryComponent>();
            LogisticsStockComponent stock = shuttleBase.GetComponent<LogisticsStockComponent>();
            ResourceDefinition food = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(FoodPath);
            if (fixture == null || fixture.DockingPort != port || workplace == null ||
                workplace.ExecutionMode != WorkplaceExecutionMode.MobileDuty ||
                !workplace.OffersRole(pilot) || workplace.DutyAnchor == null ||
                inventory == null || stock == null || stock.Inventory != inventory ||
                port == null || !port.ValidateConfiguration(out _) || food == null ||
                !stock.TryGetPolicy(food, out LogisticsStockPolicyEntry policy) || policy.role != LogisticsStockRole.Depot)
            { Debug.LogError("Shuttle Base needs valid docking nodes, Pilot mobile-duty workplace, Depot inventory, and stock policy."); errors++; }

            ColonistIdentity[] colonists = FindColonists(scene);
            for (int i = 0; i < colonists.Length; i++)
            {
                WalkingFreightCarrierComponent carrier = colonists[i].GetComponent<WalkingFreightCarrierComponent>();
                if (colonists[i].GetComponent<PersonnelRouteRunner>() == null || carrier == null ||
                    carrier.GetComponent<WalkingFreightRunner>() == null ||
                    carrier.CargoInventory != colonists[i].GetComponent<InventoryComponent>())
                { Debug.LogError(colonists[i].DisplayName + " is missing the shared colonist routing/freight composition.", colonists[i]); errors++; }
            }

            ColonistIdentity charlie = FindColonist(scene, "Charlie");
            WorkforceManager workforce = FindUniqueSceneComponent<WorkforceManager>(scene);
            if (charlie == null || workforce == null ||
                !workforce.TryGetAssignment(charlie, out WorkAssignment assignment) ||
                assignment.Role != pilot || assignment.Workplace != workplace)
            { Debug.LogError("Charlie must be assigned Pilot at Shuttle Base."); errors++; }
            else
            {
                for (int i = 0; i < workforce.Assignments.Count; i++)
                    if (workforce.Assignments[i] != null && workforce.Assignments[i].Colonist == charlie &&
                        workforce.Assignments[i].Role != pilot)
                    { Debug.LogError("Charlie has an extra job assignment."); errors++; break; }
            }
            return errors;
        }

        private static JobRoleDefinition EnsurePilotRole()
        {
            JobRoleDefinition role = AssetDatabase.LoadAssetAtPath<JobRoleDefinition>(PilotRolePath);
            if (role != null) return role;
            role = ScriptableObject.CreateInstance<JobRoleDefinition>();
            SerializedObject data = new SerializedObject(role);
            data.FindProperty("stableId").stringValue = "pilot";
            data.FindProperty("displayName").stringValue = "Pilot";
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(role, PilotRolePath);
            EditorUtility.SetDirty(role);
            return role;
        }

        private static void EnsureUniqueSceneComponent<T>(Scene scene, string objectName) where T : Component
        {
            T[] all = GetSceneComponents<T>(scene);
            if (all.Length > 1) throw new InvalidOperationException("Scene has duplicate " + typeof(T).Name + " components.");
            if (all.Length == 1) return;
            GameObject go = new GameObject(objectName);
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, "Create B1 routing manager");
            go.AddComponent<T>();
        }

        private static void EnsurePersonnelRoutingInfrastructure(Scene scene)
        {
            PersonnelRoutingManager manager = RequireOrCreateUnique<PersonnelRoutingManager>(scene, "PersonnelRoutingManager");
            PedestrianRouteProvider provider = RequireOrCreateUnique<PedestrianRouteProvider>(scene, "PedestrianRouteProvider", manager.gameObject);
            manager.ConfigureProvider(provider);
            EditorUtility.SetDirty(manager);
        }

        private static T RequireOrCreateUnique<T>(Scene scene, string objectName, GameObject host = null) where T : Component
        {
            T[] all = GetSceneComponents<T>(scene);
            if (all.Length > 1) throw new InvalidOperationException("Scene has duplicate " + typeof(T).Name + " components.");
            if (all.Length == 1) return all[0];
            GameObject go = host;
            if (go == null)
            {
                go = new GameObject(objectName);
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "Create B1 routing infrastructure");
            }
            return Undo.AddComponent<T>(go);
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        { return go.GetComponent<T>() ?? Undo.AddComponent<T>(go); }

        private static void RequireCapacity(InventoryComponent inventory, ResourceDefinition food, float capacity)
        { if (inventory == null || !inventory.SetCapacity(food, capacity)) throw new InvalidOperationException("Could not configure Shuttle Base Food capacity while preserving stock."); }

        private static bool HasDepotPolicy(GameObject module, ResourceDefinition resource)
        {
            LogisticsStockComponent stock = module != null ? module.GetComponent<LogisticsStockComponent>() : null;
            return resource != null && stock != null &&
                   stock.TryGetPolicy(resource, out LogisticsStockPolicyEntry policy) &&
                   policy.role == LogisticsStockRole.Depot;
        }

        private static Transform FindChild(Transform root, string target)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == target) return child;
                Transform nested = FindChild(child, target);
                if (nested != null) return nested;
            }
            return null;
        }

        private static bool IsTargetScene(Scene scene) => scene.IsValid() && scene.isLoaded && scene.path == ScenePath;
        private static GameObject RequireModule(Scene scene, string path) => FindModule(scene, path) ?? throw new InvalidOperationException("Missing module instance " + path + ".");
        private static GameObject FindModule(Scene scene, string path)
        {
            List<GameObject> list = FindModules(scene, path);
            return list.Count == 1 ? list[0] : null;
        }
        private static List<GameObject> FindModules(Scene scene, string path)
        {
            List<GameObject> result = new List<GameObject>();
            string expectedName = path == ShuttleBasePath
                ? "Shuttle Base"
                : System.IO.Path.GetFileNameWithoutExtension(path);
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                // A prefab variant can resolve through its base Airlock source in
                // the loaded scene. The fixture names distinguish the two roots.
                if (roots[i].name == expectedName)
                    result.Add(roots[i]);
            }
            return result;
        }
        private static ColonistIdentity[] FindColonists(Scene scene)
        {
            List<ColonistIdentity> result = new List<ColonistIdentity>();
            ColonistIdentity[] all = Resources.FindObjectsOfTypeAll<ColonistIdentity>();
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && !EditorUtility.IsPersistent(all[i]) && all[i].gameObject.scene == scene) result.Add(all[i]);
            return result.ToArray();
        }
        private static ColonistIdentity RequireColonist(Scene scene, string name) => FindColonist(scene, name) ?? throw new InvalidOperationException("Could not uniquely find " + name + ".");
        private static ColonistIdentity FindColonist(Scene scene, string name)
        {
            ColonistIdentity match = null;
            ColonistIdentity[] all = FindColonists(scene);
            for (int i = 0; i < all.Length; i++) if (all[i].DisplayName == name) { if (match != null) return null; match = all[i]; }
            return match;
        }
        private static T RequireUniqueSceneComponent<T>(Scene scene) where T : Component => FindUniqueSceneComponent<T>(scene) ?? throw new InvalidOperationException("Missing or duplicate " + typeof(T).Name + ".");
        private static T FindUniqueSceneComponent<T>(Scene scene) where T : Component
        { T[] all = GetSceneComponents<T>(scene); return all.Length == 1 ? all[0] : null; }
        private static T[] GetSceneComponents<T>(Scene scene) where T : Component
        {
            List<T> result = new List<T>();
            T[] all = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && !EditorUtility.IsPersistent(all[i]) && all[i].gameObject.scene == scene) result.Add(all[i]);
            return result.ToArray();
        }
        private static int CountSceneComponents<T>(Scene scene) where T : Component => GetSceneComponents<T>(scene).Length;
    }
}
