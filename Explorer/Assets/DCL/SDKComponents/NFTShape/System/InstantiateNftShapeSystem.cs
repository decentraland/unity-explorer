using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.SDKComponents.NFTShape.Component;
using DCL.SDKComponents.NFTShape.Frames.FramePrefabs;
using DCL.SDKComponents.NFTShape.Renderer;
using DCL.SDKComponents.NFTShape.Renderer.Factory;
using ECS.Abstract;
using ECS.Groups;
using ECS.StreamableLoading.Cache;

using ECS.Unity.Materials.Components;
using ECS.Unity.Transforms.Components;


namespace DCL.SDKComponents.NFTShape.System
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [ThrottlingEnabled]
    public partial class InstantiateNftShapeSystem : BaseUnityLoopSystem
    {
        private readonly INFTShapeRendererFactory nftShapeRendererFactory;
        private readonly IPerformanceBudget instantiationFrameTimeBudgetProvider;
        private readonly IReadOnlyFramePrefabs prefabs;

        private readonly EntityEventBuffer<NftShapeRendererComponent> changedNftShapes;

        public InstantiateNftShapeSystem(
            World world,
            INFTShapeRendererFactory nftShapeRendererFactory,
            IPerformanceBudget instantiationFrameTimeBudgetProvider,
            IReadOnlyFramePrefabs prefabs,
            EntityEventBuffer<NftShapeRendererComponent> changedNftShapes) : base(world)
        {
            this.nftShapeRendererFactory = nftShapeRendererFactory;
            this.instantiationFrameTimeBudgetProvider = instantiationFrameTimeBudgetProvider;
            this.prefabs = prefabs;
            this.changedNftShapes = changedNftShapes;
        }

        protected override void Update(float t)
        {
            ReconfigureNftShapeQuery(World);
            InstantiateRemainingQuery(World!);
        }

        [Query]
        [None(typeof(NftShapeRendererComponent), typeof(MaterialComponent))]
        private void InstantiateRemaining(Entity entity, in TransformComponent transform, in PBNftShape nftShape)
        {
            if (prefabs.IsInitialized == false || instantiationFrameTimeBudgetProvider.TrySpendBudget() == false)
                return;

            NftShapeRendererComponent component = NewNftShapeRendererComponent(transform, nftShape);

            World!.Add(entity, component);
            changedNftShapes.Add(entity, component);
        }

        [Query]
        private void ReconfigureNftShape(Entity entity, PBNftShape pbNftShape, ref NftShapeRendererComponent nftShapeRendererComponent,
            ref NFTLoadingComponent loadingComponent)
        {
            if (!pbNftShape.IsDirty) return;

            bool sourceChanged = pbNftShape.Urn != loadingComponent.OriginalUrn;

            nftShapeRendererComponent.PoolableComponent.Apply(pbNftShape, sourceChanged);

            // If URN has changed forget and delete the current loading promise so it will be started again in `LoadCycleNftShapeSystem`
            if (!sourceChanged) return;

            changedNftShapes.Add(entity, nftShapeRendererComponent);
            loadingComponent.TypePromise.ForgetLoading(World);

            if (loadingComponent.VideoPromise != null)
            {
                var videoPromise = loadingComponent.VideoPromise.Value;
                videoPromise.TryDereference(World);
                videoPromise.ForgetLoading(World);
                // Need to reassign reference, otherwise it becomes outdated due to handling a copy
                loadingComponent.VideoPromise = videoPromise;
            }

            if (loadingComponent.ImagePromise != null)
            {
                var imagePromise = loadingComponent.ImagePromise.Value;
                imagePromise.TryDereference(World);
                imagePromise.ForgetLoading(World);
                // Need to reassign reference, otherwise it becomes outdated due to handling a copy
                loadingComponent.ImagePromise = imagePromise;
            }

            World.Remove<NFTLoadingComponent>(entity);
        }

        private NftShapeRendererComponent NewNftShapeRendererComponent(in TransformComponent transform, in PBNftShape nftShape)
        {
            INftShapeRenderer renderer = nftShapeRendererFactory.New(transform.Transform);
            renderer.Apply(nftShape, true);

            return new NftShapeRendererComponent(renderer);
        }
    }
}
