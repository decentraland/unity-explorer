using Arch.Core;
using Cysharp.Threading.Tasks;
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
                                                                     .WithAll<TIntention, IPartitionComponent>()
                                                                     .WithNone<LoadingInProgress, StreamableLoadingResult<TAsset>>();

        private readonly Query query;

        private readonly IStreamableCache<TAsset, TIntention> cache;

        // asynchronous operations run independently on Update that is already synchronized
        // so they require explicit synchronisation
        private readonly MutexSync mutexSync;

        private readonly AssetsLoadingUtility.InternalFlowDelegate<TAsset, TIntention> cachedInternalFlowDelegate;

        private CancellationTokenSource cancellationTokenSource;

        private readonly IConcurrentBudgetProvider concurrentLoadingBudgetProvider;

        /// <summary>
        ///     Resolves the problem of having multiple requests to the same URL at a time
        /// </summary>
        private readonly Dictionary<string, UniTaskCompletionSource<StreamableLoadingResult<TAsset>?>> cachedRequests;

        private readonly Dictionary<string, StreamableLoadingResult<TAsset>> irrecoverableFailures;

        protected LoadSystemBase(World world, IStreamableCache<TAsset, TIntention> cache, MutexSync mutexSync, IConcurrentBudgetProvider concurrentLoadingBudgetProvider) : base(world)
        {
            this.cache = cache;
            this.mutexSync = mutexSync;
            this.concurrentLoadingBudgetProvider = concurrentLoadingBudgetProvider;
            query = World.Query(in CREATE_WEB_REQUEST);

            cachedRequests = DictionaryPool<string, UniTaskCompletionSource<StreamableLoadingResult<TAsset>?>>.Get();
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

            DictionaryPool<string, UniTaskCompletionSource<StreamableLoadingResult<TAsset>?>>.Release(cachedRequests);
            DictionaryPool<string, StreamableLoadingResult<TAsset>>.Release(irrecoverableFailures);
        }

        protected override void Update(float t)
        {
            foreach (ref Chunk chunk in query.GetChunkIterator())
            {
                ref Entity entityFirstElement = ref chunk.Entity(0);
                ref TIntention intentionFirstElement = ref chunk.GetFirst<TIntention>();
                ref IPartitionComponent partitionComponentFirstElement = ref chunk.GetFirst<IPartitionComponent>();

                foreach (int entityIndex in chunk)
                {
                    ref readonly Entity entity = ref Unsafe.Add(ref entityFirstElement, entityIndex);
                    ref TIntention intention = ref Unsafe.Add(ref intentionFirstElement, entityIndex);
                    ref IPartitionComponent partitionComponent = ref Unsafe.Add(ref partitionComponentFirstElement, entityIndex);

                    Execute(in entity, ref intention, ref partitionComponent);
                }
            }
        }

        private void Execute(in Entity entity, ref TIntention intention, ref IPartitionComponent partitionComponent)
        {
            if (!intention.IsAllowed())
                return;

            // Remove current source flag from the permitted sources
            // it indicates that the current source was used
            intention.RemoveCurrentSource();

            // Try load from cache first
            if (TryLoadFromCache(in entity, in intention))
            {
                concurrentLoadingBudgetProvider.ReleaseBudget();
                return;
            }

            // If the given URL failed irrecoverably just return the failure
            if (irrecoverableFailures.TryGetValue(intention.CommonArguments.URL, out StreamableLoadingResult<TAsset> failure))
            {
                World.Add(entity, failure);
                concurrentLoadingBudgetProvider.ReleaseBudget();
                return;
            }

            // Indicate that loading has started
            World.Add(entity, new LoadingInProgress());

            Flow(entity, intention, partitionComponent, cancellationTokenSource.Token).Forget();
        }

        private async UniTask Flow(Entity entity, TIntention intention, IPartitionComponent partition, CancellationToken disposalCt)
        {
            try
            {
                var requestIsNotFulfilled = true;
                StreamableLoadingResult<TAsset>? result = null;

                // if the request is cached wait for it
                if (cachedRequests.TryGetValue(intention.CommonArguments.URL, out UniTaskCompletionSource<StreamableLoadingResult<TAsset>?> cachedSource))

                    // if the cached request is cancelled it does not mean failure for the new intent
                    (requestIsNotFulfilled, result) = await cachedSource.Task.SuppressCancellationThrow();

                // if this request must be cancelled by `intention.CommonArguments.CancellationToken` it will be cancelled after `if (!requestIsNotFulfilled)`
                if (requestIsNotFulfilled)
                    result = await CacheableFlow(intention, partition, CancellationTokenSource.CreateLinkedTokenSource(intention.CommonArguments.CancellationToken, disposalCt).Token);

                using MutexSync.Scope sync = mutexSync.GetScope();

                if (!result.HasValue)
                {
                    // Indicate that it should be grabbed by another system
                    World.Remove<LoadingInProgress>(entity);
                    return;
                }

                // Add result to ECS
                World.Add(entity, result.Value);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception e)
            {
                // If we don't set an exception it will spin forever
                using MutexSync.Scope sync = mutexSync.GetScope();
                World.Add(entity, new StreamableLoadingResult<TAsset>(e));

                ReportException(e);
            }
            finally { concurrentLoadingBudgetProvider.ReleaseBudget(); }
        }

        /// <summary>
        ///     All exceptions are handled by the upper functions, just do pure work
        /// </summary>
        protected abstract UniTask<StreamableLoadingResult<TAsset>> FlowInternal(TIntention intention, IPartitionComponent partition, CancellationToken ct);

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
        private async UniTask<StreamableLoadingResult<TAsset>?> CacheableFlow(TIntention intention, IPartitionComponent partition, CancellationToken ct)
        {
            var source = new UniTaskCompletionSource<StreamableLoadingResult<TAsset>?>(); //AutoResetUniTaskCompletionSource<StreamableLoadingResult<TAsset>?>.Create();
            cachedRequests[intention.CommonArguments.URL] = source;

            try
            {
                StreamableLoadingResult<TAsset>? result = await RepeatLoop(intention, partition, ct);

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
            finally { cachedRequests.Remove(intention.CommonArguments.URL); }
        }

        private async UniTask<StreamableLoadingResult<TAsset>?> RepeatLoop(TIntention intention, IPartitionComponent partition, CancellationToken ct)
        {
            StreamableLoadingResult<TAsset>? result = await intention.RepeatLoop(partition, cachedInternalFlowDelegate, GetReportCategory(), ct);
            return result is { Succeeded: false } ? SetIrrecoverableFailure(intention, result.Value) : result;
        }

        private StreamableLoadingResult<TAsset> SetIrrecoverableFailure(TIntention intention, StreamableLoadingResult<TAsset> failure)
        {
            irrecoverableFailures[intention.CommonArguments.URL] = failure;
            return failure;
        }

        private bool TryLoadFromCache(in Entity entity, in TIntention intention)
        {
            if (cache.TryGet(in intention, out TAsset asset))
            {
                World.Add(entity, new StreamableLoadingResult<TAsset>(asset));
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
