using Arch.Core;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using ECS.Unity.AudioSources.Components;
using ECS.Unity.AudioSources.Systems;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using static ECS.Unity.AudioSources.Tests.AudioSourceTestsUtils;

namespace ECS.Unity.AudioSources.Tests
{
    public class CreateAudioSourceSystemShould : UnitySystemTestBase<CreateAudioSourceSystem>
    {
        private AudioSourceComponent component;
        private Entity entity;

        [SetUp]
        public void SetUp()
        {
            system = CreateSystem(world);
            CreateComponent();
            CreateEntity();
            return;

            void CreateComponent()
            {
                component = new AudioSourceComponent(CreatePBAudioSource());
                component.ClipLoadingStatus = StreamableLoading.LifeCycle.LoadingInProgress;
                component.ClipPromise = AssetPromise<AudioClip, GetAudioClipIntention>.Create(world, new GetAudioClipIntention(), PartitionComponent.TOP_PRIORITY);
            }

            void CreateEntity()
            {
                entity = world.Create(component);
                AddTransformToEntity(entity);
            }
        }

        public static CreateAudioSourceSystem CreateSystem(World world)
        {
            var poolsRegistry = Substitute.For<IComponentPoolsRegistry>();
            var audioSourcesPool = Substitute.For<IComponentPool<AudioSource>>();

            poolsRegistry.GetReferenceTypePool<AudioSource>().Returns(audioSourcesPool);
            audioSourcesPool.Get().Returns(new GameObject().AddComponent<AudioSource>());

            var budgetProvider = Substitute.For<IConcurrentBudgetProvider>();
            budgetProvider.TrySpendBudget().Returns(true);

            return new CreateAudioSourceSystem(world, poolsRegistry, budgetProvider, budgetProvider);
        }

        [TearDown]
        public void TearDown()
        {
            system.Dispose();
        }

        [Test]
        public void NotCreateAudioSourceIfClipNotFinishedLoading()
        {
            // Act
            system.Update(0);

            // Assert
            AudioSourceComponent afterUpdate = world.Get<AudioSourceComponent>(entity);
            Assert.That(afterUpdate.ClipLoadingStatus, Is.EqualTo(StreamableLoading.LifeCycle.LoadingInProgress));
            Assert.That(afterUpdate.Result, Is.Null);
        }

        [Test]
        public void CreateAudioSourceFromResolvedPromise()
        {
            // Arrange
            world.Add(component.ClipPromise!.Value.Entity, new StreamableLoadingResult<AudioClip>(TestAudioClip));

            // Act
            system.Update(0);

            // Assert
            AudioSourceComponent afterUpdate = world.Get<AudioSourceComponent>(entity);
            Assert.That(afterUpdate.ClipLoadingStatus, Is.EqualTo(StreamableLoading.LifeCycle.LoadingFinished));
            Assert.That(afterUpdate.Result, Is.Not.Null);
            Assert.That(afterUpdate.Result.clip, Is.EqualTo(TestAudioClip));
        }
    }
}
