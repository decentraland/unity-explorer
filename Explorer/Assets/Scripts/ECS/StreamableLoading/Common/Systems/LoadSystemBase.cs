using Arch.Core;
using AssetManagement;
using Cysharp.Threading.Tasks;
using ECS.Abstract;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine.Networking;
using UnityEngine.Pool;

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
                                                                     .WithAll<TIntention>()
                                                                     .WithNone<LoadingInProgress>();

        private readonly Query query;

        private readonly IStreamableCache<TAsset, TIntention> cache;
        private CancellationTokenSource cancellationTokenSource;

        /// <summary>
        ///     Resolves the problem of having multiple requests to the same URL at a time
        /// </summary>
        private readonly Dictionary<string, AutoResetUniTaskCompletionSource<StreamableLoadingResult<TAsset>?>> cachedRequests;

        protected LoadSystemBase(World world, IStreamableCache<TAsset, TIntention> cache) : base(world)
        {
            this.cache = cache;
            query = World.Query(in CREATE_WEB_REQUEST);

            cachedRequests = DictionaryPool<string, AutoResetUniTaskCompletionSource<StreamableLoadingResult<TAsset>?>>.Get();
        }

        public override void Initialize()
        {
            cancellationTokenSource = new CancellationTokenSource();
        }

        public override void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();

            DictionaryPool<string, AutoResetUniTaskCompletionSource<StreamableLoadingResult<TAsset>?>>.Release(cachedRequests);
        }

        protected override void Update(float t)
        {
            foreach (ref Chunk chunk in query.GetChunkIterator())
            {
                ref Entity entityFirstElement = ref chunk.Entity(0);
                ref TIntention intentionFirstElement = ref chunk.GetFirst<TIntention>();

                foreach (int entityIndex in chunk)
                {
                    ref readonly Entity entity = ref Unsafe.Add(ref entityFirstElement, entityIndex);
                    ref TIntention intention = ref Unsafe.Add(ref intentionFirstElement, entityIndex);

                    Execute(in entity, ref intention);
                }
            }
        }

        private void Execute(in Entity entity, ref TIntention intention)
        {
            // Remove current source flag from the permitted sources
            // it indicates that the current source was used
            intention.RemoveCurrentSource();

            // Try load from cache first
            if (TryLoadFromCache(in entity, in intention))
                return;

            // Indicate that loading has started
            World.Add(entity, new LoadingInProgress());

            Flow(entity, intention, cancellationTokenSource.Token).Forget();
        }

        private async UniTask Flow(Entity entity, TIntention intention, CancellationToken disposalCt)
        {
            try
            {
                var requestIsNotFulfilled = true;
                StreamableLoadingResult<TAsset>? result = null;

                // if the request is cached wait for it
                if (cachedRequests.TryGetValue(intention.CommonArguments.URL, out AutoResetUniTaskCompletionSource<StreamableLoadingResult<TAsset>?> cachedSource))
                {
                    // if the cached request is cancelled it does not mean failure for the new intent
                    (requestIsNotFulfilled, result) = await cachedSource.Task.SuppressCancellationThrow();

                    // if this request must be cancelled by `intention.CommonArguments.CancellationToken` it will be cancelled after `if (!requestIsNotFulfilled)`
                }

                if (requestIsNotFulfilled)
                    result = await CacheableFlow(intention, CancellationTokenSource.CreateLinkedTokenSource(intention.CommonArguments.CancellationToken, disposalCt).Token);

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
                World.Add(entity, new StreamableLoadingResult<TAsset>(e));

                // TODO errors reporting
            }
        }

        /// <summary>
        ///     All exceptions are handled by the upper functions, just do pure work
        /// </summary>
        protected abstract UniTask<StreamableLoadingResult<TAsset>> FlowInternal(TIntention intention, CancellationToken ct);

        /// <summary>
        ///     Part of the flow that can be reused by multiple intentions
        /// </summary>
        private async UniTask<StreamableLoadingResult<TAsset>?> CacheableFlow(TIntention intention, CancellationToken ct)
        {
            var source = AutoResetUniTaskCompletionSource<StreamableLoadingResult<TAsset>?>.Create();
            cachedRequests[intention.CommonArguments.URL] = source;

            try
            {
                StreamableLoadingResult<TAsset>? result = await RepeatLoop(intention, ct);

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

        private async UniTask<StreamableLoadingResult<TAsset>?> RepeatLoop(TIntention intention, CancellationToken ct)
        {
            int attemptCount = intention.CommonArguments.Attempts;

            while (true)
            {
                try { return await FlowInternal(intention, ct); }

                catch (UnityWebRequestException unityWebRequestException)
                {
                    UnityWebRequest webRequest = unityWebRequestException.UnityWebRequest;

                    // Decide if we can repeat or not
                    --attemptCount;

                    if (attemptCount <= 0 || webRequest.IsAborted() || !webRequest.IsServerError())
                    {
                        if (intention.CommonArguments.PermittedSources == AssetSource.NONE)

                            // conclude now
                            return new StreamableLoadingResult<TAsset>(unityWebRequestException);

                        // Leave other systems to decide on other sources
                        return null;
                    }
                }
                catch (Exception e)
                {
                    // General exception
                    // conclude now, we can't do anything
                    // TODO errors reporting
                    return new StreamableLoadingResult<TAsset>(e);
                }
            }
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
