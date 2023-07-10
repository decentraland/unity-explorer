using Arch.Core;
using ECS.Prioritization.Components;
using ECS.Prioritization.DeferredLoading;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.TestSuite;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.StreamableLoading.DeferredLoading.Tests
{
    public class DeferredLoadingSystemShould : UnitySystemTestBase<DeferredLoadingSystem<Texture2D, GetTextureIntention>>
    {
        private DeferredLoadingSystem<Texture2D, GetTextureIntention> deferredLoadingSystem;
        private List<Entity> entities;
        private ConcurrentLoadingBudgetProvider concurrentLoadingBudgetProvider;

        [SetUp]
        public void SetUp()
        {
            // We ll create a budget system that only allows 5 concurrent loading requests
            concurrentLoadingBudgetProvider = new ConcurrentLoadingBudgetProvider(5);
            system = new DeferredLoadingSystem<Texture2D, GetTextureIntention>(world, concurrentLoadingBudgetProvider);
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
                        CommonArguments = new CommonLoadingArguments("", deferredLoadingState: DeferredLoadingState.Forbidden),
                    },
                    new PartitionComponent
                    {
                        Bucket = (byte)i,
                        IsBehind = i % 2 == 0,
                        IsDirty = true,
                    });

                entities.Add(newEntity);
            }

            //After the first update, only the intentions with odd bucket values should be allowed
            system.Update(0);

            for (var i = 0; i < entities.Count; i++)
                Assert.AreEqual(i % 2 != 0,
                    world.Get<GetTextureIntention>(entities[i]).CommonArguments.DeferredLoadingState == DeferredLoadingState.Allowed);
        }

        [Test]
        public void IntentionsOrderedByBucket()
        {
            for (var i = 0; i < 10; i++)
            {
                Entity newEntity = world.Create(
                    new GetTextureIntention
                    {
                        CommonArguments = new CommonLoadingArguments("", deferredLoadingState: DeferredLoadingState.Forbidden),
                    },
                    new PartitionComponent
                    {
                        Bucket = (byte)i,
                        IsBehind = false,
                        IsDirty = true,
                    });

                entities.Add(newEntity);
            }

            //After the first update, only the intentions with bucket values lower than 5 should be allowed
            system.Update(0);

            for (var i = 0; i < entities.Count; i++)
                Assert.AreEqual(i < 5,
                    world.Get<GetTextureIntention>(entities[i]).CommonArguments.DeferredLoadingState == DeferredLoadingState.Allowed);
        }

        [Test]
        public void IntentionsAllowedWhenBudgetIsReleased()
        {
            for (var i = 0; i < 10; i++)
            {
                Entity newEntity = world.Create(
                    new GetTextureIntention
                    {
                        CommonArguments = new CommonLoadingArguments("", deferredLoadingState: DeferredLoadingState.Forbidden),
                    },
                    new PartitionComponent
                    {
                        Bucket = (byte)i,
                        IsBehind = false,
                        IsDirty = true,
                    });

                entities.Add(newEntity);
            }

            //After the first update, only the intentions with bucket values lower than 5 should be allowed
            system.Update(0);

            for (var i = 0; i < entities.Count; i++)
                Assert.AreEqual(i < 5,
                    world.Get<GetTextureIntention>(entities[i]).CommonArguments.DeferredLoadingState == DeferredLoadingState.Allowed);

            // We'll release 3 budget and check that additional 3 intentions are allowed
            for (int i = 0; i < 3; i++)
                concurrentLoadingBudgetProvider.ReleaseBudget();

            system.Update(0);

            for (var i = 0; i < entities.Count; i++)
                Assert.AreEqual(i < 8,
                    world.Get<GetTextureIntention>(entities[i]).CommonArguments.DeferredLoadingState == DeferredLoadingState.Allowed);
        }
    }
}
