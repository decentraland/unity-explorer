using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Optimization.Pools;
using DCL.Utilities;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;
using WearablePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.WearablesResolution, DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    public class AvatarCleanUpSystemShould : UnitySystemTestBase<AvatarCleanUpSystem>
    {
        private IComponentPool<AvatarBase> avatarPool = null!;
        private FixedComputeBufferHandler vertOutBuffer = null!;
        private AvatarTransformMatrixJobWrapper jobWrapper = null!;
        private GameObject avatarGameObject = null!;

        [SetUp]
        public void SetUp()
        {
            avatarPool = Substitute.For<IComponentPool<AvatarBase>>();
            vertOutBuffer = new FixedComputeBufferHandler(1000, 4, 4);
            jobWrapper = new AvatarTransformMatrixJobWrapper();
            avatarGameObject = new GameObject("Avatar");

            system = new AvatarCleanUpSystem(world, vertOutBuffer, Substitute.For<IAvatarMaterialPoolHandler>(), avatarPool,
                Substitute.For<IObjectPool<UnityEngine.ComputeShader>>(), Substitute.For<IAttachmentsAssetsCache>(),
                new ObjectProxy<AvatarBase>(), jobWrapper);
        }

        protected override void OnTearDown()
        {
            jobWrapper.Dispose();
            vertOutBuffer.Dispose();
            Object.DestroyImmediate(avatarGameObject);
        }

        /// <summary>
        ///     An avatar deleted before its wearables are instantiated has its deferred deletion lifted, its wearable promise forgotten and its AvatarBase released.
        /// </summary>
        [Test]
        public void ReleaseAvatarDeletedBeforeInstantiation()
        {
            // Arrange
            AvatarBase avatarBase = avatarGameObject.AddComponent<AvatarBase>();

            var wearablePromise = WearablePromise.Create(world,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(BodyShape.MALE, Array.Empty<string>(), Array.Empty<string>()),
                PartitionComponent.TOP_PRIORITY);

            var avatarShape = new AvatarShapeComponent("TEST_AVATAR", "TEST_ID", BodyShape.MALE, wearablePromise, Color.white, Color.white, Color.white);
            Entity entity = world.Create(avatarShape, avatarBase, new DeleteEntityIntention { DeferDeletion = true });

            // Act
            system.Update(0);

            // Assert
            Assert.IsFalse(world.Get<DeleteEntityIntention>(entity).DeferDeletion);
            avatarPool.Received(1).Release(avatarBase);
            Assert.IsFalse(world.IsAlive(wearablePromise.Entity));
        }

        [Test]
        public void KeepLoadingAvatarThatIsNotDeleted()
        {
            // Arrange
            AvatarBase avatarBase = avatarGameObject.AddComponent<AvatarBase>();

            var wearablePromise = WearablePromise.Create(world,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(BodyShape.MALE, Array.Empty<string>(), Array.Empty<string>()),
                PartitionComponent.TOP_PRIORITY);

            var avatarShape = new AvatarShapeComponent("TEST_AVATAR", "TEST_ID", BodyShape.MALE, wearablePromise, Color.white, Color.white, Color.white);
            world.Create(avatarShape, avatarBase);

            // Act
            system.Update(0);

            // Assert
            avatarPool.DidNotReceive().Release(Arg.Any<AvatarBase>());
            Assert.IsTrue(world.IsAlive(wearablePromise.Entity));
        }
    }
}
