using System;
using UnityEngine;

namespace AsteroidColony
{
    [Serializable]
    public sealed class WorkAssignment
    {
        [SerializeField]
        private ColonistIdentity colonist;

        [SerializeField]
        private WorkplaceComponent workplace;

        [SerializeField]
        private JobRoleDefinition role;

        [SerializeField]
        private DailyShiftWindow shift;

        public WorkAssignment()
        {
        }

        public WorkAssignment(
            ColonistIdentity colonist,
            WorkplaceComponent workplace,
            JobRoleDefinition role,
            DailyShiftWindow shift)
        {
            this.colonist = colonist;
            this.workplace = workplace;
            this.role = role;
            this.shift = shift;
        }

        public ColonistIdentity Colonist => colonist;
        public WorkplaceComponent Workplace => workplace;
        public JobRoleDefinition Role => role;
        public DailyShiftWindow Shift => shift;

        public bool IsConfigured =>
            colonist != null &&
            workplace != null &&
            role != null &&
            shift != null &&
            shift.IsConfigured;
    }
}
