using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace ECS.StreamableLoading.Cache
{
    /// <summary>
    ///     Null implementation to provide no caching capabilities explicitly
    /// </summary>
    /// <typeparam name="TAsset"></typeparam>
    /// <typeparam name="TLoadingIntention"></typeparam>
    public class NoCache<TAsset, TLoadingIntention> : IStreamableCache<TAsset, TLoadingIntention> 
        where TLoadingIntention: struct, ILoadingIntention, IEquatable<TLoadingIntention>
    {
        public static readonly NoCache<TAsset, TLoadingIntention> INSTANCE = new (false, false);

        private readonly bool useOngoingRequestCache;
        private readonly bool useIrrecoverableFailureCache;

        public IDictionary<IntentionsComparer<TLoadingIntention>.SourcedIntentionId, UniTaskCompletionSource<OngoingRequestResult<TAsset>>> OngoingRequests { get; }
        public IDictionary<IntentionsComparer<TLoadingIntention>.SourcedIntentionId, StreamableLoadingResult<TAsset>?> IrrecoverableFailures { get; }

        private bool disposed { get; set; }

        public NoCache(bool useOngoingRequestCache, bool useIrrecoverableFailureCache)
        {
            this.useOngoingRequestCache = useOngoingRequestCache;
            this.useIrrecoverableFailureCache = useIrrecoverableFailureCache;

            if (useOngoingRequestCache)
                OngoingRequests = DictionaryPool<IntentionsComparer<TLoadingIntention>.SourcedIntentionId, UniTaskCompletionSource<OngoingRequestResult<TAsset>>>.Get();
            else
                OngoingRequests = new FakeDictionaryCache<IntentionsComparer<TLoadingIntention>.SourcedIntentionId, UniTaskCompletionSource<OngoingRequestResult<TAsset>>>();

            if (useIrrecoverableFailureCache)
                IrrecoverableFailures = DictionaryPool<IntentionsComparer<TLoadingIntention>.SourcedIntentionId, StreamableLoadingResult<TAsset>?>.Get();
            else
                IrrecoverableFailures = new FakeDictionaryCache<IntentionsComparer<TLoadingIntention>.SourcedIntentionId, StreamableLoadingResult<TAsset>?>();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (useOngoingRequestCache)
                DictionaryPool<IntentionsComparer<TLoadingIntention>.SourcedIntentionId, 
                    UniTaskCompletionSource<StreamableLoadingResult<TAsset>?>>.Release(
                    OngoingRequests as Dictionary<IntentionsComparer<TLoadingIntention>.SourcedIntentionId, UniTaskCompletionSource<StreamableLoadingResult<TAsset>?>>);

            if (useIrrecoverableFailureCache)
                DictionaryPool<IntentionsComparer<TLoadingIntention>.SourcedIntentionId, StreamableLoadingResult<TAsset>>
                    .Release(IrrecoverableFailures as Dictionary<IntentionsComparer<TLoadingIntention>.SourcedIntentionId, 
                        StreamableLoadingResult<TAsset>>);

            disposed = true;
        }

        public bool TryGet(in TLoadingIntention key, out TAsset asset)
        {
            asset = default(TAsset);
            return false;
        }

        public void Add(in TLoadingIntention key, TAsset asset) { }

        public void Unload(IPerformanceBudget frameTimeBudget, int maxUnloadAmount) { }
    }
}
