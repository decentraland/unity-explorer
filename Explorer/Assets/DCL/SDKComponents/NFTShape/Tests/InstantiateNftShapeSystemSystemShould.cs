using Arch.Core;
using CommunicationData.URLHelpers;
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
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using NftTypePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.NFTShapes.NftTypeResult, ECS.StreamableLoading.NFTShapes.GetNFTTypeIntention>;
using NftImagePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.NFTShapes.GetNFTImageIntention>;

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
            var loadingComponent = new NFTLoadingComponent(INITIAL_URN,
                NftTypePromise.Create(world, new GetNFTTypeIntention(URLAddress.FromString(INITIAL_URN)), PartitionComponent.TOP_PRIORITY))
            {
                ImagePromise = NftImagePromise.Create(world, new GetNFTImageIntention(URLAddress.FromString(INITIAL_URN)), PartitionComponent.TOP_PRIORITY),
            };

            Entity entity = world.Create(shape,
                component,
                loadingComponent);

            shape.Urn = newURN;
            shape.IsDirty = true;

            system!.Update(0);

            bool urnChanged = newURN != INITIAL_URN;

            renderer.Received(1).Apply(shape, urnChanged);

            if (urnChanged)
            {
                Assert.That(loadingComponent.TypePromise.LoadingIntention.CancellationTokenSource.IsCancellationRequested, Is.True);
                Assert.That(loadingComponent.ImagePromise.Value.LoadingIntention.CancellationTokenSource.IsCancellationRequested, Is.True);

                CollectionAssert.AreEqual(new EntityRelation<NftShapeRendererComponent>[] { new (entity, component) }, changedNftShapes!.Relations);
                Assert.That(world.Has<NFTLoadingComponent>(entity), Is.False);
            }
        }
    }
}
