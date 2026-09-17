using System;

namespace AsteroidColony
{
    /// <summary>
    /// One persistent employment record. Ships use the same workplace/role/shift
    /// shape as facilities; a sibling ShipComponent identifies a crew roster
    /// without adding a second employment model.
    /// </summary>
    [Serializable]
    public class EmploymentAssignment
    {
        public StaffingComponent workplace;
        public StaffingRoleDefinition role;
        public string shiftId;

        public EmploymentAssignment()
        {
        }

        public EmploymentAssignment(StaffingComponent workplace, StaffingRoleDefinition role, string shiftId)
        {
            this.workplace = workplace;
            this.role = role;
            this.shiftId = shiftId;
        }

        public bool IsAssigned => workplace != null && role != null && !string.IsNullOrEmpty(shiftId);

        public string Describe()
        {
            string workplaceName = workplace != null ? workplace.DisplayName : "<none>";
            string roleName = role != null ? role.displayName : "<none>";
            return $"{workplaceName} / {roleName} / {shiftId}";
        }
    }
}
