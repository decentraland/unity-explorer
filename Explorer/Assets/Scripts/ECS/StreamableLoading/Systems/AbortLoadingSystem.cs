using Arch.Core;
using Arch.SystemGroups;
using ECS.Abstract;
using ECS.StreamableLoading.Components.Common;

namespace ECS.StreamableLoading.Systems
{
    /// <summary>
    ///     Aborts the request and destroys the entity.
    /// </summary>
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    public partial class AbortLoadingSystem : BaseUnityLoopSystem
    {
        private readonly QueryDescription abortQuery = new QueryDescription()
           .WithAll<LoadingRequest, ForgetLoading>();

        private Abort abort;

        public AbortLoadingSystem(World world) : base(world)
        {
            abort = new Abort();
        }

        protected override void Update(float t)
        {
            World.InlineQuery<Abort, LoadingRequest>(in abortQuery, ref abort);
            World.Destroy(in abortQuery);
        }

        private readonly struct Abort : IForEach<LoadingRequest>
        {
            public void Update(ref LoadingRequest lr)
            {
                lr.WebRequest?.Abort();
            }
        }
    }
}
