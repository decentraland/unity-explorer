using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Components
{
    public class WearableDTOCache : IStreamableCache<WearableDTO, GetWearableIntention>
    {
        private readonly Dictionary<GetWearableIntention, WearableDTO> cache;

        public WearableDTOCache()
        {
            cache = new Dictionary<GetWearableIntention, WearableDTO>();
        }

        public bool Equals(GetWearableIntention x, GetWearableIntention y) =>
            x.Pointer == y.Pointer;

        public int GetHashCode(GetWearableIntention obj) =>
            obj.Pointer.GetHashCode();

        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<WearableDTO>?>> OngoingRequests => new Dictionary<string, UniTaskCompletionSource<StreamableLoadingResult<WearableDTO>?>>();

        public bool TryGet(in GetWearableIntention key, out WearableDTO asset) =>
            cache.TryGetValue(key, out asset);

        public void Add(in GetWearableIntention key, WearableDTO asset)
        {
            cache.Add(key, asset);
        }

        public void Dereference(in GetWearableIntention key, WearableDTO asset) { }
    }
}
