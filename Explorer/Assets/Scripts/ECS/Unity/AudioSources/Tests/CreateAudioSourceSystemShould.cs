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
using UnityEditor;
using UnityEngine;

namespace ECS.Unity.AudioSources.Tests
{
    public class CreateAudioSourceSystemShould : UnitySystemTestBase<CreateAudioSourceSystem>
    {
        private static AudioClip audioClip => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Scripts/ECS/Unity/AudioSources/Tests/cuckoo-test-clip.mp3");

        private AudioSourceComponent component;
        private Entity entity;

        [SetUp]
        public void SetUp()
        {
            CreateSystem();
            CreateComponent();
            CreateEntity();
            return;

            void CreateSystem()
            {
                var poolsRegistry = Substitute.For<IComponentPoolsRegistry>();
                var audioSourcesPool = Substitute.For<IComponentPool<AudioSource>>();

                poolsRegistry.GetReferenceTypePool<AudioSource>().Returns(audioSourcesPool);
                audioSourcesPool.Get().Returns(new GameObject().AddComponent<AudioSource>());

                var budgetProvider = Substitute.For<IConcurrentBudgetProvider>();
                budgetProvider.TrySpendBudget().Returns(true);

                system = new CreateAudioSourceSystem(world, poolsRegistry, budgetProvider, budgetProvider);
                system.Initialize();
            }

            void CreateComponent()
            {
                component = new AudioSourceComponent(AudioSourceTestsUtils.CreatePBAudioSource());
                component.ClipLoadingStatus = StreamableLoading.LifeCycle.LoadingInProgress;
                component.ClipPromise = AssetPromise<AudioClip, GetAudioClipIntention>.Create(world, new GetAudioClipIntention(), PartitionComponent.TOP_PRIORITY);
            }

            void CreateEntity()
            {
                entity = world.Create(component);
                AddTransformToEntity(entity);
            }
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
            world.Add(component.ClipPromise!.Value.Entity, new StreamableLoadingResult<AudioClip>(audioClip));

            // Act
            system.Update(0);

            // Assert
            AudioSourceComponent afterUpdate = world.Get<AudioSourceComponent>(entity);
            Assert.That(afterUpdate.ClipLoadingStatus, Is.EqualTo(StreamableLoading.LifeCycle.LoadingFinished));
            Assert.That(afterUpdate.Result, Is.Not.Null);
            Assert.That(afterUpdate.Result.clip, Is.EqualTo(audioClip));
        }
    }
}
