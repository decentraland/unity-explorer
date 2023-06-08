using Arch.Core;
using ECS.Abstract;
using ECS.StreamableLoading.Components.Common;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.Systems
{
    /// <summary>
    ///     Repeats the unsuccessful request, must be executed after <see cref="ConcludeLoadingSystemBase{TAsset,TIntention}" />
    /// </summary>
    /// <typeparam name="TIntention"></typeparam>
    /// <typeparam name="TAsset"></typeparam>
    public abstract class RepeatLoadingSystemBase<TIntention, TAsset> : BaseUnityLoopSystem where TIntention: struct, ILoadingIntention
    {
        private static readonly QueryDescription REPEAT_WEB_REQUEST = new QueryDescription()
                                                                     .WithAll<TIntention, LoadingRequest>()
                                                                     .WithNone<StreamableLoadingResult<TAsset>, ForgetLoadingIntent>();

        private RepeatQuery repeatQuery;

        protected RepeatLoadingSystemBase(World world) : base(world)
        {
            repeatQuery = new RepeatQuery(CreateWebRequest);
        }

        protected abstract UnityWebRequest CreateWebRequest(in TIntention intention);

        protected override void Update(float t)
        {
            World.InlineQuery<RepeatQuery, TIntention, LoadingRequest>(in REPEAT_WEB_REQUEST, ref repeatQuery);
        }

        private readonly struct RepeatQuery : IForEach<TIntention, LoadingRequest>
        {
            private readonly CreateDelegate<TIntention> create;

            public RepeatQuery(CreateDelegate<TIntention> create)
            {
                this.create = create;
            }

            public void Update(ref TIntention intention, ref LoadingRequest loadingRequest)
            {
                // it's a tradeoff to not delete the component
                if (loadingRequest.WebRequest is { isDone: false })
                    return;

                loadingRequest.WebRequest = create(in intention);
                loadingRequest.WebRequest.SetCommonParameters(intention.CommonArguments);
                loadingRequest.WebRequest.SendWebRequest();
            }
        }
    }
}
