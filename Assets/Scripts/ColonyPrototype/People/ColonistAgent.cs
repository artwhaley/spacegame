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
    /// A single colonist. currentLocation is the last arrived logical location;
    /// transit fields describe a movement that has not committed arrival yet.
    /// Classes are hard job eligibility, skills are independent 0..1 bonuses, and at
    /// most one current employment assignment may exist at a time.
    /// </summary>
    [RequireComponent(typeof(ColonistStatusComponent))]
    public class ColonistAgent : MonoBehaviour
    {
        public string displayName;
        public LocationAnchor home;
        public LocationAnchor currentLocation;
        [SerializeField] private LocationAnchor transitOrigin;
        [SerializeField] private LocationAnchor transitDestination;
        [SerializeField] private bool inTransit;
        [SerializeField] private string transitKind;
        public ColonistActivity activity;
        public List<WorkerClassDefinition> classes = new List<WorkerClassDefinition>();
        public List<SkillRating> skills = new List<SkillRating>();

        public EmploymentAssignment currentEmployment;

        public ColonistStatusComponent Status => GetComponent<ColonistStatusComponent>();
        public ColonistDutyState CurrentDutyState => Status != null
            ? Status.CurrentDutyState : ColonistDutyState.ReleasedResting;
        public bool IsEmployed => currentEmployment != null && currentEmployment.IsAssigned;
        public bool IsInTransit => inTransit;
        public LocationAnchor TransitOrigin => transitOrigin;
        public LocationAnchor TransitDestination => transitDestination;
        public string TransitKind => transitKind;

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
            // Disable is a temporary experiment, not destruction. Population and
            // employment registries retain this identity so capacity cannot open
            // a false slot while the GameObject is inactive.
        }

        private void OnDestroy()
        {
            if (PopulationManager.Instance != null)
                PopulationManager.Instance.Unregister(this);
            if (StaffingManager.Instance != null)
                StaffingManager.Instance.UnregisterColonist(this);
        }

        /// <summary>Instant transition helper used by tests and completed handoffs.</summary>
        public void MoveToLocation(LocationAnchor newLocation)
        {
            BeginTransit(currentLocation, newLocation, "instant");
            CompleteTransit();
        }

        /// <summary>Starts a transition without changing the last arrived location.</summary>
        public bool BeginTransit(LocationAnchor origin, LocationAnchor destination, string kind = "walking")
        {
            if (destination == null)
                return false;
            if (inTransit && transitDestination != destination)
                return false;

            transitOrigin = origin != null ? origin : currentLocation;
            transitDestination = destination;
            transitKind = string.IsNullOrEmpty(kind) ? "walking" : kind;
            inTransit = true;
            return true;
        }

        /// <summary>Commits the destination exactly once after the transition completes.</summary>
        public bool CompleteTransit()
        {
            if (!inTransit || transitDestination == null)
                return false;

            LocationAnchor arrived = transitDestination;
            currentLocation = arrived;
            transitOrigin = null;
            transitDestination = null;
            transitKind = string.Empty;
            inTransit = false;
            ReadinessHistory.Record("colonist.arrival", displayName, arrived.displayName);
            return true;
        }
    }
}
