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

        public bool TryGetOngoingRequest(string key, out UniTaskCompletionSource<StreamableLoadingResult<TAsset>?> ongoingRequest) =>
            OngoingRequests.TryGetValue(key, out ongoingRequest);

        public void AddOngoingRequest(string key, UniTaskCompletionSource<StreamableLoadingResult<TAsset>?> ongoingRequest) =>
            OngoingRequests.Add(key, ongoingRequest);

        public void RemoveOngoingRequest(string key) =>
            OngoingRequests.Remove(key);

        private IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<TAsset>?>> OngoingRequests { get; }


    }
}
