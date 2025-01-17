﻿using Arch.Core;
using AssetManagement;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Utility;

namespace ECS.StreamableLoading.Common.Systems
{
    /// <summary>
    ///     All-in-one system that handles the live cycle of Unity web requests and Caching.
    ///     It was created for the sake of simplicity
    /// </summary>
    /// <typeparam name="TAsset"></typeparam>
    /// <typeparam name="TIntention"></typeparam>
    public abstract class LoadSystemBase<TAsset, TIntention> : BaseUnityLoopSystem where TIntention: struct, ILoadingIntention
    {
        private static readonly QueryDescription CREATE_WEB_REQUEST = new QueryDescription()
                                                                     .WithAll<TIntention, IPartitionComponent, StreamableLoadingState>()
                                                                     .WithNone<StreamableLoadingResult<TAsset>>();

        protected readonly IStreamableCache<TAsset, TIntention> cache;

        private readonly AssetsLoadingUtility.InternalFlowDelegate<TAsset, TIntention> cachedInternalFlowDelegate;
        private readonly Query query;
        private readonly CancellationTokenSource cancellationTokenSource;

        private bool systemIsDisposed;

        protected LoadSystemBase(World world, IStreamableCache<TAsset, TIntention> cache) : base(world)
        {
            this.cache = cache;
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

            EntityReference entityReference = World.Reference(entity);

            if (state.Value != StreamableLoadingState.Status.Allowed)
            {
                // If state is in progress the flow was already launched and it will call FinalizeLoading on its own
                // If state is finished the asset is already resolved and cancellation can be ignored
                if (state.Value != StreamableLoadingState.Status.InProgress && state.Value != StreamableLoadingState.Status.Finished && intention.CancellationTokenSource.IsCancellationRequested)

                    // If we don't finalize promises preemptively they are being stacked in DeferredLoadingSystem
                    // if it's unable to keep up with their number
                    FinalizeLoading(entityReference, intention, null, currentSource, state.AcquiredBudget);

                return;
            }

            // Remove current source flag from the permitted sources
            // it indicates that the current source was used
            intention.RemoveCurrentSource();

            // Indicate that loading has started
            state.StartProgress();

            FlowAsync(entityReference, currentSource, intention, state.AcquiredBudget!, partitionComponent, cancellationTokenSource.Token).Forget();
        }

        private async UniTask FlowAsync(
            EntityReference entity,
            AssetSource source,
            TIntention intention,
            IAcquiredBudget acquiredBudget,
            IPartitionComponent partition,
            CancellationToken disposalCt
        )
        {
            StreamableLoadingResult<TAsset>? result = null;

            try
            {
                var requestIsNotFulfilled = true;

                // if the request is cached wait for it
                // If there is an ongoing request it means that the result is neither cached, nor failed
                if (cache.OngoingRequests.SyncTryGetValue(intention.CommonArguments.GetCacheableURL(), out var cachedSource))
                {
                    // Release budget immediately, if we don't do it and load a lot of bundles with dependencies sequentially, it will be a deadlock
                    acquiredBudget.Release();

                    // if the cached request is cancelled it does not mean failure for the new intent
                    (requestIsNotFulfilled, result) = await cachedSource.Task.SuppressCancellationThrow();

                    if (requestIsNotFulfilled)
                    {
                        await FlowAsync(entity, source, intention, acquiredBudget, partition, disposalCt);
                        return;
                    }
                }

                if (cache.TryGet(intention, out TAsset asset))
                {
                    result = new StreamableLoadingResult<TAsset>(asset);
                    return;
                }

                // Try load from cache first

                // If the given URL failed irrecoverably just return the failure
                if (cache.IrrecoverableFailures.TryGetValue(intention.CommonArguments.GetCacheableURL(), out var failure))
                {
                    result = failure;
                    return;
                }

                // if this request must be cancelled by `intention.CommonArguments.CancellationToken` it will be cancelled after `if (!requestIsNotFulfilled)`
                if (requestIsNotFulfilled)
                    result = await CacheableFlowAsync(intention, acquiredBudget, partition, CancellationTokenSource.CreateLinkedTokenSource(intention.CommonArguments.CancellationToken, disposalCt).Token);

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
            finally { FinalizeLoading(entity, intention, result, source, acquiredBudget); }
        }

        protected virtual void DisposeAbandonedResult(TAsset asset) { }

        private void FinalizeLoading(EntityReference entity, TIntention intention,
            StreamableLoadingResult<TAsset>? result, AssetSource source,
            IAcquiredBudget? acquiredBudget)
        {
            if (IsWorldInvalid(entity, acquiredBudget))
                return;

            ref StreamableLoadingState state = ref World!.TryGetRef<StreamableLoadingState>(entity, out bool exists);

            if (!exists)
            {
                ReportHub.LogError(GetReportData(), $"Leak detected on loading {intention.ToString()} from {source}");

                // it could be already disposed of, but it's safe to call it again
                acquiredBudget?.Dispose();
                return;
            }

            state.DisposeBudgetIfExists();

            if (result.HasValue)
                ApplyLoadedResult(entity, ref state, intention, result, source);
            else if (intention.IsCancelled())
            {
                if (World.IsAlive(entity))
                    World.Destroy(entity);
            }
            else
                state.RequestReevaluate();
        }

        private void ApplyLoadedResult(Entity entity, ref StreamableLoadingState state, TIntention intention, StreamableLoadingResult<TAsset>? result, AssetSource source)
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

        private bool IsWorldInvalid(EntityReference entity, IAcquiredBudget? acquiredBudget)
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
        protected abstract UniTask<StreamableLoadingResult<TAsset>> FlowInternalAsync(TIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct);

        /// <summary>
        ///     Part of the flow that can be reused by multiple intentions
        /// </summary>
        private async UniTask<StreamableLoadingResult<TAsset>?> CacheableFlowAsync(TIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            var source = new UniTaskCompletionSource<StreamableLoadingResult<TAsset>?>(); //AutoResetUniTaskCompletionSource<StreamableLoadingResult<TAsset>?>.Create();

            // ReportHub.Log(GetReportCategory(), $"OngoingRequests.SyncAdd {intention.CommonArguments.URL}");
            cache.OngoingRequests.SyncAdd(intention.CommonArguments.GetCacheableURL(), source);

            var ongoingRequestRemoved = false;

            StreamableLoadingResult<TAsset>? result = null;

            try
            {
                result = await RepeatLoopAsync(intention, acquiredBudget, partition, ct);

                // Ensure that we returned to the main thread
                await UniTask.SwitchToMainThread(ct);

                // before firing the continuation of the ongoing request
                // Add result to the cache
                if (result is { Succeeded: true })
                    cache.Add(in intention, result.Value.Asset!);

                // Set result for the reusable source
                // Remove from the ongoing requests immediately because finally will be called later than
                // continuation of cachedSource.Task.SuppressCancellationThrow();
                TryRemoveOngoingRequest();

                source.TrySetResult(result);

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
                    cache.OngoingRequests.SyncRemove(intention.CommonArguments.GetCacheableURL());
                    ongoingRequestRemoved = true;
                }
            }
        }

        private async UniTask<StreamableLoadingResult<TAsset>?> RepeatLoopAsync(TIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            StreamableLoadingResult<TAsset>? result = await intention.RepeatLoopAsync(acquiredBudget, partition, cachedInternalFlowDelegate, GetReportData(), ct);
            return result is { Succeeded: false } ? SetIrrecoverableFailure(intention, result.Value) : result;
        }

        private StreamableLoadingResult<TAsset> SetIrrecoverableFailure(TIntention intention, StreamableLoadingResult<TAsset> failure)
        {
            cache.IrrecoverableFailures.Add(intention.CommonArguments.GetCacheableURL(), failure);
            return failure;
        }
    }
}
