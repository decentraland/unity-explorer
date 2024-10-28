using Arch.Core;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.SDKComponents.NFTShape.Component;
using DCL.SDKComponents.NFTShape.Frames.FramePrefabs;
using DCL.SDKComponents.NFTShape.Renderer;
using DCL.SDKComponents.NFTShape.Renderer.Factory;
using DCL.SDKComponents.NFTShape.System;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.NFTShapes;
using ECS.StreamableLoading.NFTShapes.URNs;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.NFTShapes.GetNFTShapeIntention>;

namespace DCL.SDKComponents.NFTShape.Tests
{
    public class InstantiateNftShapeSystemSystemShould : UnitySystemTestBase<InstantiateNftShapeSystem>
    {
        private const string INITIAL_URN = "INITIAL_URN";

        private EntityEventBuffer<NftShapeRendererComponent>? changedNftShapes;

        [SetUp]
        public void Setup()
        {
            system = new InstantiateNftShapeSystem(world,
                Substitute.For<INFTShapeRendererFactory>(),
                Substitute.For<IPerformanceBudget>(),
                Substitute.For<IReadOnlyFramePrefabs>(),
                changedNftShapes = new EntityEventBuffer<NftShapeRendererComponent>(1));
        }

        [Test]
        [TestCase("NEW_URN")]
        [TestCase(INITIAL_URN)]
        public void ReconfigureNftShape(string newURN)
        {
            var shape = new PBNftShape { Urn = INITIAL_URN };
            INftShapeRenderer? renderer = Substitute.For<INftShapeRenderer>();
            var component = new NftShapeRendererComponent(renderer);
            var loadingComponent = new NFTLoadingComponent(Promise.Create(world, new GetNFTShapeIntention(INITIAL_URN, Substitute.For<IURNSource>()), PartitionComponent.TOP_PRIORITY));

            Entity entity = world.Create(shape,
                component,
                loadingComponent);

            shape.Urn = newURN;
            shape.IsDirty = true;

            system.Update(0);

            bool urnChanged = newURN != INITIAL_URN;

            renderer.Received(1).Apply(shape, urnChanged);

            if (urnChanged)
            {
                Assert.That(loadingComponent.Promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested, Is.True);

                CollectionAssert.AreEqual(new EntityRelation<NftShapeRendererComponent>[] { new (entity, component) }, changedNftShapes!.Relations);
                Assert.That(world.Has<NFTLoadingComponent>(entity), Is.False);
            }
        }
    }
}
