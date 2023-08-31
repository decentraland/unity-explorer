using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Components
{
    /// <summary>
    ///     Needed because ongoing requests compares by using the url. In this case, url is always the same
    /// </summary>
    public class WearablesByPointersCache : IStreamableCache<WearableDTO[], GetWearableByPointersIntention>
    {
        //TODO: How can we avoid this OnGoingRequests issue cache?
        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<WearableDTO[]>?>> OngoingRequests => new Dictionary<string, UniTaskCompletionSource<StreamableLoadingResult<WearableDTO[]>?>>();

        public bool TryGet(in GetWearableByPointersIntention key, out WearableDTO[] asset)
        {
            asset = new WearableDTO[] { };
            return false;
        }

        public void Add(in GetWearableByPointersIntention key, WearableDTO[] asset) { }

        public void Dereference(in GetWearableByPointersIntention key, WearableDTO[] asset) { }

        public bool Equals(GetWearableByPointersIntention x, GetWearableByPointersIntention y) =>
            EqualityComparer<GetWearableByPointersIntention>.Default.Equals(x, y);

        public int GetHashCode(GetWearableByPointersIntention obj) =>
            EqualityComparer<GetWearableByPointersIntention>.Default.GetHashCode(obj);
    }
}
