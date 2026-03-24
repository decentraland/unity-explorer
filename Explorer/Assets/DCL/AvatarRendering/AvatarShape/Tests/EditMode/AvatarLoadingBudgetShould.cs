using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character.Components;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiles;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NUnit.Framework;
using System.Collections.Generic;
using Entity = Arch.Core.Entity;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    public class AvatarLoadingBudgetShould : UnitySystemTestBase<AvatarLoaderSystem>
    {
        private const int MAX_CONCURRENT = 3;

        private ConcurrentLoadingPerformanceBudget budget;

        [SetUp]
        public void Setup()
        {
            budget = new ConcurrentLoadingPerformanceBudget(MAX_CONCURRENT);
            system = new AvatarLoaderSystem(world, budget);
        }

        private Entity CreateProfileEntity(string id)
        {
            Profile profile = new ProfileBuilder().WithUserId(id).Build();
            return world.Create(profile, PartitionComponent.TOP_PRIORITY);
        }

        private Entity CreatePlayerEntity(string id)
        {
            Profile profile = new ProfileBuilder().WithUserId(id).Build();
            return world.Create(profile, PartitionComponent.TOP_PRIORITY, new PlayerComponent());
        }

        [Test]
        public void LimitConcurrentAvatarLoading()
        {
            var entities = new Entity[5];

            for (int i = 0; i < 5; i++)
                entities[i] = CreateProfileEntity($"user_{i}");

            system.Update(0);

            int loaded = 0;

            for (int i = 0; i < 5; i++)
            {
                if (world.Has<AvatarShapeComponent>(entities[i]))
                    loaded++;
            }

            Assert.AreEqual(MAX_CONCURRENT, loaded, "Only budgeted number of avatars should get AvatarShapeComponent");
            Assert.AreEqual(0, budget.currentBudget, "All budget slots should be consumed");
        }

        [Test]
        public void DeferAvatarsWhenBudgetExhausted()
        {
            var entities = new Entity[5];

            for (int i = 0; i < 5; i++)
                entities[i] = CreateProfileEntity($"user_{i}");

            system.Update(0);

            int deferred = 0;

            for (int i = 0; i < 5; i++)
            {
                if (!world.Has<AvatarShapeComponent>(entities[i]))
                    deferred++;
            }

            Assert.AreEqual(2, deferred, "Avatars beyond budget should be deferred");
        }

        [Test]
        public void LoadDeferredAvatarsWhenBudgetFreed()
        {
            var entities = new Entity[5];

            for (int i = 0; i < 5; i++)
                entities[i] = CreateProfileEntity($"user_{i}");

            system.Update(0);

            // Release budget for entities that got loaded (simulating instantiation completing)
            for (int i = 0; i < 5; i++)
            {
                if (world.Has<AvatarShapeComponent>(entities[i]))
                    world.Get<AvatarShapeComponent>(entities[i]).LoadingBudget.Release();
            }

            // Next update should pick up deferred entities
            system.Update(0);

            int totalLoaded = 0;

            for (int i = 0; i < 5; i++)
            {
                if (world.Has<AvatarShapeComponent>(entities[i]))
                    totalLoaded++;
            }

            Assert.AreEqual(5, totalLoaded, "All avatars should eventually load after budget is freed");
        }

        [Test]
        public void AlwaysLoadMainPlayerRegardlessOfBudget()
        {
            // Exhaust budget with non-player entities
            for (int i = 0; i < MAX_CONCURRENT; i++)
                CreateProfileEntity($"user_{i}");

            system.Update(0);

            Assert.AreEqual(0, budget.currentBudget, "Budget should be exhausted");

            // Create player entity — should bypass budget
            Entity player = CreatePlayerEntity("player");

            system.Update(0);

            Assert.That(world.Has<AvatarShapeComponent>(player), Is.True, "Main player should always get AvatarShapeComponent");
        }

        [Test]
        public void AssignAcquiredBudgetToLoadedAvatars()
        {
            Entity entity = CreateProfileEntity("user_0");

            system.Update(0);

            ref AvatarShapeComponent shape = ref world.Get<AvatarShapeComponent>(entity);
            Assert.That(shape.LoadingBudget, Is.Not.Null);
            Assert.That(shape.LoadingBudget, Is.Not.InstanceOf<NoAcquiredBudget>(), "Budgeted avatar should have a real AcquiredBudget");
        }

        [Test]
        public void AssignNoAcquiredBudgetToMainPlayer()
        {
            Entity player = CreatePlayerEntity("player");

            system.Update(0);

            ref AvatarShapeComponent shape = ref world.Get<AvatarShapeComponent>(player);
            Assert.That(shape.LoadingBudget, Is.InstanceOf<NoAcquiredBudget>(), "Main player should have NoAcquiredBudget");
        }

        [Test]
        public void ReleaseBudgetOnProfileUpdateReacquire()
        {
            Entity entity = CreateProfileEntity("user_0");

            system.Update(0);

            Assert.AreEqual(MAX_CONCURRENT - 1, budget.currentBudget);

            // Simulate instantiation completing by releasing budget
            world.Get<AvatarShapeComponent>(entity).LoadingBudget.Release();

            Assert.AreEqual(MAX_CONCURRENT, budget.currentBudget);

            // Trigger profile update
            ref Profile profile = ref world.Get<Profile>(entity);
            profile.IsDirty = true;

            system.Update(0);

            // Budget should be re-acquired for the update
            Assert.AreEqual(MAX_CONCURRENT - 1, budget.currentBudget);
        }

        [Test]
        public void DeferProfileUpdateWhenBudgetExhausted()
        {
            // Fill budget
            for (int i = 0; i < MAX_CONCURRENT; i++)
                CreateProfileEntity($"user_{i}");

            system.Update(0);

            // Release one budget slot and trigger its profile update
            Entity[] entities = new Entity[MAX_CONCURRENT];
            int idx = 0;

            var query = new Arch.Core.QueryDescription().WithAll<AvatarShapeComponent, Profile>();
            world.Query(in query, (Entity e) => { if (idx < MAX_CONCURRENT) entities[idx++] = e; });

            world.Get<AvatarShapeComponent>(entities[0]).LoadingBudget.Release();

            // Create a new entity to consume the freed slot
            Entity extraEntity = CreateProfileEntity("extra");
            system.Update(0);

            // Now trigger profile update on entities[1] — budget should be exhausted
            ref Profile profile1 = ref world.Get<Profile>(entities[1]);
            profile1.IsDirty = true;

            // The profile update should be deferred (IsDirty stays true)
            system.Update(0);

            Assert.That(profile1.IsDirty, Is.True, "Profile update should be deferred when budget is exhausted");
        }

        [Test]
        public void FreeBudgetWhenPendingAvatarIsDeleted()
        {
            Entity entity = CreateProfileEntity("user_0");

            system.Update(0);

            Assert.AreEqual(MAX_CONCURRENT - 1, budget.currentBudget);

            // Delete before instantiation — cleanup should release budget
            world.Add(entity, new DeleteEntityIntention());

            // Simulate AvatarCleanUpSystem's DestroyPendingAvatar
            ref AvatarShapeComponent shape = ref world.Get<AvatarShapeComponent>(entity);

            if (!shape.WearablePromise.IsConsumed)
                shape.WearablePromise.ForgetLoading(world);

            shape.LoadingBudget.Release();

            Assert.AreEqual(MAX_CONCURRENT, budget.currentBudget, "Budget should be freed when pending avatar is deleted");
        }
    }
}
