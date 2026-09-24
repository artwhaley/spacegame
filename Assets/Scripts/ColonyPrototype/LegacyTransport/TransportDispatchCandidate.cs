namespace AsteroidColony
{
    /// <summary>
    /// Internal candidate used by ordinary transport arbitration. It is deliberately
    /// narrower than a universal action or work-order abstraction.
    /// </summary>
    public sealed class TransportDispatchCandidate
    {
        public TransportContract passengerContract;
        public FreightDemand freightDemand;
        public FreightSupply freightSupply;
        public float legalQuantity;
        public int priority;
        public FreightDemandClass demandClass;
        public double age;
        public int stableId;

        public bool IsFreight => freightDemand != null;
    }
}
