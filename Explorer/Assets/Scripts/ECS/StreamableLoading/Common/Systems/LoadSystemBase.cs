using Arch.Core;
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

        private CancellationTokenSource cancellationTokenSource;

        private bool systemIsDisposed;

        protected LoadSystemBase(World world, IStreamableCache<TAsset, TIntention> cache) : base(world)
        {
            this.cache = cache;
            // this.mutexSync = mutexSync;
            query = World.Query(in CREATE_WEB_REQUEST);

            cachedInternalFlowDelegate = FlowInternalAsync;
        }

        public override void Initialize()
        {
            cancellationTokenSource = new CancellationTokenSource();
        }

        public override void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();

            systemIsDisposed = true;
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

            // Indicate that loading has started
            state.Value = StreamableLoadingState.Status.InProgress;

            FlowAsync(entity, currentSource, intention, state.AcquiredBudget, partitionComponent, cancellationTokenSource.Token).Forget();
        }

        private async UniTask FlowAsync(Entity entity,
            AssetSource source, TIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken disposalCt)
        {
            StreamableLoadingResult<TAsset>? result = null;

            try
            {
                var requestIsNotFulfilled = true;

                // if the request is cached wait for it
                // If there is an ongoing request it means that the result is neither cached, nor failed
                if (cache.OngoingRequests.SyncTryGetValue(intention.CommonArguments.URL, out UniTaskCompletionSource<StreamableLoadingResult<TAsset>?> cachedSource))
                {
                    // Release budget immediately, if we don't do it and load a lot of bundles with dependencies sequentially, it will be a deadlock
                    acquiredBudget.Release();

                    // if the cached request is cancelled it does not mean failure for the new intent
                    (requestIsNotFulfilled, result) = await cachedSource.Task.SuppressCancellationThrow();
                }

                // Try load from cache first
                if (cache.TryGet(intention, out TAsset asset))
                {
                    result = new StreamableLoadingResult<TAsset>(asset);
                    return;
                }

                // If the given URL failed irrecoverably just return the failure
                if (cache.IrrecoverableFailures.TryGetValue(intention.CommonArguments.URL, out StreamableLoadingResult<TAsset> failure))
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
                    return;
            }
            catch (Exception e)
            {
                // If we don't set an exception it will spin forever
                result = new StreamableLoadingResult<TAsset>(e);

                if (e is not OperationCanceledException)
                    ReportException(e);
            }
            finally { FinalizeLoading(entity, intention, result, source, acquiredBudget); }
        }

        private void FinalizeLoading(in Entity entity, TIntention intention,
            StreamableLoadingResult<TAsset>? result, AssetSource source,
            IAcquiredBudget acquiredBudget)
        {
            // using MutexSync.Scope sync = mutexSync.GetScope();

            if (systemIsDisposed || !World.IsAlive(entity))
            {
                // World is no longer valid, can't call World.Get
                // Just Free the budget
                acquiredBudget.Dispose();
                return;
            }

            ref StreamableLoadingState state = ref World.TryGetRef<StreamableLoadingState>(entity, out bool exists);

            if (!exists)
            {
                ReportHub.LogError(GetReportCategory(), $"Leak detected on loading {intention.ToString()} from {source}");
                // it could be already disposed of, but it's safe to call it again
                acquiredBudget.Dispose();
                return;
            }

            state.DisposeBudget();

            if (result.HasValue)
            {
                state.Value = StreamableLoadingState.Status.Finished;

                // If we make a structural change before changing refs it will invalidate them, take care!!!
                World.Add(entity, result.Value);

                if (result.Value.Succeeded)
                {
                    OnAssetSuccessfullyLoaded(result.Value.Asset);
                    ReportHub.Log(GetReportCategory(), $"{intention}'s successfully loaded from {source}");
                }
            }
            else if (intention.CancellationTokenSource.IsCancellationRequested) { World.Destroy(entity); }
            else
            {
                // Indicate that it should be reevaluated
                state.Value = StreamableLoadingState.Status.NotStarted;
            }
        }

        protected virtual void OnAssetSuccessfullyLoaded(TAsset asset) { }

        /// <summary>
        ///     All exceptions are handled by the upper functions, just do pure work
        /// </summary>
        protected abstract UniTask<StreamableLoadingResult<TAsset>> FlowInternalAsync(TIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct);

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
        private async UniTask<StreamableLoadingResult<TAsset>?> CacheableFlowAsync(TIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            var source = new UniTaskCompletionSource<StreamableLoadingResult<TAsset>?>(); //AutoResetUniTaskCompletionSource<StreamableLoadingResult<TAsset>?>.Create();

            // ReportHub.Log(GetReportCategory(), $"OngoingRequests.SyncAdd {intention.CommonArguments.URL}");
            cache.OngoingRequests.SyncAdd(intention.CommonArguments.URL, source);

            var ongoingRequestRemoved = false;

            void TryRemoveOngoingRequest()
            {
                if (!ongoingRequestRemoved)
                {
                    // ReportHub.Log(GetReportCategory(), $"OngoingRequests.SyncRemove {intention.CommonArguments.URL}");
                    cache.OngoingRequests.SyncRemove(intention.CommonArguments.URL);
                    ongoingRequestRemoved = true;
                }
            }

            try
            {
                StreamableLoadingResult<TAsset>? result = await RepeatLoopAsync(intention, acquiredBudget, partition, ct);

                // Ensure that we returned to the main thread
                await UniTask.SwitchToMainThread(ct);

                // Set result for the reusable source
                // Remove from the ongoing requests immediately because finally will be called later than
                // continuation of cachedSource.Task.SuppressCancellationThrow();
                TryRemoveOngoingRequest();
                source.TrySetResult(result);

                if (!result.HasValue)
                    return null; // it will be decided by another source

                StreamableLoadingResult<TAsset> resultValue = result.Value;

                // Add to cache if successful
                if (resultValue.Succeeded)
                    AddToCache(in intention, resultValue.Asset);

                return resultValue;
            }
            catch (OperationCanceledException operationCanceledException)
            {
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
        }

        private async UniTask<StreamableLoadingResult<TAsset>?> RepeatLoopAsync(TIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            StreamableLoadingResult<TAsset>? result = await intention.RepeatLoopAsync(acquiredBudget, partition, cachedInternalFlowDelegate, GetReportCategory(), ct);
            return result is { Succeeded: false } ? SetIrrecoverableFailure(intention, result.Value) : result;
        }

        private StreamableLoadingResult<TAsset> SetIrrecoverableFailure(TIntention intention, StreamableLoadingResult<TAsset> failure)
        {
            cache.IrrecoverableFailures.Add(intention.CommonArguments.URL, failure);
            return failure;
        }

        private void AddToCache(in TIntention intention, TAsset asset)
        {
            cache.Add(in intention, asset);
        }
    }
}
