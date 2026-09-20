using System;
using System.Collections.Generic;
using UnityEngine;

namespace Colony.Interactions
{
    public sealed class InteractableFacility : MonoBehaviour
    {
        [SerializeField] private FacilityActivityBinding[] activities =
            Array.Empty<FacilityActivityBinding>();
        [SerializeField] private FacilitySequenceBinding[] sequences =
            Array.Empty<FacilitySequenceBinding>();

        private readonly Dictionary<string, FacilityReservationToken> reservations =
            new Dictionary<string, FacilityReservationToken>(StringComparer.Ordinal);

        private readonly Dictionary<string, int> reservationGenerations =
            new Dictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyList<FacilityActivityBinding> Activities => activities;
        public IReadOnlyList<FacilitySequenceBinding> Sequences => sequences;

        public void AddEmptyActivityBinding()
        {
            FacilityActivityBinding[] expanded =
                new FacilityActivityBinding[activities.Length + 1];
            activities.CopyTo(expanded, 0);
            expanded[expanded.Length - 1] = new FacilityActivityBinding();
            activities = expanded;
        }

        public void AddEmptySequenceBinding()
        {
            FacilitySequenceBinding[] expanded =
                new FacilitySequenceBinding[sequences.Length + 1];
            sequences.CopyTo(expanded, 0);
            expanded[expanded.Length - 1] = new FacilitySequenceBinding();
            sequences = expanded;
        }

        public bool TryGetBinding(string activityId, out FacilityActivityBinding binding)
        {
            for (int index = 0; index < activities.Length; index++)
            {
                FacilityActivityBinding candidate = activities[index];
                if (candidate != null &&
                    string.Equals(candidate.ActivityId, activityId, StringComparison.Ordinal))
                {
                    binding = candidate;
                    return true;
                }
            }

            binding = null;
            return false;
        }

        public bool TryGetSequence(
            string sequenceId,
            out FacilitySequenceBinding sequence)
        {
            for (int index = 0; index < sequences.Length; index++)
            {
                FacilitySequenceBinding candidate = sequences[index];
                if (candidate != null &&
                    string.Equals(candidate.SequenceId, sequenceId, StringComparison.Ordinal))
                {
                    sequence = candidate;
                    return true;
                }
            }

            sequence = null;
            return false;
        }

        public bool TryAcquire(
            string reservationGroup,
            UnityEngine.Object owner,
            out FacilityReservationToken token)
        {
            token = null;

            if (string.IsNullOrWhiteSpace(reservationGroup) || owner == null)
            {
                return false;
            }

            if (reservations.ContainsKey(reservationGroup))
            {
                return false;
            }

            int generation = 1;
            if (reservationGenerations.TryGetValue(reservationGroup, out int previousGeneration))
            {
                generation = previousGeneration + 1;
            }

            reservationGenerations[reservationGroup] = generation;
            token = new FacilityReservationToken(this, reservationGroup, owner, generation);
            reservations.Add(reservationGroup, token);
            return true;
        }

        public bool Release(FacilityReservationToken token)
        {
            if (token == null ||
                !ReferenceEquals(token.Facility, this) ||
                token.IsReleased)
            {
                return false;
            }

            if (!reservations.TryGetValue(token.ReservationGroup, out FacilityReservationToken current) ||
                !ReferenceEquals(current, token))
            {
                return false;
            }

            reservations.Remove(token.ReservationGroup);
            token.MarkReleased();
            return true;
        }

        public bool IsReserved(string reservationGroup)
        {
            return !string.IsNullOrWhiteSpace(reservationGroup) &&
                   reservations.ContainsKey(reservationGroup);
        }
    }
}
