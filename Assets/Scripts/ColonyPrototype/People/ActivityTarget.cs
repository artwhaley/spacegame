using System;
using Colony.Interactions;
using UnityEngine;

namespace AsteroidColony
{
    [Serializable]
    public sealed class ActivityTarget
    {
        [SerializeField]
        private InteractableFacility facility;

        [SerializeField]
        private string activityId;

        public ActivityTarget()
        {
        }

        public ActivityTarget(
            InteractableFacility facility,
            string activityId)
        {
            this.facility = facility;
            this.activityId = activityId;
        }

        public InteractableFacility Facility => facility;
        public string ActivityId => activityId;

        public bool IsConfigured =>
            facility != null &&
            !string.IsNullOrWhiteSpace(activityId);
    }
}
