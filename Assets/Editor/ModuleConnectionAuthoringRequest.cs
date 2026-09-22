using System.IO;
using UnityEditor;

namespace AsteroidColony.Editor
{
    /// <summary>
    /// One-shot bridge for executing the requested prefab authoring pass in the
    /// already-open Unity Editor. The request asset is deleted before execution
    /// so a later script reload cannot repeat the bake.
    /// </summary>
    [InitializeOnLoad]
    internal static class ModuleConnectionAuthoringRequest
    {
        private const string RequestPath = "Assets/Editor/ModuleConnectionAuthoring.request";

        static ModuleConnectionAuthoringRequest()
        {
            EditorApplication.delayCall += TryExecute;
        }

        private static void TryExecute()
        {
            EditorApplication.delayCall -= TryExecute;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryExecute;
                return;
            }

            if (!File.Exists(RequestPath))
                return;

            AssetDatabase.DeleteAsset(RequestPath);
            ModuleConnectionPrefabAuthoring.AuthorAllModulePrefabs();
        }
    }
}
