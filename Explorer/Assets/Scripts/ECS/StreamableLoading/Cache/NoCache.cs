using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;

namespace ECS.StreamableLoading.Cache
{
    /// <summary>
    ///     Null implementation to provide no caching capabilities explicitly
    /// </summary>
    /// <typeparam name="TAsset"></typeparam>
    /// <typeparam name="TLoadingIntention"></typeparam>
    public class NoCache<TAsset, TLoadingIntention> : OngoingRequestsCacheBase<TAsset>, IStreamableCache<TAsset, TLoadingIntention> where TLoadingIntention: struct, ILoadingIntention
    {
        public static readonly NoCache<TAsset, TLoadingIntention> INSTANCE = new (false);

        bool IEqualityComparer<TLoadingIntention>.Equals(TLoadingIntention x, TLoadingIntention y) =>
            EqualityComparer<TLoadingIntention>.Default.Equals(x, y);

        int IEqualityComparer<TLoadingIntention>.GetHashCode(TLoadingIntention obj) =>
            EqualityComparer<TLoadingIntention>.Default.GetHashCode(obj);

        private readonly bool useOngoingRequestCache;

        public NoCache(bool useOngoingRequestCache)
        {
            this.useOngoingRequestCache = useOngoingRequestCache;
        }

        bool IStreamableCache<TAsset, TLoadingIntention>.TryGetOngoingRequest(string key, out UniTaskCompletionSource<StreamableLoadingResult<TAsset>?> ongoingRequest)
        {
            if (useOngoingRequestCache)
                return base.TryGetOngoingRequest(key, out ongoingRequest);

            ongoingRequest = null;
            return false;
        }

        void IStreamableCache<TAsset, TLoadingIntention>.RemoveOngoingRequest(string key)
        {
            if (useOngoingRequestCache)
                base.RemoveOngoingRequest(key);
        }

        void IStreamableCache<TAsset, TLoadingIntention>.AddOngoingRequest(string key, UniTaskCompletionSource<StreamableLoadingResult<TAsset>?> ongoingRequest)
        {
            if (useOngoingRequestCache)
                base.AddOngoingRequest(key, ongoingRequest);
        }

        bool IStreamableCache<TAsset, TLoadingIntention>.TryGet(in TLoadingIntention key, out TAsset asset)
        {
            asset = default(TAsset);
            return false;
        }

        void IStreamableCache<TAsset, TLoadingIntention>.Add(in TLoadingIntention key, TAsset asset) { }

        void IStreamableCache<TAsset, TLoadingIntention>.Dereference(in TLoadingIntention key, TAsset asset) { }
    }
}
