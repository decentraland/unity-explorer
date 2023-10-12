using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Rendering.Avatar;
using DCL.AvatarRendering.AvatarShape.Systems;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.ComponentsPooling;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.TestTools;
using Promise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.IWearable[], DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;


namespace DCL.AvatarRendering.AvatarShape.Tests
{
    public class AvatarInstantiatorSystemShould : UnitySystemTestBase<AvatarInstantiatorSystem>
    {
        private AvatarBase instantiatedAvatarBase;
        private AvatarShapeComponent avatarShapeComponent;

        [SetUp]
        public void Setup()
        {
            IConcurrentBudgetProvider budgetProvider = Substitute.For<IConcurrentBudgetProvider>();
            budgetProvider.TrySpendBudget().Returns(true);

            instantiatedAvatarBase = Object.Instantiate(AssetDatabase.LoadAssetAtPath<AvatarBase>("Assets/DCL/AvatarRendering/AvatarShape/Assets/AvatarBase.prefab"));
            IComponentPool<AvatarBase> avatarPoolRegistry = Substitute.For<IComponentPool<AvatarBase>>();
            avatarPoolRegistry.Get().Returns(instantiatedAvatarBase);

            avatarShapeComponent = new AvatarShapeComponent();

            avatarShapeComponent.WearablePromise = Promise.Create(world,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(BodyShape.MALE, new List<string>()),
                new PartitionComponent());

            system = new AvatarInstantiatorSystem(world, budgetProvider, avatarPoolRegistry, Substitute.For<IObjectPool<Material>>(), Substitute.For<IObjectPool<UnityEngine.ComputeShader>>(),
                new TextureArrayContainer(), Substitute.For<IWearableAssetsCache>(), new ComputeShaderSkinning(), new FixedComputeBufferHandler(1000, 4, 4));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(instantiatedAvatarBase);
        }

        private IWearable[] GetMockWearable()
        {
            IWearable mockWearable = Substitute.For<IWearable>();

            var assetBundleData
                = new StreamableLoadingResult<WearableAsset>?[BodyShape.COUNT];

            assetBundleData[BodyShape.MALE] = new StreamableLoadingResult<WearableAsset>(new WearableAsset(new GameObject(), new List<WearableAsset.RendererInfo>()));

            mockWearable.WearableAssets.Returns(assetBundleData);
            return new[] { mockWearable };
        }

        [Test]
        public void InstantiateAvatar()
        {
            world.Create(avatarShapeComponent, PartitionComponent.TOP_PRIORITY, new TransformComponent());
            system.Update(0);

            world.Add(avatarShapeComponent.WearablePromise.Entity, new StreamableLoadingResult<IWearable[]>(GetMockWearable()));

            system.Update(0);

            Assert.IsFalse(avatarShapeComponent.IsDirty);

            //Assert.AreEqual(avatarShapeComponent.InstantiatedWearables.Count, 1);
        }

        [Test]
        public void CancelAvatarLoadA()
        {
            /*Entity entityReference = world.Create(pbAvatarShape, PartitionComponent.TOP_PRIORITY, new TransformComponent());
            system.Update(0);

            ref AvatarShapeComponent avatarShapeComponent = ref world.Get<AvatarShapeComponent>(entityReference);
            GetWearablesByPointersIntention intentionToForget = avatarShapeComponent.WearablePromise.LoadingIntention;
            pbAvatarShape.IsDirty = true;
            system.Update(0);

            GetWearablesByPointersIntention newIntention = avatarShapeComponent.WearablePromise.LoadingIntention;

            Assert.IsTrue(intentionToForget.CancellationTokenSource.IsCancellationRequested);
            Assert.AreNotEqual(intentionToForget, newIntention);*/
        }

        [Test]
        [RequiresPlayMode]
        public void DestroyAvatar()
        {
            /*//Instantiate the avatar
            Entity entityReference = world.Create(pbAvatarShape, PartitionComponent.TOP_PRIORITY, new TransformComponent());
            system.Update(0);

            //Mocking the result of the WearablePromise
            ref AvatarShapeComponent avatarShapeComponent = ref world.Get<AvatarShapeComponent>(entityReference);

            world.Add(avatarShapeComponent.WearablePromise.Entity,
                new StreamableLoadingResult<IWearable[]>(new[] { GetMockWearable() }));

            system.Update(0);

            //Destroy the avatar
            world.Add(entityReference, new DeleteEntityIntention());
            system.Update(0);

            Assert.AreEqual(0, avatarShapeComponent.InstantiatedWearables.Count);*/
        }
    }
}
