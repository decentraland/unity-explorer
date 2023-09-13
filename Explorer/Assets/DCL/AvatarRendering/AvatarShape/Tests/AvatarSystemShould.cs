using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Systems;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.ECSComponents;
using ECS.ComponentsPooling;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using NSubstitute;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    public class AvatarSystemShould : UnitySystemTestBase<AvatarSystem>
    {
        private AvatarBase instantiatedAvatarBase;
        private PBAvatarShape pbAvatarShape;

        [SetUp]
        public void Setup()
        {
            IConcurrentBudgetProvider budgetProvider = Substitute.For<IConcurrentBudgetProvider>();
            budgetProvider.TrySpendBudget().Returns(true);

            instantiatedAvatarBase = Object.Instantiate(AssetDatabase.LoadAssetAtPath<AvatarBase>("Assets/DCL/AvatarRendering/AvatarShape/Resources/AvatarBase.prefab"));
            IComponentPool<AvatarBase> avatarPoolRegistry = Substitute.For<IComponentPool<AvatarBase>>();
            avatarPoolRegistry.Get().Returns(instantiatedAvatarBase);

            pbAvatarShape = new PBAvatarShape
            {
                BodyShape = WearablesLiterals.BodyShape.MALE,
            };

            system = new AvatarSystem(world, budgetProvider, avatarPoolRegistry);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(instantiatedAvatarBase);
        }

        private IWearable GetMockWearable()
        {
            IWearable mockWearable = Substitute.For<IWearable>();

            var AssetBundleData
                = new StreamableLoadingResult<AssetBundleData>?[WearablesLiterals.BodyShape.COUNT];

            AssetBundleData[WearablesLiterals.BodyShape.MALE] = new StreamableLoadingResult<AssetBundleData>(new AssetBundleData(null, null, new GameObject()));

            mockWearable.AssetBundleData.Returns(AssetBundleData);
            return mockWearable;
        }

        [Test]
        public void InstantiateAvatar()
        {
            Entity entityReference = world.Create(pbAvatarShape, PartitionComponent.TOP_PRIORITY, new TransformComponent());
            system.Update(0);

            //Mocking the result of the WearablePromise
            ref AvatarShapeComponent avatarShapeComponent = ref world.Get<AvatarShapeComponent>(entityReference);

            world.Add(avatarShapeComponent.WearablePromise.Entity,
                new StreamableLoadingResult<IWearable[]>(new[] { GetMockWearable() }));

            system.Update(0);

            Assert.IsFalse(avatarShapeComponent.IsDirty);
            Assert.AreEqual(avatarShapeComponent.InstantiatedWearables.Count, 1);
        }

        [Test]
        public void CancelAvatarLoad()
        {
            Entity entityReference = world.Create(pbAvatarShape, PartitionComponent.TOP_PRIORITY, new TransformComponent());
            system.Update(0);

            ref AvatarShapeComponent avatarShapeComponent = ref world.Get<AvatarShapeComponent>(entityReference);
            GetWearablesByPointersIntention intentionToForget = avatarShapeComponent.WearablePromise.LoadingIntention;
            pbAvatarShape.IsDirty = true;
            system.Update(0);

            GetWearablesByPointersIntention newIntention = avatarShapeComponent.WearablePromise.LoadingIntention;

            Assert.IsTrue(intentionToForget.CancellationTokenSource.IsCancellationRequested);
            Assert.AreNotEqual(intentionToForget, newIntention);
        }

        [Test]
        [RequiresPlayMode]
        public void DestroyAvatar()
        {
            //Instantiate the avatar
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

            Assert.AreEqual(0, avatarShapeComponent.InstantiatedWearables.Count);
        }
    }
}
