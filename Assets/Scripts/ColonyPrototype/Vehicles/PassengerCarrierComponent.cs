using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>Physical passenger capacity and boarding operations for a vehicle.</summary>
    public class PassengerCarrierComponent : MonoBehaviour
    {
        public int passengerCapacity = 4;
        public ShipComponent ship;
        public List<ColonistAgent> currentPassengers = new List<ColonistAgent>();

        public IReadOnlyList<ColonistAgent> CurrentPassengers => currentPassengers;
        public int AboardCount => currentPassengers.Count;

        private LocationAnchor CarrierLocation => GetComponent<LocationAnchor>();

        private void Awake()
        {
            if (ship == null)
                ship = GetComponent<ShipComponent>();
            passengerCapacity = Mathf.Max(0, passengerCapacity);
            currentPassengers.Clear();
        }

        public bool TryBoardPassengers(List<ColonistAgent> passengers, LocationAnchor source)
        {
            if (passengers == null || source == null || CarrierLocation == null ||
                currentPassengers.Count + passengers.Count > passengerCapacity)
                return false;

            for (int i = 0; i < passengers.Count; i++)
            {
                ColonistAgent passenger = passengers[i];
                if (passenger == null || passenger == ship?.assignedPilot ||
                    passenger.currentLocation != source || currentPassengers.Contains(passenger))
                    return false;
            }

            for (int i = 0; i < passengers.Count; i++)
            {
                ColonistAgent passenger = passengers[i];
                passenger.MoveToLocation(CarrierLocation);
                passenger.activity = ColonistActivity.Passenger;
                currentPassengers.Add(passenger);
            }
            return true;
        }

        public bool TryUnboardPassengers(LocationAnchor destination)
        {
            if (destination == null || CarrierLocation == null)
                return false;

            for (int i = 0; i < currentPassengers.Count; i++)
            {
                ColonistAgent passenger = currentPassengers[i];
                if (passenger == null || passenger.currentLocation != CarrierLocation)
                    return false;
            }

            for (int i = 0; i < currentPassengers.Count; i++)
            {
                ColonistAgent passenger = currentPassengers[i];
                passenger.MoveToLocation(destination);
                passenger.activity = ColonistActivity.Idle;
            }
            currentPassengers.Clear();
            return true;
        }
    }
}
