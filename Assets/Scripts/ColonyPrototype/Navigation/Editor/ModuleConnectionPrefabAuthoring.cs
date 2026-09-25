using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace AsteroidColony.Editor
{
    /// <summary>
    /// Idempotent authoring pass for the five current room-module prefabs. It
    /// creates the runtime connection components and anchors, moves each room's
    /// surface to its root, and bakes the room's own hierarchy.
    /// </summary>
    public static class ModuleConnectionPrefabAuthoring
    {
        private const float WalkAnchorInset = 0.5f;
        private const float DefaultPartnerDistance = 0.25f;
        private const float DefaultLinkWidth = 1.0f;

        private static readonly string[] ModulePrefabPaths =
        {
            "Assets/Prefabs/Airlock.prefab",
            "Assets/Prefabs/Cafeteria.prefab",
            "Assets/Prefabs/CommandCenter.prefab",
            "Assets/Prefabs/Disco.prefab",
            "Assets/Prefabs/Farm.prefab"
        };

        [MenuItem("Colony/Navigation/Author Module Connection Prefabs")]
        public static void AuthorAllModulePrefabs()
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < ModulePrefabPaths.Length; i++)
                    AuthorPrefab(ModulePrefabPaths[i]);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Debug.Log("Authored module connection points, WalkAnchors, root NavMeshSurfaces, and independent bakes for the five room prefabs.");
        }

        [MenuItem("Colony/Navigation/Validate Module Connection Prefabs")]
        public static void ValidateAllModulePrefabs()
        {
            int errors = 0;
            for (int i = 0; i < ModulePrefabPaths.Length; i++)
                errors += ValidatePrefab(ModulePrefabPaths[i]);

            if (errors == 0)
                Debug.Log("Module connection prefab validation passed: all expected points, anchors, and root surfaces are present.");
            else
                Debug.LogError("Module connection prefab validation found " + errors + " error(s). See the preceding messages.");
        }

        private static void AuthorPrefab(string prefabPath)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            if (contents == null)
            {
                Debug.LogError("Could not load module prefab " + prefabPath + ".");
                return;
            }

            try
            {
                NavMeshSurface surface = EnsureRootSurface(contents, prefabPath);
                Transform[] connectionTransforms = FindConnectionTransforms(contents);
                if (connectionTransforms.Length == 0)
                {
                    Debug.LogError("No nodeConnect transforms found in " + prefabPath + ".", contents);
                    return;
                }

                for (int i = 0; i < connectionTransforms.Length; i++)
                {
                    Transform node = connectionTransforms[i];
                    Transform anchor = EnsureWalkAnchor(node);
                    ModuleConnectionPoint point = node.GetComponent<ModuleConnectionPoint>();
                    if (point == null)
                        point = node.gameObject.AddComponent<ModuleConnectionPoint>();

                    SerializedObject serializedPoint = new SerializedObject(point);
                    serializedPoint.FindProperty("ownerSurface").objectReferenceValue = surface;
                    serializedPoint.FindProperty("walkAnchor").objectReferenceValue = anchor;
                    serializedPoint.FindProperty("maxPartnerDistance").floatValue = DefaultPartnerDistance;
                    serializedPoint.FindProperty("linkWidth").floatValue = DefaultLinkWidth;
                    serializedPoint.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(point);
                }

                surface.collectObjects = CollectObjects.Children;
                BakeAndPersistNavMesh(surface, prefabPath, contents);
                EditorUtility.SetDirty(surface);
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                Debug.Log("Authored and baked " + prefabPath + " with " + connectionTransforms.Length + " connection point(s).");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, contents);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
        private static NavMeshSurface EnsureRootSurface(GameObject root, string prefabPath)
        {
            NavMeshSurface rootSurface = root.GetComponent<NavMeshSurface>();
            NavMeshSurface[] allSurfaces = root.GetComponentsInChildren<NavMeshSurface>(true);

            if (rootSurface == null)
            {
                NavMeshSurface source = allSurfaces.Length > 0 ? allSurfaces[0] : null;
                rootSurface = root.AddComponent<NavMeshSurface>();
                if (source != null)
                    CopySurfaceSettings(source, rootSurface);
            }

            for (int i = 0; i < allSurfaces.Length; i++)
            {
                NavMeshSurface surface = allSurfaces[i];
                if (surface == null || surface == rootSurface)
                    continue;

                Debug.LogWarning(
                    "Removing superseded child NavMeshSurface " + surface.name + " while authoring " + prefabPath + ".",
                    root);
                UnityEngine.Object.DestroyImmediate(surface, true);
            }

            rootSurface.collectObjects = CollectObjects.Children;
            if (rootSurface.agentTypeID < 0)
                rootSurface.agentTypeID = 0;
            return rootSurface;
        }

        private static void BakeAndPersistNavMesh(
            NavMeshSurface surface,
            string prefabPath,
            GameObject contents)
        {
            // BuildNavMesh creates an in-memory NavMeshData. A prefab asset cannot
            // serialize that transient object, so persist it beside the prefab
            // before SaveAsPrefabAsset writes the surface reference.
            surface.navMeshData = null;
            surface.BuildNavMesh();

            NavMeshData bakedData = surface.navMeshData;
            if (bakedData == null)
            {
                throw new InvalidOperationException(
                    "Unity produced no NavMeshData for " + prefabPath + ". Check the room's walkable geometry and surface bounds.");
            }

            string directory = Path.GetDirectoryName(prefabPath);
            string assetPath = Path.Combine(directory, "NavMesh-" + SanitizeAssetName(contents.name) + ".asset")
                .Replace('\\', '/');

            NavMeshData previousData = AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath);
            if (previousData != null)
                AssetDatabase.DeleteAsset(assetPath);

            AssetDatabase.CreateAsset(bakedData, assetPath);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            surface.navMeshData = bakedData;
            EditorUtility.SetDirty(bakedData);
            AssetDatabase.SaveAssets();
        }

        private static string SanitizeAssetName(string value)
        {
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
                value = value.Replace(invalidCharacter.ToString(), "_");

            return string.IsNullOrEmpty(value) ? "Module" : value;
        }

        private static void CopySurfaceSettings(NavMeshSurface source, NavMeshSurface destination)
        {
            destination.agentTypeID = source.agentTypeID;
            destination.size = source.size;
            destination.center = source.center;
            destination.layerMask = source.layerMask;
            destination.useGeometry = source.useGeometry;
            destination.defaultArea = source.defaultArea;
            destination.ignoreNavMeshAgent = source.ignoreNavMeshAgent;
            destination.ignoreNavMeshObstacle = source.ignoreNavMeshObstacle;
            destination.overrideTileSize = source.overrideTileSize;
            destination.tileSize = source.tileSize;
            destination.overrideVoxelSize = source.overrideVoxelSize;
            destination.voxelSize = source.voxelSize;
            destination.minRegionArea = source.minRegionArea;
            destination.buildHeightMesh = source.buildHeightMesh;
        }

        private static Transform[] FindConnectionTransforms(GameObject root)
        {
            List<Transform> result = new List<Transform>();
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == "nodeConnect")
                    result.Add(transforms[i]);
            }

            result.Sort((a, b) => string.CompareOrdinal(GetHierarchyKey(a), GetHierarchyKey(b)));
            return result.ToArray();
        }

        private static Transform EnsureWalkAnchor(Transform node)
        {
            Transform anchor = null;
            for (int i = 0; i < node.childCount; i++)
            {
                Transform child = node.GetChild(i);
                if (child.name == "WalkAnchor")
                {
                    // Existing endpoint placement is authored geometry. Preserve it.
                    return child;
                }
            }

            if (anchor == null)
            {
                GameObject anchorObject = new GameObject("WalkAnchor");
                anchor = anchorObject.transform;
                anchor.SetParent(node, true);
            }

            // Existing nodeConnect forward axes point out through the doorway.
            // Put the endpoint half a metre back along -Z in world space so it
            // lands inside the room, even when a module is rotated or scaled.
            anchor.position = node.position - node.forward * WalkAnchorInset;
            anchor.rotation = node.rotation;
            anchor.localScale = Vector3.one;
            return anchor;
        }

        private static int ValidatePrefab(string prefabPath)
        {
            int errors = 0;
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            if (contents == null)
            {
                Debug.LogError("Could not load module prefab " + prefabPath + ".");
                return 1;
            }

            try
            {
                NavMeshSurface surface = contents.GetComponent<NavMeshSurface>();
                if (surface == null)
                {
                    Debug.LogError(prefabPath + " has no root NavMeshSurface.", contents);
                    errors++;
                }
                else if (surface.navMeshData == null)
                {
                    Debug.LogError(prefabPath + " root NavMeshSurface has no persisted NavMeshData bake.", surface);
                    errors++;
                }
                else if (surface.collectObjects != CollectObjects.Children)
                {
                    Debug.LogError(prefabPath + " root NavMeshSurface does not collect Current Object Hierarchy.", surface);
                    errors++;
                }

                Transform[] nodes = FindConnectionTransforms(contents);
                for (int i = 0; i < nodes.Length; i++)
                {
                    ModuleConnectionPoint point = nodes[i].GetComponent<ModuleConnectionPoint>();
                    Transform anchor = nodes[i].Find("WalkAnchor");
                    if (point == null)
                    {
                        Debug.LogError(nodes[i].GetHierarchyPath() + " has no ModuleConnectionPoint.", nodes[i]);
                        errors++;
                    }
                    else if (point.OwnerSurface != surface || point.WalkAnchor != anchor)
                    {
                        Debug.LogError(nodes[i].GetHierarchyPath() + " has incorrect connection references.", nodes[i]);
                        errors++;
                    }

                    if (anchor == null || Vector3.Distance(anchor.position, nodes[i].position) < WalkAnchorInset - 0.01f)
                    {
                        Debug.LogError(nodes[i].GetHierarchyPath() + " does not have a half-metre WalkAnchor inset.", nodes[i]);
                        errors++;
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            return errors;
        }

        private static string GetHierarchyKey(Transform transform)
        {
            List<int> indexes = new List<int>();
            Transform current = transform;
            while (current != null)
            {
                indexes.Add(current.GetSiblingIndex());
                current = current.parent;
            }

            indexes.Reverse();
            return string.Join("/", indexes) + "/" + transform.name;
        }

        private static string GetHierarchyPath(this Transform transform)
        {
            List<string> names = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }
    }
}
