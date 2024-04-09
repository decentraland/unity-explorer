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
    [Category(INTEGRATION)]
    public class AudioSourcePluginIntegrationTests
    {
        private World world;

        private StartAudioSourceLoadingSystem startLoadingSystem;
        private LoadAudioClipSystem loadAudioClipSystem;
        private UpdateAudioSourceSystem updateAudioSourceSystem;

        private PBAudioSource pbAudioSource;
        private Entity entity;


        public void SetUp()
        {
            world = World.Create();

            // Create systems
            startLoadingSystem = StartAudioClipLoadingSystemShould.CreateSystem(world);
            loadAudioClipSystem = LoadAudioClipSystemShould.CreateSystem(world);
            updateAudioSourceSystem = UpdateAudioSourceSystemShould.CreateSystem(world);

            startLoadingSystem.Initialize();
            loadAudioClipSystem.Initialize();
            updateAudioSourceSystem.Initialize();

            // Create component
            pbAudioSource = CreatePBAudioSource();

            // Create entity
            entity = world.Create(pbAudioSource, PartitionComponent.TOP_PRIORITY);
            EcsTestsUtils.AddTransformToEntity(world, entity);
        }


        public void TearDown()
        {
            startLoadingSystem?.Dispose();
            loadAudioClipSystem?.Dispose();
            updateAudioSourceSystem?.Dispose();

            world?.Dispose();
        }


        public async Task ShouldCreateAudioSource_WithDownloadedAudioClip_WhenPBAudioSourcePresented()
        {
            startLoadingSystem.Update(0);

            world.TryGet(entity, out AudioSourceComponent audioSourceComponent);
            world.Get<StreamableLoadingState>(audioSourceComponent.ClipPromise.Entity).SetAllowed(Substitute.For<IAcquiredBudget>());

            loadAudioClipSystem.Update(1);
            await UniTask.WaitUntil(() => audioSourceComponent.ClipPromise.TryGetResult(world, out _));

            updateAudioSourceSystem.Update(2);

            // Assert
            AudioSourceComponent afterUpdate = world.Get<AudioSourceComponent>(entity);
            Assert.That(afterUpdate.ClipPromise, Is.Not.Null);
            Assert.That(afterUpdate.AudioSource, Is.Not.Null);
            Assert.That(afterUpdate.AudioSource.clip, Is.Not.Null);
            Assert.That(afterUpdate.AudioSource.clip.length, Is.EqualTo(TestAudioClip.length).Within(0.1f));
        }
    }
}
