using Arch.Core;
using AssetManagement;
using Cysharp.Threading.Tasks;
using ECS.Abstract;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using System;
using UnityEngine.Networking;
using Utility;

namespace ECS.StreamableLoading.Common.Systems
{
    /// <summary>
    ///     Decides how the asset loading from the web request finishes
    /// </summary>
    /// <typeparam name="TAsset"></typeparam>
    /// <typeparam name="TIntention"></typeparam>
    public abstract class ConcludeLoadingSystemBase<TAsset, TIntention> : BaseUnityLoopSystem where TIntention: struct, ILoadingIntention
    {
        protected delegate TAsset GetAssetDelegate(UnityWebRequest webRequest, in TIntention intention);

        private readonly QueryDescription query = new QueryDescription()
                                                 .WithAll<LoadingRequest, TIntention>()
                                                 .WithNone<StreamableLoadingResult<TAsset>>();

        private TryConclude tryConclude;

        protected ConcludeLoadingSystemBase(World world, IStreamableCache<TAsset, TIntention> cache) : base(world)
        {
            tryConclude = new TryConclude(world, GetAsset, cache);
        }

        protected override void Update(float t)
        {
            World.InlineEntityQuery<TryConclude, LoadingRequest, TIntention>(in query, ref tryConclude);
        }

        protected abstract TAsset GetAsset(UnityWebRequest webRequest, in TIntention intention);

        private readonly struct TryConclude : IForEachWithEntity<LoadingRequest, TIntention>
        {
            private readonly World world;
            private readonly GetAssetDelegate getAsset;
            private readonly IStreamableCache<TAsset, TIntention> cache;

            public TryConclude(World world, GetAssetDelegate getAsset, IStreamableCache<TAsset, TIntention> cache)
            {
                this.world = world;
                this.getAsset = getAsset;
                this.cache = cache;
            }

            public void Update(in Entity entity, ref LoadingRequest r, ref TIntention intention)
            {
                switch (r.WebRequest.result)
                {
                    case UnityWebRequest.Result.Success:
                    {
                        try
                        {
                            TAsset asset = getAsset(r.WebRequest, in intention);
                            cache.Add(in intention, asset);
                            world.Add(entity, new StreamableLoadingResult<TAsset>(asset));
                        }
                        catch (Exception e)
                        {
                            // Parse error, propagate it as a fail result, can't recover
                            world.Add(entity, new StreamableLoadingResult<TAsset>(e));
                        }

                        return;
                    }
                    case UnityWebRequest.Result.InProgress:
                        return;
                    default:
                        CommonLoadingArguments commonArgs = intention.CommonArguments;
                        --commonArgs.Attempts;

                        if (commonArgs.Attempts <= 0 || r.WebRequest.IsAborted() || !r.WebRequest.IsServerError())
                        {
                            // The current source didn't work out, remove it from the permitted sources
                            commonArgs.PermittedSources.RemoveFlag(commonArgs.CurrentSource);

                            if (commonArgs.PermittedSources == AssetSource.NONE)

                                // conclude now
                                world.Add(entity, new StreamableLoadingResult<TAsset>(new UnityWebRequestException(r.WebRequest)));
                        }

                        intention.CommonArguments = commonArgs;

                        // Otherwise don't do anything as the request will be restarted in another system
                        break;
                }
            }
        }
    }
}
