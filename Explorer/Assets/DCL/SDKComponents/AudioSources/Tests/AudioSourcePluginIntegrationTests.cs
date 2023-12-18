using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.AudioClips.Tests;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System.Threading.Tasks;
using static DCL.SDKComponents.AudioSources.Tests.AudioSourceTestsUtils;
using static Utility.Tests.TestsCategories;

namespace DCL.SDKComponents.AudioSources.Tests
{
    public class AudioSourcePluginIntegrationTests
    {
        private World world;

        private StartAudioSourceLoadingSystem startLoadingSystem;
        private LoadAudioClipSystem loadAudioClipSystem;
        private CreateAudioSourceSystem createAudioSourceSystem;

        private PBAudioSource pbAudioSource;
        private Entity entity;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();

            startLoadingSystem = StartAudioClipLoadingSystemShould.CreateSystem(world);
            createAudioSourceSystem = CreateAudioSourceSystemShould.CreateSystem(world);
            loadAudioClipSystem = LoadAudioClipSystemShould.CreateSystem(world);

            startLoadingSystem.Initialize();
            createAudioSourceSystem.Initialize();
            loadAudioClipSystem.Initialize();

            pbAudioSource = CreatePBAudioSource(); // Create component

            entity = world.Create(pbAudioSource, PartitionComponent.TOP_PRIORITY); // Create entity
            EcsTestsUtils.AddTransformToEntity(world, entity);
        }

        [TearDown]
        public void TearDown()
        {
            startLoadingSystem?.Dispose();
            createAudioSourceSystem?.Dispose();
            world?.Dispose();
        }

        [Category(INTEGRATION)]
        [Test]
        public async Task ShouldCreateAudioSource_WithDownloadedAudioClip_WhenPBAudioSourcePresented()
        {
            startLoadingSystem.Update(0);

            world.TryGet(entity, out AudioSourceComponent audioSourceComponent);
            world.Get<StreamableLoadingState>(audioSourceComponent.ClipPromise!.Value.Entity).SetAllowed(Substitute.For<IAcquiredBudget>());

            loadAudioClipSystem.Update(1);
            await UniTask.WaitUntil(() => audioSourceComponent.ClipPromise.Value.TryGetResult(world, out _));

            createAudioSourceSystem.Update(2);

            // Assert
            AudioSourceComponent afterUpdate = world.Get<AudioSourceComponent>(entity);
            Assert.That(afterUpdate.ClipLoadingStatus, Is.EqualTo(ECS.StreamableLoading.LifeCycle.LoadingFinished));
            Assert.That(afterUpdate.Result, Is.Not.Null);
            Assert.That(afterUpdate.Result.clip, Is.Not.Null);
            Assert.That(afterUpdate.Result.clip.length, Is.EqualTo(TestAudioClip.length).Within(0.1f));
        }
    }
}
