using Unity.Collections;

namespace DCL.ECS.GlobalPartitioning
{
    /// <summary>
    ///     Reduction Tier Is Assigned to a batch entity
    /// </summary>
    public struct BatchTier
    {
        public const sbyte CULLED = -1;

        public readonly struct TierCalculation
        {
            /// <summary>
            ///     Weight needed for the tier
            /// </summary>
            public readonly int Weight;

            public TierCalculation(int weight)
            {
                Weight = weight;
            }
        }

        internal readonly FixedList32Bytes<TierCalculation> possibleTiers;

        /// <param name="possibleTiers">Include all possible tiers up to the desired one</param>
        public BatchTier(FixedList32Bytes<TierCalculation> possibleTiers)
        {
            this.possibleTiers = possibleTiers;
            currentTierIndex = -1; // Culled
        }

        internal sbyte currentTierIndex { get; private set; }

        internal TierCalculation currentTier => possibleTiers.IsEmpty ? default(TierCalculation) : possibleTiers[currentTierIndex];

        public sbyte DesiredTier => (sbyte)(possibleTiers.Length - 1);

        /// <summary>
        ///     Batch is culled if there was a failed attempt to allocate memory for a batch
        /// </summary>
        public bool Culled => currentTierIndex == CULLED;

        public void SetTier(byte index)
        {
            currentTierIndex = (sbyte)index;
        }

        public void Cull()
        {
            currentTierIndex = CULLED;
        }
    }
}
