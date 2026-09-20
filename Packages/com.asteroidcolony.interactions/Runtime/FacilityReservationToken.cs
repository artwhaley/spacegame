using UnityEngine;

namespace Colony.Interactions
{
    public sealed class FacilityReservationToken
    {
        internal FacilityReservationToken(
            InteractableFacility facility,
            string reservationGroup,
            Object owner,
            int generation)
        {
            Facility = facility;
            ReservationGroup = reservationGroup;
            Owner = owner;
            Generation = generation;
        }

        public InteractableFacility Facility { get; }
        public string ReservationGroup { get; }
        public Object Owner { get; }
        public int Generation { get; }
        public bool IsReleased { get; private set; }

        internal void MarkReleased()
        {
            IsReleased = true;
        }
    }
}
