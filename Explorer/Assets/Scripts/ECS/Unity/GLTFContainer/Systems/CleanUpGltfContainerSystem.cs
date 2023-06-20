using Arch.Core;
using Arch.SystemGroups;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;

namespace ECS.Unity.GLTFContainer.Systems
{
    /// <summary>
    ///     Cancel promises on the dying entities
    /// </summary>
    [UpdateInGroup(typeof(CleanUpGroup))]
    public partial class CleanUpGltfContainerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly QueryDescription entityDestroyQuery = new QueryDescription()
           .WithAll<DeleteEntityIntention, GltfContainerComponent>();

        private ReleaseOnEntityDestroy releaseOnEntityDestroy;


        internal CleanUpGltfContainerSystem(World world, IStreamableCache<GltfContainerAsset, string> cache) : base(world)
        {
            releaseOnEntityDestroy = new ReleaseOnEntityDestroy(cache, World);
        }

        protected override void Update(float t)
        {
            World.InlineQuery<ReleaseOnEntityDestroy, GltfContainerComponent>(in entityDestroyQuery, ref releaseOnEntityDestroy);
        }

        public void FinalizeComponents(in Query query)
        {
            World.InlineQuery<ReleaseOnEntityDestroy, GltfContainerComponent>(in new QueryDescription().WithAll<GltfContainerComponent>(), ref releaseOnEntityDestroy);
        }

        private readonly struct ReleaseOnEntityDestroy : IForEach<GltfContainerComponent>
        {
            private readonly IStreamableCache<GltfContainerAsset, string> cache;
            private readonly World world;

            public ReleaseOnEntityDestroy(IStreamableCache<GltfContainerAsset, string> cache, World world)
            {
                this.cache = cache;
                this.world = world;
            }

            public void Update(ref GltfContainerComponent component)
            {
                if (component.Promise.TryGetResult(world, out StreamableLoadingResult<GltfContainerAsset> result) && result.Succeeded)
                    cache.Dereference(component.Source, result.Asset);

                component.Promise.ForgetLoading(world);
            }
        }
    }
}
