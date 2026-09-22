using System;
using System.Collections.Generic;
using System.IO;
using AsteroidColony;
using Colony.Interactions;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace AsteroidColony.Stress.Editor
{
    /// <summary>
    /// Creates a reproducible production-component stress scene. The builder
    /// does not replace the real brain, food, workforce, off-duty, reservation,
    /// motor, or animation components with test doubles.
    /// </summary>
    public static class HighSpeedStressLabBuilder
    {
        private const string GeneratedFolder = "Assets/Dev/Stress/Generated";
        private const string ScenePath = GeneratedFolder + "/HighSpeedStressLab.unity";
        private const string ColonistPrefabPath = "Assets/Prefabs/Colonists/Colonist_Synty_Male_01.prefab";
        private const int Population = 200;
        private const int WorkerCount = 150;
        private const int WorkstationCount = 149;
        private const int FoodCount = 40;
        private const int RecreationCount = 50;
        private const int BedCount = 200;

        [MenuItem("Tools/Spacegame/Build High-Speed Population Stress Lab")]
        public static void Build()
        {
            EnsureFolder("Assets/Dev");
            EnsureFolder("Assets/Dev/Stress");
            EnsureFolder(GeneratedFolder);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ColonistPrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog(
                    "High-Speed Stress Lab",
                    "Could not find " + ColonistPrefabPath + ". Fix the prefab path before building.",
                    "OK");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            GameObject root = new GameObject("HighSpeedStressLab");
            GameObject systems = new GameObject("Systems");
            systems.transform.SetParent(root.transform);
            SimulationManager simulation = systems.AddComponent<SimulationManager>();
            FoodManager foodManager = systems.AddComponent<FoodManager>();
            OffDutyManager offDutyManager = systems.AddComponent<OffDutyManager>();
            WorkforceManager workforceManager = systems.AddComponent<WorkforceManager>();
            StressTelemetry telemetry = systems.AddComponent<StressTelemetry>();
            PopulationStressMonitor monitor = systems.AddComponent<PopulationStressMonitor>();
            PopulationStressHarness harness = systems.AddComponent<PopulationStressHarness>();

            ConfigureSimulation(simulation);
            ConfigureTelemetry(telemetry);

            CreateFloor(root.transform);
            NavMeshSurface surface = root.AddComponent<NavMeshSurface>();
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            JobRoleDefinition role = CreateRoleAsset();
            List<InteractableFacility> allFacilities = new List<InteractableFacility>(BedCount + FoodCount + RecreationCount + WorkerCount);
            List<WorkplaceComponent> workplaces = new List<WorkplaceComponent>(WorkerCount);
            List<InteractableFacility> beds = new List<InteractableFacility>(BedCount);

            CreateFacilities(root.transform, allFacilities, workplaces, beds, role);

            List<ColonistActivityRunner> actors = new List<ColonistActivityRunner>(Population);
            List<ColonistIdentity> identities = new List<ColonistIdentity>(Population);
            CreateColonists(root.transform, prefab, actors, identities, beds);
            ConfigureWorkforce(workforceManager, identities, workplaces, role);
            ConfigureMonitor(monitor, simulation, telemetry, actors, allFacilities);
            ConfigureHarness(harness, simulation, telemetry, monitor);

            // Bake after all floor/facility colliders exist. The generated asset
            // is intentionally a normal NavMeshSurface data asset, not a test stub.
            surface.BuildNavMesh();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = harness.gameObject;
            EditorUtility.DisplayDialog(
                "High-Speed Stress Lab",
                "Built " + ScenePath + " with " + Population + " colonists, " +
                BedCount + " beds, " + WorkstationCount + " workstations plus one counter, " + FoodCount +
                " food seats, and " + RecreationCount + " recreation seats.\n\n" +
                "Open the scene, verify the bake, then use the harness BeginRun button or auto-start setting.",
                "OK");
        }

        private static void ConfigureSimulation(SimulationManager simulation)
        {
            simulation.speedMultiplier = 10f;
            SetPrivate(simulation, "logicalStepSimulationSeconds", 1f);
            SetPrivate(simulation, "maxLogicalStepsPerFrame", 10000);
            simulation.tickIntervalSeconds = 0.1f;
        }

        private static void ConfigureTelemetry(StressTelemetry telemetry)
        {
            SetPrivate(telemetry, "observationMode", StressObservationMode.Summary);
            SetPrivate(telemetry, "eventCapacity", 4096);
            SetPrivate(telemetry, "failureCapacity", 256);
        }

        private static void CreateFloor(Transform parent)
        {
            GameObject floor = new GameObject("StressFloor");
            floor.transform.SetParent(parent);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            BoxCollider collider = floor.AddComponent<BoxCollider>();
            collider.size = new Vector3(80f, 1f, 60f);
        }

        private static void CreateFacilities(
            Transform parent,
            List<InteractableFacility> allFacilities,
            List<WorkplaceComponent> workplaces,
            List<InteractableFacility> beds,
            JobRoleDefinition role)
        {
            for (int index = 0; index < WorkstationCount; index++)
            {
                Vector3 position = GridPosition(index, 12, -25f, 16f, 1.7f);
                InteractableFacility facility = CreateFacility(
                    parent,
                    "Workstation_" + index.ToString("D3"),
                    position,
                    "Work",
                    "work_" + index.ToString("D3"));
                WorkplaceComponent workplace = facility.gameObject.AddComponent<WorkplaceComponent>();
                ConfigureWorkplace(workplace, facility, role, "Work", 1);
                workplaces.Add(workplace);
                allFacilities.Add(facility);
            }

            InteractableFacility counterFacility = CreateFacility(
                parent,
                "WorkCounter",
                new Vector3(-25f, 0f, 20f),
                "Work",
                "work_counter");
            WorkplaceComponent counter = counterFacility.gameObject.AddComponent<WorkplaceComponent>();
            ConfigureWorkplace(counter, counterFacility, role, "Work", 1);
            workplaces.Add(counter);
            allFacilities.Add(counterFacility);

            for (int index = 0; index < FoodCount; index++)
            {
                Vector3 position = GridPosition(index, 10, 5f, 17f, 1.7f);
                InteractableFacility facility = CreateFacility(
                    parent,
                    "FoodSeat_" + index.ToString("D3"),
                    position,
                    "Eat",
                    "eat_" + index.ToString("D3"));
                FoodServiceComponent service = facility.gameObject.AddComponent<FoodServiceComponent>();
                ConfigureFoodService(service, facility, 80f);
                allFacilities.Add(facility);
            }

            for (int index = 0; index < RecreationCount; index++)
            {
                Vector3 position = GridPosition(index, 10, 24f, 17f, 1.7f);
                InteractableFacility facility = CreateFacility(
                    parent,
                    "RecreationSeat_" + index.ToString("D3"),
                    position,
                    "Dance",
                    "dance_" + index.ToString("D3"));
                OffDutyComponent offDuty = facility.gameObject.AddComponent<OffDutyComponent>();
                ConfigureOffDuty(offDuty, facility, "Dance", 0.5f, 20f, 10f);
                allFacilities.Add(facility);
            }

            for (int index = 0; index < BedCount; index++)
            {
                Vector3 position = GridPosition(index, 20, -25f, -17f, 1.7f);
                InteractableFacility facility = CreateFacility(
                    parent,
                    "Bed_" + index.ToString("D3"),
                    position,
                    "Sleep",
                    "sleep_" + index.ToString("D3"));
                beds.Add(facility);
                allFacilities.Add(facility);
            }
        }

        private static void CreateColonists(
            Transform parent,
            GameObject prefab,
            List<ColonistActivityRunner> actors,
            List<ColonistIdentity> identities,
            List<InteractableFacility> beds)
        {
            GameObject populationRoot = new GameObject("Population_200");
            populationRoot.transform.SetParent(parent);

            for (int index = 0; index < Population; index++)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance == null)
                    throw new InvalidOperationException("Could not instantiate colonist prefab.");

                instance.name = "StressColonist_" + index.ToString("D3");
                instance.transform.SetParent(populationRoot.transform);
                instance.transform.position = GridPosition(index, 20, -22f, 0f, 1.1f) + Vector3.up * 0.05f;
                StressStableIdentity stable = instance.GetComponent<StressStableIdentity>();
                if (stable == null)
                    stable = instance.AddComponent<StressStableIdentity>();
                stable.Configure("colonist_" + index.ToString("D3"));

                ColonistIdentity identity = instance.GetComponent<ColonistIdentity>();
                ColonistStatsComponent stats = instance.GetComponent<ColonistStatsComponent>();
                ColonistActivityRunner runner = instance.GetComponent<ColonistActivityRunner>();
                ColonistBrain brain = instance.GetComponent<ColonistBrain>();
                ColonistAssignments assignments = instance.GetComponent<ColonistAssignments>();
                if (identity == null || stats == null || runner == null || brain == null || assignments == null)
                    throw new InvalidOperationException("Colonist prefab is missing a required production component.");

                SetPrivate(identity, "displayName", "StressColonist_" + index.ToString("D3"));
                SetPrivate(stats, "hunger", index % 10 == 0 ? 55f : 15f);
                SetPrivate(stats, "fatigue", index % 7 == 0 ? 55f : 10f);
                SetPrivate(stats, "baselineHungerPerGameHour", 3f);
                SetPrivate(stats, "baselineFatiguePerGameHour", 4f);
                SetPrivate(stats, "activityRunner", runner);
                SetPrivate(stats, "identity", identity);
                SetPrivate(brain, "stats", stats);
                SetPrivate(brain, "identity", identity);
                SetPrivate(brain, "activityRunner", runner);
                SetPrivate(assignments, "sleepTarget", new ActivityTarget(beds[index], "Sleep"));

                actors.Add(runner);
                identities.Add(identity);
            }
        }

        private static void ConfigureWorkforce(
            WorkforceManager manager,
            List<ColonistIdentity> identities,
            List<WorkplaceComponent> workplaces,
            JobRoleDefinition role)
        {
            SerializedObject serialized = new SerializedObject(manager);
            SerializedProperty assignments = serialized.FindProperty("assignments");
            assignments.arraySize = WorkerCount;
            for (int index = 0; index < WorkerCount; index++)
            {
                SerializedProperty assignment = assignments.GetArrayElementAtIndex(index);
                assignment.FindPropertyRelative("colonist").objectReferenceValue = identities[index];
                assignment.FindPropertyRelative("workplace").objectReferenceValue = workplaces[index];
                assignment.FindPropertyRelative("role").objectReferenceValue = role;
                SerializedProperty shift = assignment.FindPropertyRelative("shift");
                shift.FindPropertyRelative("startHour").floatValue = 6f;
                shift.FindPropertyRelative("endHour").floatValue = 18f;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
        }

        private static void ConfigureMonitor(
            PopulationStressMonitor monitor,
            SimulationManager simulation,
            StressTelemetry telemetry,
            List<ColonistActivityRunner> actors,
            List<InteractableFacility> facilities)
        {
            SerializedObject serialized = new SerializedObject(monitor);
            serialized.FindProperty("simulationManager").objectReferenceValue = simulation;
            serialized.FindProperty("telemetry").objectReferenceValue = telemetry;
            serialized.FindProperty("autoDiscoverActors").boolValue = false;
            serialized.FindProperty("invariantSampleIntervalSeconds").floatValue = 1f;
            SetArray(serialized.FindProperty("actors"), actors.ToArray());
            SetArray(serialized.FindProperty("facilities"), facilities.ToArray());
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(monitor);
        }

        private static void ConfigureHarness(
            PopulationStressHarness harness,
            SimulationManager simulation,
            StressTelemetry telemetry,
            PopulationStressMonitor monitor)
        {
            SerializedObject serialized = new SerializedObject(harness);
            serialized.FindProperty("simulationManager").objectReferenceValue = simulation;
            serialized.FindProperty("telemetry").objectReferenceValue = telemetry;
            serialized.FindProperty("monitor").objectReferenceValue = monitor;
            serialized.FindProperty("scenario").enumValueIndex = (int)StressScenario.ProductionPopulation;
            serialized.FindProperty("speedMultiplier").floatValue = 1000f;
            serialized.FindProperty("runDurationSimulationSeconds").floatValue = 86400f;
            serialized.FindProperty("autoStart").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(harness);
        }

        private static InteractableFacility CreateFacility(
            Transform parent,
            string name,
            Vector3 position,
            string activityId,
            string reservationGroup)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent);
            gameObject.transform.position = position;
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.3f, 0.8f, 1.3f);
            Transform approach = new GameObject("Approach").transform;
            approach.SetParent(gameObject.transform);
            approach.localPosition = new Vector3(0f, 0f, -1.2f);
            InteractableFacility facility = gameObject.AddComponent<InteractableFacility>();
            ConfigureFacilityBinding(facility, activityId, reservationGroup, approach);
            StressStableIdentity stable = gameObject.AddComponent<StressStableIdentity>();
            stable.Configure(name.ToLowerInvariant());
            return facility;
        }

        private static void ConfigureFacilityBinding(
            InteractableFacility facility,
            string activityId,
            string reservationGroup,
            Transform anchor)
        {
            SerializedObject serialized = new SerializedObject(facility);
            SerializedProperty activities = serialized.FindProperty("activities");
            activities.arraySize = 1;
            SerializedProperty binding = activities.GetArrayElementAtIndex(0);
            binding.FindPropertyRelative("activityId").stringValue = activityId;
            binding.FindPropertyRelative("reservationGroup").stringValue = reservationGroup;
            binding.FindPropertyRelative("externallyRequestable").boolValue = true;
            binding.FindPropertyRelative("completionMode").enumValueIndex = FindEnumIndex(
                binding.FindPropertyRelative("completionMode"),
                ActivityCompletionMode.Sustained.ToString());
            binding.FindPropertyRelative("approachAnchor").objectReferenceValue = anchor;
            binding.FindPropertyRelative("animationAnchor").objectReferenceValue = anchor;
            binding.FindPropertyRelative("exitAnchor").objectReferenceValue = anchor;
            binding.FindPropertyRelative("targets").objectReferenceValue = anchor;
            binding.FindPropertyRelative("entrySteps").arraySize = 0;
            binding.FindPropertyRelative("activeSteps").arraySize = 0;
            binding.FindPropertyRelative("exitSteps").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(facility);
        }

        private static void ConfigureFoodService(
            FoodServiceComponent service,
            InteractableFacility facility,
            float recovery)
        {
            SerializedObject serialized = new SerializedObject(service);
            serialized.FindProperty("facility").objectReferenceValue = facility;
            serialized.FindProperty("eatActivityId").stringValue = "Eat";
            serialized.FindProperty("hungerRecoveryPerGameHour").floatValue = recovery;
            serialized.FindProperty("requiresStaff").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(service);
        }

        private static void ConfigureOffDuty(
            OffDutyComponent offDuty,
            InteractableFacility facility,
            string activityId,
            float duration,
            float stimulation,
            float relaxation)
        {
            SerializedObject serialized = new SerializedObject(offDuty);
            serialized.FindProperty("facility").objectReferenceValue = facility;
            SerializedProperty activities = serialized.FindProperty("activities");
            activities.arraySize = 1;
            SerializedProperty activity = activities.GetArrayElementAtIndex(0);
            activity.FindPropertyRelative("activityId").stringValue = activityId;
            activity.FindPropertyRelative("plannedDurationGameHours").floatValue = duration;
            activity.FindPropertyRelative("enabled").boolValue = true;
            activity.FindPropertyRelative("requiresStaff").boolValue = false;
            activity.FindPropertyRelative("cooldownGameHours").floatValue = 2f;
            activity.FindPropertyRelative("cooldownKey").stringValue = "dance";
            activity.FindPropertyRelative("stimulationRecoveryPerGameHour").floatValue = stimulation;
            activity.FindPropertyRelative("relaxationRecoveryPerGameHour").floatValue = relaxation;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(offDuty);
        }

        private static void ConfigureWorkplace(
            WorkplaceComponent workplace,
            InteractableFacility facility,
            JobRoleDefinition role,
            string activityId,
            int capacity)
        {
            SerializedObject serialized = new SerializedObject(workplace);
            serialized.FindProperty("facility").objectReferenceValue = facility;
            SerializedProperty roles = serialized.FindProperty("roles");
            roles.arraySize = 1;
            SerializedProperty binding = roles.GetArrayElementAtIndex(0);
            binding.FindPropertyRelative("role").objectReferenceValue = role;
            binding.FindPropertyRelative("activityId").stringValue = activityId;
            binding.FindPropertyRelative("maximumConcurrentScheduledWorkers").intValue = capacity;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(workplace);
        }

        private static JobRoleDefinition CreateRoleAsset()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath(GeneratedFolder + "/StressWorkerRole.asset");
            JobRoleDefinition role = ScriptableObject.CreateInstance<JobRoleDefinition>();
            SerializedObject serialized = new SerializedObject(role);
            serialized.FindProperty("stableId").stringValue = "high_speed_stress_worker";
            serialized.FindProperty("displayName").stringValue = "Stress Worker";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(role, path);
            return role;
        }

        private static Vector3 GridPosition(
            int index,
            int columns,
            float originX,
            float originZ,
            float spacing)
        {
            int column = index % columns;
            int row = index / columns;
            return new Vector3(originX + column * spacing, 0f, originZ + row * spacing);
        }

        private static void SetArray(SerializedProperty property, UnityEngine.Object[] values)
        {
            property.arraySize = values != null ? values.Length : 0;
            for (int i = 0; i < property.arraySize; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static int FindEnumIndex(SerializedProperty property, string name)
        {
            string[] names = property.enumNames;
            for (int i = 0; i < names.Length; i++)
                if (string.Equals(names[i], name, StringComparison.Ordinal))
                    return i;
            return 0;
        }

        private static void SetPrivate(UnityEngine.Object target, string field, object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
                throw new InvalidOperationException(target.name + " has no serialized field " + field + ".");

            if (value is float floatValue)
                property.floatValue = floatValue;
            else if (value is int intValue)
                property.intValue = intValue;
            else if (value is bool boolValue)
                property.boolValue = boolValue;
            else if (value is string stringValue)
                property.stringValue = stringValue;
            else if (value is UnityEngine.Object objectValue)
                property.objectReferenceValue = objectValue;
            else if (value is ActivityTarget targetValue)
            {
                property.FindPropertyRelative("facility").objectReferenceValue = targetValue.Facility;
                property.FindPropertyRelative("activityId").stringValue = targetValue.ActivityId;
            }
            else
                throw new InvalidOperationException("Unsupported serialized value for " + field + ".");

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
