using Arch.Core;
using Cysharp.Threading.Tasks;
using ECS.Abstract;
using ECS.StreamableLoading.Components.Common;
using System;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.Systems
{
    public abstract class ConcludeLoadingSystemBase<TAsset, TIntention> : BaseUnityLoopSystem where TIntention: struct, ILoadingIntention
    {
        private readonly QueryDescription query = new QueryDescription()
                                                 .WithAll<LoadingRequest, TIntention>()
                                                 .WithNone<StreamableLoadingResult<TAsset>>();

        private TryConclude tryConclude;

        protected ConcludeLoadingSystemBase(World world) : base(world)
        {
            tryConclude = new TryConclude(world, GetAsset);
        }

        protected override void Update(float t)
        {
            World.InlineEntityQuery<TryConclude, LoadingRequest, TIntention>(in query, ref tryConclude);
        }

        protected abstract TAsset GetAsset(UnityWebRequest webRequest);

        private readonly struct TryConclude : IForEachWithEntity<LoadingRequest, TIntention>
        {
            private readonly World world;
            private readonly Func<UnityWebRequest, TAsset> getAsset;

            public TryConclude(World world, Func<UnityWebRequest, TAsset> getAsset)
            {
                this.world = world;
                this.getAsset = getAsset;
            }

            public void Update(in Entity entity, ref LoadingRequest r, ref TIntention intention)
            {
                switch (r.WebRequest.result)
                {
                    case UnityWebRequest.Result.Success:
                    {
                        TAsset asset;

                        try
                        {
                            asset = getAsset(r.WebRequest);
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
                        intention.CommonArguments = commonArgs;

                        if (commonArgs.Attempts <= 0 || r.WebRequest.IsAborted() || !r.WebRequest.IsServerError())

                            // conclude now
                            world.Add(entity, new StreamableLoadingResult<TAsset>(new UnityWebRequestException(r.WebRequest)));

                        // Otherwise don't do anything as the request will be restarted in another system
                        break;
                }
            }
        }
    }
}
