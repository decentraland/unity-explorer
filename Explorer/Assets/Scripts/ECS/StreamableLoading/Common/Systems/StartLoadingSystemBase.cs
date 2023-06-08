using Arch.Core;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.Common.Systems
{
    /// <summary>
    ///     Handles Web Requests related to Assets Loading
    /// </summary>
    /// <typeparam name="TIntention"></typeparam>
    /// <typeparam name="TAsset"></typeparam>
    public abstract class StartLoadingSystemBase<TIntention> : BaseUnityLoopSystem where TIntention: struct, ILoadingIntention
    {
        private static readonly QueryDescription CREATE_WEB_REQUEST = new QueryDescription()
                                                                     .WithAll<TIntention>()
                                                                     .WithNone<LoadingRequest, ForgetLoadingIntent>();

        private CreateQuery createQuery;

        protected StartLoadingSystemBase(World world) : base(world)
        {
            createQuery = new CreateQuery(world, CreateWebRequest);
        }

        protected abstract UnityWebRequest CreateWebRequest(in TIntention intention);

        protected override void Update(float t)
        {
            World.InlineEntityQuery<CreateQuery, TIntention>(in CREATE_WEB_REQUEST, ref createQuery);
        }

        private static void CreateWebRequest(in CreateDelegate<TIntention> createDelegate, ref TIntention loadingIntention, ref LoadingRequest loadingRequest)
        {
            loadingRequest.WebRequest = createDelegate(in loadingIntention);
            loadingRequest.WebRequest.SetCommonParameters(loadingIntention.CommonArguments);
            loadingRequest.WebRequest.SendWebRequest();
        }

        private readonly struct CreateQuery : IForEachWithEntity<TIntention>
        {
            private readonly World world;
            private readonly CreateDelegate<TIntention> create;

            public CreateQuery(World world, CreateDelegate<TIntention> create)
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


    }
}
