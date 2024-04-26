using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Common;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SDKComponents.AudioSources.Tests
{
    public class StartAudioClipLoadingSystemShould : UnitySystemTestBase<StartAudioSourceLoadingSystem>
    {
        private PBAudioSource pbAudioSource;
        private Entity entity;

        [SetUp]
        public void SetUp()
        {
            system = CreateSystem(world);
            pbAudioSource = AudioSourceTestsUtils.CreatePBAudioSource(); // Create component
            entity = world.Create(pbAudioSource, PartitionComponent.TOP_PRIORITY); // Create entity
        }

        public static StartAudioSourceLoadingSystem CreateSystem(World world)
        {
            IPerformanceBudget concurrentBudgetProvider = Substitute.For<IPerformanceBudget>();
            concurrentBudgetProvider.TrySpendBudget().Returns(true);

            ISceneData sceneData = Substitute.For<ISceneData>();

            sceneData.TryGetContentUrl(Arg.Any<string>(), out Arg.Any<URLAddress>())
                     .Returns(args =>
                      {
                          args[1] = URLAddress.FromString(args.ArgAt<string>(0));
                          return true;
                      });

            return new StartAudioSourceLoadingSystem(world, sceneData, concurrentBudgetProvider);
        }

        [TearDown]
        public void TearDown()
        {
            system.Dispose();
        }

        [Test]
        public void CreateAudioSourceComponentForPBAudioSource()
        {
            // Act
            system.Update(0);

            // Assert world
            Assert.That(world.TryGet(entity, out AudioSourceComponent audioSourceComponent), Is.True);

            // Assert component
            Assert.That(audioSourceComponent.AudioClipUrl, Is.EqualTo(pbAudioSource.AudioClipUrl));
            Assert.That(audioSourceComponent.ClipPromise, Is.Not.Null);
            Assert.That(audioSourceComponent.AudioSource, Is.Null);

            // Assert promise
            Assert.That(audioSourceComponent.ClipPromise, Is.Not.EqualTo(AssetPromise<AudioClip, GetAudioClipIntention>.NULL));
            AssetPromise<AudioClip, GetAudioClipIntention> promiseValue = audioSourceComponent.ClipPromise;
            Assert.That(world.TryGet(promiseValue.Entity, out GetAudioClipIntention intention), Is.True);
            Assert.That(intention.CommonArguments.URL, Is.EqualTo(pbAudioSource.AudioClipUrl));
            Assert.That(intention.AudioType, Is.EqualTo(pbAudioSource.AudioClipUrl.ToAudioType()));
        }
    }
}
