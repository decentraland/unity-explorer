using Arch.Core;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;

namespace ECS.StreamableLoading.DeferredLoading.Tests
{
    public class DeferredLoadingSystemShould : UnitySystemTestBase<AssetsDeferredLoadingSystem>
    {
        private List<Entity> entities;
        private ConcurrentLoadingPerformanceBudget concurrentLoadingPerformanceBudget;
        private IReleasablePerformanceBudget memoryBudgetMock;

        [SetUp]
        public void SetUp()
        {
            // We ll create a budget system that only allows 5 concurrent loading requests
            concurrentLoadingPerformanceBudget = new ConcurrentLoadingPerformanceBudget(5);
            memoryBudgetMock = Substitute.For<IReleasablePerformanceBudget>();
            memoryBudgetMock.TrySpendBudget().Returns(true);

            system = new AssetsDeferredLoadingSystem(world, concurrentLoadingPerformanceBudget, memoryBudgetMock, new SceneAssetLock());
            entities = new List<Entity>();
        }

        [Test]
        public void IntentionsOrderedByIsBehind()
        {
            // We'll create 10 intentions for testing. The one with a pair bucket value will have a
            // true value of isBehind
            for (var i = 0; i < 10; i++)
            {
                Entity newEntity = world.Create(
                    new GetTextureIntention
                    {
                        CommonArguments = new CommonLoadingArguments(""),
                    },
                    (IPartitionComponent)new PartitionComponent
                    {
                        Bucket = (byte)i,
                        IsBehind = i % 2 == 0,
                        IsDirty = true,
                    }, new StreamableLoadingState());

                entities.Add(newEntity);
            }

            //After the first update, 5 first intentions should be allowed
            system.Update(0);

            for (var i = 0; i < entities.Count; i++)
                Assert.That(world.Get<StreamableLoadingState>(entities[i]).Value,
                    Is.EqualTo(i < 5 ? StreamableLoadingState.Status.Allowed : StreamableLoadingState.Status.Forbidden));
        }

        [Test]
        public void IntentionsOrderedByBucket()
        {
            for (var i = 0; i < 10; i++)
            {
                Entity newEntity = world.Create(
                    new GetTextureIntention
                    {
                        CommonArguments = new CommonLoadingArguments(""),
                    },
                    (IPartitionComponent)new PartitionComponent
                    {
                        Bucket = (byte)i,
                        IsBehind = false,
                        IsDirty = true,
                    }, new StreamableLoadingState());

                entities.Add(newEntity);
            }

            //After the first update, only the intentions with bucket values lower than 5 should be allowed
            system.Update(0);

            for (var i = 0; i < entities.Count; i++)
                Assert.AreEqual(i < 5,
                    world.Get<StreamableLoadingState>(entities[i]).Value == StreamableLoadingState.Status.Allowed);
        }

        [Test]
        public void IntentionsAllowedWhenBudgetIsReleased()
        {
            for (var i = 0; i < 10; i++)
            {
                Entity newEntity = world.Create(
                    new GetTextureIntention
                    {
                        CommonArguments = new CommonLoadingArguments(""),
                    },
                    (IPartitionComponent)new PartitionComponent
                    {
                        Bucket = (byte)i,
                        IsBehind = false,
                        IsDirty = true,
                    }, new StreamableLoadingState());

                entities.Add(newEntity);
            }

            //After the first update, only the intentions with bucket values lower than 5 should be allowed
            system.Update(0);

            for (var i = 0; i < entities.Count; i++)
                Assert.AreEqual(i < 5,
                    world.Get<StreamableLoadingState>(entities[i]).Value == StreamableLoadingState.Status.Allowed);

            // We'll release 3 budget and check that additional 3 intentions are allowed
            for (var i = 0; i < 3; i++)
                concurrentLoadingPerformanceBudget.ReleaseBudget();

            system.Update(0);

            for (var i = 0; i < entities.Count; i++)
                Assert.AreEqual(i < 8,
                    world.Get<StreamableLoadingState>(entities[i]).Value == StreamableLoadingState.Status.Allowed);
        }
    }
}
