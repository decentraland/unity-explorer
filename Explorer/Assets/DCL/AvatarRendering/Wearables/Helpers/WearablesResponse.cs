using DCL.AvatarRendering.Wearables.Components;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public struct WearablesResponse
    {
        public readonly IWearable[] Wearables;
        public readonly int TotalAmount;

        public WearablesResponse(IWearable[] wearables, int totalAmount)
        {
            Wearables = wearables;
            TotalAmount = totalAmount;
        }
    }
}
