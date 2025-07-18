using Arch.Core;
using AssetManagement;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Cache.Generic;
using ECS.StreamableLoading.Cache.InMemory;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Utility;
using Utility.Types;

namespace ECS.StreamableLoading.Common.Systems
{
    /// <summary>
    ///     All-in-one system that handles the live cycle of Unity web requests and Caching.
    ///     It was created for the sake of simplicity
    /// </summary>
    /// <typeparam name="TAsset"></typeparam>
    /// <typeparam name="TIntention"></typeparam>
    public abstract class LoadSystemBase<TAsset, TIntention> : BaseUnityLoopSystem where TIntention: struct, ILoadingIntention, IEquatable<TIntention>
    {
        private static readonly QueryDescription CREATE_WEB_REQUEST = new QueryDescription()
                                                                     .WithAll<TIntention, IPartitionComponent, StreamableLoadingState>()
                                                                     .WithNone<StreamableLoadingResult<TAsset>>();

        private readonly IStreamableCache<TAsset, TIntention> cache;

        /// <summary>
        ///     If the disk cache is not provided, it mean the disk cache is not supported for this particular asset type
        /// </summary>
        private readonly IGenericCache<TAsset, TIntention> genericCache;

        private readonly AssetsLoadingUtility.InternalFlowDelegate<TAsset, StreamableLoadingState, TIntention> cachedInternalFlowDelegate;
        private readonly Query query;
        private readonly CancellationTokenSource cancellationTokenSource;

        private bool systemIsDisposed;

        protected LoadSystemBase(World world, IStreamableCache<TAsset, TIntention> cache, DiskCacheOptions<TAsset, TIntention>? diskCacheOptions = null) : base(world)
        {
            this.cache = cache;

            var memoryCache = new StreamableWrapMemoryCache<TAsset, TIntention>(cache);

            if (diskCacheOptions == null)
                genericCache = new MemoryOnlyGenericCache<TAsset, TIntention>(memoryCache);
            else
                genericCache = new GenericCache<TAsset, TIntention>(
                    new StreamableWrapMemoryCache<TAsset, TIntention>(cache),
                    diskCacheOptions.Value.DiskCache,
                    diskCacheOptions.Value.DiskHashCompute,
                    diskCacheOptions.Value.Extension
                );

            query = World!.Query(in CREATE_WEB_REQUEST);
            cachedInternalFlowDelegate = FlowInternalAsync;
            cancellationTokenSource = new CancellationTokenSource();
        }

        protected override void OnDispose()
        {
            systemIsDisposed = true;

            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        protected override void Update(float t)
        {
            foreach (ref Chunk chunk in query.GetChunkIterator())
            {
                ref Entity entityFirstElement = ref chunk.Entity(0);
                ref TIntention intentionFirstElement = ref chunk.GetFirst<TIntention>();
                ref IPartitionComponent partitionComponentFirstElement = ref chunk.GetFirst<IPartitionComponent>();
                ref StreamableLoadingState stateFirstElement = ref chunk.GetFirst<StreamableLoadingState>();

                foreach (int entityIndex in chunk)
                {
                    ref readonly Entity entity = ref Unsafe.Add(ref entityFirstElement, entityIndex);
                    ref TIntention intention = ref Unsafe.Add(ref intentionFirstElement, entityIndex);
                    ref IPartitionComponent partitionComponent = ref Unsafe.Add(ref partitionComponentFirstElement, entityIndex)!;
                    ref StreamableLoadingState state = ref Unsafe.Add(ref stateFirstElement, entityIndex);

                    Execute(entity, ref state, ref intention, ref partitionComponent);
                }
            }
        }

        private void Execute(Entity entity, ref StreamableLoadingState state, ref TIntention intention, ref IPartitionComponent partitionComponent)
        {
            AssetSource currentSource = intention.CommonArguments.CurrentSource;

            var intentionId = new IntentionsComparer<TIntention>.SourcedIntentionId(intention, currentSource);
                
            //If a chunk is already loading, don't start another one, if it is a partial request it will resume from the point it was stopped
            if (state.Value != StreamableLoadingState.Status.Allowed)
            {
                // If state is in progress the flow was already launched and it will call FinalizeLoading on its own
                // If state is finished the asset is already resolved and cancellation can be ignored
                if (state.Value != StreamableLoadingState.Status.InProgress && state.Value != StreamableLoadingState.Status.Finished && intention.CancellationTokenSource.IsCancellationRequested)

                    // If we don't finalize promises preemptively they are being stacked in DeferredLoadingSystem
                    // if it's unable to keep up with their number
                    FinalizeLoading(entity, intention, null, currentSource, state);

                return;
            }

            // Indicate that loading has started
            state.StartProgress();

            FlowAsync(entity, currentSource, intention, state, partitionComponent, intentionId, cancellationTokenSource.Token).Forget();
        }

        private async UniTask FlowAsync(
            Entity entity,
            AssetSource source,
            TIntention intention,
            StreamableLoadingState state,
            IPartitionComponent partition,
            IntentionsComparer<TIntention>.SourcedIntentionId intentionId,
            CancellationToken disposalCt
        )
        {
            StreamableLoadingResult<TAsset>? result = null;

            try
            {
                var requestIsNotFulfilled = true;

                // if the request is cached wait for it
                // If there is an ongoing request it means that the result is neither cached, nor failed
                if (cache.OngoingRequests.SyncTryGetValue(intentionId, out UniTaskCompletionSource<OngoingRequestResult<TAsset>>? cachedSource))
                {
                    // Release budget immediately, if we don't do it and load a lot of bundles with dependencies sequentially, it will be a deadlock
                    state.AcquiredBudget?.Release();

                    OngoingRequestResult<TAsset> ongoingRequestResult;

                    // if the cached request is cancelled it does not mean failure for the new intent
                    (requestIsNotFulfilled, ongoingRequestResult) = await cachedSource.Task.SuppressCancellationThrow();

                    //Temporarly disabled as we don't have partial loading integrated
                    //SynchronizePartialData(state, ongoingRequestResult);

                    result = ongoingRequestResult.Result;

                    if (requestIsNotFulfilled)
                    {
                        await FlowAsync(entity, source, intention, state, partition, intentionId, disposalCt);
                        return;
                    }
                }

                // If the given URL failed irrecoverably just return the failure
                if (cache.IrrecoverableFailures.TryGetValue(intentionId, out StreamableLoadingResult<TAsset>? failure))
                {
                    result = failure;
                    return;
                }

                // if this request must be cancelled by `intention.CommonArguments.CancellationToken` it will be cancelled after `if (!requestIsNotFulfilled)`
                if (requestIsNotFulfilled)
                    result = await CacheableFlowAsync(intention, state, partition, intentionId, CancellationTokenSource.CreateLinkedTokenSource(intention.CommonArguments.CancellationToken, disposalCt).Token);

                if (!result.HasValue)

                    // Indicate that it should be grabbed by another system
                    // finally will handle the rest
                    // ReSharper disable once RedundantJumpStatement
                    return;
            }
            catch (Exception e)
            {
                // If we don't set an exception it will spin forever
                result = new StreamableLoadingResult<TAsset>(GetReportCategory(), e);
            }
            finally { FinalizeLoading(entity, intention, result, source, state); }
        }

        /// <summary>
        ///     Synchronizes Partial Loading Data of the request that waiting for another requests of the same Asset to finish
        ///     <para>
        ///         Provokes Partial Data to be copied in order to keep both promises independent
        ///     </para>
        /// </summary>
        private static void SynchronizePartialData(StreamableLoadingState state, in OngoingRequestResult<TAsset> ongoingRequestResult)
        {
            state.PartialDownloadingData?.Dispose();
            state.PartialDownloadingData = null;

            if (ongoingRequestResult is { PartialDownloadingData: { FullyDownloaded: false } })
                state.PartialDownloadingData = new PartialLoadingState(ongoingRequestResult.PartialDownloadingData.Value);
        }

        protected virtual void DisposeAbandonedResult(TAsset asset) { }

        private void FinalizeLoading(Entity entity, TIntention intention,
            StreamableLoadingResult<TAsset>? result, AssetSource source,
            StreamableLoadingState state)
        {
            if (IsWorldInvalid(entity, state.AcquiredBudget))
                return;

            state.DisposeBudgetIfExists();

            //Temporarly disabled as we don't have partial loading integrated
            // Special path for partial downloading
            /*if (state.PartialDownloadingData is { FullyDownloaded: false } && !cache.IrrecoverableFailures.TryGetValue(intention.CommonArguments.GetCacheableURL(), out _))
            {
                // Return the promise for re-evaluation
                state.RequestReevaluate();
                return;
            }*/

            // Remove current source flag from the permitted sources
            // it indicates that the current source was used
            // don't do it for partial requests
            intention.RemoveCurrentSource();

            World.Set(entity, intention);

            if (result.HasValue)
                ApplyLoadedResult(entity, state, intention, result, source);
            else if (intention.IsCancelled())
            {
                if (World.IsAlive(entity))
                    World.Destroy(entity);
            }
            else
                state.RequestReevaluate();
        }

        private void ApplyLoadedResult(Entity entity, StreamableLoadingState state, TIntention intention, StreamableLoadingResult<TAsset>? result, AssetSource source)
        {
            state.Finish();

            // If we make a structural change before changing refs it will invalidate them, take care!!!
            World!.Add(entity, result!.Value);

            if (result.Value.Succeeded)
            {
                IncreaseRefCount(in intention, result.Value.Asset!);

                ReportHub.Log(GetReportData(), $"{intention}'s successfully loaded from {source}");
            }
        }

        private bool IsWorldInvalid(Entity entity, IAcquiredBudget? acquiredBudget)
        {
            if (systemIsDisposed || !World!.IsAlive(entity))
            {
                // World is no longer valid, can't call World.Get
                // Just Free the budget
                acquiredBudget?.Dispose();
                return true;
            }

            return false;
        }

        private void IncreaseRefCount(in TIntention intention, TAsset asset)
        {
            cache.AddReference(in intention, asset);
        }

        /// <summary>
        ///     All exceptions are handled by the upper functions, just do pure work
        /// </summary>
        protected abstract UniTask<StreamableLoadingResult<TAsset>> FlowInternalAsync(TIntention intention, 
            StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct);

        /// <summary>
        ///     Part of the flow that can be reused by multiple intentions
        /// </summary>
        private async UniTask<StreamableLoadingResult<TAsset>?> CacheableFlowAsync(TIntention intention, 
            StreamableLoadingState state, IPartitionComponent partition, 
            IntentionsComparer<TIntention>.SourcedIntentionId intentionId, CancellationToken ct)
        {
            var source = new UniTaskCompletionSource<OngoingRequestResult<TAsset>>(); //AutoResetUniTaskCompletionSource<StreamableLoadingResult<TAsset>?>.Create();
            
            cache.OngoingRequests.SyncTryAdd(intentionId, source);
            var ongoingRequestRemoved = false;

            StreamableLoadingResult<TAsset>? result = null;

            try
            {
                // Try load from cache first
                result = await TryLoadFromCacheAsync(intention, ct) ?? await RepeatLoopAsync(intention, state, partition, intentionId, ct);

                // Ensure that we returned to the main thread
                await UniTask.SwitchToMainThread(ct);

                // before firing the continuation of the ongoing request
                // Add result to the cache
                if (result is { Succeeded: true })
                    genericCache
                       .PutAsync(intention, result.Value.Asset!, intention.IsQualifiedForDiskCache(), ct)
                       .Forget(
                            static e =>
                                ReportHub.LogError(ReportCategory.STREAMABLE_LOADING, $"Error putting cache content: {e.Message}")
                        );

                // Set result for the reusable source
                // Remove from the ongoing requests immediately because finally will be called later than
                // continuation of cachedSource.Task.SuppressCancellationThrow();
                TryRemoveOngoingRequest();

                source.TrySetResult(new OngoingRequestResult<TAsset>(state.PartialDownloadingData, result));

                // if result is null the next available source will be picked by another system
                // which will prepare proper `Intention` accordingly
                // (e.g. if in StreamingAssets the requested asset is not present, arguments to download from WEB source will be prepared separately)
                return result;
            }
            catch (OperationCanceledException operationCanceledException)
            {
                if (result is { Succeeded: true })
                    DisposeAbandonedResult(result.Value.Asset!);

                // Remove from the ongoing requests immediately because finally will be called later than
                // continuation of cachedSource.Task.SuppressCancellationThrow();
                TryRemoveOngoingRequest();

                // Cancellation does not produce asset result
                source.TrySetCanceled(operationCanceledException.CancellationToken);
                throw;
            }
            finally
            {
                // We need to remove the request the same frame to prevent de-sync with new requests
                TryRemoveOngoingRequest();
            }

            void TryRemoveOngoingRequest()
            {
                if (!ongoingRequestRemoved)
                {
                    // ReportHub.Log(GetReportCategory(), $"OngoingRequests.SyncRemove {intention.CommonArguments.URL}");
                    cache.OngoingRequests.SyncRemove(intentionId);
                    ongoingRequestRemoved = true;
                }
            }
        }

        private async UniTask<StreamableLoadingResult<TAsset>?> TryLoadFromCacheAsync(TIntention intention, CancellationToken ct)
        {
            EnumResult<Option<TAsset>, TaskError> cachedContent = await genericCache.ContentAsync(intention, intention.IsQualifiedForDiskCache(), ct);

            if (cachedContent.Success)
            {
                Option<TAsset> option = cachedContent.Value;

                if (option.Has)
                    return new StreamableLoadingResult<TAsset>(option.Value);
            }

            return null;
        }

        private async UniTask<StreamableLoadingResult<TAsset>?> RepeatLoopAsync(TIntention intention, 
            StreamableLoadingState state, IPartitionComponent partition, 
            IntentionsComparer<TIntention>.SourcedIntentionId intentionId, CancellationToken ct)
        {
            StreamableLoadingResult<TAsset>? result = await intention.RepeatLoopAsync(state, partition, 
                cachedInternalFlowDelegate, GetReportData(), ct);
            return result is { Succeeded: false, IsInitialized: true } ? SetIrrecoverableFailure(intention, 
                intentionId, result.Value) : result;
        }

        private StreamableLoadingResult<TAsset> SetIrrecoverableFailure(TIntention intention, 
            IntentionsComparer<TIntention>.SourcedIntentionId intentionId, StreamableLoadingResult<TAsset> failure)
        {
            bool result = cache.IrrecoverableFailures.SyncTryAdd(intentionId, failure);
            if (result == false) ReportHub.LogError(GetReportData(), $"Irrecoverable failure for {intention} is already added");
            return failure;
        }
    }
}
