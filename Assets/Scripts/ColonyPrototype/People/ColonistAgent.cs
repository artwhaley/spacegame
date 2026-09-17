using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    public enum ColonistActivity
    {
        Idle,
        WaitingForTransport,
        Passenger,
        Working,
        Resting,
        Sleeping,
        OnDutyCrew
    }

    /// <summary>
    /// A single colonist. Logical location is tracked via LocationAnchor references;
    /// the transform may simply be moved/parented when the location changes (no walking).
    /// Classes are hard job eligibility, skills are independent 0..1 bonuses, and at
    /// most one current employment assignment may exist at a time.
    /// </summary>
    [RequireComponent(typeof(ColonistStatusComponent))]
    public class ColonistAgent : MonoBehaviour
    {
        public string displayName;
        public LocationAnchor home;
        public LocationAnchor currentLocation;
        public ColonistActivity activity;
        public List<WorkerClassDefinition> classes = new List<WorkerClassDefinition>();
        public List<SkillRating> skills = new List<SkillRating>();

        public EmploymentAssignment currentEmployment;

        public ColonistStatusComponent Status => GetComponent<ColonistStatusComponent>();
        public ColonistDutyState CurrentDutyState => Status != null
            ? Status.CurrentDutyState : ColonistDutyState.ReleasedResting;
        public bool IsEmployed => currentEmployment != null && currentEmployment.IsAssigned;

        public bool HasClass(WorkerClassDefinition requiredClass)
        {
            if (requiredClass == null)
                return false;
            for (int i = 0; i < classes.Count; i++)
                if (classes[i] == requiredClass)
                    return true;
            return false;
        }

        public float GetSkill(SkillDefinition requestedSkill)
        {
            if (requestedSkill == null)
                return 0f;
            for (int i = 0; i < skills.Count; i++)
            {
                SkillRating rating = skills[i];
                if (rating != null && rating.skill == requestedSkill)
                {
                    rating.Clamp();
                    return rating.proficiency;
                }
            }
            return 0f;
        }

        /// <summary>Reports authoring problems without mutating the lists.</summary>
        public void CollectAuthoringErrors(List<string> errors)
        {
            if (errors == null)
                return;

            var seenClasses = new HashSet<WorkerClassDefinition>();
            for (int i = 0; i < classes.Count; i++)
            {
                WorkerClassDefinition workerClass = classes[i];
                if (workerClass == null)
                    errors.Add($"{name}: class entry {i} is null.");
                else if (!seenClasses.Add(workerClass))
                    errors.Add($"{name}: duplicate class '{workerClass.displayName}'.");
            }

            var seenSkills = new HashSet<SkillDefinition>();
            for (int i = 0; i < skills.Count; i++)
            {
                SkillRating rating = skills[i];
                if (rating == null)
                {
                    errors.Add($"{name}: skill entry {i} is null.");
                    continue;
                }
                if (rating.skill == null)
                {
                    errors.Add($"{name}: skill entry {i} has a null skill definition.");
                    continue;
                }
                if (!seenSkills.Add(rating.skill))
                    errors.Add($"{name}: duplicate skill '{rating.skill.displayName}'.");
            }
        }

        private void OnValidate()
        {
            for (int i = 0; i < skills.Count; i++)
                if (skills[i] != null)
                    skills[i].Clamp();

            var errors = new List<string>();
            CollectAuthoringErrors(errors);
            for (int i = 0; i < errors.Count; i++)
                Debug.LogError($"Colonist authoring: {errors[i]}", this);
        }

        private void OnEnable()
        {
            if (PopulationManager.Instance != null)
                PopulationManager.Instance.Register(this);
            if (StaffingManager.Instance != null)
                StaffingManager.Instance.RegisterColonist(this);
        }

        private void OnDisable()
        {
            if (PopulationManager.Instance != null)
                PopulationManager.Instance.Unregister(this);
            if (StaffingManager.Instance != null)
                StaffingManager.Instance.UnregisterColonist(this);
        }

        private void OnDestroy()
        {
            if (PopulationManager.Instance != null)
                PopulationManager.Instance.Unregister(this);
            if (StaffingManager.Instance != null)
                StaffingManager.Instance.UnregisterColonist(this);
        }

        /// <summary>Changes the colonist's logical location and parents the transform to the anchor.</summary>
        public void MoveToLocation(LocationAnchor newLocation)
        {
            currentLocation = newLocation;
            if (newLocation != null)
                transform.SetParent(newLocation.transform, true);
        }
    }
}
