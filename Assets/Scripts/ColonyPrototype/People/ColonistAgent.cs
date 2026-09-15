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
        OnDutyCrew
    }

    /// <summary>
    /// A single colonist. Logical location is tracked via LocationAnchor references;
    /// the transform may simply be moved/parented when the location changes (no walking).
    /// </summary>
    public class ColonistAgent : MonoBehaviour
    {
        public string displayName;
        public LocationAnchor home;
        public LocationAnchor currentLocation;
        public LocationAnchor assignedWorkplace;
        public ColonistActivity activity;
        public List<WorkerClassDefinition> classes = new List<WorkerClassDefinition>();
        public List<SkillRating> skills = new List<SkillRating>();

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

        private void OnValidate()
        {
            for (int i = 0; i < skills.Count; i++)
                if (skills[i] != null)
                    skills[i].Clamp();
        }

        private void Start()
        {
            if (PopulationManager.Instance != null)
                PopulationManager.Instance.Register(this);
        }

        private void OnDestroy()
        {
            if (PopulationManager.Instance != null)
                PopulationManager.Instance.Unregister(this);
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
