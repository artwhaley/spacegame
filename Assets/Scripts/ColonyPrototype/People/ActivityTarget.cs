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

        public InteractableFacility Facility => facility;
        public string ActivityId => activityId;

        public bool IsConfigured =>
            facility != null &&
            !string.IsNullOrWhiteSpace(activityId);
    }
}
