using UnityEngine;
using UnityEngine.SceneManagement;

namespace AsteroidColony.Editor
{
    /// <summary>Rejects the pre-P4b transport authorities from modern fixture scenes.</summary>
    internal static class LegacyTransportSceneGuard
    {
        public static int Validate(Scene scene, string fixtureName)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return 0;

            int errors = 0;
            errors += Check<LogisticsManager>(scene, fixtureName);
            errors += Check<ContractManager>(scene, fixtureName);
            errors += Check<TransportExecutorComponent>(scene, fixtureName);
            return errors;
        }

        private static int Check<T>(Scene scene, string fixtureName) where T : Behaviour
        {
            int errors = 0;
            T[] components = Resources.FindObjectsOfTypeAll<T>();
            for (int index = 0; index < components.Length; index++)
            {
                T component = components[index];
                if (component == null || component.gameObject.scene != scene ||
                    !component.isActiveAndEnabled)
                {
                    continue;
                }

                Debug.LogError(fixtureName + " uses active legacy transport authority " +
                    component.GetType().Name + " on " + component.gameObject.name +
                    ". Use FreightLogisticsManager and modern freight contracts instead.", component);
                errors++;
            }
            return errors;
        }
    }
}
