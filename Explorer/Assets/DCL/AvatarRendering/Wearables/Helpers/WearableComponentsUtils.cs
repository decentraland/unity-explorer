using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.Optimization.Pools;
using System.Buffers;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public static class WearableComponentsUtils
    {
        internal static readonly ListObjectPool<string> POINTERS_POOL = new (listInstanceDefaultCapacity: 10, defaultCapacity: 20);
        internal static readonly ArrayPool<IWearable> RESULTS_POOL = ArrayPool<IWearable>.Create(20, 20);

        public static GetWearablesByPointersIntention CreateGetWearablesByPointersIntention(BodyShape bodyShape, IList<string> wearables)
        {
            List<string> pointers = POINTERS_POOL.Get();
            pointers.Add(bodyShape);
            pointers.AddRange(wearables);

            IWearable[] results = RESULTS_POOL.Rent(pointers.Count);
            return new GetWearablesByPointersIntention(pointers, results, bodyShape);
        }
    }
}
