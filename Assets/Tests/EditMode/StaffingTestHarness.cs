using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AsteroidColony.Tests
{
    /// <summary>Shared object/asset ownership for staffing tests. Not a test class.</summary>
    public class StaffingTestHarness
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<Object> assets = new List<Object>();

        public GameObject New(string objectName)
        {
            GameObject created = new GameObject(objectName);
            objects.Add(created);
            return created;
        }

        public T Asset<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            assets.Add(asset);
            return asset;
        }

        public WorkerClassDefinition Class(string displayName)
        {
            WorkerClassDefinition definition = Asset<WorkerClassDefinition>();
            definition.displayName = displayName;
            return definition;
        }

        public SkillDefinition Skill(string displayName)
        {
            SkillDefinition definition = Asset<SkillDefinition>();
            definition.displayName = displayName;
            return definition;
        }

        public FacilityEffectDefinition Effect(string displayName)
        {
            FacilityEffectDefinition definition = Asset<FacilityEffectDefinition>();
            definition.displayName = displayName;
            return definition;
        }

        public ShiftPatternDefinition Pattern(params (string id, float start, float duration)[] shifts)
        {
            ShiftPatternDefinition pattern = Asset<ShiftPatternDefinition>();
            pattern.stableId = "test-pattern";
            for (int i = 0; i < shifts.Length; i++)
                pattern.shifts.Add(new ShiftDefinition
                {
                    shiftId = shifts[i].id,
                    displayName = shifts[i].id,
                    startHour = shifts[i].start,
                    durationHours = shifts[i].duration
                });
            return pattern;
        }

        /// <summary>Builds a facility GameObject with staffing + performance components.</summary>
        public StaffingComponent Facility(
            string name, LocationAnchor location, ShiftPatternDefinition pattern, params StaffingRoleDefinition[] roles)
        {
            GameObject go = New(name);
            if (location == null)
                location = go.AddComponent<LocationAnchor>();
            StaffingComponent staffing = go.AddComponent<StaffingComponent>();
            staffing.workplaceLocation = location;
            staffing.shiftPattern = pattern;
            for (int i = 0; i < roles.Length; i++)
                staffing.offeredRoles.Add(roles[i]);
            go.AddComponent<FacilityPerformanceComponent>();
            return staffing;
        }

        public ColonistAgent Colonist(
            string displayName, LocationAnchor home, params WorkerClassDefinition[] classes)
        {
            ColonistAgent colonist = New(displayName).AddComponent<ColonistAgent>();
            colonist.displayName = displayName;
            colonist.home = home;
            colonist.currentLocation = home;
            for (int i = 0; i < classes.Length; i++)
                if (classes[i] != null)
                    colonist.classes.Add(classes[i]);
            return colonist;
        }

        public void SetGameHour(SimulationManager clock, float hour)
        {
            FieldInfo field = typeof(SimulationManager).GetField(
                "currentGameHour", BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(clock, hour);
        }

        public void Dispose()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null)
                    Object.DestroyImmediate(objects[i]);
            objects.Clear();

            for (int i = assets.Count - 1; i >= 0; i--)
                if (assets[i] != null)
                    Object.DestroyImmediate(assets[i]);
            assets.Clear();
        }
    }
}
