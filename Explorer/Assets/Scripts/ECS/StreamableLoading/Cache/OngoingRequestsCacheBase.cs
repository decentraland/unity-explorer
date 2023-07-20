using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;

namespace ECS.StreamableLoading.Cache
{
    public abstract class OngoingRequestsCacheBase<TAsset>
    {
        protected OngoingRequestsCacheBase()
        {
            OngoingRequests = new Dictionary<string, UniTaskCompletionSource<StreamableLoadingResult<TAsset>?>>(256);
        }

        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<TAsset>?>> OngoingRequests { get; }
    }
}
