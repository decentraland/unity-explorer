using DCL.AvatarRendering.Wearables.Components;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public struct WearablesResponse
    {
        public readonly IReadOnlyList<ITrimmedWearable> Wearables;
        public readonly int TotalAmount;

        public WearablesResponse(IReadOnlyList<ITrimmedWearable> wearables, int totalAmount)
        {
            Wearables = wearables;
            TotalAmount = totalAmount;
        }
    }
}
