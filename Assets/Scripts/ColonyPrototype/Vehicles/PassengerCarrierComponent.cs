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

        public bool CanBoardPassengers(List<ColonistAgent> passengers, LocationAnchor source, out string reason)
        {
            reason = "available";
            if (passengers == null || passengers.Count == 0)
            {
                reason = "empty passenger manifest";
                return false;
            }
            if (source == null || CarrierLocation == null)
            {
                reason = "missing source or carrier anchor";
                return false;
            }
            if (currentPassengers.Count + passengers.Count > passengerCapacity)
            {
                reason = "passenger capacity exceeded";
                return false;
            }
            HashSet<ColonistAgent> manifestSeen = new HashSet<ColonistAgent>();
            for (int i = 0; i < passengers.Count; i++)
            {
                ColonistAgent passenger = passengers[i];
                if (passenger == null)
                {
                    reason = "manifest contains a null passenger";
                    return false;
                }
                if (passenger == ship?.ResponsiblePilot)
                {
                    reason = "manifest contains the carrier's responsible pilot";
                    return false;
                }
                if (passenger.currentLocation != source)
                {
                    reason = $"{passenger.displayName} is not at pickup";
                    return false;
                }
                if (currentPassengers.Contains(passenger))
                {
                    reason = $"{passenger.displayName} is already aboard";
                    return false;
                }
                if (!manifestSeen.Add(passenger))
                {
                    reason = $"{passenger.displayName} appears more than once";
                    return false;
                }
            }
            return true;
        }

        public bool CanBoardPassenger(ColonistAgent passenger, LocationAnchor source, out string reason)
        {
            return CanBoardPassengers(new List<ColonistAgent> { passenger }, source, out reason);
        }

        public bool TryBoardPassengers(List<ColonistAgent> passengers, LocationAnchor source)
        {
            string reason;
            if (!CanBoardPassengers(passengers, source, out reason))
                return false;

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
