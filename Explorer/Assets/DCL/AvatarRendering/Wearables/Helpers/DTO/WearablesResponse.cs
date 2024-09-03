using DCL.AvatarRendering.Wearables.Components;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public struct WearablesResponse
    {
        public readonly IReadOnlyList<IWearable> Wearables;

        public WearablesResponse(IReadOnlyList<IWearable> wearables)
        {
            Wearables = wearables;
        }
    }
}
