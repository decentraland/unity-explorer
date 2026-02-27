using DCL.AvatarRendering.Wearables.Components;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public readonly struct TrimmedWearablesResponse
    {
        public readonly IReadOnlyList<ITrimmedWearable> Wearables;
        public readonly int TotalAmount;

        public TrimmedWearablesResponse(IReadOnlyList<ITrimmedWearable> wearables, int totalAmount)
        {
            Wearables = wearables;
            TotalAmount = totalAmount;
        }
    }
}
