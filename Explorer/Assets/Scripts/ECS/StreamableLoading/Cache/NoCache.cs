using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace ECS.StreamableLoading.Cache
{
    /// <summary>
    ///     Null implementation to provide no caching capabilities explicitly
    /// </summary>
    /// <typeparam name="TAsset"></typeparam>
    /// <typeparam name="TLoadingIntention"></typeparam>
    public class NoCache<TAsset, TLoadingIntention> : IStreamableCache<TAsset, TLoadingIntention> where TLoadingIntention: struct, ILoadingIntention
    {
        public static readonly NoCache<TAsset, TLoadingIntention> INSTANCE = new (false, false);

        private readonly bool useOngoingRequestCache;
        private readonly bool useIrrecoverableFailureCache;

        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<TAsset>?>> OngoingRequests { get; }
        public IDictionary<string, StreamableLoadingResult<TAsset>> IrrecoverableFailures { get; }

        private bool disposed { get; set; }

        public NoCache(bool useOngoingRequestCache, bool useIrrecoverableFailureCache)
        {
            this.useOngoingRequestCache = useOngoingRequestCache;
            this.useIrrecoverableFailureCache = useIrrecoverableFailureCache;

            if (useOngoingRequestCache)
                OngoingRequests = DictionaryPool<string, UniTaskCompletionSource<StreamableLoadingResult<TAsset>?>>.Get();
            else
                OngoingRequests = new FakeDictionaryCache<UniTaskCompletionSource<StreamableLoadingResult<TAsset>?>>();

            if (useIrrecoverableFailureCache)
                IrrecoverableFailures = DictionaryPool<string, StreamableLoadingResult<TAsset>>.Get();
            else
                IrrecoverableFailures = new FakeDictionaryCache<StreamableLoadingResult<TAsset>>();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (useOngoingRequestCache)
                DictionaryPool<string, UniTaskCompletionSource<StreamableLoadingResult<TAsset>?>>.Release(OngoingRequests as Dictionary<string, UniTaskCompletionSource<StreamableLoadingResult<TAsset>?>>);

            if (useIrrecoverableFailureCache)
                DictionaryPool<string, StreamableLoadingResult<TAsset>>.Release(IrrecoverableFailures as Dictionary<string, StreamableLoadingResult<TAsset>>);

            disposed = true;
        }

        public bool TryGet(in TLoadingIntention key, out TAsset asset)
        {
            asset = default(TAsset);
            return false;
        }

        public void Add(in TLoadingIntention key, TAsset asset) { }

        public void Unload(IPerformanceBudget frameTimeBudget, int maxUnloadAmount) { }

        public void UnloadImmediate()
        {
        }

        bool IEqualityComparer<TLoadingIntention>.Equals(TLoadingIntention x, TLoadingIntention y) =>
            EqualityComparer<TLoadingIntention>.Default.Equals(x, y);

        int IEqualityComparer<TLoadingIntention>.GetHashCode(TLoadingIntention obj) =>
            EqualityComparer<TLoadingIntention>.Default.GetHashCode(obj);
    }
}
