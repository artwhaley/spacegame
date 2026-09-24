using System.Globalization;
using System.Text;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Builds deterministic scene/hierarchy keys for ordering and diagnostics.</summary>
    public static class SceneStableIdentity
    {
        public static string GetKey(Object target)
        {
            if (target == null)
                return string.Empty;

            Transform current = target is Transform transform
                ? transform
                : target is Component component
                    ? component.transform
                    : target is GameObject gameObject
                        ? gameObject.transform
                        : null;
            if (current == null)
                return target.name;

            StringBuilder key = new StringBuilder();
            string scenePath = current.gameObject.scene.path;
            key.Append(string.IsNullOrEmpty(scenePath)
                ? current.gameObject.scene.name
                : scenePath);

            string hierarchy = string.Empty;
            for (Transform cursor = current; cursor != null; cursor = cursor.parent)
            {
                hierarchy = cursor.name + "[" +
                    cursor.GetSiblingIndex().ToString(CultureInfo.InvariantCulture) + "]/" +
                    hierarchy;
            }
            return key.Append('/').Append(hierarchy).ToString();
        }
    }
}
