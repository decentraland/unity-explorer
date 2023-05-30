using Arch.Core;
using ECS.Abstract;
using ECS.StreamableLoading.Components.Common;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.Systems
{
    public abstract class StartLoadingSystemBase<TIntention, TAsset> : BaseUnityLoopSystem where TIntention: struct, ILoadingIntention
    {
        private delegate UnityWebRequest CreateDelegate(in TIntention intention);

        private readonly QueryDescription createWebRequest = new QueryDescription()
                                                            .WithAll<TIntention>()
                                                            .WithNone<LoadingRequest, ForgetLoadingIntent>();

        private readonly QueryDescription repeatWebRequest = new QueryDescription()
                                                            .WithAll<TIntention, LoadingRequest>()
                                                            .WithNone<StreamableLoadingResult<TAsset>, ForgetLoadingIntent>();

        private CreateQuery createQuery;
        private RepeatQuery repeatQuery;

        protected StartLoadingSystemBase(World world) : base(world)
        {
            CreateDelegate createCached = CreateWebRequest;

            createQuery = new CreateQuery(world, createCached);
            repeatQuery = new RepeatQuery(createCached);
        }

        protected abstract UnityWebRequest CreateWebRequest(in TIntention intention);

        protected override void Update(float t)
        {
            World.InlineEntityQuery<CreateQuery, TIntention>(in createWebRequest, ref createQuery);
            World.InlineQuery<RepeatQuery, TIntention, LoadingRequest>(in repeatWebRequest, ref repeatQuery);
        }

        private static void CreateWebRequest(in CreateDelegate createDelegate, ref TIntention loadingIntention, ref LoadingRequest loadingRequest)
        {
            loadingRequest.WebRequest = createDelegate(in loadingIntention);
            loadingRequest.WebRequest.SetCommonParameters(loadingIntention.CommonArguments);
            loadingRequest.WebRequest.SendWebRequest();
        }

        private readonly struct CreateQuery : IForEachWithEntity<TIntention>
        {
            private readonly World world;
            private readonly CreateDelegate create;

            public CreateQuery(World world, CreateDelegate create)
            {
                this.world = world;
                this.create = create;
            }

            public void Update(in Entity entity, ref TIntention intention)
            {
                var component = new LoadingRequest();
                CreateWebRequest(in create, ref intention, ref component);
                world.Add(entity, component);
            }
        }

        private readonly struct RepeatQuery : IForEach<TIntention, LoadingRequest>
        {
            private readonly CreateDelegate create;

            public RepeatQuery(CreateDelegate create)
            {
                this.create = create;
            }

            public void Update(ref TIntention intention, ref LoadingRequest loadingRequest)
            {
                // it's a tradeoff to not delete the component
                if (loadingRequest.WebRequest is { isDone: false })
                    return;

                CreateWebRequest(in create, ref intention, ref loadingRequest);
            }
        }
    }
}
