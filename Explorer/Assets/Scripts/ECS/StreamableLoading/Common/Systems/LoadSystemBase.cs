using Arch.Core;
using AssetManagement;
using Cysharp.Threading.Tasks;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine.Pool;
using Utility.Multithreading;

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

        private readonly IStreamableCache<TAsset, TIntention> cache;

        private readonly AssetsLoadingUtility.InternalFlowDelegate<TAsset, TIntention> cachedInternalFlowDelegate;

        private readonly Dictionary<string, StreamableLoadingResult<TAsset>> irrecoverableFailures;

        // asynchronous operations run independently on Update that is already synchronized
        // so they require explicit synchronisation
        private readonly MutexSync mutexSync;

        private readonly Query query;

        private CancellationTokenSource cancellationTokenSource;

        protected LoadSystemBase(World world, IStreamableCache<TAsset, TIntention> cache, MutexSync mutexSync) : base(world)
        {
            this.cache = cache;
            this.mutexSync = mutexSync;
            query = World.Query(in CREATE_WEB_REQUEST);
            irrecoverableFailures = DictionaryPool<string, StreamableLoadingResult<TAsset>>.Get();

            cachedInternalFlowDelegate = FlowInternal;
        }

        public override void Initialize()
        {
            cancellationTokenSource = new CancellationTokenSource();
        }

        public override void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();

            DictionaryPool<string, StreamableLoadingResult<TAsset>>.Release(irrecoverableFailures);
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
                    ref IPartitionComponent partitionComponent = ref Unsafe.Add(ref partitionComponentFirstElement, entityIndex);
                    ref StreamableLoadingState state = ref Unsafe.Add(ref stateFirstElement, entityIndex);

                    Execute(in entity, ref state, ref intention, ref partitionComponent);
                }
            }
        }

        private void Execute(in Entity entity, ref StreamableLoadingState state, ref TIntention intention, ref IPartitionComponent partitionComponent)
        {
            if (state.Value != StreamableLoadingState.Status.Allowed)
                return;

            AssetSource currentSource = intention.CommonArguments.CurrentSource;

            // Remove current source flag from the permitted sources
            // it indicates that the current source was used
            intention.RemoveCurrentSource();

            // Try load from cache first
            if (TryLoadFromCache(in entity, in intention, currentSource))
                return;

            // If the given URL failed irrecoverably just return the failure
            if (irrecoverableFailures.TryGetValue(intention.CommonArguments.URL, out StreamableLoadingResult<TAsset> failure))
            {
                FinalizeLoading(entity, intention, failure, currentSource);
                return;
            }

            // Indicate that loading has started
            state.Value = StreamableLoadingState.Status.InProgress;

            Flow(entity, currentSource, intention, state.AcquiredBudget, partitionComponent, cancellationTokenSource.Token).Forget();
        }

        private async UniTask Flow(Entity entity,
            AssetSource source, TIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken disposalCt)
        {
            StreamableLoadingResult<TAsset>? result = null;

            try
            {
                var requestIsNotFulfilled = true;

                // if the request is cached wait for it
                if (cache.OngoingRequests.TryGetValue(intention.CommonArguments.URL, out UniTaskCompletionSource<StreamableLoadingResult<TAsset>?> cachedSource))
                {
                    // Release budget immediately, if we don't do it and load a lot of bundles with dependencies sequentially, it will be a deadlock
                    acquiredBudget.Release();
                    // if the cached request is cancelled it does not mean failure for the new intent
                    (requestIsNotFulfilled, result) = await cachedSource.Task.SuppressCancellationThrow();
                }

                // if this request must be cancelled by `intention.CommonArguments.CancellationToken` it will be cancelled after `if (!requestIsNotFulfilled)`
                if (requestIsNotFulfilled)
                    result = await CacheableFlow(intention, acquiredBudget, partition, CancellationTokenSource.CreateLinkedTokenSource(intention.CommonArguments.CancellationToken, disposalCt).Token);

                if (!result.HasValue)

                    // Indicate that it should be grabbed by another system
                    // finally will handle the rest
                    return;
            }
            catch (Exception e)
            {
                // If we don't set an exception it will spin forever
                result = new StreamableLoadingResult<TAsset>(e);

                if (e is not OperationCanceledException)
                    ReportException(e);
            }
            finally
            {
                await UniTask.SwitchToMainThread();
                FinalizeLoading(entity, intention, result, source);
            }
        }

        private void FinalizeLoading(in Entity entity, TIntention intention, StreamableLoadingResult<TAsset>? result, AssetSource source)
        {
            using MutexSync.Scope sync = mutexSync.GetScope();
            ref StreamableLoadingState state = ref World.Get<StreamableLoadingState>(entity);

            state.DisposeBudget();

            if (result.HasValue)
            {
                state.Value = StreamableLoadingState.Status.Finished;

                // If we make a structural change before changing refs it will invalidate them, take care!!!
                World.Add(entity, result.Value);

                if (result.Value.Succeeded)
                    ReportHub.Log(GetReportCategory(), $"{intention}'s successfully loaded from {source}");
            }
            else
            {
                // Indicate that it should be reevaluated
                state.Value = StreamableLoadingState.Status.NotStarted;
            }
        }

        /// <summary>
        ///     All exceptions are handled by the upper functions, just do pure work
        /// </summary>
        protected abstract UniTask<StreamableLoadingResult<TAsset>> FlowInternal(TIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct);

        /// <summary>
        ///     Can't move it to another system as the update cycle is not synchronized with systems but based on UniTasks
        /// </summary>
        private void ReportException(Exception exception)
        {
            AssetsLoadingUtility.ReportException(GetReportCategory(), exception);
        }

        /// <summary>
        ///     Part of the flow that can be reused by multiple intentions
        /// </summary>
        private async UniTask<StreamableLoadingResult<TAsset>?> CacheableFlow(TIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            var source = new UniTaskCompletionSource<StreamableLoadingResult<TAsset>?>(); //AutoResetUniTaskCompletionSource<StreamableLoadingResult<TAsset>?>.Create();
            cache.OngoingRequests[intention.CommonArguments.URL] = source;

            try
            {
                StreamableLoadingResult<TAsset>? result = await RepeatLoop(intention, acquiredBudget, partition, ct);

                // Ensure that we returned to the main thread
                await UniTask.SwitchToMainThread();

                // Set result for the reusable source
                source.TrySetResult(result);

                if (!result.HasValue)

                    // it will be decided by another source
                    return null;

                StreamableLoadingResult<TAsset> resultValue = result.Value;

                // Add to cache if successful
                if (resultValue.Succeeded)
                    AddToCache(in intention, resultValue.Asset);

                return resultValue;
            }
            catch (OperationCanceledException operationCanceledException)
            {
                // Cancellation does not produce asset result
                source.TrySetCanceled(operationCanceledException.CancellationToken);
                throw;
            }
            finally
            {
                // If we don't switch to the main thread in finally we are in trouble because of
                // race conditions in non-concurrent collections
                await UniTask.SwitchToMainThread();
                cache.OngoingRequests.Remove(intention.CommonArguments.URL);
            }
        }

        private async UniTask<StreamableLoadingResult<TAsset>?> RepeatLoop(TIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            StreamableLoadingResult<TAsset>? result = await intention.RepeatLoop(acquiredBudget, partition, cachedInternalFlowDelegate, GetReportCategory(), ct);
            return result is { Succeeded: false } ? SetIrrecoverableFailure(intention, result.Value) : result;
        }

        private StreamableLoadingResult<TAsset> SetIrrecoverableFailure(TIntention intention, StreamableLoadingResult<TAsset> failure)
        {
            irrecoverableFailures[intention.CommonArguments.URL] = failure;
            return failure;
        }

        private bool TryLoadFromCache(in Entity entity, in TIntention intention, AssetSource source)
        {
            if (cache.TryGet(in intention, out TAsset asset))
            {
                FinalizeLoading(entity, intention, new StreamableLoadingResult<TAsset>(asset), source);
                return true;
            }

            return false;
        }

        private void AddToCache(in TIntention intention, TAsset asset)
        {
            cache.Add(in intention, asset);
        }
    }
}
