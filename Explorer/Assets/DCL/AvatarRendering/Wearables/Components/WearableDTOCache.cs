using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Components
{
    public class WearableDTOCache : IStreamableCache<WearableDTO, GetWearableByPointersIntention>
    {
        private readonly Dictionary<GetWearableByPointersIntention, WearableDTO> cache;

        public WearableDTOCache()
        {
            cache = new Dictionary<GetWearableByPointersIntention, WearableDTO>();
        }

        public bool Equals(GetWearableByPointersIntention x, GetWearableByPointersIntention y) =>
            x.Pointers == y.Pointers;

        public int GetHashCode(GetWearableByPointersIntention obj) =>
            obj.Pointers.GetHashCode();

        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<WearableDTO>?>> OngoingRequests => new Dictionary<string, UniTaskCompletionSource<StreamableLoadingResult<WearableDTO>?>>();

        public bool TryGet(in GetWearableByPointersIntention key, out WearableDTO asset) =>
            cache.TryGetValue(key, out asset);

        public void Add(in GetWearableByPointersIntention key, WearableDTO asset)
        {
            cache.Add(key, asset);
        }

        public void Dereference(in GetWearableByPointersIntention key, WearableDTO asset) { }
    }
}
