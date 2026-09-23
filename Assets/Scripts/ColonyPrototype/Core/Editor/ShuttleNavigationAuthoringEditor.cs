using UnityEditor;
using UnityEngine;

namespace AsteroidColony.Editor
{
    [CustomEditor(typeof(ShuttleNavigationProfile))]
    internal sealed class ShuttleNavigationProfileEditor : UnityEditor.Editor
    {
        private Transform shuttleRoot;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Shuttle bounds helper", EditorStyles.boldLabel);
            shuttleRoot = (Transform)EditorGUILayout.ObjectField("Shuttle Root", shuttleRoot,
                typeof(Transform), true);
            using (new EditorGUI.DisabledScope(shuttleRoot == null))
            {
                if (GUILayout.Button("Recommend Navigation Radius From Bounds"))
                {
                    float radius = RecommendRadius(shuttleRoot);
                    ShuttleNavigationProfile profile = (ShuttleNavigationProfile)target;
                    Undo.RecordObject(profile, "Set Shuttle Navigation Radius");
                    profile.navigationRadius = Mathf.Max(0.01f, radius);
                    EditorUtility.SetDirty(profile);
                }
            }
        }

        private static float RecommendRadius(Transform root)
        {
            Vector3 origin = root.position;
            float radius = 0f;
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null || colliders[i].isTrigger) continue;
                radius = Mathf.Max(radius, BoundsRadius(colliders[i].bounds, origin));
            }
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null)
                    radius = Mathf.Max(radius, BoundsRadius(renderers[i].bounds, origin));
            return Mathf.Max(0.01f, radius);
        }

        private static float BoundsRadius(Bounds bounds, Vector3 origin)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            float furthest = 0f;
            for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                    for (int z = 0; z < 2; z++)
                        furthest = Mathf.Max(furthest, Vector3.Distance(origin,
                            new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y,
                                z == 0 ? min.z : max.z)));
            return furthest;
        }
    }

    [CustomEditor(typeof(ShuttleRoutePlanner))]
    internal sealed class ShuttleRoutePlannerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Draws the latest visibility candidates, validated graph edges, raw path, and simplified path in the Scene view.", MessageType.Info);
        }
    }

    internal static class SpaceNavigationObstacleMenu
    {
        [MenuItem("GameObject/Spacegame/Mark As Navigation Obstacle", false, 20)]
        private static void MarkSelected()
        {
            foreach (GameObject selected in Selection.gameObjects)
            {
                if (selected.GetComponent<SpaceNavigationObstacle>() == null)
                    Undo.AddComponent<SpaceNavigationObstacle>(selected);
            }
        }

        [MenuItem("GameObject/Spacegame/Mark As Navigation Obstacle", true)]
        private static bool CanMarkSelected() => Selection.gameObjects.Length > 0;
    }
}
