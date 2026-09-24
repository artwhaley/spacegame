using System;
using UnityEngine;

namespace Colony.Interactions
{
    /// <summary>
    /// Package-neutral seam for selecting and starting the physical approach to
    /// an activity. The game assembly binds this to PersonnelRoutingManager.
    /// </summary>
    public interface IActivityApproachRouter
    {
        bool TryStartRoute(
            Transform destination,
            out string failureReason);

        void StopRoute();
    }
}
