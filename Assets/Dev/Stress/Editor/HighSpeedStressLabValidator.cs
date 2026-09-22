using System;
using System.Collections.Generic;
using System.IO;
using AsteroidColony;
using Colony.Interactions;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace AsteroidColony.Stress.Editor
{
    public static class HighSpeedStressLabValidator
    {
        private const string ReportPath = "Assets/Dev/Stress/Generated/HighSpeedStressLabValidation.md";

        [MenuItem("Tools/Spacegame/Validate High-Speed Population Stress Lab")]
        public static void ValidateActiveScene()
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            NavMeshSurface[] surfaces = UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Exclude);
            if (surfaces.Length == 0)
                errors.Add("No NavMeshSurface found.");

            ColonistActivityRunner[] actors = UnityEngine.Object.FindObjectsByType<ColonistActivityRunner>(FindObjectsInactive.Exclude);
            if (actors.Length == 0)
                errors.Add("No ColonistActivityRunner actors found.");

            for (int i = 0; i < actors.Length; i++)
            {
                ColonistActivityRunner runner = actors[i];
                if (runner == null)
                    continue;
                if (runner.GetComponent<ColonistBrain>() == null ||
                    runner.GetComponent<ColonistStatsComponent>() == null ||
                    runner.GetComponent<ColonistIdentity>() == null)
                {
                    errors.Add(runner.name + " is missing a required brain/stat/identity component.");
                }

                NavMeshAgent agent = runner.GetComponent<NavMeshAgent>();
                if (agent == null)
                    errors.Add(runner.name + " has no NavMeshAgent.");
                else if (!agent.isOnNavMesh)
                    warnings.Add(runner.name + " is not currently on a baked NavMesh; verify in Play Mode.");
            }

            InteractableFacility[] facilities = UnityEngine.Object.FindObjectsByType<InteractableFacility>(FindObjectsInactive.Exclude);
            for (int i = 0; i < facilities.Length; i++)
            {
                InteractableFacility facility = facilities[i];
                if (facility == null)
                    continue;
                IReadOnlyList<FacilityActivityBinding> bindings = facility.Activities;
                if (bindings == null || bindings.Count == 0)
                {
                    errors.Add(facility.name + " has no activity binding.");
                    continue;
                }

                for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
                {
                    FacilityActivityBinding binding = bindings[bindingIndex];
                    if (binding == null ||
                        string.IsNullOrWhiteSpace(binding.ActivityId) ||
                        string.IsNullOrWhiteSpace(binding.ReservationGroup) ||
                        !binding.ExternallyRequestable ||
                        binding.ApproachAnchor == null)
                    {
                        errors.Add(facility.name + " has an unusable activity binding at index " + bindingIndex + ".");
                    }
                }
            }

            string report = BuildReport(SceneManager.GetActiveScene(), actors.Length, facilities.Length, errors, warnings);
            string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllText(absolutePath, report);
            AssetDatabase.Refresh();

            if (errors.Count == 0)
                Debug.Log("High-speed stress lab validation passed with " + warnings.Count + " warnings. Report: " + ReportPath);
            else
                Debug.LogError("High-speed stress lab validation found " + errors.Count + " errors. Report: " + ReportPath);
        }

        private static string BuildReport(
            Scene scene,
            int actorCount,
            int facilityCount,
            List<string> errors,
            List<string> warnings)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.AppendLine("# High-Speed Stress Lab Validation");
            builder.AppendLine();
            builder.Append("- Scene: `").Append(scene.name).AppendLine("`");
            builder.Append("- Actors: `").Append(actorCount).AppendLine("`");
            builder.Append("- Facilities: `").Append(facilityCount).AppendLine("`");
            builder.AppendLine();
            builder.AppendLine("## Errors");
            AppendItems(builder, errors);
            builder.AppendLine();
            builder.AppendLine("## Warnings");
            AppendItems(builder, warnings);
            return builder.ToString();
        }

        private static void AppendItems(System.Text.StringBuilder builder, List<string> items)
        {
            if (items.Count == 0)
            {
                builder.AppendLine("- None");
                return;
            }

            for (int i = 0; i < items.Count; i++)
                builder.Append("- ").AppendLine(items[i]);
        }
    }
}
