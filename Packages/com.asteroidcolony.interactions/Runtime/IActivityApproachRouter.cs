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
        event Action<string> ApproachRouteCompleted;
        event Action<string, string> ApproachRouteFailed;

        bool TryStartRoute(
            Transform destination,
            out string routeId,
            out string failureReason);

        void StopRoute(string routeId);
    }
}
