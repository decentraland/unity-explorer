using Arch.Core;
using DCL.ECSComponents;
using DCL.SDKComponents.NFTShape.Component;
using DCL.SDKComponents.NFTShape.System;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.NFTShapes;
using ECS.StreamableLoading.NFTShapes.URNs;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.NFTShapes.GetNFTShapeIntention>;

namespace DCL.SDKComponents.NFTShape.Tests
{
    public class LoadCycleNftShapeSystemShould : UnitySystemTestBase<LoadCycleNftShapeSystem>
    {
        private IURNSource? urnSource;

        [SetUp]
        public void Setup()
        {
            system = new LoadCycleNftShapeSystem(world, urnSource = Substitute.For<IURNSource>());
        }

        [Test]
        public void AbortLoadingIfComponentDeleted()
        {
            Entity entity = world.Create(
                new PBNftShape(),
                new NFTLoadingComponent(Promise.Create(world, new GetNFTShapeIntention("URN", urnSource!), PartitionComponent.TOP_PRIORITY)));

            system.Update(0);

            world.Remove<PBNftShape>(entity);

            system.Update(0);

            Assert.That(world.TryGet(entity, out NFTLoadingComponent loadingComponent), Is.True);
            Assert.That(loadingComponent.Promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested, Is.True);
            Assert.That(loadingComponent.Promise.Entity, Is.EqualTo(EntityReference.Null));
        }
    }
}
