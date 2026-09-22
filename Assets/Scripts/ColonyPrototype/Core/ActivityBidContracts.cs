using System;
using UnityEngine;

namespace AsteroidColony
{
    /// <summary>
    /// Small, domain-neutral identity shared by the two round-based opportunity systems.
    /// This file intentionally keeps the contract narrow; policy remains in ColonistBrain.
    /// Domain facts remain on FoodBid and OffDutyBid instead of being forced into this type.
    /// </summary>
    public readonly struct ActivityBidIdentity
    {
        public ActivityBidIdentity(
            ColonistIdentity requester,
            long sourceSimulationTick,
            int brainPreferenceRank)
        {
            Requester = requester;
            SourceSimulationTick = sourceSimulationTick;
            BrainPreferenceRank = brainPreferenceRank;
        }

        public ColonistIdentity Requester { get; }
        public long SourceSimulationTick { get; }
        public int BrainPreferenceRank { get; }
    }

    public sealed class FoodBid
    {
        public FoodBid(
            ColonistIdentity requester,
            Vector3 position,
            float currentGameHour,
            float hunger,
            bool isCritical,
            int brainPreferenceRank,
            long sourceSimulationTick,
            long outstandingNeedAge)
        {
            Identity = new ActivityBidIdentity(
                requester,
                sourceSimulationTick,
                brainPreferenceRank);
            Position = position;
            CurrentGameHour = currentGameHour;
            Hunger = hunger;
            IsCritical = isCritical;
            OutstandingNeedAge = outstandingNeedAge;
        }

        public ActivityBidIdentity Identity { get; }
        public ColonistIdentity Requester => Identity.Requester;
        public Vector3 Position { get; }
        public float CurrentGameHour { get; }
        public float Hunger { get; }
        public bool IsCritical { get; }
        public long OutstandingNeedAge { get; }
    }

    public sealed class FoodOffer
    {
        public FoodOffer(
            FoodBid bid,
            FoodServiceOpportunity opportunity,
            long validForSimulationTick)
        {
            Bid = bid;
            Opportunity = opportunity;
            ValidForSimulationTick = validForSimulationTick;
        }

        public FoodBid Bid { get; }
        public ColonistIdentity Requester => Bid?.Requester;
        public FoodServiceOpportunity Opportunity { get; }
        public long ValidForSimulationTick { get; }
        public bool IsValidFor(long simulationTick) => simulationTick == ValidForSimulationTick;
    }

    public sealed class OffDutyBid
    {
        public OffDutyBid(
            ColonistIdentity requester,
            Vector3 position,
            OffDutyDrive desiredDrive,
            float maximumSafeDurationGameHours,
            OffDutyCompletionHistory completionHistory,
            float currentGameHour,
            int brainPreferenceRank,
            long sourceSimulationTick)
        {
            Identity = new ActivityBidIdentity(
                requester,
                sourceSimulationTick,
                brainPreferenceRank);
            Position = position;
            DesiredDrive = desiredDrive;
            MaximumSafeDurationGameHours = maximumSafeDurationGameHours;
            CompletionHistory = completionHistory;
            CurrentGameHour = currentGameHour;
        }

        public ActivityBidIdentity Identity { get; }
        public ColonistIdentity Requester => Identity.Requester;
        public Vector3 Position { get; }
        public OffDutyDrive DesiredDrive { get; }
        public float MaximumSafeDurationGameHours { get; }
        public OffDutyCompletionHistory CompletionHistory { get; }
        public float CurrentGameHour { get; }
    }

    public sealed class OffDutyOffer
    {
        public OffDutyOffer(
            OffDutyBid bid,
            OffDutyOpportunity opportunity,
            long validForSimulationTick)
        {
            Bid = bid;
            Opportunity = opportunity;
            ValidForSimulationTick = validForSimulationTick;
        }

        public OffDutyBid Bid { get; }
        public ColonistIdentity Requester => Bid?.Requester;
        public OffDutyOpportunity Opportunity { get; }
        public long ValidForSimulationTick { get; }
        public bool IsValidFor(long simulationTick) => simulationTick == ValidForSimulationTick;
    }

    public sealed class FoodMealCommitment
    {
        internal FoodMealCommitment(
            ColonistIdentity requester,
            FoodServiceComponent service,
            InventoryComponent inventory,
            ResourceDefinition resource,
            float amount)
        {
            Requester = requester;
            Service = service;
            Inventory = inventory;
            Resource = resource;
            Amount = amount;
        }

        public ColonistIdentity Requester { get; }
        public FoodServiceComponent Service { get; }
        public InventoryComponent Inventory { get; }
        public ResourceDefinition Resource { get; }
        public float Amount { get; }
        public bool Reserved { get; internal set; }
        public bool Consumed { get; internal set; }
        public bool Released { get; internal set; }
    }
}
