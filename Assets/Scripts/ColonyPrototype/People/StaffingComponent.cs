using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Observes manually assigned workers at a workplace. It does not allocate
    /// jobs or control commuting; it only reports existing physical/activity state.
    /// </summary>
    public class StaffingComponent : MonoBehaviour
    {
        public LocationAnchor workplace;
        public List<ColonistAgent> assignedWorkers = new List<ColonistAgent>();

        public int AssignedCount => CountAssigned();
        public int PresentCount => CountWorkers(false, false);
        public int WorkingCount => CountWorkers(true, true);

        public IReadOnlyList<ColonistAgent> GetWorkingWorkers()
        {
            List<ColonistAgent> working = new List<ColonistAgent>();
            for (int i = 0; i < assignedWorkers.Count; i++)
            {
                ColonistAgent worker = assignedWorkers[i];
                if (worker != null && worker.currentLocation == workplace &&
                    worker.activity == ColonistActivity.Working)
                    working.Add(worker);
            }
            return working;
        }

        public int CountWorkingWithClass(WorkerClassDefinition requiredClass)
        {
            if (requiredClass == null)
                return WorkingCount;

            int count = 0;
            for (int i = 0; i < assignedWorkers.Count; i++)
            {
                ColonistAgent worker = assignedWorkers[i];
                if (worker != null && worker.currentLocation == workplace &&
                    worker.activity == ColonistActivity.Working && worker.HasClass(requiredClass))
                    count++;
            }
            return count;
        }

        private int CountAssigned()
        {
            int count = 0;
            for (int i = 0; i < assignedWorkers.Count; i++)
                if (assignedWorkers[i] != null)
                    count++;
            return count;
        }

        private int CountWorkers(bool requireWorking, bool requirePresent)
        {
            int count = 0;
            for (int i = 0; i < assignedWorkers.Count; i++)
            {
                ColonistAgent worker = assignedWorkers[i];
                if (worker == null)
                    continue;
                if (requirePresent && worker.currentLocation != workplace)
                    continue;
                if (requireWorking && worker.activity != ColonistActivity.Working)
                    continue;
                count++;
            }
            return count;
        }
    }
}
