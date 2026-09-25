using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AsteroidColony.Editor
{
    /// <summary>
    /// Builds the explicit B2 two-station fixture without changing the accepted
    /// B1 modular scene. The command is deliberately idempotent so the scene can
    /// be rebuilt while the physical layout is being inspected.
    /// </summary>
    public static class BobAndFriendsCommuteSceneAuthoring
    {
        private const string SourceScenePath = "Assets/bobandfriends_modular.unity";
        private const string CommuteScenePath = "Assets/bobandfriends_commute.unity";
        private const string ShuttleBasePrefabPath = "Assets/Prefabs/ShuttleBase.prefab";
        private const string ShuttlePrefabPath = "Assets/Prefabs/Shuttle.prefab";
        private const string FoodPath = "Assets/GameData/Resources/Food.asset";
        private const string PilotPath = "Assets/GameData/Jobs/Pilot.asset";
        private const string PorterPath = "Assets/GameData/Jobs/Porter.asset";

        [MenuItem("Colony/Navigation/Build Bob And Friends Commute Scene")]
        public static void BuildCommuteScene()
        {
            try
            {
                Scene scene = OpenOrCloneCommuteScene();
                AuthorScene(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("Unity failed to save " + CommuteScenePath + ".");

                int errors = ValidateScene(scene, true);
                if (errors == 0)
                    Debug.Log("B2 commute scene authored successfully. Perform the human layout and Play Mode acceptance gate before changing the fixture further.");
                else
                    Debug.LogError("B2 commute scene was saved, but validation found " + errors + " error(s).");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem("Colony/Navigation/Validate Bob And Friends Commute Scene")]
        public static void ValidateCommuteScene()
        {
            try
            {
                Scene scene = OpenOrCloneCommuteScene();

                int errors = ValidateScene(scene, true);
                if (errors == 0)
                    Debug.Log("B2 commute scene authoring validation passed. NavMesh connectivity and flight behavior remain human acceptance checks.");
                else
                    Debug.LogError("B2 commute scene validation found " + errors + " error(s).");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static Scene OpenOrCloneCommuteScene()
        {
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(CommuteScenePath))
            {
                if (!AssetDatabase.CopyAsset(SourceScenePath, CommuteScenePath))
                    throw new InvalidOperationException("Could not clone " + SourceScenePath + " to " + CommuteScenePath + ".");
                AssetDatabase.Refresh();
            }

            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && string.Equals(active.path, CommuteScenePath, StringComparison.Ordinal))
                return active;

            return EditorSceneManager.OpenScene(CommuteScenePath, OpenSceneMode.Single);
        }

        private static void AuthorScene(Scene scene)
        {
            GameObject farm = RequireRoot(scene, "Farm");
            GameObject farmAirlock = RequireRoot(scene, "Airlock");
            GameObject shuttleBase = EnsureRootPrefab(scene, ShuttleBasePrefabPath, "Shuttle Base");
            shuttleBase.name = "Shuttle Base";

            ArrangeMainStation(scene, shuttleBase);
            ArrangeFarmStation(farm, farmAirlock);

            JobRoleDefinition pilotRole = RequireAsset<JobRoleDefinition>(PilotPath);
            JobRoleDefinition porterRole = RequireAsset<JobRoleDefinition>(PorterPath);
            ResourceDefinition food = RequireAsset<ResourceDefinition>(FoodPath);

            Transform baseDutyAnchor = EnsureInteriorAnchor(shuttleBase, "DutyAnchor", new Vector3(0f, 0f, 0.5f));
            Transform farmDutyAnchor = EnsureInteriorAnchor(farmAirlock, "DutyAnchor", new Vector3(0f, 0f, 0.5f));

            ConfigureStationEndpoint(shuttleBase, "main-shuttle-base", baseDutyAnchor, food,
                pilotRole, porterRole, true);
            ConfigureStationEndpoint(farmAirlock, "farm-airlock", farmDutyAnchor, food,
                null, porterRole, false);

            EnsureCanonicalAssignments(scene, shuttleBase, farmAirlock, pilotRole, porterRole);
            EnsureShuttleManager(scene);
            EnsureB2Shuttle(scene, shuttleBase);
        }

        private static void ArrangeMainStation(Scene scene, GameObject shuttleBase)
        {
            GameObject disco = RequireRoot(scene, "Disco");
            GameObject cafeteria = RequireRoot(scene, "Cafeteria");
            GameObject commandCenter = RequireRoot(scene, "CommandCenter");

            SetRootPose(cafeteria, Vector3.zero, Quaternion.identity);
            SnapModule(disco, GetPoints(disco, 1)[0], GetPoints(cafeteria, 2)[0]);
            SnapModule(commandCenter, GetPoints(commandCenter, 2)[0], GetPoints(cafeteria, 2)[1]);
            SnapModule(shuttleBase, GetPoints(shuttleBase, 1)[0], GetPoints(commandCenter, 2)[1]);
        }

        private static void ArrangeFarmStation(GameObject farm, GameObject farmAirlock)
        {
            Vector3 farmAirlockOffset = farmAirlock.transform.position - farm.transform.position;
            Quaternion farmAirlockRotation = farmAirlock.transform.rotation;

            SetRootPose(farm, new Vector3(0f, 0f, 150f), Quaternion.identity);
            farmAirlock.transform.SetPositionAndRotation(farm.transform.position + farmAirlockOffset, farmAirlockRotation);
            EditorUtility.SetDirty(farmAirlock.transform);

            ModuleConnectionPoint farmPoint = GetPoints(farm, 2)[0];
            ModuleConnectionPoint airlockPoint = GetPoints(farmAirlock, 1)[0];
            SnapModule(farmAirlock, airlockPoint, farmPoint);
        }

        private static void ConfigureStationEndpoint(
            GameObject stationObject,
            string stableId,
            Transform dutyAnchor,
            ResourceDefinition food,
            JobRoleDefinition pilotRole,
            JobRoleDefinition porterRole,
            bool isMainBase)
        {
            DockingPortComponent port = EnsureComponent<DockingPortComponent>(stationObject);
            InventoryComponent inventory = EnsureComponent<InventoryComponent>(stationObject);
            LogisticsStockComponent stock = EnsureComponent<LogisticsStockComponent>(stationObject);
            WorkplaceComponent workplace = EnsureComponent<WorkplaceComponent>(stationObject);
            WalkingFreightWorkService freight = EnsureComponent<WalkingFreightWorkService>(stationObject);
            ShuttleTransferEndpoint endpoint = EnsureComponent<ShuttleTransferEndpoint>(stationObject);

            inventory.SetCapacity(food, 100f);
            stock.SetBindings(inventory, dutyAnchor);
            stock.ConfigurePolicy(food, LogisticsStockRole.Depot, 0f, false, 0f, 0f, 1f);

            if (isMainBase)
            {
                workplace.ConfigureMobileDuty(pilotRole, 1, porterRole, 1, dutyAnchor);
                freight.ConfigureRoutineFreight(true, porterRole, 10f);

                ShuttleBaseComponent shuttleBase = EnsureComponent<ShuttleBaseComponent>(stationObject);
                shuttleBase.Configure(port, workplace, inventory, stock, dutyAnchor);
                ShuttlePilotWorkService pilotService = EnsureComponent<ShuttlePilotWorkService>(stationObject);
                pilotService.Configure(shuttleBase, workplace, pilotRole);
            }
            else
            {
                workplace.ConfigureMobileDuty(porterRole, 1, dutyAnchor);
                freight.ConfigureRoutineFreight(true, porterRole, 10f);
            }

            endpoint.Configure(stableId, port, dutyAnchor, inventory, stock);
            EditorUtility.SetDirty(stationObject);
            EditorUtility.SetDirty(workplace);
            EditorUtility.SetDirty(freight);
            EditorUtility.SetDirty(stock);
            EditorUtility.SetDirty(endpoint);
        }

        private static void EnsureCanonicalAssignments(
            Scene scene,
            GameObject shuttleBase,
            GameObject farmAirlock,
            JobRoleDefinition pilotRole,
            JobRoleDefinition porterRole)
        {
            WorkforceManager workforce = FindSceneComponent<WorkforceManager>(scene);
            if (workforce == null)
                throw new InvalidOperationException("B2 scene is missing WorkforceManager.");

            ColonistIdentity charlie = FindColonist(scene, "Charlie");
            ColonistIdentity dana = FindColonist(scene, "Dana");
            WorkplaceComponent baseWorkplace = shuttleBase.GetComponent<WorkplaceComponent>();
            WorkplaceComponent farmWorkplace = farmAirlock.GetComponent<WorkplaceComponent>();

            EnsureAssignment(workforce, charlie, baseWorkplace, pilotRole, 8f, 16f);
            EnsureAssignment(workforce, dana, farmWorkplace, porterRole, 8f, 20f);
            EditorUtility.SetDirty(workforce);
        }

        private static void EnsureAssignment(
            WorkforceManager workforce,
            ColonistIdentity colonist,
            WorkplaceComponent workplace,
            JobRoleDefinition role,
            float startHour,
            float endHour)
        {
            if (workforce.TryGetAssignment(colonist, out WorkAssignment existing) &&
                existing.Workplace == workplace && existing.Role == role)
                return;

            WorkAssignmentResult result = workforce.Assign(
                colonist, workplace, role, new DailyShiftWindow(startHour, endHour));
            if (result != WorkAssignmentResult.Applied)
                throw new InvalidOperationException("Could not author " + colonist.name + " assignment: " + result + ".");
        }

        private static ShuttleManager EnsureShuttleManager(Scene scene)
        {
            ShuttleManager manager = FindSceneComponent<ShuttleManager>(scene);
            if (manager != null)
                return manager;

            GameObject root = new GameObject("ShuttleManager");
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Create B2 ShuttleManager");
            manager = Undo.AddComponent<ShuttleManager>(root);
            return manager;
        }

        private static ShuttleServiceComponent EnsureB2Shuttle(Scene scene, GameObject shuttleBase)
        {
            ShuttleServiceComponent service = FindSceneComponent<ShuttleServiceComponent>(scene);
            if (service == null)
            {
                RemoveUnconfiguredShuttles(scene);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShuttlePrefabPath);
                if (prefab == null)
                    throw new InvalidOperationException("Could not load " + ShuttlePrefabPath + ".");

                GameObject shuttle = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (shuttle == null)
                    throw new InvalidOperationException("Could not instantiate " + ShuttlePrefabPath + ".");
                Undo.RegisterCreatedObjectUndo(shuttle, "Create B2 Shuttle");
                shuttle.name = "B2 Shuttle";
                service = EnsureComponent<ShuttleServiceComponent>(shuttle);
            }

            ShuttleVoyageComponent voyage = service.GetComponent<ShuttleVoyageComponent>();
            ShuttleDockingProbeComponent probe = service.GetComponent<ShuttleDockingProbeComponent>();
            InventoryComponent cargo = EnsureComponent<InventoryComponent>(service.gameObject);
            ResourceDefinition food = RequireAsset<ResourceDefinition>(FoodPath);
            cargo.SetCapacity(food, 50f);
            Transform passengerAnchor = EnsureChild(service.gameObject, "PassengerAnchor", new Vector3(0f, 0.75f, 0f));
            ShuttlePilotWorkService pilotService = shuttleBase.GetComponent<ShuttlePilotWorkService>();
            ShuttleBaseComponent baseComponent = shuttleBase.GetComponent<ShuttleBaseComponent>();
            ShuttleTransferEndpoint mainEndpoint = shuttleBase.GetComponent<ShuttleTransferEndpoint>();
            ShuttleTransferEndpoint farmEndpoint = RequireRootComponent<ShuttleTransferEndpoint>(scene, "Airlock");

            if (voyage == null || probe == null || baseComponent == null || mainEndpoint == null || farmEndpoint == null)
                throw new InvalidOperationException("B2 Shuttle is missing a required flight or endpoint reference.");

            DockingPortComponent mainPort = mainEndpoint.DockingPort;
            if (mainPort == null || mainPort.DockingNode == null)
                throw new InvalidOperationException("Main Shuttle Base has no valid docking node.");

            if (!DockingPoseUtility.TryGetProbePoseRelativeToRoot(
                    service.transform, probe.ProbeTransform,
                    out Vector3 probeLocalPosition, out Quaternion probeLocalRotation))
                throw new InvalidOperationException("Could not solve B2 Shuttle probe pose.");

            Vector3 targetPosition = mainPort.DockingNode.position;
            Quaternion targetRotation = DockingPoseUtility.GetMatingProbeRotation(mainPort.DockingNode.rotation);
            if (!DockingPoseUtility.TrySolveRootPose(
                    probeLocalPosition, probeLocalRotation, targetPosition, targetRotation,
                    out Vector3 rootPosition, out Quaternion rootRotation))
                throw new InvalidOperationException("Could not solve B2 Shuttle root pose.");

            service.transform.SetPositionAndRotation(rootPosition, rootRotation);
            voyage.CurrentDock = mainPort;
            if (!mainPort.TryReserve(probe) && mainPort.Occupant != probe)
                throw new InvalidOperationException("Could not reserve the Main Shuttle Base berth.");
            if (mainPort.Occupant != probe && !mainPort.TryOccupy(probe))
                throw new InvalidOperationException("Could not occupy the Main Shuttle Base berth.");
            // Dock occupancy is a prefab-instance override in this scene. Persist it
            // alongside CurrentDock so the first runtime departure sees the same berth.
            PrefabUtility.RecordPrefabInstancePropertyModifications(mainPort);
            EditorUtility.SetDirty(mainPort);

            service.Configure("b2-shuttle", baseComponent, voyage, cargo, passengerAnchor,
                8, 10, pilotService);
            EditorUtility.SetDirty(service);
            EditorUtility.SetDirty(voyage);
            EditorUtility.SetDirty(service.transform);
            return service;
        }

        private static void RemoveUnconfiguredShuttles(Scene scene)
        {
            HashSet<GameObject> roots = new HashSet<GameObject>();
            ShuttleVoyageComponent[] voyages = FindSceneComponents<ShuttleVoyageComponent>(scene);
            for (int index = 0; index < voyages.Length; index++)
            {
                if (voyages[index] == null)
                    continue;
                GameObject root = voyages[index].transform.root.gameObject;
                if (root != null)
                    roots.Add(root);
            }

            foreach (GameObject root in roots)
                Undo.DestroyObjectImmediate(root);
        }

        private static int ValidateScene(Scene scene, bool logSuccesses)
        {
            int errors = 0;
            GameObject farm = FindRoot(scene, "Farm");
            GameObject airlock = FindRoot(scene, "Airlock");
            GameObject shuttleBase = FindRoot(scene, "Shuttle Base");
            if (shuttleBase == null)
                shuttleBase = FindRoot(scene, "ShuttleBase");

            if (farm == null || airlock == null || shuttleBase == null)
            {
                Debug.LogError("B2 fixture is missing Farm, Airlock, or Shuttle Base.");
                return 1;
            }

            if (FindSceneComponents<ShuttleBaseComponent>(scene).Length != 1)
            {
                Debug.LogError("B2 fixture must contain exactly one ShuttleBaseComponent.");
                errors++;
            }

            ShuttleTransferEndpoint[] endpoints = FindSceneComponents<ShuttleTransferEndpoint>(scene);
            if (endpoints.Length != 2)
            {
                Debug.LogError("B2 fixture must contain exactly two ShuttleTransferEndpoint components.");
                errors++;
            }

            if (shuttleBase.GetComponent<ShuttleTransferEndpoint>() == null ||
                airlock.GetComponent<ShuttleTransferEndpoint>() == null)
            {
                Debug.LogError("B2 endpoints must be on Shuttle Base and Farm Airlock.");
                errors++;
            }

            if (FindSceneComponents<ShuttleServiceComponent>(scene).Length != 1)
            {
                Debug.LogError("B2 fixture must contain exactly one ShuttleServiceComponent.");
                errors++;
            }

            if (FindSceneComponents<ShuttleManager>(scene).Length != 1)
            {
                Debug.LogError("B2 fixture must contain exactly one ShuttleManager.");
                errors++;
            }

            errors += ValidateStation(shuttleBase, true);
            errors += ValidateStation(airlock, false);
            errors += ValidateModulePair(farm, airlock, "Farm", "Airlock");

            float stationSeparation = Vector3.Distance(shuttleBase.transform.position, farm.transform.position);
            if (stationSeparation < 50f)
            {
                Debug.LogError("Main and Farm stations are too close for the B2 disconnected-station slice: " + stationSeparation + " m.");
                errors++;
            }

            if (logSuccesses && errors == 0)
                Debug.Log("B2 fixture validation: one main Shuttle Base, one remote Farm Airlock, two endpoints, one Shuttle, and distinct station spacing.");
            return errors;
        }

        private static int ValidateStation(GameObject station, bool requirePilot)
        {
            int errors = 0;
            string portError = "missing docking port";
            DockingPortComponent port = station.GetComponent<DockingPortComponent>();
            WorkplaceComponent workplace = station.GetComponent<WorkplaceComponent>();
            ShuttleTransferEndpoint endpoint = station.GetComponent<ShuttleTransferEndpoint>();
            Transform dutyAnchor = FindChild(station.transform, "DutyAnchor");

            if (port == null || !port.ValidateConfiguration(out portError))
            {
                Debug.LogError(station.name + " has an invalid docking port: " + portError, station);
                errors++;
            }
            if (workplace == null || workplace.ExecutionMode != WorkplaceExecutionMode.MobileDuty ||
                workplace.DutyAnchor == null || workplace.DutyAnchor == port?.ApproachNode ||
                workplace.DutyAnchor == port?.DockingNode || workplace.DutyAnchor == port?.ClearanceNode)
            {
                Debug.LogError(station.name + " has no valid interior MobileDuty anchor.", station);
                errors++;
            }
            if (endpoint == null || endpoint.TransferAnchor == null || endpoint.StagingInventory == null || endpoint.DepotStock == null)
            {
                Debug.LogError(station.name + " has an incomplete Shuttle transfer endpoint.", station);
                errors++;
            }
            if (requirePilot && (workplace == null || !workplace.OffersRole(RequireAsset<JobRoleDefinition>(PilotPath))))
            {
                Debug.LogError(station.name + " does not offer Pilot duty.", station);
                errors++;
            }
            if (dutyAnchor == null)
            {
                Debug.LogError(station.name + " is missing DutyAnchor.", station);
                errors++;
            }
            return errors;
        }

        private static int ValidateModulePair(GameObject first, GameObject second, string firstName, string secondName)
        {
            ModuleConnectionPoint[] firstPoints = first.GetComponentsInChildren<ModuleConnectionPoint>(true);
            ModuleConnectionPoint[] secondPoints = second.GetComponentsInChildren<ModuleConnectionPoint>(true);
            if (firstPoints.Length == 0 || secondPoints.Length == 0)
            {
                Debug.LogError(firstName + " and " + secondName + " do not expose module connection points.");
                return 1;
            }
            return 0;
        }

        private static ModuleConnectionPoint[] GetPoints(GameObject module, int expectedCount)
        {
            ModuleConnectionPoint[] points = module.GetComponentsInChildren<ModuleConnectionPoint>(true);
            Array.Sort(points, (left, right) => string.CompareOrdinal(left.GetStableHierarchyKey(), right.GetStableHierarchyKey()));
            if (points.Length != expectedCount)
                throw new InvalidOperationException(module.name + " expected " + expectedCount + " ModuleConnectionPoint(s), found " + points.Length + ".");
            return points;
        }

        private static void SnapModule(GameObject movingModule, ModuleConnectionPoint movingPoint, ModuleConnectionPoint fixedPoint)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(-fixedPoint.transform.forward, fixedPoint.transform.up);
            movingModule.transform.rotation = desiredRotation * Quaternion.Inverse(movingPoint.transform.rotation) * movingModule.transform.rotation;
            movingModule.transform.position += fixedPoint.transform.position - movingPoint.transform.position;
            EditorUtility.SetDirty(movingModule.transform);
        }

        private static void SetRootPose(GameObject module, Vector3 position, Quaternion rotation)
        {
            module.transform.SetPositionAndRotation(position, rotation);
            EditorUtility.SetDirty(module.transform);
        }

        private static Transform EnsureInteriorAnchor(GameObject owner, string name, Vector3 localPosition)
        {
            Transform anchor = FindChild(owner.transform, name);
            if (anchor == null)
                anchor = EnsureChild(owner, name, localPosition);
            else
                anchor.localPosition = localPosition;
            anchor.localRotation = Quaternion.identity;
            EditorUtility.SetDirty(anchor);
            return anchor;
        }

        private static Transform EnsureChild(GameObject owner, string name, Vector3 localPosition)
        {
            GameObject child = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(child, "Create B2 " + name);
            child.transform.SetParent(owner.transform, false);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = Quaternion.identity;
            return child.transform;
        }

        private static T EnsureComponent<T>(GameObject owner) where T : Component
        {
            T component = owner.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(owner);
        }

        private static GameObject EnsureRootPrefab(Scene scene, string prefabPath, string name)
        {
            GameObject existing = FindRoot(scene, name);
            if (existing != null)
                return existing;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                throw new InvalidOperationException("Could not load " + prefabPath + ".");
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
                throw new InvalidOperationException("Could not instantiate " + prefabPath + ".");
            Undo.RegisterCreatedObjectUndo(instance, "Create B2 " + name);
            instance.name = name;
            return instance;
        }

        private static GameObject RequireRoot(Scene scene, string name)
        {
            GameObject root = FindRoot(scene, name);
            if (root == null)
                throw new InvalidOperationException("B2 scene is missing root " + name + ".");
            return root;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
                if (string.Equals(roots[index].name, name, StringComparison.Ordinal))
                    return roots[index];
            return null;
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null)
                return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
                if (all[index] != root && string.Equals(all[index].name, name, StringComparison.Ordinal))
                    return all[index];
            return null;
        }

        private static T RequireRootComponent<T>(Scene scene, string rootName) where T : Component
        {
            GameObject root = RequireRoot(scene, rootName);
            T component = root.GetComponent<T>();
            if (component == null)
                throw new InvalidOperationException(rootName + " is missing " + typeof(T).Name + ".");
            return component;
        }

        private static ColonistIdentity FindColonist(Scene scene, string displayName)
        {
            ColonistIdentity[] all = FindSceneComponents<ColonistIdentity>(scene);
            for (int index = 0; index < all.Length; index++)
                if (all[index] != null && string.Equals(all[index].DisplayName, displayName, StringComparison.Ordinal))
                    return all[index];
            throw new InvalidOperationException("B2 scene is missing colonist " + displayName + ".");
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException("Could not load required B2 asset " + path + ".");
            return asset;
        }

        private static T FindSceneComponent<T>(Scene scene) where T : Component
        {
            T[] all = FindSceneComponents<T>(scene);
            return all.Length == 1 ? all[0] : null;
        }

        private static T[] FindSceneComponents<T>(Scene scene) where T : Component
        {
            List<T> result = new List<T>();
            T[] all = Resources.FindObjectsOfTypeAll<T>();
            for (int index = 0; index < all.Length; index++)
            {
                T component = all[index];
                if (component != null && !EditorUtility.IsPersistent(component) && component.gameObject.scene == scene)
                    result.Add(component);
            }
            return result.ToArray();
        }
    }
}
